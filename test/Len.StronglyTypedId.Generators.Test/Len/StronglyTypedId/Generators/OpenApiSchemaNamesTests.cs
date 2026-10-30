// xUnit1051：本文件中的 CSharpSyntaxTree.ParseText 均为测试内同步调用，取消令牌无意义，
// 统一在此文件禁用该测试框架分析器警告。
#pragma warning disable xUnit1051
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Len.StronglyTypedId.Generators;

/// <summary>
/// <see cref="OpenApiSchemaNames"/> 的单元测试：Microsoft.OpenApi 1.x / 2.x 两套类型名与字面量。
/// </summary>
/// <remarks>
/// 两个 OpenAPI 集成共用这张表，版本判定错了会让产物引用不存在的命名空间（1.x 写 <c>JsonSchemaType</c>
/// 或 2.x 写 <c>Models</c> 都是 CS0234）。生成器层用例只覆盖了 Guid / int 两种基元，
/// 这里把 <c>TypeLiteral</c> 的每个分支单独钉住，尤其是 <c>number</c> 与默认分支。
/// </remarks>
public class OpenApiSchemaNamesTests
{
    #region UsesVersion2：主版本判定

    [Fact]
    public void UsesVersion2_Should_ReturnTrue_WhenMicrosoftOpenApiMajorIsTwo()
    {
        var modules = Modules(("Microsoft.OpenApi.dll", "2.0.0.0"));

        OpenApiSchemaNames.UsesVersion2(modules).Should().BeTrue();
    }

    [Fact]
    public void UsesVersion2_Should_ReturnFalse_WhenMicrosoftOpenApiMajorIsOne()
    {
        var modules = Modules(("Microsoft.OpenApi.dll", "1.6.22.0"));

        OpenApiSchemaNames.UsesVersion2(modules).Should().BeFalse();
    }

    [Fact]
    public void UsesVersion2_Should_ReturnFalse_WhenMicrosoftOpenApiMissing()
    {
        var modules = Modules(("SomeOther.dll", "9.0.0.0"));

        OpenApiSchemaNames.UsesVersion2(modules).Should().BeFalse();
    }

    #endregion

    #region TypeLiteral：1.x 用字符串、2.x 用 JsonSchemaType 枚举

    /// <summary>
    /// 1.x 的 <c>schema.Type</c> 是字符串，直接回显类型关键字。
    /// </summary>
    [Theory]
    [InlineData(PrimitiveIdSchemaMap.String, "\"string\"")]
    [InlineData(PrimitiveIdSchemaMap.Integer, "\"integer\"")]
    [InlineData(PrimitiveIdSchemaMap.Number, "\"number\"")]
    public void TypeLiteral_Should_ReturnQuotedKeyword_ForVersion1(string type, string expected)
        => OpenApiSchemaNames.TypeLiteral(type, useVersion2: false).Should().Be(expected);

    /// <summary>
    /// 2.x 的 <c>schema.Type</c> 是 <c>JsonSchemaType</c> 枚举，三个关键字各有对应成员。
    /// </summary>
    [Theory]
    [InlineData(PrimitiveIdSchemaMap.String, "global::Microsoft.OpenApi.JsonSchemaType.String")]
    [InlineData(PrimitiveIdSchemaMap.Integer, "global::Microsoft.OpenApi.JsonSchemaType.Integer")]
    [InlineData(PrimitiveIdSchemaMap.Number, "global::Microsoft.OpenApi.JsonSchemaType.Number")]
    public void TypeLiteral_Should_ReturnEnumMember_ForVersion2(string type, string expected)
        => OpenApiSchemaNames.TypeLiteral(type, useVersion2: true).Should().Be(expected);

    /// <summary>
    /// 未知关键字在 2.x 下回退到 <c>String</c>：宁可给一个宽泛的 schema，也不要生成不存在的枚举成员。
    /// </summary>
    [Fact]
    public void TypeLiteral_Should_FallBackToString_ForUnknownKeywordInVersion2()
        => OpenApiSchemaNames.TypeLiteral("object", useVersion2: true)
            .Should().Be("global::Microsoft.OpenApi.JsonSchemaType.String");

    #endregion

    #region FormatLiteral：null 与带引号文本

    [Fact]
    public void FormatLiteral_Should_ReturnNullLiteral_WhenFormatIsNull()
        => OpenApiSchemaNames.FormatLiteral(null).Should().Be("null");

    [Fact]
    public void FormatLiteral_Should_ReturnQuotedText_WhenFormatIsPresent()
        => OpenApiSchemaNames.FormatLiteral("uuid").Should().Be("\"uuid\"");

    #endregion

    #region 类型名：两套命名空间

    [Theory]
    [InlineData(false, "global::Microsoft.OpenApi.Models.OpenApiSchema")]
    [InlineData(true, "global::Microsoft.OpenApi.OpenApiSchema")]
    public void ConcreteSchemaType_Should_UseVersionSpecificNamespace(bool useVersion2, string expected)
        => OpenApiSchemaNames.ConcreteSchemaType(useVersion2).Should().Be(expected);

    /// <summary>
    /// 1.x 没有 schema 接口，直接用具体类；2.x 的 <c>Properties</c> 值是接口，泛型不变。
    /// </summary>
    [Theory]
    [InlineData(false, "global::Microsoft.OpenApi.Models.OpenApiSchema")]
    [InlineData(true, "global::Microsoft.OpenApi.IOpenApiSchema")]
    public void SchemaInterfaceType_Should_UseVersionSpecificType(bool useVersion2, string expected)
        => OpenApiSchemaNames.SchemaInterfaceType(useVersion2).Should().Be(expected);

    [Theory]
    [InlineData(false, "global::Microsoft.OpenApi.Models.OpenApiOperation")]
    [InlineData(true, "global::Microsoft.OpenApi.OpenApiOperation")]
    public void OperationType_Should_UseVersionSpecificNamespace(bool useVersion2, string expected)
        => OpenApiSchemaNames.OperationType(useVersion2).Should().Be(expected);

    [Theory]
    [InlineData(false, "global::Microsoft.OpenApi.Models.OpenApiParameter")]
    [InlineData(true, "global::Microsoft.OpenApi.IOpenApiParameter")]
    public void ParameterType_Should_UseVersionSpecificType(bool useVersion2, string expected)
        => OpenApiSchemaNames.ParameterType(useVersion2).Should().Be(expected);

    [Theory]
    [InlineData(false, "string")]
    [InlineData(true, "global::Microsoft.OpenApi.JsonSchemaType")]
    public void SchemaTypeValueType_Should_UseVersionSpecificType(bool useVersion2, string expected)
        => OpenApiSchemaNames.SchemaTypeValueType(useVersion2).Should().Be(expected);

    [Theory]
    [InlineData(false, "\"object\"")]
    [InlineData(true, "global::Microsoft.OpenApi.JsonSchemaType.Object")]
    public void ObjectTypeLiteral_Should_UseVersionSpecificForm(bool useVersion2, string expected)
        => OpenApiSchemaNames.ObjectTypeLiteral(useVersion2).Should().Be(expected);

    #endregion

    #region 辅助

    private static System.Collections.Immutable.ImmutableArray<ModuleInfo> Modules(params (string Name, string Version)[] modules)
        => [.. modules.Select(module => new ModuleInfo(module.Name, Version.Parse(module.Version), null))];

    #endregion
}
