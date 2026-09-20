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
/// 分析器不再报 STIAO008，生成器却把该类型嵌进生成代码，最终在使用者项目里报出多条 CS0315
/// （该类型不满足泛型约束），错误指向生成文件而非使用者自己的源码。故必须同时校验命名空间。
/// </para>
/// </remarks>
public class SupportedPrimitiveTypesTests
{
    #region BCL 基元类型

    [Theory]
    [InlineData(SpecialType.System_String)]
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
        var type = EmptyCompilation.GetSpecialType(specialType);

        SupportedPrimitiveTypes.IsSupported(type).Should().BeTrue();
    }

    /// <summary>
    /// <see cref="System.Guid"/> 不是 C# 的 <see cref="SpecialType"/>，走的是「具名类型 + 命名空间」这一支。
    /// </summary>
    [Fact]
    public void IsSupported_Should_ReturnTrue_ForGuid()
    {
        var type = EmptyCompilation.GetTypeByMetadataName("System.Guid");

        type.Should().NotBeNull();
        SupportedPrimitiveTypes.IsSupported(type!).Should().BeTrue();
    }

    #endregion

    #region 不受支持的 BCL 类型

    [Theory]
    [InlineData(SpecialType.System_Boolean)]
    [InlineData(SpecialType.System_Char)]
    [InlineData(SpecialType.System_Decimal)]
    [InlineData(SpecialType.System_DateTime)]
    [InlineData(SpecialType.System_Object)]
    public void IsSupported_Should_ReturnFalse_ForUnsupportedBclType(SpecialType specialType)
    {
        var type = EmptyCompilation.GetSpecialType(specialType);

        SupportedPrimitiveTypes.IsSupported(type).Should().BeFalse();
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
        var type = EmptyCompilation.CreateArrayTypeSymbol(EmptyCompilation.GetSpecialType(SpecialType.System_Int32));

        SupportedPrimitiveTypes.IsSupported(type).Should().BeFalse();
    }

    #endregion

    #region 辅助

    private static readonly CSharpCompilation EmptyCompilation = CreateCompilation("namespace Probe { public class Marker { } }");

    private static CSharpCompilation CreateCompilation(string sourceCode)
        => CSharpCompilation.Create(
            "Probe",
            [CSharpSyntaxTree.ParseText(sourceCode)],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    #endregion
}
