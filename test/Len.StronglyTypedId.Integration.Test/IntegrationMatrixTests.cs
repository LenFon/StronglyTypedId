using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Dapper;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Len.StronglyTypedId;

/// <summary>
/// 集成矩阵：把生成器为「每一种基元」产出的 Dapper 处理器与 EF Core 转换器全部拿出来跑一遍。
/// </summary>
/// <remarks>
/// 集成产物按 Id 逐个生成，单点用例只覆盖被点名的几个。Dapper 的读取路径曾因只测 <c>string</c> 基元而把「直接拆箱强转」的缺陷藏了很久。
/// 这里反射枚举本编译生成的全部处理器/转换器，按「驱动真实返回的形状」逐个喂值。
/// </remarks>
public class IntegrationMatrixTests
{
    /// <summary>
    /// 每个基元取一个「普通」值：非默认、非边界，避免把断言变成边界测试。
    /// </summary>
    private static IReadOnlyDictionary<Type, object> SampleValues => IntegrationSampleValues.ByPrimitive;

    [Fact]
    public void Dapper_Should_GenerateHandler_ForEveryPrimitive()
    {
        var covered = DapperHandlers().Select(handler => handler.PrimitiveType).ToHashSet();

        covered.Should().Contain(SampleValues.Keys, "每种基元都必须有 Dapper 处理器");
    }

    [Fact]
    public void EfCore_Should_GenerateConverter_ForEveryPrimitive()
    {
        var covered = EfCoreConverters().Select(converter => converter.PrimitiveType).ToHashSet();

        covered.Should().Contain(SampleValues.Keys, "每种基元都必须有 EF Core 转换器");
    }
    [Fact]
    public void Dapper_Handler_Should_RoundTripEveryPrimitive()
    {
        foreach (var (idType, primitiveType, handler) in DapperHandlers())
        {
            var value = SampleValues[primitiveType];
            var expected = CreateId(idType, value);
            var parse = handler.GetType().GetMethod("Parse", BindingFlags.Public | BindingFlags.Instance)!;

            // 1. 驱动直接返回基元 CLR 类型（SQL Server / PostgreSQL 的常规情形）。
            parse.Invoke(handler, [value]).Should().Be(expected, $"{idType.Name}：Parse 必须接受基元 CLR 类型");

            // 2. 驱动返回「形状不同」的 CLR 类型（SQLite 的 TEXT / INTEGER / REAL）。
            var shaped = ProviderShapedValue(primitiveType, value);

            parse.Invoke(handler, [shaped]).Should().Be(expected, $"{idType.Name}：Parse 必须转换驱动返回的 {shaped.GetType().Name}");

            // 3. NULL / DBNull 落到 default。
            parse.Invoke(handler, [null]).Should().Be(DefaultOf(idType), $"{idType.Name}：NULL 必须落到 default");
            parse.Invoke(handler, [DBNull.Value]).Should().Be(DefaultOf(idType), $"{idType.Name}：DBNull 必须落到 default");
        }
    }

    [Fact]
    public void Dapper_Handler_Should_WritePrimitiveValue()
    {
        foreach (var (idType, primitiveType, handler) in DapperHandlers())
        {
            var id = CreateId(idType, SampleValues[primitiveType]);
            var setValue = handler.GetType().GetMethod("SetValue", BindingFlags.Public | BindingFlags.Instance)!;
            var parameter = new StubParameter();

            setValue.Invoke(handler, [parameter, id]);

            // 比对 Id 实际持有的值而不是样本值：归一化器可能改写它（"abc" → "ABC"）。
            // 没有 provider 类型的基元（BigInteger / Int128 / UInt128 / Half）落库的是规范文本，
            // 直接塞基元会让驱动在绑定时抛「No mapping exists」。
            parameter.Value.Should().Be(
                IntegrationSampleValues.DatabaseProviderValue(primitiveType, ValueOf(id)!),
                $"{idType.Name}：SetValue 必须把驱动认识的形态交给参数");
        }
    }

    [Fact]
    public void Dapper_Handler_Should_WriteDbNull_ForNullReferenceId()
    {
        var referenceHandlers = DapperHandlers().Where(handler => !handler.IdType.IsValueType).ToList();

        referenceHandlers.Should().NotBeEmpty("集成测试程序集里必须有引用类型 Id 才能覆盖这条分支");

        foreach (var (idType, _, handler) in referenceHandlers)
        {
            var setValue = handler.GetType().GetMethod("SetValue", BindingFlags.Public | BindingFlags.Instance)!;
            var parameter = new StubParameter();

            setValue.Invoke(handler, [parameter, null]);

            parameter.Value.Should().Be(DBNull.Value, $"{idType.Name}：null 必须写成 DBNull");
        }
    }

    /// <summary>
    /// 值类型 Id 包装引用类型基元（<c>string</c> 或自定义 <c>class</c>）且基元为 <see langword="null"/> 时，
    /// 同样必须写 <see cref="DBNull"/>：把 <c>null</c> 交给驱动会让 Microsoft.Data.Sqlite 抛
    /// <c>Value must be set</c>，而不是把它当成 SQL NULL。
    /// </summary>
    /// <remarks>
    /// 与上一条覆盖的是不同分支：那条的 Id 本身是 <see langword="null"/>，这条的 Id 非 null、只有基元为 null。
    /// </remarks>
    [Fact]
    public void Dapper_Handler_Should_WriteDbNull_ForNullReferenceTypePrimitive()
    {
        var handlers = DapperHandlers()
            .Where(handler => handler.IdType.IsValueType && !PrimitiveOf(handler.IdType).IsValueType)
            .ToList();

        handlers.Should().NotBeEmpty("集成测试程序集里必须有「值类型 Id + 引用类型基元」才能覆盖这条分支");

        foreach (var (idType, _, handler) in handlers)
        {
            var setValue = handler.GetType().GetMethod("SetValue", BindingFlags.Public | BindingFlags.Instance)!;
            var parameter = new StubParameter();

            setValue.Invoke(handler, [parameter, Activator.CreateInstance(idType)]);

            parameter.Value.Should().Be(DBNull.Value, $"{idType.Name}：null 基元必须写成 DBNull");
        }
    }

    [Fact]
    public void EfCore_Converter_Should_RoundTripEveryPrimitive()
    {
        foreach (var (idType, primitiveType, converter) in EfCoreConverters())
        {
            var id = CreateId(idType, SampleValues[primitiveType]);
            var providerValue = IntegrationSampleValues.DatabaseProviderValue(primitiveType, ValueOf(id)!);

            // 同上：写出的必须是 Id 实际持有的值，归一化器可能已经改写过样本值。
            // 没有 provider 类型的基元（BigInteger / Int128 / UInt128 / Half）落库的是规范文本。
            converter.ConvertToProvider(id).Should().Be(providerValue, $"{idType.Name}：ConvertToProvider 必须写出驱动认识的形态");
            converter.ConvertFromProvider(providerValue).Should().Be(id, $"{idType.Name}：ConvertFromProvider 必须还原 Id");
        }
    }

    /// <summary>
    /// EF Core 的枚举转换器同样必须拒绝未定义取值，与 Dapper / STJ / Newtonsoft / MessagePack / BSON 对齐。
    /// </summary>
    /// <remarks>
    /// 数据库里一个脏的枚举列（手工改库、迁移脚本写错、上游写入未定义值）会让 Id 带着一个 <c>switch</c>
    /// 永远落空的取值进入领域层。此前 EF 转换器走 <c>Create</c> 且无已定义性校验，同一份数据经 Dapper 读
    /// 会被拒绝、经 EF 读却放行。
    /// </remarks>
    [Fact]
    public void EfCore_Converter_Should_RejectUndefinedEnumValue()
    {
        var (_, _, converter) = EfCoreConverters().Single(c => c.IdType == typeof(SampleStatusId));

        var act = () => converter.ConvertFromProvider((SampleStatus)999);

        act.Should().Throw<InvalidOperationException>("未定义的枚举取值必须被拒绝");
    }

    /// <summary>
    /// <c>[Flags]</c> 枚举的 EF 转换器按「不许出现未定义的位」校验，与 Dapper 的位校验同源。
    /// </summary>
    [Fact]
    public void EfCore_Converter_Should_RejectUndefinedFlagsBits()
    {
        var (_, _, converter) = EfCoreConverters().Single(c => c.IdType == typeof(SampleAccessId));

        // 已定义位的组合照常通过。
        converter.ConvertFromProvider(SampleAccess.Read | SampleAccess.Write)
            .Should().Be(SampleAccessId.Create(SampleAccess.Read | SampleAccess.Write));

        // 含未定义的位必须拒绝。
        var act = () => converter.ConvertFromProvider((SampleAccess)999);
        act.Should().Throw<InvalidOperationException>("未定义的位必须被拒绝");
    }

    /// <summary>
    /// 驱动返回的 CLR 类型与基元不一致时，<c>DateOnly</c> / <c>TimeOnly</c> 必须按分量还原。
    /// </summary>
    /// <remarks>
    /// SQL Server 的 <c>DATE</c> 列返回 <see cref="DateTime"/>、<c>TIME</c> 列返回 <see cref="TimeSpan"/>，
    /// 而 <c>DateOnly.Parse</c> 拒绝带时间分量的文本、<c>TimeOnly.Parse</c> 拒绝带日期的文本。
    /// 只走文本入口会在物化时抛 <c>FormatException</c>，而文档承诺「驱动返回什么都能还原」。
    /// </remarks>
    [Fact]
    public void Dapper_Handler_Should_ConvertDateTimeAndTimeSpan_ForTemporalPrimitives()
    {
        var (_, _, dateHandler) = DapperHandlers().Single(h => h.IdType == typeof(DateOnlyId));
        var dateParse = dateHandler.GetType().GetMethod("Parse", BindingFlags.Public | BindingFlags.Instance)!;

        dateParse.Invoke(dateHandler, [new DateTime(2020, 1, 2)])
            .Should().Be(DateOnlyId.Create(new DateOnly(2020, 1, 2)), "SQL Server 的 DATE 列返回 DateTime");

        var (_, _, timeHandler) = DapperHandlers().Single(h => h.IdType == typeof(TimeOnlyId));
        var timeParse = timeHandler.GetType().GetMethod("Parse", BindingFlags.Public | BindingFlags.Instance)!;

        timeParse.Invoke(timeHandler, [new TimeSpan(13, 45, 30)])
            .Should().Be(TimeOnlyId.Create(new TimeOnly(13, 45, 30)), "SQL Server 的 TIME 列返回 TimeSpan");
        timeParse.Invoke(timeHandler, [new DateTime(2020, 1, 2, 13, 45, 30)])
            .Should().Be(TimeOnlyId.Create(new TimeOnly(13, 45, 30)), "某些驱动把 TIME 列返回成 DateTime");
    }

    /// <summary>
    /// 枚举列的文本路径必须与 <c>Id.TryParse</c> 同一套收紧规则：忽略大小写，且拒绝未定义的取值。
    /// </summary>
    /// <remarks>
    /// 驱动返回成员名文本时大小写约定各不相同（Oracle 默认大写、PostgreSQL 原样返回），
    /// 而 <c>Enum.Parse</c> 区分大小写；同时它对 <c>"999"</c>、<c>"1,2"</c> 一律返回 true，
    /// 放行会造出一个不对应任何成员的 Id，后续 <c>switch</c> 落空、序列化写出野值。
    /// </remarks>
    [Fact]
    public void Dapper_Handler_Should_ParseEnumText_CaseInsensitively_AndRejectUndefined()
    {
        var (_, _, handler) = DapperHandlers().Single(h => h.IdType == typeof(SampleStatusId));
        var parse = handler.GetType().GetMethod("Parse", BindingFlags.Public | BindingFlags.Instance)!;

        parse.Invoke(handler, ["Active"]).Should().Be(SampleStatusId.Create(SampleStatus.Active));
        parse.Invoke(handler, ["active"]).Should().Be(SampleStatusId.Create(SampleStatus.Active),
            "驱动返回的成员名大小写不应影响解析");
        parse.Invoke(handler, ["SUSPENDED"]).Should().Be(SampleStatusId.Create(SampleStatus.Suspended));

        foreach (var undefined in new[] { "999", "1,2", "NotAMember" })
        {
            var act = () => parse.Invoke(handler, [undefined]);

            act.Should().Throw<System.Reflection.TargetInvocationException>()
                .WithInnerException<ArgumentException>($"{undefined} 不对应任何成员，必须拒绝");
        }
    }

    /// <summary>
    /// <c>[Flags]</c> 枚举的文本路径：位组合合法，含未定义位的取值必须拒绝。
    /// </summary>
    [Fact]
    public void Dapper_Handler_Should_ParseFlagsEnumText_ByUndefinedBits()
    {
        var (_, _, handler) = DapperHandlers().Single(h => h.IdType == typeof(SampleAccessId));
        var parse = handler.GetType().GetMethod("Parse", BindingFlags.Public | BindingFlags.Instance)!;

        parse.Invoke(handler, ["Read, Write"]).Should().Be(SampleAccessId.Create(SampleAccess.Read | SampleAccess.Write),
            "位组合本身就是合法取值，IsDefined 会误杀，故只校验未定义位");
        parse.Invoke(handler, ["read, write"]).Should().Be(SampleAccessId.Create(SampleAccess.Read | SampleAccess.Write));

        var act = () => parse.Invoke(handler, ["999"]);

        act.Should().Throw<System.Reflection.TargetInvocationException>()
            .WithInnerException<ArgumentException>("999 含未定义的位，必须拒绝");
    }

    /// <summary>
    /// 本编译里生成的全部 Dapper 处理器：<c>StronglyTypedIds</c> 下继承 <c>SqlMapper.TypeHandler&lt;T&gt;</c> 的嵌套类。
    /// </summary>
    private static IEnumerable<(Type IdType, Type PrimitiveType, object Handler)> DapperHandlers()
        => typeof(StronglyTypedIds)
            .GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public)
            .Where(type => type.BaseType is { IsGenericType: true } baseType
                && baseType.GetGenericTypeDefinition() == typeof(SqlMapper.TypeHandler<>))
            .Select(type => (
                IdType: type.BaseType!.GetGenericArguments()[0],
                PrimitiveType: PrimitiveOf(type.BaseType!.GetGenericArguments()[0]),
                Handler: Activator.CreateInstance(type)!))
            // 自定义基元不在本矩阵的职责内：这里锁的是「24 种内置基元 × 驱动返回的各种 CLR 形状」，
            // 样本值表按内置基元建立。自定义基元由 CustomPrimitiveIntegrationTests 单独覆盖。
            .Where(handler => IntegrationSampleValues.ByPrimitive.ContainsKey(handler.PrimitiveType));

    /// <summary>
    /// 本编译里生成的全部 EF Core 转换器：<c>StronglyTypedIds</c> 下继承
    /// <c>ValueConverter&lt;TId, TPrimitive&gt;</c> 的嵌套类。
    /// </summary>
    private static IEnumerable<(Type IdType, Type PrimitiveType, ValueConverter Converter)> EfCoreConverters()
        => typeof(StronglyTypedIds)
            .GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public)
            .Where(type => type.BaseType is { IsGenericType: true } baseType
                && baseType.GetGenericTypeDefinition() == typeof(ValueConverter<,>))
            .Select(type => (
                IdType: type.BaseType!.GetGenericArguments()[0],
                // provider 类型对自定义基元是 string，故基元类型必须从 Id 的接口读，不能取泛型实参。
                PrimitiveType: PrimitiveOf(type.BaseType!.GetGenericArguments()[0]),
                Converter: (ValueConverter)Activator.CreateInstance(type)!))
            // 同 DapperHandlers：自定义基元由 CustomPrimitiveIntegrationTests 覆盖。
            .Where(converter => IntegrationSampleValues.ByPrimitive.ContainsKey(converter.PrimitiveType));

    /// <summary>
    /// 从 <c>IStronglyTypedId&lt;TSelf, TPrimitiveId&gt;</c> 读出基元类型。
    /// </summary>
    private static Type PrimitiveOf(Type idType)
        => idType
            .GetInterfaces()
            .Single(@interface => @interface.IsGenericType
                && @interface.GetGenericTypeDefinition() == typeof(IStronglyTypedId<,>))
            .GetGenericArguments()[1];

    /// <summary>
    /// 按 Id 的 <c>Create</c> 静态方法构造实例（不直接 <c>new</c>，以免绕过验证器）。
    /// </summary>
    private static object CreateId(Type idType, object value)
    {
        var primitiveType = PrimitiveOf(idType);
        var create = idType
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "Create"
                && method.GetParameters() is [{ ParameterType: var parameterType }]
                && parameterType == primitiveType);

        return create.Invoke(null, [value])!;
    }

    private static object? DefaultOf(Type idType) => idType.IsValueType ? Activator.CreateInstance(idType) : null;

    /// <summary>
    /// 读 Id 实际持有的基元值（归一化之后）。
    /// </summary>
    private static object? ValueOf(object id) => id.GetType().GetProperty("Value")!.GetValue(id);

    /// <summary>
    /// 模拟驱动真实返回的 CLR 类型：SQLite 的 INTEGER 是 <c>long</c>、REAL 是 <c>double</c>、
    /// TEXT 是 <c>string</c>，与基元本身的类型几乎从不一致。
    /// </summary>
    private static object ProviderShapedValue(Type primitiveType, object value)
        => primitiveType switch
        {
            _ when primitiveType == typeof(byte)
                || primitiveType == typeof(sbyte)
                || primitiveType == typeof(short)
                || primitiveType == typeof(ushort)
                || primitiveType == typeof(int)
                || primitiveType == typeof(uint)
                || primitiveType == typeof(long)
                || primitiveType == typeof(ulong)
                || primitiveType.IsEnum => Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture),
            _ when primitiveType == typeof(float)
                || primitiveType == typeof(double)
                || primitiveType == typeof(decimal) => Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)!,
        };

    /// <summary>
    /// 最小 <see cref="IDbDataParameter"/> 桩：生成的 <c>SetValue</c> 只写 <c>Value</c>。
    /// </summary>
    private sealed class StubParameter : IDbDataParameter
    {
        public DbType DbType { get; set; }

        public ParameterDirection Direction { get; set; }

        public bool IsNullable { get; set; }

        public int Size { get; set; }

        [AllowNull]
        public string SourceColumn { get; set; } = string.Empty;

        public bool SourceColumnNullMapping { get; set; }

        [AllowNull]
        public string ParameterName { get; set; } = string.Empty;

        public object? Value { get; set; }

        public byte Precision { get; set; }

        public byte Scale { get; set; }

        public DataRowVersion SourceVersion { get; set; }
    }
}
