// xUnit1051：本文件中的 CSharpSyntaxTree.ParseText 均为测试内同步调用，取消令牌无意义，
// 统一在此文件禁用该测试框架分析器警告。
#pragma warning disable xUnit1051
using System.Reflection;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Len.StronglyTypedId.Generators;

/// <summary>
/// 生成器「模块版本闸门」的单元测试：三类生成器各按一个被引用程序集的主版本号决定是否启用。
/// </summary>
public class CodeGeneratorModuleGateTests
{
    #region EF Core：Microsoft.EntityFrameworkCore 主版本 >= 7

    [Fact]
    public void EfCore_Should_Generate_WhenEntityFrameworkCoreMajorVersionIsSeven()
    {
        var generated = RunGenerator(
            EfCoreSource,
            SyntheticAssembly.GetOrCreate("Microsoft.EntityFrameworkCore", "7.0.0.0"));

        generated.Should().ContainKey("Len.StronglyTypedId.OrderId.EntityFrameworkCore.g.cs");
        generated.Should().ContainKey("StronglyTypedIds.EntityFrameworkCore.g.cs");
    }

    [Fact]
    public void EfCore_Should_NotGenerate_WhenEntityFrameworkCoreMajorVersionIsSix()
    {
        var generated = RunGenerator(
            EfCoreSource,
            SyntheticAssembly.GetOrCreate("Microsoft.EntityFrameworkCore", "6.0.0.0"));

        generated.Keys.Should().NotContain(key => key.EndsWith(".EntityFrameworkCore.g.cs", StringComparison.Ordinal));
    }

    /// <summary>
    /// 闸门只看主版本：同为 7.x，带补丁号与不带补丁号都必须生成。
    /// </summary>
    [Fact]
    public void EfCore_Should_IgnoreMinorAndPatchVersion()
    {
        var generated = RunGenerator(
            EfCoreSource,
            SyntheticAssembly.GetOrCreate("Microsoft.EntityFrameworkCore", "7.42.13.7"));

        generated.Should().ContainKey("StronglyTypedIds.EntityFrameworkCore.g.cs");
    }

    #endregion

    #region Swagger：Swashbuckle.AspNetCore.SwaggerGen 主版本 >= 6

    [Fact]
    public void Swagger_Should_Generate_WhenSwashbuckleMajorVersionIsSix()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Swashbuckle.AspNetCore.SwaggerGen", "6.0.0.0"));

        generated.Should().ContainKey("StronglyTypedIds.Swagger.g.cs");
    }

    [Fact]
    public void Swagger_Should_NotGenerate_WhenSwashbuckleMajorVersionIsFive()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Swashbuckle.AspNetCore.SwaggerGen", "5.0.0.0"));

        generated.Keys.Should().NotContain("StronglyTypedIds.Swagger.g.cs");
    }

    #endregion

    #region Newtonsoft.Json：主版本 >= 13

    [Fact]
    public void NewtonsoftJson_Should_Generate_WhenMajorVersionIsThirteen()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Newtonsoft.Json", "13.0.0.0"));

        generated.Should().ContainKey("Len.StronglyTypedId.OrderId.NewtonsoftJson.g.cs");
    }

    /// <summary>
    /// Newtonsoft.Json 12.x 仍被广泛使用，但其 <c>JsonConverter&lt;T&gt;</c> 与 13.x 存在差异，生成器据此只在 13+ 产出转换器。阈值若被误写成
    /// 12，本用例即失败。
    /// </summary>
    [Fact]
    public void NewtonsoftJson_Should_NotGenerate_WhenMajorVersionIsTwelve()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Newtonsoft.Json", "12.0.0.0"));

        generated.Keys.Should().NotContain(key => key.EndsWith(".NewtonsoftJson.g.cs", StringComparison.Ordinal));
    }

    #endregion

    #region 闸门互不影响：低于阈值的模块不应连带关闭别的生成器

    [Fact]
    public void CoreGenerators_Should_StayEnabled_WhenOnlyLowVersionModulesReferenced()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Newtonsoft.Json", "12.0.0.0"),
            SyntheticAssembly.GetOrCreate("Swashbuckle.AspNetCore.SwaggerGen", "5.0.0.0"));

        // 核心与 System.Text.Json 产物与被排除的模块无关，必须照常产出。
        generated.Should().ContainKey("Len.StronglyTypedId.OrderId.g.cs");
        generated.Should().ContainKey("Len.StronglyTypedId.OrderId.SystemTextJson.g.cs");
    }

    #endregion

    #region Dapper：Dapper 主版本 >= 2

    [Fact]
    public void Dapper_Should_Generate_WhenDapperMajorVersionIsTwo()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Dapper", "2.0.0.0"));

        generated.Should().ContainKey("Len.StronglyTypedId.OrderId.Dapper.g.cs");
        generated.Should().ContainKey("StronglyTypedIds.Dapper.g.cs");
    }

    [Fact]
    public void Dapper_Should_NotGenerate_WhenDapperMajorVersionIsOne()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Dapper", "1.0.0.0"));

        generated.Keys.Should().NotContain(key => key.EndsWith(".Dapper.g.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void Dapper_Should_GenerateTypeHandlerAndApplyTo()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Dapper", "2.1.0.0"));

        generated["Len.StronglyTypedId.OrderId.Dapper.g.cs"]
            .Should().Contain("class OrderIdTypeHandler : global::Dapper.SqlMapper.TypeHandler<global::Len.StronglyTypedId.OrderId>")
            .And.Contain("public override void SetValue(global::System.Data.IDbDataParameter parameter, global::Len.StronglyTypedId.OrderId value)")
            .And.Contain("public override global::Len.StronglyTypedId.OrderId Parse(object? value)");

        generated["StronglyTypedIds.Dapper.g.cs"]
            .Should().Contain("public static void ApplyTo()")
            .And.Contain("public static void ApplyTo(global::System.Data.IDbConnection connection) => ApplyTo();")
            .And.Contain("global::Dapper.SqlMapper.AddTypeHandler");
    }

    /// <summary>
    /// 引用类型 Id（<c>partial record</c>）的 <c>SetValue</c> 也必须写出基元值。<c>DbParameter.Value</c> 只接受驱动认识的类型，
    /// 直接把 Id 包装对象塞进去会让 SqlClient / Sqlite 在绑定时抛「No mapping exists from object type ...」。
    /// 该分支此前正是这么写的，而测试矩阵里没有任何引用类型 Id 走过 Dapper 路径，缺陷因此长期未被发现。
    /// </summary>
    [Fact]
    public void Dapper_Should_UnwrapPrimitiveValue_ForReferenceTypeId()
    {
        var generated = RunGenerator(
            ClassIdSource,
            SyntheticAssembly.GetOrCreate("Dapper", "2.1.0.0"));

        generated["Len.StronglyTypedId.OrderClassId.Dapper.g.cs"]
            .Should().Contain("public override void SetValue(global::System.Data.IDbDataParameter parameter, global::Len.StronglyTypedId.OrderClassId? value)")
            .And.Contain("parameter.Value = value is null ? (object)global::System.DBNull.Value : value.Value;");
    }

    /// <summary>
    /// <c>Parse</c> 不能把 <c>object</c> 直接拆箱成基元：驱动返回的 CLR 类型常常不是基元本身
    /// （SQLite 下枚举列是 <c>long</c>、<c>char</c> / <c>Guid</c> 列是 <c>string</c>、<c>decimal</c> 列是 <c>double</c>），
    /// 裸强转会在读取时抛 <see cref="InvalidCastException"/>。本用例逐个基元锁定转换分支。
    /// </summary>
    [Fact]
    public void Dapper_Should_ConvertProviderShapedValues_InsteadOfUnboxing()
    {
        var generated = RunGenerator(
            MixedPrimitiveIdSource,
            SyntheticAssembly.GetOrCreate("Dapper", "2.1.0.0"));

        // 枚举：三条路径（直通 / 文本 / 数值）共用同一套 IsDefined 校验。
        generated["Len.StronglyTypedId.StatusId.Dapper.g.cs"]
            .Should().Contain("if (value is global::Len.StronglyTypedId.OrderStatus primitiveValue)")
            .And.Contain("if (value is string enumText)")
            .And.Contain("global::System.Enum.TryParse<global::Len.StronglyTypedId.OrderStatus>(enumText, ignoreCase: true, out var enumValue)")
            .And.Contain("&& global::System.Enum.IsDefined(typeof(global::Len.StronglyTypedId.OrderStatus), enumValue)")
            .And.Contain("return global::Len.StronglyTypedId.StatusId.Create(enumValue);")
            // 数值路径：先拒绝小数、再按底层类型做范围检查，最后才 Enum.ToObject + IsDefined。
            .And.Contain("if (numeric == global::System.Decimal.Truncate(numeric))")
            .And.Contain("global::System.Convert.ChangeType(numeric, typeof(int), global::System.Globalization.CultureInfo.InvariantCulture)")
            .And.Contain("global::System.Enum.ToObject(typeof(global::Len.StronglyTypedId.OrderStatus), underlyingValue)")
            .And.Contain("if (global::System.Enum.IsDefined(typeof(global::Len.StronglyTypedId.OrderStatus), enumValue))");

        // char：TEXT 列返回 string，IConvertible.ToChar 正好按「长度恰为 1」判定。
        generated["Len.StronglyTypedId.LetterId.Dapper.g.cs"]
            .Should().Contain("global::System.Convert.ToChar(value, global::System.Globalization.CultureInfo.InvariantCulture)");

        // decimal：REAL 列返回 double。
        generated["Len.StronglyTypedId.AmountId.Dapper.g.cs"]
            .Should().Contain("global::System.Convert.ToDecimal(value, global::System.Globalization.CultureInfo.InvariantCulture)");

        // Guid：TEXT 列返回 string，走 IParsable 的文本入口。
        generated["Len.StronglyTypedId.OrderId.Dapper.g.cs"]
            .Should().Contain("global::System.Guid.Parse(global::System.Convert.ToString(value, global::System.Globalization.CultureInfo.InvariantCulture)!, global::System.Globalization.CultureInfo.InvariantCulture)");

        // 任何基元都不许再出现「拆箱强转」形态。
        generated.Values.Should().NotContain(code => code.Contains(")value!)", StringComparison.Ordinal));
    }

    /// <summary>
    /// <c>Parse</c> 方法体必须与声明同列（8 空格），语句 12 空格、嵌套块 16 空格。
    /// 多行插值洞不会随模板重新缩进，此前产物里 <c>{</c> 在 12 列而语句在 4 列。
    /// </summary>
    [Fact]
    public void Dapper_ParseBody_Should_KeepConsistentIndentation()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Dapper", "2.1.0.0"));

        generated["Len.StronglyTypedId.OrderId.Dapper.g.cs"].Replace("\r\n", "\n")
            .Should().Contain("\n        public override global::Len.StronglyTypedId.OrderId Parse(object? value)\n        {\n            if (value is null or global::System.DBNull)\n            {\n                return default;\n            }\n");
    }

    /// <summary>
    /// 注册入口的方法体此前用制表符缩进，与其余生成物的 4 空格风格不一致。
    /// </summary>
    [Fact]
    public void Dapper_ApplyTo_Should_UseSpacesForIndentation()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Dapper", "2.1.0.0"));

        generated["StronglyTypedIds.Dapper.g.cs"].Replace("\r\n", "\n")
            .Should().Contain("\n    {\n        global::Dapper.SqlMapper.AddTypeHandler<global::Len.StronglyTypedId.OrderId>(new OrderIdTypeHandler());\n    }")
            .And.NotContain("\t");
    }

    #endregion

    #region Dapper：处理器名消歧与基元转换矩阵

    /// <summary>
    /// 两个命名空间的同名 Id 会撞处理器类名（都叫 <c>OrderIdTypeHandler</c>），此时必须用 <c>HintName</c> 消歧。
    /// </summary>
    [Fact]
    public void Dapper_Should_QualifyHandlerName_WhenNamesCollide()
    {
        var generated = RunGenerator(
            AmbiguousIdSource,
            SyntheticAssembly.GetOrCreate("Dapper", "2.1.0.0"));

        generated["Sales.OrderId.Dapper.g.cs"]
            .Should().Contain("class Sales_OrderIdTypeHandler");

        generated["Billing.OrderId.Dapper.g.cs"]
            .Should().Contain("class Billing_OrderIdTypeHandler");
    }

    /// <summary>
    /// 每个数值基元都必须映射到 <c>System.Convert</c> 上对应的重载：驱动返回的 CLR 类型与基元并不总是一致
    /// （SQLite 下整型列可能回 <c>long</c>、实数列回 <c>double</c>），漏一个就会在读取时抛
    /// <see cref="InvalidCastException"/>。本用例逐基元锁定映射表。
    /// </summary>
    [Fact]
    public void Dapper_Should_MapEveryNumericPrimitive_ToItsConvertOverload()
    {
        var generated = RunGenerator(
            AllConvertPrimitiveIdSource,
            SyntheticAssembly.GetOrCreate("Dapper", "2.1.0.0"));

        var expectations = new (string Id, string Method)[]
        {
            ("ByteId", "ToByte"),
            ("SByteId", "ToSByte"),
            ("Int16Id", "ToInt16"),
            ("UInt16Id", "ToUInt16"),
            ("UInt32Id", "ToUInt32"),
            ("Int64Id", "ToInt64"),
            ("UInt64Id", "ToUInt64"),
            ("SingleId", "ToSingle"),
            ("DoubleId", "ToDouble"),
            ("DateTimeId", "ToDateTime"),
        };

        foreach (var (id, method) in expectations)
        {
            generated[$"Len.StronglyTypedId.{id}.Dapper.g.cs"]
                .Should().Contain($"global::System.Convert.{method}(value, global::System.Globalization.CultureInfo.InvariantCulture)");
        }

        // TimeSpan 刻意不在映射表里：System.Convert 没有 ToTimeSpan，只能走文本入口。
        generated["Len.StronglyTypedId.DurationId.Dapper.g.cs"]
            .Should().Contain("global::System.TimeSpan.Parse(global::System.Convert.ToString(value, global::System.Globalization.CultureInfo.InvariantCulture)!, global::System.Globalization.CultureInfo.InvariantCulture)");
    }

    #endregion

    #region MessagePack：MessagePack 主版本 >= 2

    [Fact]
    public void MessagePack_Should_Generate_WhenMajorVersionIsTwo()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("MessagePack", "2.5.0.0"));

        generated.Should().ContainKey("Len.StronglyTypedId.OrderId.MessagePack.g.cs");
        generated.Should().ContainKey("StronglyTypedIds.MessagePack.g.cs");
    }

    [Fact]
    public void MessagePack_Should_NotGenerate_WhenMajorVersionIsOne()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("MessagePack", "1.9.0.0"));

        generated.Keys.Should().NotContain(key => key.EndsWith(".MessagePack.g.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void MessagePack_Should_GenerateFormatterAndApplyTo()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("MessagePack", "2.5.0.0"));

        generated["Len.StronglyTypedId.OrderId.MessagePack.g.cs"]
            .Should().Contain("internal sealed class OrderIdMessagePackFormatter : global::MessagePack.Formatters.IMessagePackFormatter<global::Len.StronglyTypedId.OrderId>")
            .And.Contain("public void Serialize(")
            .And.Contain("ref global::MessagePack.MessagePackWriter writer,")
            .And.Contain("public global::Len.StronglyTypedId.OrderId Deserialize(");

        generated["StronglyTypedIds.MessagePack.g.cs"]
            .Should().Contain("public static global::MessagePack.MessagePackSerializerOptions ApplyTo(global::MessagePack.MessagePackSerializerOptions options)")
            .And.Contain("options.WithResolver(global::MessagePack.Resolvers.CompositeResolver.Create(")
            .And.Contain("new OrderIdMessagePackFormatter(),")
            .And.Contain("new global::MessagePack.IFormatterResolver[] { options.Resolver }));");
    }

    /// <summary>
    /// 枚举基元必须由 <c>ApplyTo</c> 显式注册零反射格式化器：MessagePack 在 AOT（AvoidDynamicCode）下的
    /// StandardResolver 链不含任何枚举 resolver，不注册则枚举 Id 的值通道在 Native AOT 下直接抛
    /// 「formatter not found」（CI aot job 实证）。
    /// </summary>
    [Fact]
    public void MessagePack_Should_RegisterEnumFormatter_ForEnumPrimitive()
    {
        var generated = RunGenerator(
            MixedPrimitiveIdSource,
            SyntheticAssembly.GetOrCreate("MessagePack", "2.5.0.0"));

        generated["StronglyTypedIds.MessagePack.g.cs"]
            .Should().Contain("new global::MessagePack.Formatters.GenericEnumFormatter<global::Len.StronglyTypedId.OrderStatus>(),");
    }

    /// <summary>
    /// 没有枚举基元时不得出现枚举格式化器（枚举项按基元类型去重，只写一份）。
    /// </summary>
    [Fact]
    public void MessagePack_Should_NotRegisterEnumFormatter_WhenNoEnumPrimitive()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("MessagePack", "2.5.0.0"));

        generated["StronglyTypedIds.MessagePack.g.cs"]
            .Should().NotContain("new global::MessagePack.Formatters.GenericEnumFormatter");
    }

    /// <summary>
    /// 二进制载荷必须是基元本身（<c>value.Value</c>），不能是规范文本形：文本前缀只是文本形的装饰，
    /// 一旦混进二进制，「改前缀」就会让已落盘的数据读不回来。
    /// </summary>
    [Fact]
    public void MessagePack_Should_StorePrimitivePayload_InsteadOfCanonicalText()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("MessagePack", "2.5.0.0"));

        generated["Len.StronglyTypedId.OrderId.MessagePack.g.cs"]
            .Should().Contain("global::MessagePack.FormatterResolverExtensions.GetFormatterWithVerify<global::System.Guid>(options.Resolver).Serialize(ref writer, value.Value, options);")
            .And.Contain("var value = global::MessagePack.FormatterResolverExtensions.GetFormatterWithVerify<global::System.Guid>(options.Resolver).Deserialize(ref reader, options);")
            .And.Contain("if (global::Len.StronglyTypedId.OrderId.TryCreate(value, out var id))")
            .And.NotContain("writer.Write(value.ToString());");
    }

    /// <summary>
    /// 值类型 Id 读到 nil 说明载荷与契约不符（nil 只可能来自 <c>Nullable&lt;TId&gt;</c> 的写入）。
    /// 静默落成 default 会把「数据缺失」伪装成 <c>Guid.Empty</c> 这样的合法值。
    /// </summary>
    [Fact]
    public void MessagePack_Should_Throw_WhenValueTypeIdReadsNil()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("MessagePack", "2.5.0.0"));

        generated["Len.StronglyTypedId.OrderId.MessagePack.g.cs"]
            .Should().Contain("if (reader.TryReadNil())")
            .And.Contain("throw new global::MessagePack.MessagePackSerializationException(\"Cannot convert a nil MessagePack value to a OrderId.\");")
            .And.NotContain("return default!;");
    }

    /// <summary>
    /// 引用类型 Id 的 null 必须写成 nil：走到格式化器这一步已经没有别人替我们补 nil，
    /// 直接取 <c>value.Value</c> 会抛 <see cref="NullReferenceException"/>。
    /// </summary>
    [Fact]
    public void MessagePack_Should_WriteNil_ForNullReferenceTypeId()
    {
        var generated = RunGenerator(
            ClassIdSource,
            SyntheticAssembly.GetOrCreate("MessagePack", "2.5.0.0"));

        generated["Len.StronglyTypedId.OrderClassId.MessagePack.g.cs"]
            .Should().Contain("if (value is null)")
            .And.Contain("writer.WriteNil();")
            .And.Contain("return default!;");
    }

    /// <summary>
    /// <c>Int128</c> / <c>UInt128</c> / <c>BigInteger</c> 没有 MessagePack 的原生格式化器，
    /// 直接按基元写会在运行期抛「格式化器未注册」，故退回规范文本形。
    /// </summary>
    [Fact]
    public void MessagePack_Should_FallBackToCanonicalText_ForBinaryTextPrimitive()
    {
        var generated = RunGenerator(
            BinaryTextIdSource,
            SyntheticAssembly.GetOrCreate("MessagePack", "2.5.0.0"));

        generated["Len.StronglyTypedId.BigId.MessagePack.g.cs"]
            .Should().Contain("writer.Write(value.ToString());")
            .And.Contain("var text = reader.ReadString();")
            .And.Contain("if (text is not null && global::Len.StronglyTypedId.BigId.TryParse(text, global::System.Globalization.CultureInfo.InvariantCulture, out var id))")
            .And.Contain("throw new global::MessagePack.MessagePackSerializationException(\"Cannot convert the MessagePack value to a BigId.\");");
    }

    /// <summary>
    /// 两个不同命名空间的同名 Id 会撞格式化器类名（都叫 <c>OrderIdMessagePackFormatter</c>），
    /// 此时必须用 <c>HintName</c> 消歧。
    /// </summary>
    [Fact]
    public void MessagePack_Should_QualifyFormatterName_WhenNamesCollide()
    {
        var generated = RunGenerator(
            AmbiguousIdSource,
            SyntheticAssembly.GetOrCreate("MessagePack", "2.5.0.0"));

        generated["Sales.OrderId.MessagePack.g.cs"]
            .Should().Contain("internal sealed class Sales_OrderIdMessagePackFormatter");

        generated["Billing.OrderId.MessagePack.g.cs"]
            .Should().Contain("internal sealed class Billing_OrderIdMessagePackFormatter");
    }

    /// <summary>
    /// 一个 Id 都没有时不产出空壳装配入口，与 Dapper / JSON 的注册入口保持一致。
    /// </summary>
    [Fact]
    public void MessagePack_Should_NotGenerate_WhenNoIds()
    {
        var generated = RunGenerator(
            NoIdSource,
            SyntheticAssembly.GetOrCreate("MessagePack", "2.5.0.0"));

        generated.Keys.Should().NotContain("StronglyTypedIds.MessagePack.g.cs");
    }

    #endregion

    #region MongoDB.Bson：主版本 >= 3

    [Fact]
    public void MongoDb_Should_Generate_WhenMajorVersionIsThree()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("MongoDB.Bson", "3.2.0.0"));

        generated.Should().ContainKey("Len.StronglyTypedId.OrderId.MongoDb.g.cs");
        generated.Should().ContainKey("StronglyTypedIds.MongoDb.g.cs");
    }

    /// <summary>
    /// driver 2.x 的 <c>SerializerBase&lt;T&gt;</c> 与 3.x 的抽象成员签名不同，闸门据此只认 3+。
    /// </summary>
    [Fact]
    public void MongoDb_Should_NotGenerate_WhenMajorVersionIsTwo()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("MongoDB.Bson", "2.19.0.0"));

        generated.Keys.Should().NotContain(key => key.EndsWith(".MongoDb.g.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void MongoDb_Should_GenerateSerializerAndRegistration()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("MongoDB.Bson", "3.2.0.0"));

        generated["Len.StronglyTypedId.OrderId.MongoDb.g.cs"]
            .Should().Contain("internal sealed class OrderIdBsonSerializer : global::MongoDB.Bson.Serialization.Serializers.SerializerBase<global::Len.StronglyTypedId.OrderId>")
            .And.Contain("public override void Serialize(")
            .And.Contain("global::MongoDB.Bson.Serialization.BsonSerializationContext context,")
            .And.Contain("public override global::Len.StronglyTypedId.OrderId Deserialize(");

        generated["StronglyTypedIds.MongoDb.g.cs"]
            .Should().Contain("public static void RegisterBsonSerializers()")
            .And.Contain("global::MongoDB.Bson.Serialization.BsonSerializer.TryRegisterSerializer(new OrderIdBsonSerializer());");
    }

    /// <summary>
    /// 基元序列化器必须由 <c>RegisterBsonSerializers</c> 以默认配置显式预注册：BSON 的
    /// <c>PrimitiveSerializationProvider</c> 靠反射创建 <c>Int32Serializer</c> / <c>EnumSerializer&lt;T&gt;</c> 等实例，
    /// Native AOT 下未被静态引用的构造器被裁剪，创建直接抛 MissingMethodException（CI aot job 实证）。
    /// 文本形基元不走 <c>LookupSerializer</c>，不参与注册；Guid 因「3.0 起无全局默认表示」刻意排除。
    /// </summary>
    [Fact]
    public void MongoDb_Should_RegisterPrimitiveSerializers_ForBinaryPrimitives()
    {
        var generated = RunGenerator(
            MixedPrimitiveIdSource,
            SyntheticAssembly.GetOrCreate("MongoDB.Bson", "3.2.0.0"));

        generated["StronglyTypedIds.MongoDb.g.cs"]
            .Should().Contain("global::MongoDB.Bson.Serialization.BsonSerializer.TryRegisterSerializer(typeof(global::Len.StronglyTypedId.OrderStatus), new global::MongoDB.Bson.Serialization.Serializers.EnumSerializer<global::Len.StronglyTypedId.OrderStatus>());")
            .And.Contain("TryRegisterSerializer(typeof(char), new global::MongoDB.Bson.Serialization.Serializers.CharSerializer());")
            .And.Contain("TryRegisterSerializer(typeof(decimal), new global::MongoDB.Bson.Serialization.Serializers.DecimalSerializer());")
            .And.Contain("TryRegisterSerializer(typeof(string), new global::MongoDB.Bson.Serialization.Serializers.StringSerializer());")
            // Guid 由应用自选表示，生成产物不得替它注册（remarks 里的文字说明不算注册语句）。
            .And.NotContain("TryRegisterSerializer(typeof(global::System.Guid)");
    }

    /// <summary>
    /// BSON 的根序列化上下文会无条件取 <c>BsonDefaults.DynamicArraySerializer</c>（<c>List&lt;object&gt;</c>）与
    /// <c>BsonDefaults.DynamicDocumentSerializer</c>（<c>ExpandoObject</c>），这两个属性首次读取时去注册表查，
    /// provider 靠反射创建 → Native AOT 下抛 MissingMethodException。故必须预注册这三个动态类型，且顺序固定
    /// （<c>object</c> → <c>List&lt;object&gt;</c> → <c>ExpandoObject</c>：后者的元素序列化器取自前者）。
    /// </summary>
    [Fact]
    public void MongoDb_Should_RegisterDynamicTypes_ForRootContext()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("MongoDB.Bson", "3.2.0.0"));

        generated["StronglyTypedIds.MongoDb.g.cs"]
            .Should().Contain("TryRegisterBsonSerializer(typeof(object), new global::MongoDB.Bson.Serialization.Serializers.ObjectSerializer());")
            .And.Contain("TryRegisterBsonSerializer(typeof(global::System.Collections.Generic.List<object>), new global::MongoDB.Bson.Serialization.Serializers.EnumerableInterfaceImplementerSerializer<global::System.Collections.Generic.List<object>, object>());")
            .And.Contain("TryRegisterBsonSerializer(typeof(global::System.Dynamic.ExpandoObject), new global::MongoDB.Bson.Serialization.Serializers.ExpandoObjectSerializer());")
            .And.Contain("private static void TryRegisterBsonSerializer(global::System.Type type, global::MongoDB.Bson.Serialization.IBsonSerializer serializer)");
    }

    /// <summary>
    /// 没有可预注册的基元（仅 Guid / 文本形）时，注册入口不得出现基元注册语句。
    /// </summary>
    [Fact]
    public void MongoDb_Should_NotRegisterPrimitiveSerializers_WhenNothingToRegister()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("MongoDB.Bson", "3.2.0.0"));

        generated["StronglyTypedIds.MongoDb.g.cs"]
            .Should().NotContain("TryRegisterSerializer(typeof(");
    }

    /// <summary>
    /// 载荷同样是裸基元：<c>LookupSerializer&lt;T&gt;</c> 直接写 <c>value.Value</c>。
    /// </summary>
    [Fact]
    public void MongoDb_Should_StorePrimitivePayload_InsteadOfCanonicalText()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("MongoDB.Bson", "3.2.0.0"));

        generated["Len.StronglyTypedId.OrderId.MongoDb.g.cs"]
            .Should().Contain("global::MongoDB.Bson.Serialization.BsonSerializer.LookupSerializer<global::System.Guid>()")
            .And.Contain("    .Serialize(context, args, value.Value);")
            .And.Contain("var value = global::MongoDB.Bson.Serialization.BsonSerializer.LookupSerializer<global::System.Guid>().Deserialize(context, args);")
            .And.Contain("if (global::Len.StronglyTypedId.OrderId.TryCreate(value, out var id))")
            .And.NotContain("context.Writer.WriteString(value.ToString());");
    }

    [Fact]
    public void MongoDb_Should_Throw_WhenValueTypeIdReadsNull()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("MongoDB.Bson", "3.2.0.0"));

        generated["Len.StronglyTypedId.OrderId.MongoDb.g.cs"]
            .Should().Contain("if (context.Reader.CurrentBsonType == global::MongoDB.Bson.BsonType.Null)")
            .And.Contain("context.Reader.ReadNull();")
            .And.Contain("throw new global::MongoDB.Bson.BsonSerializationException(\"Cannot convert a BSON null to a OrderId.\");")
            .And.NotContain("return default!;");
    }

    [Fact]
    public void MongoDb_Should_WriteNull_ForNullReferenceTypeId()
    {
        var generated = RunGenerator(
            ClassIdSource,
            SyntheticAssembly.GetOrCreate("MongoDB.Bson", "3.2.0.0"));

        generated["Len.StronglyTypedId.OrderClassId.MongoDb.g.cs"]
            .Should().Contain("if (value is null)")
            .And.Contain("context.Writer.WriteNull();")
            .And.Contain("return default!;");
    }

    [Fact]
    public void MongoDb_Should_FallBackToCanonicalText_ForBinaryTextPrimitive()
    {
        var generated = RunGenerator(
            BinaryTextIdSource,
            SyntheticAssembly.GetOrCreate("MongoDB.Bson", "3.2.0.0"));

        generated["Len.StronglyTypedId.BigId.MongoDb.g.cs"]
            .Should().Contain("context.Writer.WriteString(value.ToString());")
            .And.Contain("var text = context.Reader.ReadString();")
            .And.Contain("if (global::Len.StronglyTypedId.BigId.TryParse(text, global::System.Globalization.CultureInfo.InvariantCulture, out var id))")
            .And.Contain("throw new global::MongoDB.Bson.BsonSerializationException(\"Cannot convert the BSON string to a BigId.\");");
    }

    [Fact]
    public void MongoDb_Should_QualifySerializerName_WhenNamesCollide()
    {
        var generated = RunGenerator(
            AmbiguousIdSource,
            SyntheticAssembly.GetOrCreate("MongoDB.Bson", "3.2.0.0"));

        generated["Sales.OrderId.MongoDb.g.cs"]
            .Should().Contain("internal sealed class Sales_OrderIdBsonSerializer");

        generated["Billing.OrderId.MongoDb.g.cs"]
            .Should().Contain("internal sealed class Billing_OrderIdBsonSerializer");
    }

    [Fact]
    public void MongoDb_Should_NotGenerate_WhenNoIds()
    {
        var generated = RunGenerator(
            NoIdSource,
            SyntheticAssembly.GetOrCreate("MongoDB.Bson", "3.2.0.0"));

        generated.Keys.Should().NotContain("StronglyTypedIds.MongoDb.g.cs");
    }

    #endregion

    #region Microsoft.AspNetCore.OpenApi：主版本 >= 9

    [Fact]
    public void AspNetCoreOpenApi_Should_Generate_WhenMajorVersionIsNine()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Microsoft.AspNetCore.OpenApi", "9.0.0.0"));

        generated.Should().ContainKey("StronglyTypedIds.AspNetCoreOpenApi.g.cs");
    }

    [Fact]
    public void AspNetCoreOpenApi_Should_NotGenerate_WhenMajorVersionIsEight()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Microsoft.AspNetCore.OpenApi", "8.0.0.0"));

        generated.Keys.Should().NotContain("StronglyTypedIds.AspNetCoreOpenApi.g.cs");
    }

    /// <summary>
    /// 8.0 线上没有 schema 转换器 API（<c>IOpenApiSchemaTransformer</c> / <c>OpenApiOptions</c> 都是 9.0.0
    /// 才引入的），OpenAPI 文档由 Swashbuckle 产出。本用例锁定这条替代路径：同一个编译里
    /// OpenApi 8.0 + Swashbuckle 6 时，只产出 Swagger 入口，不产出 OpenAPI 入口，否则消费者会拿到
    /// 一份引用 8.0 里不存在类型的产物，编译直接 CS0246。
    /// </summary>
    [Fact]
    public void AspNetCoreOpenApi_Should_FallBackToSwagger_WhenMajorVersionIsEight()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Microsoft.AspNetCore.OpenApi", "8.0.0.0"),
            SyntheticAssembly.GetOrCreate("Swashbuckle.AspNetCore.SwaggerGen", "6.0.0.0"));

        generated.Should().ContainKey("StronglyTypedIds.Swagger.g.cs");
        generated.Keys.Should().NotContain("StronglyTypedIds.AspNetCoreOpenApi.g.cs");
    }

    /// <summary>
    /// 8.0 线的每一个补丁版本都必须落在闸门外：闸门看主版本，8.0.0 与 8.0.32 行为必须一致。
    /// </summary>
    [Theory]
    [InlineData("8.0.0.0")]
    [InlineData("8.0.32.0")]
    [InlineData("8.1.0.0")]
    public void AspNetCoreOpenApi_Should_NotGenerate_ForAnyEightVersion(string assemblyVersion)
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Microsoft.AspNetCore.OpenApi", assemblyVersion));

        generated.Keys.Should().NotContain("StronglyTypedIds.AspNetCoreOpenApi.g.cs");
    }

    [Fact]
    public void AspNetCoreOpenApi_Should_GenerateSchemaTransformer()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Microsoft.AspNetCore.OpenApi", "9.0.0.0"));

        generated["StronglyTypedIds.AspNetCoreOpenApi.g.cs"]
            .Should().Contain("class StronglyTypedIdOpenApiSchemaTransformer : global::Microsoft.AspNetCore.OpenApi.IOpenApiSchemaTransformer")
            .And.Contain("public static void ApplyTo(global::Microsoft.AspNetCore.OpenApi.OpenApiOptions options)")
            .And.Contain("[typeof(global::Len.StronglyTypedId.OrderId)] = (\"string\", \"uuid\", false),");
    }

    #endregion

    #region ASP.NET Core MVC：Microsoft.AspNetCore.Mvc 主版本 >= 2

    [Fact]
    public void AspNetCoreMvc_Should_Generate_WhenMajorVersionIsTwo()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Microsoft.AspNetCore.Mvc", "2.0.0.0"));

        generated.Should().ContainKey("StronglyTypedIds.AspNetCoreMvc.g.cs");
    }

    [Fact]
    public void AspNetCoreMvc_Should_NotGenerate_WhenMajorVersionIsOne()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Microsoft.AspNetCore.Mvc", "1.0.0.0"));

        generated.Keys.Should().NotContain("StronglyTypedIds.AspNetCoreMvc.g.cs");
    }

    [Fact]
    public void AspNetCoreMvc_Should_GenerateProviderAndApplyTo()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Microsoft.AspNetCore.Mvc", "2.1.0.0"));

        var code = generated["StronglyTypedIds.AspNetCoreMvc.g.cs"];

        code.Should().Contain("public static void ApplyTo(global::Microsoft.AspNetCore.Mvc.MvcOptions options)")
            .And.Contain("options.ModelBinderProviders.Insert(0, new StronglyTypedIdModelBinderProvider());")
            .And.Contain("public static void ApplyTo(global::Microsoft.AspNetCore.Routing.RouteOptions options)")
            .And.Contain("options.ConstraintMap[\"OrderId\"] = typeof(global::Len.StronglyTypedId.StronglyTypedIds.StronglyTypedIdRouteConstraint<global::Len.StronglyTypedId.OrderId>);")
            .And.Contain("private sealed class StronglyTypedIdModelBinderProvider")
            .And.Contain("private sealed class StronglyTypedIdModelBinder<TId>")
            .And.Contain("private sealed class StronglyTypedIdRouteConstraint<TId>");
    }

    #endregion

    #region System.Text.Json / Newtonsoft.Json 全局注册入口

    /// <summary>
    /// STJ 侧此前只有逐 Id 的 <c>[JsonConverter]</c>，没有与 EF / Swagger / Dapper / MVC / 内置 OpenAPI 对称的
    /// <c>ApplyTo</c>。本用例锁住聚合入口的存在与内容：注册的必须是各 Id 内嵌的转换器实例。
    /// </summary>
    [Fact]
    public void SystemTextJsonRegistration_Should_GenerateApplyTo()
    {
        var generated = RunGenerator(IdSource);

        generated.Should().ContainKey("StronglyTypedIds.SystemTextJson.g.cs");

        generated["StronglyTypedIds.SystemTextJson.g.cs"]
            .Should().Contain("internal static partial class StronglyTypedIds")
            .And.Contain("public static void ApplyTo(global::System.Text.Json.JsonSerializerOptions options)")
            .And.Contain("if (registered.Add(typeof(global::Len.StronglyTypedId.OrderId.SystemTextJsonConverter)))")
            .And.Contain("options.Converters.Add(new global::Len.StronglyTypedId.OrderId.SystemTextJsonConverter());");
    }

    /// <summary>
    /// Newtonsoft 侧的入口沿用 <c>sample1</c> 里曾被注释掉的 <c>AddStronglyTypedId()</c> 形态： <c>JsonSerializerSettings</c>
    /// 上的扩展方法。
    /// </summary>
    [Fact]
    public void NewtonsoftJsonRegistration_Should_GenerateExtensionMethod()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Newtonsoft.Json", "13.0.0.0"));

        generated.Should().ContainKey("StronglyTypedIds.NewtonsoftJson.g.cs");

        generated["StronglyTypedIds.NewtonsoftJson.g.cs"]
            .Should().Contain("internal static partial class StronglyTypedIds")
            .And.Contain("public static void AddStronglyTypedId(this global::Newtonsoft.Json.JsonSerializerSettings settings)")
            .And.Contain("if (registered.Add(typeof(global::Len.StronglyTypedId.OrderId.NewtonsoftJsonConverter)))")
            .And.Contain("settings.Converters.Add(new global::Len.StronglyTypedId.OrderId.NewtonsoftJsonConverter());");
    }

    /// <summary>
    /// 引用程序集里的 Id 若没有内嵌 <c>SystemTextJsonConverter</c>（声明它的程序集没引用 System.Text.Json），注册入口必须跳过它。否则生成
    /// <c>new X.SystemTextJsonConverter()</c> 会 CS0426，而这条编译错误出现在<b>消费者</b>的编译里，库作者完全看不到。
    /// </summary>
    [Fact]
    public void SystemTextJsonRegistration_Should_Skip_WhenReferencedIdHasNoConverter()
    {
        var generated = RunGenerator(
            IdSource,
            CreateReferencedAssembly("AlreadyGenerated", ReferencedIdWithoutConvertersSource));

        var code = generated["StronglyTypedIds.SystemTextJson.g.cs"];

        code.Should().Contain("new global::Len.StronglyTypedId.OrderId.SystemTextJsonConverter()")
            .And.NotContain("LegacyOrderId");
    }

    /// <summary>
    /// Newtonsoft 侧的同一判据：领域库不引 Newtonsoft、宿主应用引，是完全正常的分层。
    /// </summary>
    /// <remarks>
    /// 领域库里的 Id 没有内嵌转换器，而内嵌转换器只能生成在声明 Id 的程序集里，消费方补一份会撞 CS0426。
    /// 宿主应用于是自己生成一份顶层转换器并注册，否则该 Id 在 Newtonsoft 下会被写成对象形态，
    /// 而 System.Text.Json 写的是字符串。
    /// </remarks>
    [Fact]
    public void NewtonsoftJsonRegistration_Should_ProvideExternalConverter_WhenReferencedIdHasNoConverter()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Newtonsoft.Json", "13.0.0.0"),
            CreateReferencedAssembly("AlreadyGenerated", ReferencedIdWithoutConvertersSource));

        var code = generated["StronglyTypedIds.NewtonsoftJson.g.cs"];

        code.Should().Contain("new global::Len.StronglyTypedId.OrderId.NewtonsoftJsonConverter()")
            .And.Contain("new global::Len.StronglyTypedId.StronglyTypedIds.LegacyOrderIdNewtonsoftJsonConverter()");

        generated["Already.Generated.LegacyOrderId.NewtonsoftJsonExternal.g.cs"].Should()
            .Contain("internal sealed class LegacyOrderIdNewtonsoftJsonConverter");
    }

    /// <summary>
    /// 注册入口与核心生成器共用同一程序集门控：Newtonsoft 12.x 既不产出转换器，也不产出注册入口。
    /// </summary>
    [Fact]
    public void NewtonsoftJsonRegistration_Should_NotGenerate_WhenMajorVersionIsTwelve()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Newtonsoft.Json", "12.0.0.0"));

        generated.Keys.Should().NotContain("StronglyTypedIds.NewtonsoftJson.g.cs");
    }

    /// <summary>
    /// 一个 Id 都没有时不产出空壳注册类：否则会平白多出一个没有任何成员的 <c>StronglyTypedIds</c>。
    /// </summary>
    [Fact]
    public void Registrations_Should_NotGenerate_WhenNoIds()
    {
        var generated = RunGenerator(NoIdSource);

        generated.Keys.Should().NotContain("StronglyTypedIds.SystemTextJson.g.cs");
        generated.Keys.Should().NotContain("StronglyTypedIds.NewtonsoftJson.g.cs");
    }

    #endregion

    #region 基元类型解析：多契约与构造顺序

    /// <summary>
    /// 一个类型同时实现多份 <c>IStronglyTypedId&lt;,&gt;</c> 时，必须选 <c>TSelf</c> 等于自身的那个接口。
    /// </summary>
    /// <remarks>
    /// 运行时侧 <c>StronglyTypedIdExtensions.ResolvePrimitiveIdType</c> 就是这么选的（并有
    /// <c>MultiContractId</c> 用例守着）。生成器若取 <c>FirstOrDefault</c>，结果会取决于接口枚举顺序，
    /// 与模型绑定器 / EF 转换器选中的基元不一致，同一个类型两套答案。
    /// </remarks>
    [Fact]
    public void PrimitiveResolution_Should_PreferOwnContract_WhenTypeImplementsSeveral()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Dapper", "2.1.0.0"),
            CreateReferencedAssembly("MultiContract", MultiContractIdSource));

        // 自身契约是 Guid；外来契约是 int。选错会让 Dapper 处理器按 int 生成。
        generated.Should().ContainKey("MultiContract.MultiContractId.Dapper.g.cs");
        generated["MultiContract.MultiContractId.Dapper.g.cs"]
            .Should().Contain("if (value is global::System.Guid primitiveValue)")
            .And.NotContain("if (value is int primitiveValue)");
    }

    /// <summary>
    /// 没有主构造函数、只有手写构造的 record：合成的拷贝构造会排在 <c>Constructors[0]</c>，
    /// 按 <c>Constructors[0].Parameters[0]</c> 取基元会绑到 record 自身，导致静默不生成。
    /// </summary>
    /// <remarks>
    /// 这种写法同时会被分析器判为 <c>STIA0005</c>（必须有单参数主构造函数），所以用户不会真的卡住；
    /// 但生成器不该依赖「主构造恰好排第一」这个实现细节。
    /// </remarks>
    [Fact]
    public void PrimitiveResolution_Should_SkipCopyConstructor_WhenNoPrimaryConstructor()
    {
        var generated = RunGenerator(HandWrittenConstructorIdSource);

        generated.Should().ContainKey("Len.StronglyTypedId.HandCtorId.g.cs");
        generated["Len.StronglyTypedId.HandCtorId.g.cs"]
            .Should().Contain("public static HandCtorId Create(global::System.Guid value)");
    }

    #endregion

    #region 辅助

    private const string IdSource = """
        using System;

        namespace Len.StronglyTypedId;

        [StronglyTypedId]
        public partial record struct OrderId(Guid Value);
        """;

    /// <summary>
    /// 手写的多契约类型：同时实现 <c>IStronglyTypedId&lt;ForeignId, int&gt;</c> 与
    /// <c>IStronglyTypedId&lt;MultiContractId, Guid&gt;</c>。自身契约是 Guid，外来契约是 int。
    /// </summary>
    /// <remarks>
    /// 刻意把外来契约写在前面，让「取第一个」与「取自身契约」得到不同答案。
    /// </remarks>
    private const string MultiContractIdSource = """
        namespace MultiContract;

        public readonly record struct ForeignId(int Value);

        public readonly struct MultiContractId
            : global::Len.StronglyTypedId.IStronglyTypedId<ForeignId, int>,
              global::Len.StronglyTypedId.IStronglyTypedId<MultiContractId, global::System.Guid>
        {
            public MultiContractId(global::System.Guid value) => Value = value;

            public global::System.Guid Value { get; }

            int global::Len.StronglyTypedId.IStronglyTypedId<ForeignId, int>.Value => 0;

            public static MultiContractId Create(global::System.Guid value) => new(value);

            static ForeignId global::Len.StronglyTypedId.IStronglyTypedId<ForeignId, int>.Create(int value)
                => new(value);
        }
        """;

    /// <summary>
    /// 没有主构造函数、只有手写构造的 record：合成的拷贝构造会排在 <c>Constructors[0]</c>。
    /// </summary>
    private const string HandWrittenConstructorIdSource = """
        using System;

        namespace Len.StronglyTypedId;

        [StronglyTypedId]
        public partial record HandCtorId
        {
            public HandCtorId(Guid value) => Value = value;

            public Guid Value { get; }
        }
        """;

    /// <summary>
    /// 引用类型 Id（<c>partial record</c>）：用于验证 Dapper 处理器对引用类型分支的产物形态。
    /// </summary>
    private const string ClassIdSource = """
        using System;

        namespace Len.StronglyTypedId;

        [StronglyTypedId]
        public partial record OrderClassId(Guid Value);
        """;

    /// <summary>
    /// 混合基元源码：覆盖 Dapper <c>Parse</c> 的每一条转换分支，枚举（底层整数 / 成员名文本）、
    /// <c>char</c>（string）、<c>decimal</c>（double）、<c>Guid</c>（string）、<c>string</c>。
    /// </summary>
    private const string MixedPrimitiveIdSource = """
        using System;

        namespace Len.StronglyTypedId;

        public enum OrderStatus
        {
            Unknown,
            Active,
        }

        [StronglyTypedId]
        public partial record struct StatusId(OrderStatus Value);

        [StronglyTypedId]
        public partial record struct LetterId(char Value);

        [StronglyTypedId]
        public partial record struct AmountId(decimal Value);

        [StronglyTypedId]
        public partial record struct OrderId(Guid Value);

        [StronglyTypedId]
        public partial record struct Sku(string Value);
        """;

    /// <summary>
    /// 一个强类型 Id 都没有的源码：用来验证「无 Id 时不产出空壳注册类」。
    /// </summary>
    private const string NoIdSource = """
        namespace Len.StronglyTypedId;

        public class NotAnId
        {
        }
        """;

    /// <summary>
    /// 缺少 MessagePack / BSON 原生表示的基元（<c>Int128</c>）：两种二进制集成都必须退回规范文本形。
    /// </summary>
    private const string BinaryTextIdSource = """
        using System;

        namespace Len.StronglyTypedId;

        [StronglyTypedId]
        public partial record struct BigId(Int128 Value);
        """;

    /// <summary>
    /// Dapper 的基元转换矩阵：每个数值基元一个 Id，外加一个刻意落在映射表之外的 <c>TimeSpan</c>。
    /// </summary>
    private const string AllConvertPrimitiveIdSource = """
        using System;

        namespace Len.StronglyTypedId;

        [StronglyTypedId]
        public partial record struct ByteId(byte Value);

        [StronglyTypedId]
        public partial record struct SByteId(sbyte Value);

        [StronglyTypedId]
        public partial record struct Int16Id(short Value);

        [StronglyTypedId]
        public partial record struct UInt16Id(ushort Value);

        [StronglyTypedId]
        public partial record struct UInt32Id(uint Value);

        [StronglyTypedId]
        public partial record struct Int64Id(long Value);

        [StronglyTypedId]
        public partial record struct UInt64Id(ulong Value);

        [StronglyTypedId]
        public partial record struct SingleId(float Value);

        [StronglyTypedId]
        public partial record struct DoubleId(double Value);

        [StronglyTypedId]
        public partial record struct DateTimeId(DateTime Value);

        [StronglyTypedId]
        public partial record struct DurationId(TimeSpan Value);
        """;

    /// <summary>
    /// 两个命名空间下的同名 Id：格式化器 / 序列化器的类名会撞车，必须靠 <c>HintName</c> 消歧。
    /// </summary>
    private const string AmbiguousIdSource = """
        using System;
        using Len.StronglyTypedId;

        namespace Sales
        {
            [StronglyTypedId]
            public partial record struct OrderId(Guid Value);
        }

        namespace Billing
        {
            [StronglyTypedId]
            public partial record struct OrderId(Guid Value);
        }
        """;

    /// <summary>
    /// 一个「已由生成器补齐接口实现、但没有内嵌任何 JSON 转换器」的强类型 Id，等价于「声明它的程序集既没引用 System.Text.Json 也没引用 Newtonsoft.Json」的产物。
    /// </summary>
    private const string ReferencedIdWithoutConvertersSource = """
        namespace Already.Generated;
        public readonly record struct LegacyOrderId(global::System.Guid Value)
            : global::Len.StronglyTypedId.IStronglyTypedId<LegacyOrderId, global::System.Guid>
        {
            public static LegacyOrderId Create(global::System.Guid value) => new(value);

            public static LegacyOrderId Parse(string value, global::System.IFormatProvider? provider)
                => new(global::System.Guid.Parse(value, provider));

            public static bool TryParse(
                string? value,
                global::System.IFormatProvider? provider,
                out LegacyOrderId result)
            {
                if (global::System.Guid.TryParse(value, provider, out var primitiveId))
                {
                    result = new LegacyOrderId(primitiveId);
                    return true;
                }

                result = default;
                return false;
            }
        }
        """;

    /// <summary>
    /// EF Core 闸门只看引用的 <c>Microsoft.EntityFrameworkCore</c> 主版本，与源码里是否重写
    /// <c>ConfigureConventions</c> 无关，因此示例只需一个 Id。
    /// </summary>
    private const string EfCoreSource = """
        using System;

        namespace Len.StronglyTypedId;

        [StronglyTypedId]
        public partial record struct OrderId(Guid Value);
        """;

    /// <summary>
    /// 测试宿主 AppDomain 里必然加载了这些包的真实版本（且都是满足闸门的版本）。它们必须排除在默认引用之外，只由用例通过 <see cref="SyntheticAssembly"/>
    /// 按需注入指定版本，否则同名程序集出现两份引用，闸门判定的将是真实版本而非用例指定的版本。
    /// </summary>
    private static readonly string[] _versionGatedAssemblies =
    [
        "Len.StronglyTypedId.TestBase",
        "Microsoft.EntityFrameworkCore",
        "Swashbuckle.AspNetCore.SwaggerGen",
        "Microsoft.OpenApi",
        "Newtonsoft.Json",
        "Microsoft.AspNetCore.Mvc",
        "Microsoft.AspNetCore.Mvc.Core",
        "Microsoft.AspNetCore.Mvc.Abstractions",
        "Microsoft.AspNetCore.OpenApi",
        "Microsoft.AspNetCore.Routing",
        "MessagePack",
        "MongoDB.Bson",
    ];

    private static Dictionary<string, string> RunGenerator(string sourceCode, params MetadataReference[] extraReferences)
    {
        var compilation = CreateCompilation(sourceCode, extraReferences);

        var result = CSharpGeneratorDriver
            .Create(new StronglyTypedIdGenerator())
            .RunGenerators(compilation)
            .GetRunResult();

        result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        return result.Results[0].GeneratedSources
            .ToDictionary(source => source.HintName, source => source.SourceText.ToString());
    }

    private static CSharpCompilation CreateCompilation(string sourceCode, params MetadataReference[] extraReferences)
        => CreateCompilation("Len.StronglyTypedId.ModuleGate.Test", sourceCode, extraReferences);

    /// <summary>
    /// 把一个源码片段编译成「被引用程序集」的引用（<see cref="CompilationReference"/>）。
    /// </summary>
    private static CompilationReference CreateReferencedAssembly(string assemblyName, string sourceCode)
        => (CompilationReference)CreateCompilation(assemblyName, sourceCode, []).ToMetadataReference();

    private static CSharpCompilation CreateCompilation(
        string assemblyName,
        string sourceCode,
        MetadataReference[] extraReferences)
    {
        // AppDomain.GetAssemblies() 只返回「已加载」的程序集，而 System.Text.Json 是延迟加载的：
        // 必须像下面这样显式引用一次，它才会出现在编译引用里，SystemTextJsonCodeGenerator 也才会被选入。
        var seed = new[]
        {
            typeof(StronglyTypedIdAttribute).Assembly,
            typeof(System.Text.Json.JsonSerializer).Assembly,
        };

        var references = AppDomain.CurrentDomain
            .GetAssemblies()
            .Union(seed)
            .Distinct()
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
            .Where(assembly => !_versionGatedAssemblies.Contains(assembly.GetName().Name, StringComparer.Ordinal))
            .Select(AssemblyReferenceCache.FromAssembly)
            .Concat(extraReferences)
            .ToArray();

        return CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(sourceCode)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    #endregion
}
