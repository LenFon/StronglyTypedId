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
