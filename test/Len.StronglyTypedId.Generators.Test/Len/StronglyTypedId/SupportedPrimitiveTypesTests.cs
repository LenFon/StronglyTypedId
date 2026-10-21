// xUnit1051：本文件中的 CSharpCompilation.Create 等为测试内同步调用，取消令牌无意义，
// 统一在此文件禁用该测试框架分析器警告。
#pragma warning disable xUnit1051
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Len.StronglyTypedId;

/// <summary>
/// <see cref="SupportedPrimitiveTypes.IsSupported"/> 的单元测试。
/// </summary>
/// <remarks>
/// <para>
/// 该判据由生成器与分析器共用，回答的是「这个类型算不算强类型 Id 的基元类型」。
/// </para>
/// <para>
/// 曾经只比简单名，于是使用者自定义的 <c>X.Guid</c> 也被当成 BCL 的 <see cref="System.Guid"/>：
/// 分析器不再报 STIA0008，生成器却把该类型嵌进生成代码，最终在使用者项目里报出多条 CS0315
/// （该类型不满足泛型约束），错误指向生成文件而非使用者自己的源码。故必须同时校验命名空间。
/// </para>
/// </remarks>
public class SupportedPrimitiveTypesTests
{
    #region BCL 基元类型

    [Theory]
    [InlineData(SpecialType.System_String)]
    [InlineData(SpecialType.System_Char)]
    [InlineData(SpecialType.System_Byte)]
    [InlineData(SpecialType.System_SByte)]
    [InlineData(SpecialType.System_Int16)]
    [InlineData(SpecialType.System_Int32)]
    [InlineData(SpecialType.System_Int64)]
    [InlineData(SpecialType.System_UInt16)]
    [InlineData(SpecialType.System_UInt32)]
    [InlineData(SpecialType.System_UInt64)]
    public void IsSupported_Should_ReturnTrue_ForBclSpecialType(SpecialType specialType)
    {
        var type = _emptyCompilation.GetSpecialType(specialType);

        SupportedPrimitiveTypes.IsSupported(type).Should().BeTrue();
    }

    /// <summary>
    /// <see cref="System.Guid"/> 不是 C# 的 <see cref="SpecialType"/>，走的是「具名类型 + 命名空间」这一支。
    /// </summary>
    [Fact]
    public void IsSupported_Should_ReturnTrue_ForGuid()
    {
        var type = _emptyCompilation.GetTypeByMetadataName("System.Guid");

        type.Should().NotBeNull();
        SupportedPrimitiveTypes.IsSupported(type!).Should().BeTrue();
    }

    /// <summary>
    /// 时间型、decimal、扩展整型、浮点、大整数等新基元（非 C# 的 <see cref="SpecialType"/>，
    /// 走「具名类型 + 命名空间」分支）。
    /// </summary>
    [Theory]
    [InlineData("System.DateTime")]
    [InlineData("System.DateTimeOffset")]
    [InlineData("System.TimeSpan")]
    [InlineData("System.Decimal")]
    [InlineData("System.DateOnly")]
    [InlineData("System.TimeOnly")]
    [InlineData("System.Single")]
    [InlineData("System.Double")]
    [InlineData("System.Half")]
    [InlineData("System.Int128")]
    [InlineData("System.UInt128")]
    [InlineData("System.Numerics.BigInteger")]
    public void IsSupported_Should_ReturnTrue_ForExtendedBclType(string metadataName)
    {
        var type = _emptyCompilation.GetTypeByMetadataName(metadataName);

        type.Should().NotBeNull($"测试前提：{metadataName} 必须可被合成编译解析");
        SupportedPrimitiveTypes.IsSupported(type!).Should().BeTrue();
    }

    /// <summary>
    /// 枚举（无论命名空间、无论底层类型）一律支持：强类型 Id 的基元可以是任意枚举。
    /// </summary>
    [Fact]
    public void IsSupported_Should_ReturnTrue_ForEnum()
    {
        var compilation = CreateCompilation("""
            namespace Probe.Fake;

            public enum Status
            {
                Active,
                Inactive
            }
            """);

        var type = compilation.GetTypeByMetadataName("Probe.Fake.Status");

        type.Should().NotBeNull("测试前提：枚举必须可被合成编译解析");
        type!.TypeKind.Should().Be(TypeKind.Enum);
        SupportedPrimitiveTypes.IsSupported(type).Should().BeTrue();
    }

    #endregion

    #region 不受支持的 BCL 类型

    [Theory]
    [InlineData(SpecialType.System_Boolean)]
    [InlineData(SpecialType.System_Object)]
    public void IsSupported_Should_ReturnFalse_ForUnsupportedBclType(SpecialType specialType)
    {
        var type = _emptyCompilation.GetSpecialType(specialType);

        SupportedPrimitiveTypes.IsSupported(type).Should().BeFalse();
    }

    #endregion

    #region UTF-8 解析须有公开的 TryParse 重载

    /// <summary>
    /// 生成代码按具体类型名调用 <c>TryParse(ReadOnlySpan&lt;byte&gt;, IFormatProvider, out T)</c>，
    /// 故只有暴露了该公开重载的基元才生成 UTF-8 解析。数值型 / <c>decimal</c> 都有；
    /// 时间型没有 UTF-8 语义（连 <c>IUtf8SpanParsable</c> 都不实现），<c>BigInteger</c> 则把它实现为
    /// 显式接口实现 —— 两者都拿不到公开重载，故都不生成。
    /// </summary>
    /// <remarks>
    /// <c>Guid</c> 单列：它的 UTF-8 公开入口是 .NET 10 才加的，两个目标框架下判定结果本就不同，
    /// 见 <see cref="HasPublicStaticTryParse_Should_DependOnTargetFramework_ForGuid"/>。
    /// </remarks>
    [Theory]
    [InlineData("System.Int32", true)]
    [InlineData("System.Decimal", true)]
    [InlineData("System.Int128", true)]
    [InlineData("System.Double", true)]
    [InlineData("System.Numerics.BigInteger", false)]
    [InlineData("System.DateTime", false)]
    [InlineData("System.DateOnly", false)]
    public void HasPublicStaticTryParse_Should_DetectUtf8Overload(string metadataName, bool expected)
    {
        var type = _emptyCompilation.GetTypeByMetadataName(metadataName);

        type.Should().NotBeNull($"测试前提：{metadataName} 必须可被合成编译解析");
        StronglyTypedIdInfo.HasPublicStaticTryParse(type!, SpecialType.System_Byte).Should().Be(expected,
            $"{metadataName} 的公开 UTF-8 TryParse 判定应为 {expected}");
    }

    /// <summary>
    /// <c>Guid</c> 的公开 UTF-8 <c>TryParse</c> 自 .NET 10 起才有：net8.0 消费者下判定为假，
    /// 生成产物因此不含 UTF-8 解析 —— 同一份源码在两个目标框架下的产物本就允许不同。
    /// </summary>
    [Fact]
    public void HasPublicStaticTryParse_Should_DependOnTargetFramework_ForGuid()
    {
        var type = _emptyCompilation.GetTypeByMetadataName("System.Guid");

        type.Should().NotBeNull("测试前提：System.Guid 必须可被合成编译解析");
#if NET10_0_OR_GREATER
        StronglyTypedIdInfo.HasPublicStaticTryParse(type!, SpecialType.System_Byte).Should().BeTrue(
            "net10.0 下 Guid 暴露了公开的 UTF-8 TryParse");
#else
        StronglyTypedIdInfo.HasPublicStaticTryParse(type!, SpecialType.System_Byte).Should().BeFalse(
            "net8.0 下 Guid 尚无公开的 UTF-8 TryParse");
#endif
    }

    /// <summary>
    /// 使用者自定义的 <c>Probe.Fake.Double</c> 与 BCL <c>System.Double</c> 同名，命名空间校验必须拦截它。
    /// </summary>
    [Fact]
    public void HasPublicStaticTryParse_Should_ReturnFalse_ForUserDefinedSameNameType()
    {
        var compilation = CreateCompilation("""
            namespace Probe.Fake;

            public struct Double
            {
            }
            """);

        var type = compilation.GetTypeByMetadataName("Probe.Fake.Double");

        type.Should().NotBeNull("测试前提：自定义同名类型必须可被合成编译解析");
        StronglyTypedIdInfo.HasPublicStaticTryParse(type!, SpecialType.System_Byte).Should().BeFalse(
            "自定义的同名 Double 不该被误判为具备公开的 UTF-8 TryParse");
    }

    #endregion

    #region 缺陷回归：与 BCL 基元同名、但不属于 System 命名空间的类型

    [Theory]
    [InlineData("Guid")]
    [InlineData("Int32")]
    [InlineData("String")]
    public void IsSupported_Should_ReturnFalse_ForUserDefinedTypeWithSameSimpleName(string simpleName)
    {
        var compilation = CreateCompilation($$"""
            namespace Probe.Fake;

            public struct {{simpleName}}
            {
            }
            """);

        var type = compilation.GetTypeByMetadataName($"Probe.Fake.{simpleName}");

        type.Should().NotBeNull($"测试前提：{simpleName} 必须是具名类型且与 BCL 基元同名");
        type!.Name.Should().Be(simpleName);
        SupportedPrimitiveTypes.IsSupported(type).Should().BeFalse();
    }

    /// <summary>
    /// 非具名类型（数组等）既没有 <c>ContainingNamespace</c>，也不可能承载强类型 Id。
    /// </summary>
    [Fact]
    public void IsSupported_Should_ReturnFalse_ForArrayType()
    {
        var type = _emptyCompilation.CreateArrayTypeSymbol(_emptyCompilation.GetSpecialType(SpecialType.System_Int32));

        SupportedPrimitiveTypes.IsSupported(type).Should().BeFalse();
    }

    #endregion

    #region 辅助

    private static readonly CSharpCompilation _emptyCompilation = CreateCompilation("namespace Probe { public class Marker { } }");

    private static CSharpCompilation CreateCompilation(string sourceCode)
        => CSharpCompilation.Create(
            "Probe",
            [CSharpSyntaxTree.ParseText(sourceCode)],
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                // BigInteger 不在 CoreLib，而在 System.Runtime.Numerics；扩展基元测试需要解析它。
                MetadataReference.CreateFromFile(typeof(System.Numerics.BigInteger).Assembly.Location),
            ],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    #endregion
}
