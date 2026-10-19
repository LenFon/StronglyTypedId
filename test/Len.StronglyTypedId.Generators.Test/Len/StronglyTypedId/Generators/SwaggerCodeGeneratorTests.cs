// xUnit1051：本文件中的 CSharpSyntaxTree.ParseText / Emit 等均为测试内同步调用，取消令牌无意义，
// 统一在此文件禁用该测试框架分析器警告。
#pragma warning disable xUnit1051
using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Len.StronglyTypedId.Generators;

/// <summary>
/// <see cref="SwaggerCodeGenerator"/> 的单元测试：覆盖 V1 / V2 两条 OpenApi 模式分支、
/// <see cref="SwaggerCodeGenerator.CreateOpenApiSchema"/> 的全部基元类型分支（含默认分支），以及多 Id 生成的 MapType 连接。
/// </summary>
public class SwaggerCodeGeneratorTests
{
    #region 辅助：驱动生成器并捕获 Swagger 产物

    private static MetadataReference Swagger6Reference { get; }
        = MetadataReference.CreateFromFile(typeof(Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions).Assembly.Location);

    private static MetadataReference OpenApiReference { get; }
        = MetadataReference.CreateFromFile(typeof(Microsoft.OpenApi.Models.OpenApiSchema).Assembly.Location);

    private static CSharpCompilation CreateCompilation(string sourceCode, params MetadataReference[] extraReferences)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);

        var seedAssemblies = new[]
        {
            typeof(StronglyTypedIdAttribute).Assembly,
            typeof(System.Text.Json.JsonSerializer).Assembly,
            typeof(Newtonsoft.Json.JsonSerializer).Assembly,
            // SwaggerGenOptions.MapType<T> 所在的命名空间由 Abstractions 提供；不预置的话它未必已被
            // 加载进 AppDomain，合成编译里就会出现「使用了不存在的 using」的假阳性。
            typeof(Microsoft.Extensions.DependencyInjection.IServiceCollection).Assembly,
        };

        // 过滤会触发其它生成器 / 污染发现结果的程序集（Swashbuckle / Microsoft.OpenApi 等需显式注入，
        // 见 Swagger6Reference 与 EmitSyntheticReference），避免生成非 Swagger 产物或重复引用。
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
            "Len.StronglyTypedId.Swagger.Test",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static string GetSwaggerGeneratedCode(string sourceCode, params MetadataReference[] extraReferences)
    {
        var compilation = CreateCompilation(sourceCode, extraReferences);

        var result = CSharpGeneratorDriver
            .Create(new StronglyTypedIdGenerator())
            .RunGenerators(compilation)
            .GetRunResult();

        result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var swagger = result.Results[0].GeneratedSources
            .FirstOrDefault(g => g.HintName == "StronglyTypedIds.Swagger.g.cs");
        return swagger.SourceText?.ToString() ?? string.Empty;
    }

    private static bool ShouldReferenceAssembly(Assembly assembly)
        => assembly.GetName().Name is not
            ("Len.StronglyTypedId.TestBase" or "Microsoft.EntityFrameworkCore" or "Swashbuckle.AspNetCore.SwaggerGen" or "Microsoft.OpenApi" or "Microsoft.AspNetCore.Mvc" or "Microsoft.AspNetCore.Mvc.Core" or "Microsoft.AspNetCore.Mvc.Abstractions" or "Microsoft.AspNetCore.OpenApi" or "Microsoft.AspNetCore.Routing");

    #endregion

    #region 顺序属性

    [Fact]
    public void Swagger_Should_HaveOrderFour() => SwaggerCodeGenerator.Instance.Order.Should().Be(4);

    #endregion

    #region V1（Microsoft.OpenApi 1.x / Swashbuckle 6.x–9.x）：覆盖全部基元分支

    // 源码必须带 using System;：否则 Guid 在合成编译里是「错误类型符号」，
    // SymbolDisplayFormat.FullyQualifiedFormat 会把它渲染成裸 Guid，与真实消费者环境
    // （global::System.Guid）不一致，令按短名匹配的基元分支「恰好」命中而掩盖缺陷。
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
    // string 不在显式列表内 → 命中默认分支，生成 Type 但无 Format。
    [InlineData("string", "string", null)]
    public void Swagger_V1_Should_GenerateMapType_ForPrimitive(string primitive, string openApiType, string? format)
    {
        var code = $$"""
            using System;

            namespace Len.StronglyTypedId;

            [StronglyTypedId]
            public partial record OrderId({{primitive}} Value);
            """;

        var generated = GetSwaggerGeneratedCode(code, Swagger6Reference);

        generated.Should().NotBeEmpty();

        var schema = format is null
            ? $$"""new global::Microsoft.OpenApi.Models.OpenApiSchema { Type = "{{openApiType}}" }"""
            : $$"""new global::Microsoft.OpenApi.Models.OpenApiSchema { Type = "{{openApiType}}", Format = "{{format}}" }""";

        generated.Should().Contain(
            $$"""options.MapType<global::Len.StronglyTypedId.OrderId>(() => {{schema}});""");
    }

    [Fact]
    public void Swagger_V1_Should_GenerateMultipleMapTypeCalls_ForMultipleIds()
    {
        var code = """
            using System;

            namespace Len.StronglyTypedId;

            [StronglyTypedId]
            public partial record OrderId(Guid Value);

            [StronglyTypedId]
            public partial record ProductId(int Value);
            """;

        var generated = GetSwaggerGeneratedCode(code, Swagger6Reference);

        generated.Should().Contain("options.MapType<global::Len.StronglyTypedId.OrderId>(() => new global::Microsoft.OpenApi.Models.OpenApiSchema { Type = \"string\", Format = \"uuid\" });");
        generated.Should().Contain("options.MapType<global::Len.StronglyTypedId.ProductId>(() => new global::Microsoft.OpenApi.Models.OpenApiSchema { Type = \"integer\", Format = \"int32\" });");
        // 两个调用之间应有换行 + 缩进分隔（string.Join 的分隔符）。
        // 断言写 \n 而非 \r\n：产物行尾随检出（CRLF / LF）而定，而 \n 在两种检出的产物里都成立。
        generated.Should().Contain("\n\t\t");
    }

    /// <summary>
    /// 回归用例：源码能解析出 <c>System.Guid</c> 时，基元分支仍必须命中并输出 <c>format: uuid</c>。
    /// </summary>
    [Fact]
    public void Swagger_V1_Should_GenerateUuidFormat_WhenGuidIsResolved()
    {
        var code = """
            using System;

            namespace Len.StronglyTypedId;

            [StronglyTypedId]
            public partial record OrderId(Guid Value);
            """;

        var generated = GetSwaggerGeneratedCode(code, Swagger6Reference);

        generated.Should().Contain(
            """options.MapType<global::Len.StronglyTypedId.OrderId>(() => new global::Microsoft.OpenApi.Models.OpenApiSchema { Type = "string", Format = "uuid" });""");
    }

    /// <summary>
    /// 回归用例：Swashbuckle 7/8/9 仍依赖 Microsoft.OpenApi 1.6.x，必须继续按 V1 生成；只有 Swashbuckle 10+ 才引入
    /// Microsoft.OpenApi 2.x。
    /// </summary>
    [Theory]
    [InlineData("7.0.0.0")]
    [InlineData("8.0.0.0")]
    [InlineData("9.0.0.0")]
    public void Swagger_V1_Should_StayOnV1_WhenSwashbuckleV7ToV9Referenced(string swaggerGenVersion)
    {
        var code = """
            using System;

            namespace Len.StronglyTypedId;

            [StronglyTypedId]
            public partial record OrderId(Guid Value);
            """;

        // 只注入合成的 Swashbuckle.AspNetCore.SwaggerGen.dll（不含 Microsoft.OpenApi 2.x），
        // 且不引用真实 Swashbuckle 6.x，避免同名程序集重复引用。
        var generated = GetSwaggerGeneratedCode(
            code,
            SyntheticAssembly.GetOrCreate("Swashbuckle.AspNetCore.SwaggerGen", swaggerGenVersion));

        generated.Should().Contain(
            """options.MapType<global::Len.StronglyTypedId.OrderId>(() => new global::Microsoft.OpenApi.Models.OpenApiSchema { Type = "string", Format = "uuid" });""");
    }

    #endregion

    #region V2（Microsoft.OpenApi 2.x / Swashbuckle 10+）：OpenApiSchema 命名空间迁移 + JsonSchemaType 枚举

    [Fact]
    public void Swagger_V2_Should_UseOpenApiSchemaEnum_WhenMicrosoftOpenApiV2Referenced()
    {
        var code = """
            namespace Len.StronglyTypedId;

            [StronglyTypedId]
            public partial record OrderId(int Value);
            """;

        // 注入合成 Microsoft.OpenApi.dll v2.0.0.0：触发 UsesMicrosoftOpenApiV2 判据。
        var generated = GetSwaggerGeneratedCode(
            code,
            Swagger6Reference,
            SyntheticAssembly.GetOrCreate("Microsoft.OpenApi", "2.0.0.0"));

        generated.Should().Contain(
            "options.MapType<global::Len.StronglyTypedId.OrderId>(() => new global::Microsoft.OpenApi.OpenApiSchema { Type = global::Microsoft.OpenApi.JsonSchemaType.Integer, Format = \"int32\" });");
    }

    [Fact]
    public void Swagger_V2_Should_UseOpenApiSchemaEnum_WhenSwashbuckleV10AndOpenApiV2Referenced()
    {
        var code = """
            using System;

            namespace Len.StronglyTypedId;

            [StronglyTypedId]
            public partial record OrderId(Guid Value);
            """;

        // 真实配对：Swashbuckle.AspNetCore.SwaggerGen 10.x 携带 Microsoft.OpenApi 2.x。
        // 注入两个合成程序集（不引用真实 Swashbuckle 6.x，避免同名程序集重复引用）。
        var generated = GetSwaggerGeneratedCode(
            code,
            SyntheticAssembly.GetOrCreate("Swashbuckle.AspNetCore.SwaggerGen", "10.0.0.0"),
            SyntheticAssembly.GetOrCreate("Microsoft.OpenApi", "2.0.0.0"));

        generated.Should().Contain(
            "options.MapType<global::Len.StronglyTypedId.OrderId>(() => new global::Microsoft.OpenApi.OpenApiSchema { Type = global::Microsoft.OpenApi.JsonSchemaType.String, Format = \"uuid\" });");
    }

    [Fact]
    public void Swagger_V2_Should_UseStringEnumWithoutFormat_ForUnsupportedPrimitive()
    {
        var code = """
            namespace Len.StronglyTypedId;

            [StronglyTypedId]
            public partial record OrderId(string Value);
            """;

        // V2 + 默认分支（string）：Type 为 JsonSchemaType.String 枚举且无 Format。
        var generated = GetSwaggerGeneratedCode(
            code,
            Swagger6Reference,
            SyntheticAssembly.GetOrCreate("Microsoft.OpenApi", "2.0.0.0"));

        generated.Should().Contain(
            "options.MapType<global::Len.StronglyTypedId.OrderId>(() => new global::Microsoft.OpenApi.OpenApiSchema { Type = global::Microsoft.OpenApi.JsonSchemaType.String });");
    }

    #endregion

    #region 消费者可编译性：生成文件必须自带扩展方法所需的 using

    /// <summary>
    /// 生成文件顶部声明的 <c>using Microsoft.Extensions.DependencyInjection;</c>。
    /// </summary>
    private const string DependencyInjectionUsing = "using Microsoft.Extensions.DependencyInjection;";

    /// <summary>
    /// 回归用例：Swagger 产物自带 <c>using Microsoft.Extensions.DependencyInjection;</c>。
    /// </summary>
    [Fact]
    public void Swagger_Should_DeclareDependencyInjectionUsing()
    {
        var code = """
            using System;

            namespace Len.StronglyTypedId;

            [StronglyTypedId]
            public partial record OrderId(Guid Value);
            """;

        var generated = GetSwaggerGeneratedCode(code, Swagger6Reference);

        generated.Should().Contain(DependencyInjectionUsing);
    }

    /// <summary>
    /// 回归用例：Swagger 产物在「隐式 using 不含 DependencyInjection 的消费者」里必须能编译。
    /// </summary>
    [Fact]
    public void Swagger_Should_Compile_WhenConsumerHasNoImplicitDependencyInjectionUsing()
    {
        var code = """
            using System;

            namespace Len.StronglyTypedId;

            [StronglyTypedId]
            public partial record OrderId(Guid Value);
            """;

        var compilation = CreateCompilation(code, Swagger6Reference, OpenApiReference);

        var result = CSharpGeneratorDriver
            .Create(new StronglyTypedIdGenerator())
            .RunGenerators(compilation)
            .GetRunResult();

        result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        // 只编译 Swagger 产物：其它产物（Newtonsoft / EF Core / 核心）的可编译性由
        // StronglyTypedIdGeneratorTests.GeneratedCode_Should_Compile_WithoutImplicitUsings 统一守护，
        // 避免本用例被别的生成器的问题带偏。
        var swaggerSource = result.Results[0].GeneratedSources
            .First(source => source.HintName == "StronglyTypedIds.Swagger.g.cs");

        using var stream = new MemoryStream();
        var emit = compilation
            .AddSyntaxTrees(CSharpSyntaxTree.ParseText(swaggerSource.SourceText.ToString(), path: swaggerSource.HintName))
            .Emit(stream);

        var errors = emit.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();

        errors.Should().BeEmpty(string.Join("\n", errors.Select(error => error.ToString())));
    }

    #endregion

    #region C10：文本前缀使 schema 变为无 format 的 string

    /// <summary>
    /// 有前缀时规范文本形是字符串，故 schema 一律为 <c>string</c> 且不带 format ——数字 / 枚举 / 日期基元在有前缀时对外也是字符串。
    /// </summary>
    [Fact]
    public void Swagger_V1_Should_UsePlainStringSchema_WhenIdHasPrefix()
    {
        var code = """
            using System;

            namespace Len.StronglyTypedId;

            [StronglyTypedId(Prefix = "ord_")]
            public partial record OrderId(int Value);
            """;

        var generated = GetSwaggerGeneratedCode(code, Swagger6Reference);

        generated.Should().Contain(
            """options.MapType<global::Len.StronglyTypedId.OrderId>(() => new global::Microsoft.OpenApi.Models.OpenApiSchema { Type = "string" });""");
    }

    [Fact]
    public void Swagger_V2_Should_UsePlainStringSchema_WhenIdHasPrefix()
    {
        var code = """
            using System;

            namespace Len.StronglyTypedId;

            [StronglyTypedId(Prefix = "ord_")]
            public partial record OrderId(Guid Value);
            """;

        var generated = GetSwaggerGeneratedCode(
            code,
            Swagger6Reference,
            SyntheticAssembly.GetOrCreate("Microsoft.OpenApi", "2.0.0.0"));

        generated.Should().Contain(
            "options.MapType<global::Len.StronglyTypedId.OrderId>(() => new global::Microsoft.OpenApi.OpenApiSchema { Type = global::Microsoft.OpenApi.JsonSchemaType.String });");
    }

    #endregion
}
