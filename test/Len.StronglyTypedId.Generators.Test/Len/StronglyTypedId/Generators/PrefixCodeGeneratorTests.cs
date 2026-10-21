// xUnit1051：本文件中的 CSharpSyntaxTree.ParseText / Emit 等均为测试内同步调用，取消令牌无意义，
// 统一在此文件禁用该测试框架分析器警告。
#pragma warning disable xUnit1051
using System.Reflection;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Len.StronglyTypedId.Generators;

/// <summary>
/// C10（文本前缀）的生成器产物契约：核心生成器的 <c>ToString</c> / <c>TryParse</c> / 低层格式化，
/// 以及 STJ / Newtonsoft 的读写路径在有前缀时的形态。
/// </summary>
/// <remarks>
/// <para>
/// 本文件同时守住一条<b>回归护栏</b>：无前缀的 Id 产物里不得出现任何前缀管线
/// （<c>StartsWith</c> / 前缀字面量），即「opt-in 之外逐字节不变」。
/// </para>
/// <para>
/// 前缀进入规范文本形（<c>ToString</c> 前置、<c>Parse</c> / <c>TryParse</c> 剥离），
/// 因此 MVC 绑定 / 路由约束 / <c>TypeConverter</c> 等经文本路径的集成<b>零改动</b>即自动生效。
/// </para>
/// </remarks>
public class PrefixCodeGeneratorTests
{
    #region 辅助：驱动生成器并捕获产物

    private static CSharpCompilation CreateCompilation(string sourceCode, params MetadataReference[] extraReferences)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);

        var seedAssemblies = new[]
        {
            typeof(StronglyTypedIdAttribute).Assembly,
            typeof(System.Text.Json.JsonSerializer).Assembly,
            typeof(Newtonsoft.Json.JsonSerializer).Assembly,
        };

        var references = AppDomain.CurrentDomain
            .GetAssemblies()
            .Union(seedAssemblies)
            .Distinct()
            .Where(assembly => !assembly.IsDynamic)
            .Where(ShouldReferenceAssembly)
            .Select(assembly => MetadataReference.CreateFromFile(assembly.Location))
            .Cast<MetadataReference>()
            .Concat(extraReferences)
            .ToArray();

        return CSharpCompilation.Create(
            "Len.StronglyTypedId.Prefix.Test",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static Dictionary<string, string> GetGeneratedCode(string sourceCode, params MetadataReference[] extraReferences)
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

    private static bool ShouldReferenceAssembly(Assembly assembly)
        => assembly.GetName().Name is not
            ("Len.StronglyTypedId.TestBase"
            or "Microsoft.EntityFrameworkCore"
            or "Swashbuckle.AspNetCore.SwaggerGen"
            or "Microsoft.OpenApi"
            or "Microsoft.AspNetCore.Mvc"
            or "Microsoft.AspNetCore.Mvc.Core"
            or "Microsoft.AspNetCore.Mvc.Abstractions"
            or "Microsoft.AspNetCore.OpenApi"
            or "Microsoft.AspNetCore.Routing"
            or "Dapper");

    private const string PrefixIdSource = """
        using System;

        namespace Len.StronglyTypedId;

        [StronglyTypedId(Prefix = "ord_")]
        public partial record struct OrderId(Guid Value);
        """;

    private const string StrictPrefixIdSource = """
        using System;

        namespace Len.StronglyTypedId;

        [StronglyTypedId(Prefix = "ord_", PrefixReadMode = PrefixReadMode.Strict)]
        public partial record struct OrderId(Guid Value);
        """;

    private const string PlainIdSource = """
        using System;

        namespace Len.StronglyTypedId;

        [StronglyTypedId]
        public partial record struct OrderId(Guid Value);
        """;

    #endregion

    #region 核心生成器：ToString / TryParse / 低层格式化

    [Fact]
    public void Core_Should_PrependPrefix_InToString()
    {
        var generated = GetGeneratedCode(PrefixIdSource)["Len.StronglyTypedId.OrderId.g.cs"];

        generated.Should().Contain(
            """public override string ToString() => "ord_" + (Value.ToString(null, global::System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty);""");
    }

    /// <summary>
    /// 宽松读入：前缀可选，剥离若存在。
    /// </summary>
    [Fact]
    public void Core_Should_StripPrefixLeniently_InTryParse()
    {
        var generated = GetGeneratedCode(PrefixIdSource)["Len.StronglyTypedId.OrderId.g.cs"];

        generated.Should().Contain(
            """if (value is not null && value.StartsWith("ord_", global::System.StringComparison.Ordinal))""");
        generated.Should().Contain("value = value.Substring(4);");
        // span 重载同构。
        generated.Should().Contain(
            """if (value.StartsWith("ord_", global::System.StringComparison.Ordinal))""");
        generated.Should().Contain("value = value.Slice(4);");
    }

    /// <summary>
    /// 严格读入：缺前缀即失败（早退），随后才剥离。
    /// </summary>
    [Fact]
    public void Core_Should_RequirePrefix_UnderStrictRead()
    {
        var generated = GetGeneratedCode(StrictPrefixIdSource)["Len.StronglyTypedId.OrderId.g.cs"];

        generated.Should().Contain(
            """if (value is null || !value.StartsWith("ord_", global::System.StringComparison.Ordinal))""");
        generated.Should().Contain("value = value.Substring(4);");
    }

    /// <summary>
    /// 低层格式化成员在 <c>format</c> 为空时带前缀，保证 <c>$"{id}"</c> / 结构化日志与 <c>ToString()</c> 一致。
    /// </summary>
    [Fact]
    public void Core_Should_PrependPrefix_InLowLevelFormatting_WhenFormatIsEmpty()
    {
        var generated = GetGeneratedCode(PrefixIdSource)["Len.StronglyTypedId.OrderId.g.cs"];

        generated.Should().Contain(
            """global::System.String.IsNullOrEmpty(format) ? "ord_" + Value.ToString(format, formatProvider) : Value.ToString(format, formatProvider)""");
    }

    /// <summary>
    /// 回归护栏：无前缀的 Id 产物里不得出现任何前缀管线。
    /// </summary>
    [Fact]
    public void Core_Should_OmitPrefixPlumbing_WhenIdHasNoPrefix()
    {
        var generated = GetGeneratedCode(PlainIdSource)["Len.StronglyTypedId.OrderId.g.cs"];

        generated.Should().NotContain("StartsWith(");
        generated.Should().NotContain("\"ord_\"");
        generated.Should().Contain(
            """public override string ToString() => Value.ToString(null, global::System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;""");
    }

    #endregion

    #region STJ / Newtonsoft：读写改走规范文本形

    [Fact]
    public void SystemTextJson_Should_WriteAndReadViaCanonicalText_WhenIdHasPrefix()
    {
        var generated = GetGeneratedCode(PrefixIdSource)["Len.StronglyTypedId.OrderId.SystemTextJson.g.cs"];

        generated.Should().Contain("writer.WriteStringValue(value.ToString());");
        generated.Should().Contain("writer.WritePropertyName(value.ToString());");
        generated.Should().Contain(
            "OrderId.TryParse(text, global::System.Globalization.CultureInfo.InvariantCulture, out var id)");
        generated.Should().Contain(
            "OrderId.TryParse(propertyName, global::System.Globalization.CultureInfo.InvariantCulture, out var id)");
    }

    [Fact]
    public void Newtonsoft_Should_WriteAndReadViaCanonicalText_WhenIdHasPrefix()
    {
        var generated = GetGeneratedCode(PrefixIdSource)["Len.StronglyTypedId.OrderId.NewtonsoftJson.g.cs"];

        generated.Should().Contain("writer.WriteValue(value.ToString());");
        generated.Should().Contain(
            "OrderId.TryParse(text, global::System.Globalization.CultureInfo.InvariantCulture, out var id)");
    }

    /// <summary>
    /// 回归护栏：无前缀时 STJ 产物仍按基元值读写，不引入文本往返。
    /// </summary>
    [Fact]
    public void SystemTextJson_Should_KeepPrimitivePath_WhenIdHasNoPrefix()
    {
        var generated = GetGeneratedCode(PlainIdSource)["Len.StronglyTypedId.OrderId.SystemTextJson.g.cs"];

        generated.Should().NotContain("value.ToString()");
        generated.Should().Contain("global::System.Text.Json.JsonSerializer.Serialize(writer, primitiveId, options);");
    }

    #endregion
}
