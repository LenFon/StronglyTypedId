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
/// 集成产物是按 Id 逐个生成的，单点用例只覆盖被点名的那几个 Id。历史上 Dapper 的读取路径就因为
/// 只测了 <c>string</c> 基元，把「直接拆箱强转」的缺陷藏了很久（枚举列返回 <c>long</c>、
/// <c>char</c> / <c>Guid</c> 列返回 <c>string</c>、<c>decimal</c> 列返回 <c>double</c>）。
/// 这里的做法是反射枚举本编译里生成的全部处理器/转换器，按「驱动真实返回的形状」逐个喂值。
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
            parameter.Value.Should().Be(ValueOf(id), $"{idType.Name}：SetValue 必须把基元值交给参数");
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

    [Fact]
    public void EfCore_Converter_Should_RoundTripEveryPrimitive()
    {
        foreach (var (idType, primitiveType, converter) in EfCoreConverters())
        {
            var id = CreateId(idType, SampleValues[primitiveType]);

            // 同上：写出的必须是 Id 实际持有的值，归一化器可能已经改写过样本值。
            converter.ConvertToProvider(id).Should().Be(ValueOf(id), $"{idType.Name}：ConvertToProvider 必须写出基元值");
            converter.ConvertFromProvider(ValueOf(id)).Should().Be(id, $"{idType.Name}：ConvertFromProvider 必须还原 Id");
        }
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
                Handler: Activator.CreateInstance(type)!));

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
                PrimitiveType: type.BaseType!.GetGenericArguments()[1],
                Converter: (ValueConverter)Activator.CreateInstance(type)!));

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
