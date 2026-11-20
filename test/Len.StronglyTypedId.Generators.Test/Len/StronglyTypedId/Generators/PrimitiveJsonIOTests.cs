// xUnit1051：本文件中的 CSharpSyntaxTree.ParseText 均为测试内同步调用，取消令牌无意义，
// 统一在此文件禁用该测试框架分析器警告。
#pragma warning disable xUnit1051
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Len.StronglyTypedId.Generators;

/// <summary>
/// <see cref="PrimitiveJsonIo"/> 的单元测试：基元短名到 System.Text.Json 直写 / 直读表达式的映射。
/// </summary>
/// <remarks>
/// 返回 <see langword="null"/> 表示该基元必须委托给 <c>JsonSerializer</c>，调用方据此走另一条分支。
/// 两张表都按短名分派，漏一个分支的表现是「某种基元被静默委托」，产物仍能编译、只在运行期多一次反射。
/// </remarks>
public class PrimitiveJsonIOTests
{
    #region 写侧：直写表达式

    [Theory]
    [InlineData("Guid", "writer.WriteStringValue(primitiveId);")]
    [InlineData("string", "writer.WriteStringValue(primitiveId);")]
    [InlineData("char", "writer.WriteStringValue(primitiveId.ToString());")]
    [InlineData("byte", "writer.WriteNumberValue((int)primitiveId);")]
    [InlineData("sbyte", "writer.WriteNumberValue((int)primitiveId);")]
    [InlineData("short", "writer.WriteNumberValue((int)primitiveId);")]
    [InlineData("ushort", "writer.WriteNumberValue((int)primitiveId);")]
    [InlineData("int", "writer.WriteNumberValue(primitiveId);")]
    [InlineData("uint", "writer.WriteNumberValue(primitiveId);")]
    [InlineData("long", "writer.WriteNumberValue(primitiveId);")]
    [InlineData("ulong", "writer.WriteNumberValue(primitiveId);")]
    [InlineData("decimal", "writer.WriteNumberValue(primitiveId);")]
    [InlineData("float", "writer.WriteNumberValue(primitiveId);")]
    [InlineData("double", "writer.WriteNumberValue(primitiveId);")]
    [InlineData("DateTime", "writer.WriteStringValue(primitiveId);")]
    [InlineData("DateTimeOffset", "writer.WriteStringValue(primitiveId);")]
    public void TryGetWriteExpression_Should_ReturnDirectExpression_ForPrimitive(string primitive, string expected)
        => PrimitiveJsonIo.TryGetWriteExpression(CreateInfo(primitive)).Should().Be(expected);

    /// <summary>
    /// 没有直写形态的基元（<c>Int128</c> / <c>Half</c> / 时间跨度 / 大整数）返回 <see langword="null"/>，
    /// 由调用方委托给 <c>JsonSerializer</c>。
    /// </summary>
    [Theory]
    [InlineData("Int128")]
    [InlineData("UInt128")]
    [InlineData("Half")]
    [InlineData("TimeSpan")]
    [InlineData("DateOnly")]
    [InlineData("TimeOnly")]
    [InlineData("System.Numerics.BigInteger")]
    public void TryGetWriteExpression_Should_ReturnNull_ForPrimitiveWithoutDirectForm(string primitive)
        => PrimitiveJsonIo.TryGetWriteExpression(CreateInfo(primitive)).Should().BeNull();

    #endregion

    #region 读侧：直读表达式

    [Theory]
    [InlineData("Guid", "reader.GetGuid()")]
    [InlineData("string", "reader.GetString()!")]
    [InlineData("byte", "reader.GetByte()")]
    [InlineData("sbyte", "reader.GetSByte()")]
    [InlineData("short", "reader.GetInt16()")]
    [InlineData("ushort", "reader.GetUInt16()")]
    [InlineData("int", "reader.GetInt32()")]
    [InlineData("uint", "reader.GetUInt32()")]
    [InlineData("long", "reader.GetInt64()")]
    [InlineData("ulong", "reader.GetUInt64()")]
    [InlineData("decimal", "reader.GetDecimal()")]
    [InlineData("float", "reader.GetSingle()")]
    [InlineData("double", "reader.GetDouble()")]
    [InlineData("DateTime", "reader.GetDateTime()")]
    [InlineData("DateTimeOffset", "reader.GetDateTimeOffset()")]
    public void TryGetReadExpression_Should_ReturnDirectExpression_ForPrimitive(string primitive, string expected)
        => PrimitiveJsonIo.TryGetReadExpression(CreateInfo(primitive)).Should().Be(expected);

    [Theory]
    [InlineData("Int128")]
    [InlineData("Half")]
    [InlineData("TimeSpan")]
    [InlineData("DateOnly")]
    [InlineData("TimeOnly")]
    [InlineData("System.Numerics.BigInteger")]
    public void TryGetReadExpression_Should_ReturnNull_ForPrimitiveWithoutDirectForm(string primitive)
        => PrimitiveJsonIo.TryGetReadExpression(CreateInfo(primitive)).Should().BeNull();

    #endregion

    #region NumberHandling：字符串分支

    /// <summary>
    /// 有符号整数只接受前导符号：STJ 拒绝首尾空白与小数点，<c>NumberStyles.Integer</c> 会放行空白。
    /// </summary>
    [Theory]
    [InlineData("sbyte")]
    [InlineData("short")]
    [InlineData("int")]
    [InlineData("long")]
    public void StringReadPlan_Should_UseAllowLeadingSign_ForSignedIntegers(string primitive)
    {
        var plan = PrimitiveJsonIo.TryGetStringReadPlan(CreateInfo(primitive));

        plan.Should().NotBeNull();
        plan!.Value.ParseExpression.Should()
            .Be($"{primitive}.Parse(text, global::System.Globalization.NumberStyles.AllowLeadingSign, global::System.Globalization.CultureInfo.InvariantCulture)");
        plan.Value.InvalidCondition.Should().BeNull("整数没有 NaN / Infinity 这类越界形态");
    }

    /// <summary>
    /// 无符号整数连前导 <c>+</c> 都拒绝，故用 <c>NumberStyles.None</c>。
    /// </summary>
    [Theory]
    [InlineData("byte")]
    [InlineData("ushort")]
    [InlineData("uint")]
    [InlineData("ulong")]
    public void StringReadPlan_Should_UseNone_ForUnsignedIntegers(string primitive)
    {
        var plan = PrimitiveJsonIo.TryGetStringReadPlan(CreateInfo(primitive));

        plan.Should().NotBeNull();
        plan!.Value.ParseExpression.Should()
            .Be($"{primitive}.Parse(text, global::System.Globalization.NumberStyles.None, global::System.Globalization.CultureInfo.InvariantCulture)");
    }

    /// <summary>
    /// 浮点与 <c>decimal</c> 接受符号、小数点与指数；STJ 拒绝千位分隔符，故不含 <c>AllowThousands</c>。
    /// </summary>
    [Theory]
    [InlineData("decimal")]
    [InlineData("float")]
    [InlineData("double")]
    public void StringReadPlan_Should_UseFloatStyles_ForFractionalTypes(string primitive)
    {
        var plan = PrimitiveJsonIo.TryGetStringReadPlan(CreateInfo(primitive));

        plan.Should().NotBeNull();
        plan!.Value.ParseExpression.Should().Contain("NumberStyles.AllowLeadingSign")
            .And.Contain("NumberStyles.AllowDecimalPoint")
            .And.Contain("NumberStyles.AllowExponent")
            .And.NotContain("AllowThousands");
    }

    /// <summary>
    /// <c>Parse</c> 认小写 <c>nan</c>、也把溢出折成无穷；STJ 两者都拒绝，故浮点必须带后置校验。
    /// </summary>
    [Theory]
    [InlineData("float")]
    [InlineData("double")]
    public void StringReadPlan_Should_GuardSpecialFloats(string primitive)
    {
        var plan = PrimitiveJsonIo.TryGetStringReadPlan(CreateInfo(primitive));

        plan!.Value.InvalidCondition.Should().NotBeNull()
            .And.Contain("IsNaN")
            .And.Contain("IsInfinity")
            .And.Contain("\"NaN\" or \"Infinity\" or \"-Infinity\"");
    }

    /// <summary>
    /// 枚举、自定义基元与 <c>BigInteger</c> 不生成字符串分支：前两者不受 <c>NumberHandling</c> 影响
    /// （STJ 的枚举转换器完全不看它），后者本来就是文本形。
    /// </summary>
    [Fact]
    public void StringReadPlan_Should_BeNull_ForExcludedPrimitives()
    {
        PrimitiveJsonIo.TryGetStringReadPlan(CreateEnumInfo("int")).Should().BeNull();
        PrimitiveJsonIo.TryGetStringReadPlan(CreateInfo("Email", customPrimitiveSource: true)).Should().BeNull();
        PrimitiveJsonIo.TryGetStringReadPlan(CreateInfo("System.Numerics.BigInteger")).Should().BeNull();
    }

    /// <summary>
    /// <c>Guid</c> / <c>string</c> / 日期类不受 <c>NumberHandling</c> 影响，产物必须保持原样。
    /// </summary>
    [Theory]
    [InlineData("Guid")]
    [InlineData("string")]
    [InlineData("DateTime")]
    [InlineData("DateTimeOffset")]
    [InlineData("char")]
    public void StringReadPlan_Should_BeNull_ForNonNumericPrimitives(string primitive)
    {
        PrimitiveJsonIo.TryGetStringReadPlan(CreateInfo(primitive)).Should().BeNull();
        PrimitiveJsonIo.TryGetStringWriteExpression(CreateInfo(primitive)).Should().BeNull();
    }

    /// <summary>
    /// 写侧必须用 <c>WriteRawValue</c>：<c>WriteStringValue</c> 会把 <c>+</c> 转义成 <c>\u002B</c>，
    /// 与 STJ 数值路径写出的裸 <c>"1E+300"</c> 不逐字节相同。
    /// </summary>
    [Theory]
    [InlineData("byte")]
    [InlineData("sbyte")]
    [InlineData("short")]
    [InlineData("ushort")]
    [InlineData("int")]
    [InlineData("uint")]
    [InlineData("long")]
    [InlineData("ulong")]
    [InlineData("decimal")]
    [InlineData("float")]
    [InlineData("double")]
    public void StringWriteExpression_Should_UseWriteRawValue_ForNumericPrimitives(string primitive)
    {
        PrimitiveJsonIo.TryGetStringWriteExpression(CreateInfo(primitive)).Should()
            .Be("writer.WriteRawValue(\"\\\"\" + primitiveId.ToString(global::System.Globalization.CultureInfo.InvariantCulture) + \"\\\"\");");
    }

    #endregion

    #region 枚举：按底层类型分派

    /// <summary>
    /// <c>long</c> / <c>ulong</c> 底层枚举必须按 64 位写出：转成 <c>int</c> 会截断高位。
    /// </summary>
    [Theory]
    [InlineData("long", "writer.WriteNumberValue((long)primitiveId);", "reader.GetInt64()")]
    [InlineData("ulong", "writer.WriteNumberValue((ulong)primitiveId);", "reader.GetUInt64()")]
    [InlineData("int", "writer.WriteNumberValue((int)primitiveId);", "reader.GetInt32()")]
    [InlineData("byte", "writer.WriteNumberValue((int)primitiveId);", "reader.GetByte()")]
    [InlineData("sbyte", "writer.WriteNumberValue((int)primitiveId);", "reader.GetSByte()")]
    [InlineData("short", "writer.WriteNumberValue((int)primitiveId);", "reader.GetInt16()")]
    [InlineData("ushort", "writer.WriteNumberValue((int)primitiveId);", "reader.GetUInt16()")]
    [InlineData("uint", "writer.WriteNumberValue((uint)primitiveId);", "reader.GetUInt32()")]
    public void Enum_Should_DispatchByUnderlyingType(string underlying, string write, string read)
    {
        var info = CreateEnumInfo(underlying);

        PrimitiveJsonIo.TryGetWriteExpression(info).Should().Be(write);
        PrimitiveJsonIo.TryGetReadExpression(info).Should().Be($"(global::Probe.Access){read}");
    }

    #endregion

    #region 自定义基元：一律委托

    /// <summary>
    /// 自定义基元没有可枚举的 JSON 形态，读写都返回 <see langword="null"/>，由调用方走规范文本形。
    /// </summary>
    [Fact]
    public void CustomPrimitive_Should_ReturnNull_ForBothDirections()
    {
        var info = CreateInfo("Email", customPrimitiveSource: true);

        info.IsCustomPrimitive.Should().BeTrue("测试前提：Email 必须被判成自定义基元");
        PrimitiveJsonIo.TryGetWriteExpression(info).Should().BeNull();
        PrimitiveJsonIo.TryGetReadExpression(info).Should().BeNull();
    }

    #endregion

    #region 辅助

    private static StronglyTypedIdInfo CreateInfo(string primitive, bool customPrimitiveSource = false)
        => new(GetIdType(primitive, customPrimitiveSource));

    private static StronglyTypedIdInfo CreateEnumInfo(string underlying)
        => new(GetIdType("Access", enumUnderlying: underlying));

    /// <summary>
    /// 在合成编译里取出 Id 类型符号。源码必须真正编译通过，否则接口 / 构造函数绑定失败会让用例退化成「什么都没测」。
    /// </summary>
    private static INamedTypeSymbol GetIdType(string primitive, bool customPrimitiveSource = false, string? enumUnderlying = null)
    {
        var source = enumUnderlying is null
            ? customPrimitiveSource ? CustomPrimitiveSource : IdSource(primitive)
            : EnumSource(enumUnderlying);

        var compilation = CSharpCompilation.Create(
            "Probe",
            [CSharpSyntaxTree.ParseText(source)],
            [
                AssemblyReferenceCache.FromAssembly(typeof(object).Assembly),
                AssemblyReferenceCache.FromAssembly(typeof(System.Numerics.BigInteger).Assembly),
            ],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var type = compilation.GetTypeByMetadataName("Probe.OrderId");

        type.Should().NotBeNull("测试前提：OrderId 必须能被解析成符号");

        return type!;
    }

    private static string IdSource(string primitive)
        => $$"""
            namespace Probe;

            public partial record struct OrderId({{primitive}} Value);
            """;

    private static string EnumSource(string underlying)
        => $$"""
            namespace Probe;

            public enum Access : {{underlying}}
            {
                None = 0,
                Read = 1,
            }

            public partial record struct OrderId(Access Value);
            """;

    private const string CustomPrimitiveSource = """
        using System;

        namespace Probe;

        public readonly record struct Email(string Value) : IParsable<Email>, IComparable<Email>, IComparable
        {
            public static Email Parse(string s, IFormatProvider? provider) => new(s);

            public static bool TryParse(string? s, IFormatProvider? provider, out Email result)
            {
                result = new Email(s ?? string.Empty);
                return s is not null;
            }

            public int CompareTo(Email other) => string.CompareOrdinal(Value, other.Value);

            public int CompareTo(object? obj) => obj is Email other ? CompareTo(other) : throw new ArgumentException();
        }

        public partial record struct OrderId(Email Value);
        """;

    #endregion
}
