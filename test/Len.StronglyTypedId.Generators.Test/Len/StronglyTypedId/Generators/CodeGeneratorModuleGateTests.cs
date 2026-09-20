// xUnit1051：本文件中的 CSharpSyntaxTree.ParseText 均为测试内同步调用，取消令牌无意义，
// 统一在此文件禁用该测试框架分析器警告。
#pragma warning disable xUnit1051
using System.Reflection;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Len.StronglyTypedId.Generators;

/// <summary>
/// 生成器「模块版本闸门」的单元测试：三类生成器各按一个被引用程序集的主版本号决定是否启用。
/// </summary>
/// <remarks>
/// <para>
/// 闸门分别是 <c>Microsoft.EntityFrameworkCore</c> 主版本 ≥ 7（<c>EfCoreCodeGenerator</c>）、
/// <c>Swashbuckle.AspNetCore.SwaggerGen</c> 主版本 ≥ 6（<c>SwaggerCodeGenerator</c>）、
/// <c>Newtonsoft.Json</c> 主版本 ≥ 13（<c>GetCodeGenerators</c> 的模块匹配项）。
/// </para>
/// <para>
/// 既有用例只覆盖了「没有引用该程序集」与「引用的是满足闸门的真实版本」两种情形；
/// 「引用了、但版本低于阈值」这条最易写错（例如把 <c>≥13</c> 写成 <c>≥12</c>）的分支此前无人把守。
/// 低于阈值的版本无法靠测试宿主的真实引用构造，故通过
/// <see cref="SyntheticAssembly"/> 注入只有名称与版本、没有真实 API 的合成程序集。
/// </para>
/// <para>
/// 每条闸门都成对给出正/反用例：反向用例的「不生成」只有在正向用例「生成」成立时才说明是闸门生效，
/// 否则可能只是示例源码根本没走到对应生成器（例如缺少 <c>ConfigureConventions</c> 语法）。
/// </para>
/// </remarks>
public class CodeGeneratorModuleGateTests
{
    #region EF Core：Microsoft.EntityFrameworkCore 主版本 >= 7

    [Fact]
    public void EfCore_Should_Generate_WhenEntityFrameworkCoreMajorVersionIsSeven()
    {
        var generated = RunGenerator(
            DbContextSource,
            SyntheticAssembly.GetOrCreate("Microsoft.EntityFrameworkCore", "7.0.0.0"));

        generated.Should().ContainKey("Len.StronglyTypedId.OrderId.EntityFrameworkCore.g.cs");
        generated.Should().ContainKey("StronglyTypedIds.EntityFrameworkCore.g.cs");
    }

    [Fact]
    public void EfCore_Should_NotGenerate_WhenEntityFrameworkCoreMajorVersionIsSix()
    {
        var generated = RunGenerator(
            DbContextSource,
            SyntheticAssembly.GetOrCreate("Microsoft.EntityFrameworkCore", "6.0.0.0"));

        generated.Keys.Should().NotContain(key => key.EndsWith(".EntityFrameworkCore.g.cs", StringComparison.Ordinal));
    }

    /// <summary>
    /// 闸门只看主版本：同为 7.x，带补丁号与不带补丁号都必须生成。
    /// </summary>
    [Fact]
    public void EfCore_Should_IgnoreMinorAndPatchVersion()
    {
        var generated = RunGenerator(
            DbContextSource,
            SyntheticAssembly.GetOrCreate("Microsoft.EntityFrameworkCore", "7.42.13.7"));

        generated.Should().ContainKey("StronglyTypedIds.EntityFrameworkCore.g.cs");
    }

    #endregion

    #region Swagger：Swashbuckle.AspNetCore.SwaggerGen 主版本 >= 6

    [Fact]
    public void Swagger_Should_Generate_WhenSwashbuckleMajorVersionIsSix()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Swashbuckle.AspNetCore.SwaggerGen", "6.0.0.0"));

        generated.Should().ContainKey("StronglyTypedIds.Swagger.g.cs");
    }

    [Fact]
    public void Swagger_Should_NotGenerate_WhenSwashbuckleMajorVersionIsFive()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Swashbuckle.AspNetCore.SwaggerGen", "5.0.0.0"));

        generated.Keys.Should().NotContain("StronglyTypedIds.Swagger.g.cs");
    }

    #endregion

    #region Newtonsoft.Json：主版本 >= 13

    [Fact]
    public void NewtonsoftJson_Should_Generate_WhenMajorVersionIsThirteen()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Newtonsoft.Json", "13.0.0.0"));

        generated.Should().ContainKey("Len.StronglyTypedId.OrderId.NewtonsoftJson.g.cs");
    }

    /// <summary>
    /// Newtonsoft.Json 12.x 仍被广泛使用，但其 <c>JsonConverter&lt;T&gt;</c> 与 13.x 存在差异，
    /// 生成器据此只在 13+ 产出转换器。阈值若被误写成 12，本用例即失败。
    /// </summary>
    [Fact]
    public void NewtonsoftJson_Should_NotGenerate_WhenMajorVersionIsTwelve()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Newtonsoft.Json", "12.0.0.0"));

        generated.Keys.Should().NotContain(key => key.EndsWith(".NewtonsoftJson.g.cs", StringComparison.Ordinal));
    }

    #endregion

    #region 闸门互不影响：低于阈值的模块不应连带关闭别的生成器

    [Fact]
    public void CoreGenerators_Should_StayEnabled_WhenOnlyLowVersionModulesReferenced()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Newtonsoft.Json", "12.0.0.0"),
            SyntheticAssembly.GetOrCreate("Swashbuckle.AspNetCore.SwaggerGen", "5.0.0.0"));

        // 核心与 System.Text.Json 产物与被排除的模块无关，必须照常产出。
        generated.Should().ContainKey("Len.StronglyTypedId.OrderId.g.cs");
        generated.Should().ContainKey("Len.StronglyTypedId.OrderId.SystemTextJson.g.cs");
    }

    #endregion

    #region Dapper：Dapper 主版本 >= 2

    [Fact]
    public void Dapper_Should_Generate_WhenDapperMajorVersionIsTwo()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Dapper", "2.0.0.0"));

        generated.Should().ContainKey("Len.StronglyTypedId.OrderId.Dapper.g.cs");
        generated.Should().ContainKey("StronglyTypedIds.Dapper.g.cs");
    }

    [Fact]
    public void Dapper_Should_NotGenerate_WhenDapperMajorVersionIsOne()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Dapper", "1.0.0.0"));

        generated.Keys.Should().NotContain(key => key.EndsWith(".Dapper.g.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void Dapper_Should_GenerateTypeHandlerAndApplyTo()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Dapper", "2.1.0.0"));

        generated["Len.StronglyTypedId.OrderId.Dapper.g.cs"]
            .Should().Contain("class OrderIdTypeHandler : global::Dapper.SqlMapper.TypeHandler<global::Len.StronglyTypedId.OrderId>")
            .And.Contain("public override void SetValue(")
            .And.Contain("public override global::Len.StronglyTypedId.OrderId? Parse(object value)");

        generated["StronglyTypedIds.Dapper.g.cs"]
            .Should().Contain("public static void ApplyTo(global::Dapper.IDbConnection connection)")
            .And.Contain("global::Dapper.SqlMapper.AddTypeHandler");
    }

    #endregion

    #region Microsoft.AspNetCore.OpenApi：主版本 >= 9

    [Fact]
    public void AspNetCoreOpenApi_Should_Generate_WhenMajorVersionIsNine()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Microsoft.AspNetCore.OpenApi", "9.0.0.0"));

        generated.Should().ContainKey("StronglyTypedIds.AspNetCoreOpenApi.g.cs");
    }

    [Fact]
    public void AspNetCoreOpenApi_Should_NotGenerate_WhenMajorVersionIsEight()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Microsoft.AspNetCore.OpenApi", "8.0.0.0"));

        generated.Keys.Should().NotContain("StronglyTypedIds.AspNetCoreOpenApi.g.cs");
    }

    [Fact]
    public void AspNetCoreOpenApi_Should_GenerateSchemaTransformer()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Microsoft.AspNetCore.OpenApi", "9.0.0.0"));

        generated["StronglyTypedIds.AspNetCoreOpenApi.g.cs"]
            .Should().Contain("class StronglyTypedIdOpenApiSchemaTransformer : global::Microsoft.AspNetCore.OpenApi.IOpenApiSchemaTransformer")
            .And.Contain("public static void ApplyTo(global::Microsoft.AspNetCore.OpenApi.OpenApiOptions options)")
            .And.Contain("[typeof(global::Len.StronglyTypedId.OrderId)] = (\"string\", \"uuid\"),");
    }

    #endregion

    #region ASP.NET Core MVC：Microsoft.AspNetCore.Mvc 主版本 >= 2

    [Fact]
    public void AspNetCoreMvc_Should_Generate_WhenMajorVersionIsTwo()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Microsoft.AspNetCore.Mvc", "2.0.0.0"));

        generated.Should().ContainKey("StronglyTypedIds.AspNetCoreMvc.g.cs");
    }

    [Fact]
    public void AspNetCoreMvc_Should_NotGenerate_WhenMajorVersionIsOne()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Microsoft.AspNetCore.Mvc", "1.0.0.0"));

        generated.Keys.Should().NotContain("StronglyTypedIds.AspNetCoreMvc.g.cs");
    }

    [Fact]
    public void AspNetCoreMvc_Should_GenerateProviderAndApplyTo()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Microsoft.AspNetCore.Mvc", "2.1.0.0"));

        var code = generated["StronglyTypedIds.AspNetCoreMvc.g.cs"];

        code.Should().Contain("public static void ApplyTo(global::Microsoft.AspNetCore.Mvc.MvcOptions options)")
            .And.Contain("options.ModelBinderProviders.Insert(0, new StronglyTypedIdModelBinderProvider());")
            .And.Contain("public static void ApplyTo(global::Microsoft.AspNetCore.Routing.RouteOptions options)")
            .And.Contain("options.ConstraintMap[\"OrderId\"] = typeof(global::Len.StronglyTypedId.StronglyTypedIds.StronglyTypedIdRouteConstraint<global::Len.StronglyTypedId.OrderId>);")
            .And.Contain("private sealed class StronglyTypedIdModelBinderProvider")
            .And.Contain("private sealed class StronglyTypedIdModelBinder<TId>")
            .And.Contain("private sealed class StronglyTypedIdRouteConstraint<TId>");
    }

    #endregion

    #region 辅助

    private const string IdSource = """
        using System;

        namespace Len.StronglyTypedId;

        [StronglyTypedId]
        public partial record struct OrderId(Guid Value);
        """;

    /// <summary>
    /// 示例源码必须出现 <c>protected override void ConfigureConventions(…)</c> 语法，
    /// 否则 EF Core 生成器根本不会被选入，反向用例将失去判别力。
    /// </summary>
    private const string DbContextSource = """
        using System;

        namespace Len.StronglyTypedId;

        [StronglyTypedId]
        public partial record struct OrderId(Guid Value);

        public class TestDbContext
        {
            protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) { }
        }
        """;

    /// <summary>
    /// 测试宿主 AppDomain 里必然加载了这些包的真实版本（且都是满足闸门的版本）。
    /// 它们必须排除在默认引用之外，只由用例通过 <see cref="SyntheticAssembly"/> 按需注入指定版本，
    /// 否则同名程序集出现两份引用，闸门判定的将是真实版本而非用例指定的版本。
    /// </summary>
    private static readonly string[] _versionGatedAssemblies =
    [
        "Len.StronglyTypedId.TestBase",
        "Microsoft.EntityFrameworkCore",
        "Swashbuckle.AspNetCore.SwaggerGen",
        "Microsoft.OpenApi",
        "Newtonsoft.Json",
        "Microsoft.AspNetCore.Mvc",
        "Microsoft.AspNetCore.Mvc.Core",
        "Microsoft.AspNetCore.Mvc.Abstractions",
        "Microsoft.AspNetCore.OpenApi",
        "Microsoft.AspNetCore.Routing",
    ];

    private static IReadOnlyDictionary<string, string> RunGenerator(string sourceCode, params MetadataReference[] extraReferences)
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

    private static CSharpCompilation CreateCompilation(string sourceCode, params MetadataReference[] extraReferences)
    {
        // AppDomain.GetAssemblies() 只返回「已加载」的程序集，而 System.Text.Json 是延迟加载的：
        // 必须像下面这样显式引用一次，它才会出现在编译引用里，SystemTextJsonCodeGenerator 也才会被选入。
        var seed = new[]
        {
            typeof(StronglyTypedIdAttribute).Assembly,
            typeof(System.Text.Json.JsonSerializer).Assembly,
        };

        var references = AppDomain.CurrentDomain
            .GetAssemblies()
            .Union(seed)
            .Distinct()
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
            .Where(assembly => !_versionGatedAssemblies.Contains(assembly.GetName().Name, StringComparer.Ordinal))
            .Select(assembly => (MetadataReference)MetadataReference.CreateFromFile(assembly.Location))
            .Concat(extraReferences)
            .ToArray();

        return CSharpCompilation.Create(
            "Len.StronglyTypedId.ModuleGate.Test",
            [CSharpSyntaxTree.ParseText(sourceCode)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    #endregion
}
