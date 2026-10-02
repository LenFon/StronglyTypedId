// xUnit1051：本文件中的 CSharpSyntaxTree.ParseText / Emit 等均为测试内同步调用，取消令牌无意义，
// 统一在此文件禁用该测试框架分析器警告。
#pragma warning disable xUnit1051
using System.IO;
using System.Reflection;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Len.StronglyTypedId.Generators;

/// <summary>
/// <see cref="AspNetCoreOpenApiCodeGenerator"/> 的单元测试：覆盖 <see cref="AspNetCoreOpenApiCodeGenerator.GetTypeLiteral"/>
/// 与 <see cref="AspNetCoreOpenApiCodeGenerator.GetFormatLiteral"/> 的全部基元类型分支（含默认分支）。
/// </summary>
/// <remarks>
/// <para>
/// 内置 OpenAPI 的闸门是 <c>HasModule("Microsoft.AspNetCore.OpenApi.dll", 9)</c>：仅 .NET 9+ 消费者引用
/// <c>Microsoft.AspNetCore.OpenApi</c>（主版本 ≥ 9）时才会生成。本机共享框架只含 ASP.NET Core 的运行/门面程序集，
/// 并不随附 <c>Microsoft.AspNetCore.OpenApi.dll</c>，NuGet 缓存里的也只到 8.x——直接用真实引用无法打开闸门。
/// 因此这里用 <see cref="SyntheticAssembly"/> 合成一个「名为 Microsoft.AspNetCore.OpenApi、版本 9.0.0.0、
/// 仅带占位类型」的程序集引用：它足以让模块闸门按主版本号判定为真，生成的产物是纯文本（不要求 OpenApi 类型可解析），
/// 故能稳定地把两个分支方法的每一个分支跑到位。
/// </para>
/// <para>
/// 生成产物引用的 <c>Microsoft.OpenApi.Models.OpenApiSchema</c> 等真实类型不在本机可用，故本文件只断言「生成文本形态」
/// 而不做 Emit 编译——文本形态一旦锁定，分支覆盖率即达成；编译期可解析性由真实 .NET 9+ 消费者的包引用背书。
/// </para>
/// </remarks>
public class AspNetCoreOpenApiCodeGeneratorTests
{
    #region 辅助：驱动生成器并捕获内置 OpenAPI 产物

    /// <summary>
    /// 合成「Microsoft.AspNetCore.OpenApi 9.0.0.0」占位程序集：名字 + 主版本号满足闸门判据，
    /// 但没有任何真实 API 表面（生成器只产文本，无需真实类型可解析）。
    /// </summary>
    private static MetadataReference OpenApi9Reference { get; }
        = SyntheticAssembly.GetOrCreate("Microsoft.AspNetCore.OpenApi", "9.0.0.0");

    private static CSharpCompilation CreateCompilation(string sourceCode, params MetadataReference[] extraReferences)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);

        // 种子运行时（提供 [StronglyTypedId] 与 IStronglyTypedId）与 System.Text.Json（核心 + STJ 生成器可用），
        // 但刻意不引入会触发 EF Core / Swagger / MVC 生成器的门控程序集，避免本用例被无关产物带偏。
        var seedAssemblies = new[]
        {
            typeof(StronglyTypedIdAttribute).Assembly,
            typeof(System.Text.Json.JsonSerializer).Assembly,
        };

        var references = AppDomain.CurrentDomain
            .GetAssemblies()
            .Union(seedAssemblies)
            .Distinct()
            .Where(a => !a.IsDynamic)
            .Where(ShouldReferenceAssembly)
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .Concat(extraReferences)
            .ToArray();

        return CSharpCompilation.Create(
            "Len.StronglyTypedId.AspNetCoreOpenApi.Test",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static string GetAspNetCoreOpenApiGeneratedCode(string sourceCode, params MetadataReference[] extraReferences)
    {
        var compilation = CreateCompilation(sourceCode, extraReferences);

        var result = CSharpGeneratorDriver
            .Create(new StronglyTypedIdGenerator())
            .RunGenerators(compilation)
            .GetRunResult();

        result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var openApi = result.Results[0].GeneratedSources
            .FirstOrDefault(g => g.HintName == "StronglyTypedIds.AspNetCoreOpenApi.g.cs");
        return openApi.SourceText?.ToString() ?? string.Empty;
    }

    /// <summary>
    /// 排除会触发其它生成器 / 污染发现结果的程序集；Microsoft.AspNetCore.OpenApi 由 <see cref="OpenApi9Reference"/>
    /// 显式注入，不在此排除之列。
    /// </summary>
    private static bool ShouldReferenceAssembly(Assembly assembly)
        => assembly.GetName().Name is not
            ("Len.StronglyTypedId.TestBase" or "Microsoft.EntityFrameworkCore" or "Swashbuckle.AspNetCore.SwaggerGen" or "Microsoft.OpenApi" or "Microsoft.AspNetCore.Mvc" or "Microsoft.AspNetCore.Mvc.Core" or "Microsoft.AspNetCore.Mvc.Abstractions" or "Microsoft.AspNetCore.OpenApi" or "Microsoft.AspNetCore.Routing" or "Newtonsoft.Json");

    #endregion

    #region 顺序属性

    [Fact]
    public void AspNetCoreOpenApi_Should_HaveOrderSix()
    {
        AspNetCoreOpenApiCodeGenerator.Instance.Order.Should().Be(6);
    }

    #endregion

    #region 闸门：引用 Microsoft.AspNetCore.OpenApi 9.x 时必须生成

    [Fact]
    public void AspNetCoreOpenApi_Should_Generate_WhenModuleGateOpen()
    {
        var code = """
            using System;

            namespace Len.StronglyTypedId;

            [StronglyTypedId]
            public partial record struct OrderId(Guid Value);
            """;

        var generated = GetAspNetCoreOpenApiGeneratedCode(code, OpenApi9Reference);

        generated.Should().NotBeEmpty();
        generated.Should().Contain("internal static partial class StronglyTypedIds");
        generated.Should().Contain("class StronglyTypedIdOpenApiSchemaTransformer : global::Microsoft.AspNetCore.OpenApi.IOpenApiSchemaTransformer");
    }

    [Fact]
    public void AspNetCoreOpenApi_Should_NotGenerate_WhenModuleGateClosed()
    {
        var code = """
            using System;

            namespace Len.StronglyTypedId;

            [StronglyTypedId]
            public partial record struct OrderId(Guid Value);
            """;

        // 不注入 OpenApi 引用 → 闸门关闭 → 不产生内置 OpenAPI 产物。
        var generated = GetAspNetCoreOpenApiGeneratedCode(code);

        generated.Should().BeEmpty();
    }

    #endregion

    #region 基元类型分支：GetTypeLiteral / GetFormatLiteral 全覆盖

    // 每个用例对应一对 (schema.Type, schema.Format) 字面量，覆盖 GetTypeLiteral 的 "string"/"integer" 两分支
    // 与 GetFormatLiteral 的 uuid/int32/int64/uint32/uint64/byte/sbyte/int16/uint16/null 全部分支。
    [Theory]
    [InlineData("Guid", "string", "uuid")]
    [InlineData("int", "integer", "int32")]
    [InlineData("long", "integer", "int64")]
    [InlineData("uint", "integer", "uint32")]
    [InlineData("ulong", "integer", "uint64")]
    [InlineData("byte", "integer", "byte")]
    [InlineData("sbyte", "integer", "sbyte")]
    [InlineData("short", "integer", "int16")]
    [InlineData("ushort", "integer", "uint16")]
    // string 不在显式 Format 列表内 → 命中默认分支，生成 (Type, null)。
    [InlineData("string", "string", null)]
    public void AspNetCoreOpenApi_Should_GenerateSchemaEntry_ForPrimitive(string primitive, string openApiType, string? format)
    {
        var code = $$"""
            using System;

            namespace Len.StronglyTypedId;

            [StronglyTypedId]
            public partial record struct OrderId({{primitive}} Value);
            """;

        var generated = GetAspNetCoreOpenApiGeneratedCode(code, OpenApi9Reference);

        generated.Should().NotBeEmpty();

        var entry = format is null
            ? $$"""[typeof(global::Len.StronglyTypedId.OrderId)] = ("{{openApiType}}", null),"""
            : $$"""[typeof(global::Len.StronglyTypedId.OrderId)] = ("{{openApiType}}", "{{format}}"),""";

        generated.Should().Contain(entry);
    }

    [Fact]
    public void AspNetCoreOpenApi_Should_GenerateOneEntryPerId()
    {
        var code = """
            using System;

            namespace Len.StronglyTypedId;

            [StronglyTypedId]
            public partial record struct OrderId(Guid Value);

            [StronglyTypedId]
            public partial record struct ProductId(int Value);
            """;

        var generated = GetAspNetCoreOpenApiGeneratedCode(code, OpenApi9Reference);

        generated.Should().Contain("[typeof(global::Len.StronglyTypedId.OrderId)] = (\"string\", \"uuid\"),");
        generated.Should().Contain("[typeof(global::Len.StronglyTypedId.ProductId)] = (\"integer\", \"int32\"),");
    }

    #endregion
}
