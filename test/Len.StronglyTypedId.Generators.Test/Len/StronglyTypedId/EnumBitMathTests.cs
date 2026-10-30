// xUnit1051：本文件中的 CSharpSyntaxTree.ParseText 均为测试内同步调用，取消令牌无意义，
// 统一在此文件禁用该测试框架分析器警告。
#pragma warning disable xUnit1051
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Len.StronglyTypedId;

/// <summary>
/// <see cref="EnumBitMath"/> 的单元测试：位宽分派、常量按位展开与未定义位掩码。
/// </summary>
/// <remarks>
/// 这些判据被生成器与分析器共用，且各有两张按 <see cref="SpecialType"/> 分派的表。
/// 表里漏一个分支的表现是「某种底层类型的 <c>[Flags]</c> 永远拒绝合法位组合」，
/// 而 <see cref="FlagsEnumUnderlyingTypeTests"/> 只覆盖了生成产物里的可见形态，
/// 这里直接打底层函数，把每个分支单独钉住。
/// </remarks>
public class EnumBitMathTests
{
    #region GetIntegralBitWidth：八种整型 + 非整型

    [Theory]
    [InlineData(SpecialType.System_SByte, 8)]
    [InlineData(SpecialType.System_Byte, 8)]
    [InlineData(SpecialType.System_Int16, 16)]
    [InlineData(SpecialType.System_UInt16, 16)]
    [InlineData(SpecialType.System_Int32, 32)]
    [InlineData(SpecialType.System_UInt32, 32)]
    [InlineData(SpecialType.System_Int64, 64)]
    [InlineData(SpecialType.System_UInt64, 64)]
    public void GetIntegralBitWidth_Should_ReturnWidth_ForIntegralType(SpecialType type, int expected)
        => EnumBitMath.GetIntegralBitWidth(type).Should().Be(expected);

    /// <summary>
    /// 非整型返回 0：调用方据此判定「该枚举底层类型不可按位运算」，从而放弃位校验。
    /// </summary>
    [Theory]
    [InlineData(SpecialType.System_String)]
    [InlineData(SpecialType.System_Boolean)]
    [InlineData(SpecialType.System_Char)]
    [InlineData(SpecialType.System_Double)]
    [InlineData(SpecialType.System_Decimal)]
    [InlineData(SpecialType.System_DateTime)]
    public void GetIntegralBitWidth_Should_ReturnZero_ForNonIntegralType(SpecialType type)
        => EnumBitMath.GetIntegralBitWidth(type).Should().Be(0);

    #endregion

    #region ToBits：八种整型常量 + 非整型常量

    /// <summary>
    /// 每种整型常量都要按补码展开成 <see cref="ulong"/>：负值展开后高位全 1，由宽度掩码裁掉。
    /// </summary>
    [Theory]
    [InlineData((sbyte)-1, ulong.MaxValue)]
    [InlineData((byte)255, 255UL)]
    [InlineData((short)-1, ulong.MaxValue)]
    [InlineData((ushort)65535, 65535UL)]
    [InlineData(-1, ulong.MaxValue)]
    [InlineData(4294967295U, 4294967295UL)]
    [InlineData(-1L, ulong.MaxValue)]
    [InlineData(18446744073709551615UL, ulong.MaxValue)]
    public void ToBits_Should_ExpandIntegralConstant(object constant, ulong expected)
        => EnumBitMath.ToBits(constant).Should().Be(expected);

    /// <summary>
    /// 非整型常量（枚举成员理论上不会出现）返回 0，而不是抛异常：这条路径跑在诊断渲染与生成期，
    /// 抛出会把整条诊断带崩。
    /// </summary>
    [Fact]
    public void ToBits_Should_ReturnZero_ForNonIntegralConstant()
        => EnumBitMath.ToBits("not-a-number").Should().Be(0UL);

    #endregion

    #region GetUndefinedBitsMask：非枚举 / 非整型底层 / 全定义

    /// <summary>
    /// 非枚举类型没有底层类型，返回 <see langword="null"/> 表示「无需位校验」。
    /// </summary>
    [Fact]
    public void GetUndefinedBitsMask_Should_ReturnNull_ForNonEnumType()
    {
        var type = GetTypeSymbol("public struct NotAnEnum { }", "Probe.NotAnEnum");

        EnumBitMath.GetUndefinedBitsMask(type).Should().BeNull();
    }

    /// <summary>
    /// 非具名类型（数组）同样拿不到 <c>EnumUnderlyingType</c>，必须安全返回 <see langword="null"/>。
    /// </summary>
    [Fact]
    public void GetUndefinedBitsMask_Should_ReturnNull_ForNonNamedType()
    {
        var compilation = CreateCompilation("public class Marker { }");
        var arrayType = compilation.CreateArrayTypeSymbol(compilation.GetSpecialType(SpecialType.System_Int32));

        EnumBitMath.GetUndefinedBitsMask(arrayType).Should().BeNull();
    }

    /// <summary>
    /// 全部位都已定义时返回 <see langword="null"/>：调用方据此整条省掉位校验，
    /// 否则 <c>return</c> 之后的 <c>throw</c> 会变成不可达代码（CS0162）。
    /// </summary>
    [Fact]
    public void GetUndefinedBitsMask_Should_ReturnNull_WhenEveryBitIsDefined()
    {
        var type = GetTypeSymbol(
            """
            [System.Flags]
            public enum Access : byte
            {
                All = 0xFF,
            }
            """,
            "Probe.Access");

        EnumBitMath.GetUndefinedBitsMask(type).Should().BeNull();
    }

    /// <summary>
    /// 掩码按底层宽度裁剪：<c>sbyte</c> 的 <c>-1</c> 在 <see cref="ulong"/> 里是满位，
    /// 不裁剪会把整个掩码吃掉，导致任何取值都被判成「含未定义位」。
    /// </summary>
    [Fact]
    public void GetUndefinedBitsMask_Should_ClipToUnderlyingWidth()
    {
        var type = GetTypeSymbol(
            """
            [System.Flags]
            public enum Access : sbyte
            {
                None = 0,
                Read = 1,
                Write = 2,
            }
            """,
            "Probe.Access");

        // 低两位已定义，宽度 8 内其余位未定义：0xFC。
        EnumBitMath.GetUndefinedBitsMask(type).Should().Be(0xFCUL);
    }

    /// <summary>
    /// 64 位宽度的掩码不能写成 <c>(1UL &lt;&lt; 64) - 1</c>（移位计数按 64 取模，结果是 0）。
    /// </summary>
    [Fact]
    public void GetUndefinedBitsMask_Should_Handle64BitWidth()
    {
        var type = GetTypeSymbol(
            """
            [System.Flags]
            public enum Access : ulong
            {
                None = 0,
                Read = 1,
            }
            """,
            "Probe.Access");

        EnumBitMath.GetUndefinedBitsMask(type).Should().Be(0xFFFFFFFFFFFFFFFEUL);
    }

    #endregion

    #region HasFlagsAttribute

    [Fact]
    public void HasFlagsAttribute_Should_ReturnTrue_ForFlagsEnum()
    {
        var type = GetTypeSymbol(
            """
            [System.Flags]
            public enum Access { None = 0, Read = 1 }
            """,
            "Probe.Access");

        EnumBitMath.HasFlagsAttribute(type).Should().BeTrue();
    }

    [Fact]
    public void HasFlagsAttribute_Should_ReturnFalse_ForPlainEnum()
    {
        var type = GetTypeSymbol(
            """
            public enum Status { Active, Suspended }
            """,
            "Probe.Status");

        EnumBitMath.HasFlagsAttribute(type).Should().BeFalse();
    }

    #endregion

    #region 辅助

    /// <summary>
    /// 把片段包进 <c>Probe</c> 命名空间：各用例只写类型声明，元数据名统一以 <c>Probe.</c> 开头。
    /// </summary>
    private static CSharpCompilation CreateCompilation(string sourceCode)
        => CSharpCompilation.Create(
            "Probe",
            [CSharpSyntaxTree.ParseText("using System;\n\nnamespace Probe\n{\n" + sourceCode + "\n}")],
            [AssemblyReferenceCache.FromAssembly(typeof(object).Assembly)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    private static INamedTypeSymbol GetTypeSymbol(string sourceCode, string metadataName)
    {
        var type = CreateCompilation(sourceCode).GetTypeByMetadataName(metadataName);

        type.Should().NotBeNull($"测试前提：{metadataName} 必须能被解析成符号");

        return type!;
    }

    #endregion
}
