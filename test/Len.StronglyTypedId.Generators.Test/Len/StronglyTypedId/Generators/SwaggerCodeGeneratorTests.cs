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
/// <see cref="SwaggerCodeGenerator.CreateOpenApiSchema"/> 的全部基元类型分支（含默认分支），以及多 Id 生成的 schema 表。
/// </summary>
public class SwaggerCodeGeneratorTests
{
    #region 辅助：驱动生成器并捕获 Swagger 产物

    private static MetadataReference Swagger6Reference { get; }
        = AssemblyReferenceCache.FromAssembly(typeof(Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions).Assembly);

    private static MetadataReference OpenApiReference { get; }
        = AssemblyReferenceCache.FromAssembly(typeof(Microsoft.OpenApi.Models.OpenApiSchema).Assembly);

    private static CSharpCompilation CreateCompilation(string sourceCode, params MetadataReference[] extraReferences)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);

        var seedAssemblies = new[]
        {
            typeof(StronglyTypedIdAttribute).Assembly,
            typeof(System.Text.Json.JsonSerializer).Assembly,
            typeof(Newtonsoft.Json.JsonSerializer).Assembly,
            // 产物里的 TryAdd 是 System.Collections.Generic 上的扩展方法，需要该程序集在引用集里。
            typeof(System.Collections.Generic.Dictionary<,>).Assembly,
            // 操作过滤器要读 ApiDescription（OperationFilterContext.ApiDescription 的类型）。
            // 真实项目里它经 ASP.NET Core 框架引用传递进来，引用 Swashbuckle 必然带上它。
            typeof(Microsoft.AspNetCore.Mvc.ApiExplorer.ApiDescription).Assembly,
            // 连带 MVC 生成器也会被触发（它的门控认 Mvc.Abstractions），故 MVC 侧的类型也得在引用集里。
            typeof(Microsoft.AspNetCore.Mvc.MvcOptions).Assembly,
            typeof(Microsoft.AspNetCore.Routing.RouteOptions).Assembly,
            typeof(Microsoft.AspNetCore.Http.HttpContext).Assembly,
        };

        // 过滤会触发其它生成器 / 污染发现结果的程序集（Swashbuckle / Microsoft.OpenApi 等需显式注入，
        // 见 Swagger6Reference 与 EmitSyntheticReference），避免生成非 Swagger 产物或重复引用。
        var references = AppDomain.CurrentDomain
            .GetAssemblies()
            .Union(seedAssemblies)
            .Distinct()
            .Where(a => !a.IsDynamic)
            .Where(ShouldReferenceAssembly)
            .Select(AssemblyReferenceCache.TryResolveReference)
            .OfType<MetadataReference>()
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

    /// <summary>
    /// 排除与 Swagger 无关、且会触发其它生成器的门控程序集。
    /// </summary>
    /// <remarks>
    /// ASP.NET Core 程序集一律保留：操作过滤器要用 <c>ApiDescription</c>，而真实项目里它是经框架
    /// 引用传递进来的，引用了 Swashbuckle 就必然带上它。连带的后果是 MVC 生成器也会被触发，
    /// 但那不影响本文件对 Swagger 产物的断言。
    /// </remarks>
    private static bool ShouldReferenceAssembly(Assembly assembly)
        => assembly.GetName().Name is not
            ("Len.StronglyTypedId.TestBase" or "Microsoft.EntityFrameworkCore" or "Microsoft.OpenApi" or "Microsoft.AspNetCore.OpenApi" or "Newtonsoft.Json");

    #endregion

    #region 顺序属性

    [Fact]
    public void Swagger_Should_HaveOrderFour() => SwaggerCodeGenerator.Instance.Order.Should().Be(4);

    #endregion

    #region V1（Microsoft.OpenApi 1.x / Swashbuckle 6.x–9.x）：覆盖全部基元分支

    // 源码必须带 using System;：否则 Guid 在合成编译里是「错误类型符号」，会被渲染成裸 Guid，令按短名匹配的基元分支「恰好」命中而掩盖缺陷。
    // 映射口径对齐 System.Text.Json 的默认写出形态；枚举另有用例。
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
    public void Swagger_V1_Should_GenerateMapType_ForPrimitive(string primitive, string openApiType, string? format)
    {
        var code = $$"""
            using System;
            using System.Numerics;

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
            $$"""[typeof(global::Len.StronglyTypedId.OrderId)] = () => {{schema}},""");
    }

    /// <summary>
    /// 枚举在 JSON 里按底层整数写出（System.Text.Json 默认行为），schema 必须是 <c>integer</c> 而不是 <c>string</c>。
    /// </summary>
    [Fact]
    public void Swagger_V1_Should_MapEnumToInteger()
    {
        var code = """
            using System;

            namespace Len.StronglyTypedId;

            public enum Status { Active, Suspended }

            [StronglyTypedId]
            public partial record struct StatusId(Status Value);
            """;

        GetSwaggerGeneratedCode(code, Swagger6Reference)
            .Should().Contain(
                """[typeof(global::Len.StronglyTypedId.StatusId)] = () => new global::Microsoft.OpenApi.Models.OpenApiSchema { Type = "integer" },""");
    }

    [Fact]
    public void Swagger_V1_Should_GenerateSchemaEntryForEveryId()
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

        generated.Should().Contain("[typeof(global::Len.StronglyTypedId.OrderId)] = () => new global::Microsoft.OpenApi.Models.OpenApiSchema { Type = \"string\", Format = \"uuid\" },");
        generated.Should().Contain("[typeof(global::Len.StronglyTypedId.ProductId)] = () => new global::Microsoft.OpenApi.Models.OpenApiSchema { Type = \"integer\", Format = \"int32\" },");
        // 两个条目之间应有换行分隔（string.Join 的分隔符）。断言跨到第二个条目，
        // 否则首个条目前面的换行也能满足它，等于什么都没锁。
        // 先把行尾归一化成 \n：产物行尾随检出（CRLF / LF）而定，而跨行断言里的「,\n」在 CRLF 产物中并不成立。
        generated.Replace("\r\n", "\n")
            .Should().Contain(
                ",\n            [typeof(global::Len.StronglyTypedId.ProductId)]");
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
            """[typeof(global::Len.StronglyTypedId.OrderId)] = () => new global::Microsoft.OpenApi.Models.OpenApiSchema { Type = "string", Format = "uuid" },""");
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
            """[typeof(global::Len.StronglyTypedId.OrderId)] = () => new global::Microsoft.OpenApi.Models.OpenApiSchema { Type = "string", Format = "uuid" },""");
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
            "[typeof(global::Len.StronglyTypedId.OrderId)] = () => new global::Microsoft.OpenApi.OpenApiSchema { Type = global::Microsoft.OpenApi.JsonSchemaType.Integer, Format = \"int32\" },");
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
            "[typeof(global::Len.StronglyTypedId.OrderId)] = () => new global::Microsoft.OpenApi.OpenApiSchema { Type = global::Microsoft.OpenApi.JsonSchemaType.String, Format = \"uuid\" },");
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
            "[typeof(global::Len.StronglyTypedId.OrderId)] = () => new global::Microsoft.OpenApi.OpenApiSchema { Type = global::Microsoft.OpenApi.JsonSchemaType.String },");
    }

    #endregion

    #region 消费者可编译性：生成文件必须自带扩展方法所需的 using

    /// <summary>
    /// 生成文件顶部声明的 <c>using System.Collections.Generic;</c>（<c>TryAdd</c> 是它的扩展方法）。
    /// </summary>
    private const string GenericCollectionsUsing = "using System.Collections.Generic;";

    /// <summary>
    /// 回归用例：Swagger 产物自带 <c>using System.Collections.Generic;</c>。
    /// </summary>
    [Fact]
    public void Swagger_Should_DeclareGenericCollectionsUsing()
    {
        var code = """
            using System;

            namespace Len.StronglyTypedId;

            [StronglyTypedId]
            public partial record OrderId(Guid Value);
            """;

        var generated = GetSwaggerGeneratedCode(code, Swagger6Reference);

        generated.Should().Contain(GenericCollectionsUsing);
    }

    /// <summary>
    /// 回归用例：Swagger 产物在「隐式 using 不含 System.Collections.Generic 的消费者」里必须能编译。
    /// </summary>
    [Fact]
    public void Swagger_Should_Compile_WhenConsumerHasNoImplicitGenericCollectionsUsing()
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
    /// 有前缀时规范文本形是字符串，故 schema 一律为 <c>string</c> 且不带 format，数字 / 枚举 / 日期基元在有前缀时对外也是字符串。
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
            """[typeof(global::Len.StronglyTypedId.OrderId)] = () => new global::Microsoft.OpenApi.Models.OpenApiSchema { Type = "string" },""");
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
            "[typeof(global::Len.StronglyTypedId.OrderId)] = () => new global::Microsoft.OpenApi.OpenApiSchema { Type = global::Microsoft.OpenApi.JsonSchemaType.String },");
    }

    #endregion

    #region JsonShape.Object：线上报文是 {"value": <基元>}，schema 必须照写

    /// <summary>
    /// <c>JsonShape.Object</c> 的 JSON 形是 <c>{"value":"..."}</c>；若仍按裸基元发 schema，生成的客户端会按字符串收发，
    /// 与服务端实际报文不符。
    /// </summary>
    [Fact]
    public void Swagger_V1_Should_GenerateObjectSchema_WhenJsonShapeIsObject()
    {
        var code = """
            using System;

            namespace Len.StronglyTypedId;

            [StronglyTypedId(JsonShape = JsonShape.Object)]
            public partial record OrderId(Guid Value);
            """;

        GetSwaggerGeneratedCode(code, Swagger6Reference)
            .Should().Contain(
                """[typeof(global::Len.StronglyTypedId.OrderId)] = () => new global::Microsoft.OpenApi.Models.OpenApiSchema { Type = "object", Properties = new global::System.Collections.Generic.Dictionary<string, global::Microsoft.OpenApi.Models.OpenApiSchema> { ["value"] = new global::Microsoft.OpenApi.Models.OpenApiSchema { Type = "string", Format = "uuid" } }, Required = new global::System.Collections.Generic.HashSet<string> { "value" } },""");
    }

    /// <summary>
    /// 对象形态下 <c>value</c> 属性的 schema 仍按基元映射（含前缀时的纯 string 规则）。
    /// </summary>
    [Fact]
    public void Swagger_V1_Should_UsePrimitiveSchemaForValueProperty_WhenIdHasPrefix()
    {
        var code = """
            using System;

            namespace Len.StronglyTypedId;

            [StronglyTypedId(Prefix = "ord_", JsonShape = JsonShape.Object)]
            public partial record OrderId(int Value);
            """;

        GetSwaggerGeneratedCode(code, Swagger6Reference)
            .Should().Contain(
                """["value"] = new global::Microsoft.OpenApi.Models.OpenApiSchema { Type = "string" }""");
    }

    /// <summary>
    /// 2.x 的 <c>Properties</c> 值是 <c>IOpenApiSchema</c> 接口，泛型不变，不能用 <c>Dictionary&lt;string, OpenApiSchema&gt;</c> 顶替。
    /// </summary>
    [Fact]
    public void Swagger_V2_Should_GenerateObjectSchema_WhenJsonShapeIsObject()
    {
        var code = """
            using System;

            namespace Len.StronglyTypedId;

            [StronglyTypedId(JsonShape = JsonShape.Object)]
            public partial record OrderId(Guid Value);
            """;

        GetSwaggerGeneratedCode(
                code,
                Swagger6Reference,
                SyntheticAssembly.GetOrCreate("Microsoft.OpenApi", "2.0.0.0"))
            .Should().Contain(
                "[typeof(global::Len.StronglyTypedId.OrderId)] = () => new global::Microsoft.OpenApi.OpenApiSchema { Type = global::Microsoft.OpenApi.JsonSchemaType.Object, Properties = new global::System.Collections.Generic.Dictionary<string, global::Microsoft.OpenApi.IOpenApiSchema> { [\"value\"] = new global::Microsoft.OpenApi.OpenApiSchema { Type = global::Microsoft.OpenApi.JsonSchemaType.String, Format = \"uuid\" } }, Required = new global::System.Collections.Generic.HashSet<string> { \"value\" } },");
    }

    /// <summary>
    /// 对象形态的产物同样必须能在消费者里编译（<c>Properties</c> / <c>Required</c> 的赋值目标类型由真实包决定）。
    /// </summary>
    [Fact]
    public void Swagger_ObjectShape_Should_Compile_AgainstRealOpenApiPackage()
    {
        var code = """
            using System;

            namespace Len.StronglyTypedId;

            [StronglyTypedId(JsonShape = JsonShape.Object)]
            public partial record OrderId(Guid Value);
            """;

        var compilation = CreateCompilation(code, Swagger6Reference, OpenApiReference);

        var result = CSharpGeneratorDriver
            .Create(new StronglyTypedIdGenerator())
            .RunGenerators(compilation)
            .GetRunResult();

        result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

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

    #region 产物格式

    /// <summary>
    /// 注册入口的方法体此前用制表符缩进，且左大括号后带一个尾随空格，与其余生成物的 4 空格风格不一致。
    /// </summary>
    /// <remarks>
    /// 断言里带上换行与空白：只匹配 <c>TryAdd</c> 的子串对缩进完全免疫。
    /// </remarks>
    [Fact]
    public void Swagger_AddStronglyTypedIds_Should_UseSpacesForIndentation()
    {
        var code = """
            using System;

            namespace Len.StronglyTypedId;

            [StronglyTypedId]
            public partial record OrderId(Guid Value);
            """;

        GetSwaggerGeneratedCode(code, Swagger6Reference).Replace("\r\n", "\n")
            .Should().Contain(
                "\n    {\n        var schemaMappings = options.SchemaGeneratorOptions.CustomTypeMappings;\n"
                + "\n        foreach (var schemaMapping in StronglyTypedIdOperationFilter.Schemas)\n"
                + "        {\n"
                + "            schemaMappings.TryAdd(schemaMapping.Key, schemaMapping.Value);\n"
                + "        }")
            .And.NotContain("\t")
            .And.NotContain("    { \n");
    }

    #endregion

    #region 操作过滤器：补回 Minimal API 参数的 schema

    /// <summary>
    /// Minimal API 的处理器参数到达 <c>ApiExplorer</c> 时，<c>ApiParameterDescription.Type</c> 仍是真实的 Id 类型，
    /// 但 <c>ModelMetadata.ModelType</c> 已被框架换成 <see cref="string"/>，Swashbuckle 据后者生成 schema，
    /// 于是 Id 参数在文档里只剩裸的 <c>type: string</c>。过滤器按参数的真实类型查表，把 schema 补回参数上。
    /// </summary>
    private const string OperationFilterIdSource = """
        using System;

        namespace Len.StronglyTypedId;

        [StronglyTypedId]
        public partial record struct OrderId(Guid Value);
        """;

    [Fact]
    public void Swagger_Should_GenerateOperationFilter()
    {
        var generated = GetSwaggerGeneratedCode(OperationFilterIdSource, Swagger6Reference);

        generated.Should().Contain(
            "class StronglyTypedIdOperationFilter : global::Swashbuckle.AspNetCore.SwaggerGen.IOperationFilter");
    }

    /// <summary>
    /// 查表的键必须是<b>参数的真实类型</b>：框架换掉的 <c>ModelMetadata.ModelType</c> 在这里没有用。
    /// </summary>
    [Fact]
    public void SwaggerOperationFilter_Should_LookUpByParameterType()
    {
        var generated = GetSwaggerGeneratedCode(OperationFilterIdSource, Swagger6Reference);

        generated.Should()
            .Contain("Schemas.TryGetValue(parameterDescription.Type, out var factory)")
            .And.NotContain("Schemas.TryGetValue(parameterDescription.ModelMetadata");
    }

    /// <summary>
    /// 参数的 schema 靠名字对齐：<c>ApiParameterDescription.Name</c> 与 <c>OpenApiParameter.Name</c>。
    /// </summary>
    [Fact]
    public void SwaggerOperationFilter_Should_MatchParameterByName()
    {
        var generated = GetSwaggerGeneratedCode(OperationFilterIdSource, Swagger6Reference);

        generated.Should().Contain(
            "string.Equals(parameter.Name, parameterDescription.Name, global::System.StringComparison.OrdinalIgnoreCase)");
    }

    /// <summary>
    /// 注册必须幂等：重复调用 <c>AddStronglyTypedIds</c> 不该让过滤器列表增长。
    /// </summary>
    [Fact]
    public void SwaggerOperationFilter_Should_BeRegisteredIdempotently()
    {
        var generated = GetSwaggerGeneratedCode(OperationFilterIdSource, Swagger6Reference);

        generated.Should()
            .Contain("if (filter is StronglyTypedIdOperationFilter)")
            .And.Contain("generatorOptions.OperationFilters.Add(new StronglyTypedIdOperationFilter());")
            // 幂等检查只能「跳过注册」，不能提前返回，否则第二次调用 AddStronglyTypedIds 会把 schema 注册一起跳过。
            .And.NotContain("return;\n                    }\n\n                    generatorOptions");
    }

    /// <summary>
    /// schema 表按 Id 类型登记，值与注册入口用的是同一份口径（类型 / format / 是否对象形态）。
    /// </summary>
    /// <remarks>
    /// 表里存的是工厂而不是现成的 schema：每个参数拿到的必须是自己的实例。
    /// </remarks>
    [Fact]
    public void SwaggerOperationFilter_Should_RegisterSchemaPerIdType()
    {
        var generated = GetSwaggerGeneratedCode(OperationFilterIdSource, Swagger6Reference);

        generated.Should().Contain(
            "[typeof(global::Len.StronglyTypedId.OrderId)] = () => new global::Microsoft.OpenApi.Models.OpenApiSchema { Type = \"string\", Format = \"uuid\" },");
    }

    /// <summary>
    /// 对象形态（<c>JsonShape.Object</c>）的 Id 参数，线上报文是 <c>{"value": &lt;基元&gt;}</c>，
    /// schema 必须照着写，否则生成的客户端按裸基元收发。
    /// </summary>
    [Fact]
    public void SwaggerOperationFilter_Should_GenerateObjectSchema_WhenJsonShapeIsObject()
    {
        var code = """
            using System;

            namespace Len.StronglyTypedId;

            [StronglyTypedId(JsonShape = JsonShape.Object)]
            public partial record struct OrderId(Guid Value);
            """;

        var generated = GetSwaggerGeneratedCode(code, Swagger6Reference);

        generated.Should().Contain(
            "[typeof(global::Len.StronglyTypedId.OrderId)] = () => new global::Microsoft.OpenApi.Models.OpenApiSchema { Type = \"object\", Properties = new global::System.Collections.Generic.Dictionary<string, global::Microsoft.OpenApi.Models.OpenApiSchema> { [\"value\"] = new global::Microsoft.OpenApi.Models.OpenApiSchema { Type = \"string\", Format = \"uuid\" } }, Required = new global::System.Collections.Generic.HashSet<string> { \"value\" } },");
    }

    #endregion

    #region 操作过滤器的 OpenApi 1.x / 2.x 类型名分派

    [Fact]
    public void Swagger_V1_Should_UseOpenApiV1Types_InOperationFilter()
    {
        var generated = GetSwaggerGeneratedCode(OperationFilterIdSource, Swagger6Reference);

        generated.Should()
            .Contain("global::Microsoft.OpenApi.Models.OpenApiOperation")
            .And.Contain("global::Microsoft.OpenApi.Models.OpenApiSchema")
            .And.NotContain("global::Microsoft.OpenApi.OpenApiSchema");
    }

    [Fact]
    public void Swagger_V2_Should_UseOpenApiV2Types_InOperationFilter()
    {
        var generated = GetSwaggerGeneratedCode(
            OperationFilterIdSource,
            Swagger6Reference,
            SyntheticAssembly.GetOrCreate("Microsoft.OpenApi", "2.0.0.0"));

        generated.Should()
            .Contain("global::Microsoft.OpenApi.OpenApiOperation")
            .And.Contain("global::Microsoft.OpenApi.OpenApiSchema")
            .And.Contain("global::Microsoft.OpenApi.JsonSchemaType.String")
            .And.NotContain("global::Microsoft.OpenApi.Models.OpenApiSchema");
    }

    /// <summary>
    /// Microsoft.OpenApi 2.x 的 <c>IOpenApiParameter</c> 成员全是只读的，直接给 <c>parameter.Schema</c>
    /// 赋值编译不过（CS0200），必须落到具体的 <c>OpenApiParameter</c> 上写。
    /// </summary>
    [Fact]
    public void Swagger_V2_Should_WriteSchemaOntoTheConcreteParameter()
    {
        var generated = GetSwaggerGeneratedCode(
            OperationFilterIdSource,
            Swagger6Reference,
            SyntheticAssembly.GetOrCreate("Microsoft.OpenApi", "2.0.0.0"));

        generated.Should()
            .Contain("private static void ApplySchema(global::Microsoft.OpenApi.IOpenApiParameter parameter, global::Microsoft.OpenApi.IOpenApiSchema schema)")
            .And.Contain("if (parameter is global::Microsoft.OpenApi.OpenApiParameter concreteParameter)")
            .And.Contain("concreteParameter.Schema = schema;");
    }

    /// <summary>
    /// 1.x 的参数列表装的是具体类，直接赋值即可。
    /// </summary>
    [Fact]
    public void Swagger_V1_Should_WriteSchemaDirectly()
    {
        var generated = GetSwaggerGeneratedCode(OperationFilterIdSource, Swagger6Reference);

        generated.Should()
            .Contain("private static void ApplySchema(global::Microsoft.OpenApi.Models.OpenApiParameter parameter, global::Microsoft.OpenApi.Models.OpenApiSchema schema)")
            .And.Contain("parameter.Schema = schema;");
    }

    /// <summary>
    /// <c>ApplySchema</c> 嵌在 <c>StronglyTypedIdOperationFilter</c> 里，方法体在产物中必须比花括号深一级（12 空格）。
    /// </summary>
    /// <remarks>
    /// 原始字符串模板的插值洞不随模板重新缩进，方法体文本必须自带绝对缩进；漏掉时产物仍能编译，
    /// 但方法体与花括号同列，读起来像语法错误。
    /// </remarks>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Swagger_Should_IndentApplySchemaBody(bool useOpenApiV2)
    {
        var extraReferences = useOpenApiV2
            ? new[] { SyntheticAssembly.GetOrCreate("Microsoft.OpenApi", "2.0.0.0") }
            : [];

        var generated = GetSwaggerGeneratedCode(OperationFilterIdSource, [Swagger6Reference, .. extraReferences]);

        var lines = generated.Replace("\r\n", "\n").Split('\n');
        var signatureIndex = Array.FindIndex(lines, line => line.Contains("private static void ApplySchema(", StringComparison.Ordinal));

        signatureIndex.Should().BeGreaterThan(-1);

        var signatureIndent = lines[signatureIndex].Length - lines[signatureIndex].TrimStart().Length;

        lines[signatureIndex + 1].Should().Be(new string(' ', signatureIndent) + "{");

        // 方法体首行（1.x 的赋值语句 / 2.x 的 if）必须比花括号深一级。
        lines[signatureIndex + 2].Should().StartWith(new string(' ', signatureIndent + 4));
        lines[signatureIndex + 2].Trim().Should().NotBeEmpty();
    }

    #endregion
}
