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
/// <see cref="SwaggerCodeGenerator.CreateOpenApiSchema"/> 的全部基元类型分支（含默认分支），
/// 以及多 Id 生成的 MapType 连接。
/// </summary>
/// <remarks>
/// V1 与 V2 由 <see cref="SwaggerCodeGenerator.UsesMicrosoftOpenApiV2"/> 决定，且只取决于编译引用中
/// Microsoft.OpenApi 的主版本。测试环境仅直接引用 Swashbuckle 6.x（Microsoft.OpenApi 1.x），
/// 故 V2 通过合成 Microsoft.OpenApi 2.x 的程序集引用（emit 临时 DLL）注入对应模块。
/// 注意 Swashbuckle 7/8/9 仍依赖 Microsoft.OpenApi 1.6.x，直到 10.0.0 才升级到 2.x，
/// 因此「Swashbuckle 主版本」不能作为 OpenApi 主版本的代理判据（见下方 V1 回归用例）。
/// </remarks>
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
            ("Len.StronglyTypedId.TestBase" or "Microsoft.EntityFrameworkCore" or "Swashbuckle.AspNetCore.SwaggerGen" or "Microsoft.OpenApi");

    /// <summary>
    /// emit 一个指定程序集名与版本（仅含占位类型）的临时 DLL，作为 MetadataReference 注入，
    /// 使 GetModules 产出的 ModuleInfo 命中目标名称与版本，从而触发对应的 OpenApi 模式分支。
    /// </summary>
    private static MetadataReference EmitSyntheticReference(string assemblyName, string version)
    {
        var dir = Path.Combine(Path.GetTempPath(), "StiSwaggerHarness");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, assemblyName + ".dll");

        var source = $$"""
            using System.Reflection;
            [assembly: AssemblyVersion("{{version}}")]
            public class Placeholder { }
            """;

        var compilation = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source)],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var ms = new MemoryStream();
        var emit = compilation.Emit(ms);
        emit.Success.Should().BeTrue(string.Join("\n", emit.Diagnostics));
        File.WriteAllBytes(path, ms.ToArray());
        return MetadataReference.CreateFromFile(path);
    }

    #endregion

    #region 顺序属性

    [Fact]
    public void Swagger_Should_HaveOrderFour()
    {
        SwaggerCodeGenerator.Instance.Order.Should().Be(4);
    }

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
        generated.Should().Contain("\r\n\t\t");
    }

    /// <summary>
    /// 回归用例：源码能解析出 <c>System.Guid</c> 时，基元分支仍必须命中并输出 <c>format: uuid</c>。
    /// </summary>
    /// <remarks>
    /// <c>SymbolDisplayFormat.FullyQualifiedFormat</c> 的 UseSpecialTypes 只把 C# 关键字渲染成短名，
    /// <c>System.Guid</c> 这类普通 BCL 类型得到的是 <c>global::System.Guid</c>。曾按短名字面量匹配
    /// 基元类型，<c>"Guid"</c> 分支因而永不命中，Guid 强类型 Id 在 OpenAPI 文档里丢掉 format: uuid。
    /// 既有用例未能发现，是因为源码片段缺少 <c>using System;</c>，<c>Guid</c> 在那里是错误类型符号、
    /// 反而显示为裸 <c>Guid</c>。
    /// </remarks>
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
    /// 回归用例：Swashbuckle 7/8/9 仍依赖 Microsoft.OpenApi 1.6.x，必须继续按 V1 生成；
    /// 只有 Swashbuckle 10+ 才引入 Microsoft.OpenApi 2.x。
    /// </summary>
    /// <remarks>
    /// 曾经以「Swashbuckle.AspNetCore.SwaggerGen 主版本 ≥ 7」代理 OpenApi 2.x 判据，
    /// 使 7/8/9 的使用者拿到 1.6.x 下并不存在的 <c>Microsoft.OpenApi.JsonSchemaType</c>
    /// 与迁移后的命名空间，直接编译失败。
    /// </remarks>
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
            EmitSyntheticReference("Swashbuckle.AspNetCore.SwaggerGen", swaggerGenVersion));

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
            EmitSyntheticReference("Microsoft.OpenApi", "2.0.0.0"));

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
            EmitSyntheticReference("Swashbuckle.AspNetCore.SwaggerGen", "10.0.0.0"),
            EmitSyntheticReference("Microsoft.OpenApi", "2.0.0.0"));

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
            EmitSyntheticReference("Microsoft.OpenApi", "2.0.0.0"));

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
    /// <remarks>
    /// <c>SwaggerGenOptions.MapType&lt;T&gt;</c> 是定义在 <c>Microsoft.Extensions.DependencyInjection</c>
    /// 命名空间下的扩展方法（容器类 <c>SwaggerGenOptionsExtensions</c>，Swashbuckle 6.x 与 10.x 一致），
    /// 只能由 using 指令引入，无法改用全限定名调用。
    /// </remarks>
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
    /// <remarks>
    /// <para>
    /// Web SDK（<c>Microsoft.NET.Sdk.Web</c>）的隐式 using 恰好包含
    /// <c>Microsoft.Extensions.DependencyInjection</c>，因此 Sample 项目全绿也掩盖了此缺陷；
    /// 类库 / 控制台等非 Web SDK 消费者不含该隐式 using，若生成文件不自带，会得到
    /// <c>CS1061：“SwaggerGenOptions”未包含“MapType”的定义</c>。
    /// </para>
    /// <para>
    /// 下方合成编译的源码里刻意只有 <c>using System;</c>（不含任何 DependencyInjection 相关 using），
    /// 等价于非 Web SDK 消费者 —— 这正是「572 全绿仍有缺陷」的那类系统性环境偏差，
    /// 故必须真正 Emit 一次生成产物，而不是只比对文本。
    /// </para>
    /// </remarks>
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
}
