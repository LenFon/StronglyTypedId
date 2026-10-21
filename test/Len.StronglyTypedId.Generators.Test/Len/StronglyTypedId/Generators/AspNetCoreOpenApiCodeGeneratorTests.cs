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
/// <see cref="AspNetCoreOpenApiCodeGenerator"/> 的单元测试：覆盖 
/// <see cref="AspNetCoreOpenApiCodeGenerator.GetTypeLiteral"/> 与 
/// <see cref="AspNetCoreOpenApiCodeGenerator.GetFormatLiteral"/> 的全部基元类型分支（含默认分支）。
/// </summary>
public class AspNetCoreOpenApiCodeGeneratorTests
{
    #region 辅助：驱动生成器并捕获内置 OpenAPI 产物

    /// <summary>
    /// 合成「Microsoft.AspNetCore.OpenApi 9.0.0.0」占位程序集：名字 + 主版本号满足闸门判据，但没有任何真实 API 表面（生成器只产文本，无需真实类型可解析）。
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
            .Select(AssemblyReferenceCache.FromAssembly)
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
    /// 排除会触发其它生成器 / 污染发现结果的程序集；Microsoft.AspNetCore.OpenApi 由 <see cref="OpenApi9Reference"/> 显式注入，不在此排除之列。
    /// </summary>
    private static bool ShouldReferenceAssembly(Assembly assembly)
        => assembly.GetName().Name is not
            ("Len.StronglyTypedId.TestBase" or "Microsoft.EntityFrameworkCore" or "Swashbuckle.AspNetCore.SwaggerGen" or "Microsoft.OpenApi" or "Microsoft.AspNetCore.Mvc" or "Microsoft.AspNetCore.Mvc.Core" or "Microsoft.AspNetCore.Mvc.Abstractions" or "Microsoft.AspNetCore.OpenApi" or "Microsoft.AspNetCore.Routing" or "Newtonsoft.Json");

    #endregion

    #region 顺序属性

    [Fact]
    public void AspNetCoreOpenApi_Should_HaveOrderSix() => AspNetCoreOpenApiCodeGenerator.Instance.Order.Should().Be(6);

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
    [InlineData("string", "string", null)]
    [InlineData("char", "string", null)]
    [InlineData("byte", "integer", "byte")]
    [InlineData("sbyte", "integer", "sbyte")]
    [InlineData("short", "integer", "int16")]
    [InlineData("ushort", "integer", "uint16")]
    [InlineData("int", "integer", "int32")]
    [InlineData("uint", "integer", "uint32")]
    [InlineData("long", "integer", "int64")]
    [InlineData("ulong", "integer", "uint64")]
    // 超出 int64 / uint64 范围，OpenAPI 没有对应 format。
    [InlineData("Int128", "integer", null)]
    [InlineData("UInt128", "integer", null)]
    [InlineData("BigInteger", "integer", null)]
    [InlineData("decimal", "number", null)]
    [InlineData("double", "number", "double")]
    [InlineData("float", "number", "float")]
    [InlineData("Half", "number", null)]
    [InlineData("DateTime", "string", "date-time")]
    [InlineData("DateTimeOffset", "string", "date-time")]
    [InlineData("DateOnly", "string", "date")]
    [InlineData("TimeOnly", "string", "time")]
    // TimeSpan 的 JSON 形是 "hh:mm:ss"，不是 ISO 8601 duration。
    [InlineData("TimeSpan", "string", null)]
    public void AspNetCoreOpenApi_Should_GenerateSchemaEntry_ForPrimitive(string primitive, string openApiType, string? format)
    {
        var code = $$"""
            using System;
            using System.Numerics;

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

    /// <summary>
    /// 枚举在 JSON 里按底层整数写出（System.Text.Json 默认行为），schema 必须是 <c>integer</c> 而不是 <c>string</c>。
    /// </summary>
    [Fact]
    public void AspNetCoreOpenApi_Should_MapEnumToInteger()
    {
        var code = """
            using System;

            namespace Len.StronglyTypedId;

            public enum Status { Active, Suspended }

            [StronglyTypedId]
            public partial record struct StatusId(Status Value);
            """;

        GetAspNetCoreOpenApiGeneratedCode(code, OpenApi9Reference)
            .Should().Contain("""[typeof(global::Len.StronglyTypedId.StatusId)] = ("integer", null),""");
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

    #region C10：文本前缀使 schema 变为无 format 的 string

    /// <summary>
    /// 有前缀时规范文本形是字符串，故 schema 一律为 <c>("string", null)</c>。
    /// </summary>
    [Fact]
    public void AspNetCoreOpenApi_Should_UsePlainStringSchema_WhenIdHasPrefix()
    {
        var code = """
            using System;

            namespace Len.StronglyTypedId;

            [StronglyTypedId(Prefix = "ord_")]
            public partial record struct OrderId(Guid Value);
            """;

        var generated = GetAspNetCoreOpenApiGeneratedCode(code, OpenApi9Reference);

        generated.Should().Contain("[typeof(global::Len.StronglyTypedId.OrderId)] = (\"string\", null),");
    }

    #endregion

    #region V2（Microsoft.OpenApi 2.x / ASP.NET Core 10+）：命名空间迁移 + JsonSchemaType 枚举

    /// <summary>
    /// 合成「Microsoft.OpenApi 2.0.0.0」占位程序集：只用于让生成器按主版本切到 V2 分支。
    /// </summary>
    private static MetadataReference OpenApi2Reference { get; }
        = SyntheticAssembly.GetOrCreate("Microsoft.OpenApi", "2.0.0.0");

    /// <summary>
    /// ASP.NET Core 10 携带 Microsoft.OpenApi 2.x：<c>OpenApiSchema</c> 迁到根命名空间， <c>Type</c> 由 <c>string?</c>
    /// 改为 <c>JsonSchemaType?</c>。回归用例锁住这两处差异。
    /// </summary>
    [Fact]
    public void AspNetCoreOpenApi_V2_Should_UseJsonSchemaType_WhenMicrosoftOpenApiV2Referenced()
    {
        var code = """
            using System;

            namespace Len.StronglyTypedId;

            [StronglyTypedId]
            public partial record struct OrderId(Guid Value);

            [StronglyTypedId]
            public partial record struct ProductId(int Value);
            """;

        var generated = GetAspNetCoreOpenApiGeneratedCode(code, OpenApi9Reference, OpenApi2Reference);

        generated.Should().Contain("[typeof(global::Len.StronglyTypedId.OrderId)] = (global::Microsoft.OpenApi.JsonSchemaType.String, \"uuid\"),");
        generated.Should().Contain("[typeof(global::Len.StronglyTypedId.ProductId)] = (global::Microsoft.OpenApi.JsonSchemaType.Integer, \"int32\"),");
        generated.Should().Contain("(global::Microsoft.OpenApi.JsonSchemaType Type, string? Format)");
        generated.Should().Contain("global::Microsoft.OpenApi.OpenApiSchema schema,");
        generated.Should().NotContain("Microsoft.OpenApi.Models");
    }

    #endregion
}
