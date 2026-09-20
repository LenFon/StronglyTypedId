// xUnit1051：本文件中的 CSharpSyntaxTree.ParseText / Emit 等均为测试内同步调用，取消令牌无意义，
// 统一在此文件禁用该测试框架分析器警告。
#pragma warning disable xUnit1051
using System.IO;
using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Len.StronglyTypedId.Generators;

/// <summary>
/// <see cref="AspNetCoreMvcCodeGenerator"/> 的单元测试：覆盖顺序属性、真实 MVC 引用下的产物形态，
/// 以及「模型绑定 / 路由约束」产物在真实 ASP.NET Core 消费者画像下的可编译性。
/// </summary>
/// <remarks>
/// 本测试与 <see cref="CodeGeneratorModuleGateTests"/> 的 MVC 闸门用例互补：后者用合成
/// <c>Microsoft.AspNetCore.Mvc</c> 程序集验证「版本门控是否生效」，本文件则在真实框架引用下
/// 验证「生成的 MvcOptions / RouteOptions 注册代码确实能编译」。
/// </remarks>
public class AspNetCoreMvcCodeGeneratorTests
{
    #region 辅助：驱动生成器并捕获 MVC 产物

    private static CSharpCompilation CreateCompilation(string sourceCode, params MetadataReference[] extraReferences)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);

        // 显式 seed 若干 ASP.NET Core 程序集，确保它们出现在合成编译引用里（即使尚未被 AppDomain 延迟加载）。
        //   · MvcOptions   所在程序集（net10 为 Microsoft.AspNetCore.Mvc.Core，net8 为 Abstractions）—— 门控触发来源；
        //   · IModelBinder  所在程序集（Microsoft.AspNetCore.Mvc.Abstractions）—— 生成代码里 IModelBinder / IModelBinderProvider 的来源；
        //   · IRouteConstraint 所在程序集（Microsoft.AspNetCore.Routing.Abstractions）—— IRouteConstraint / IRouter 的来源；
        //   · RouteOptions / HttpContext 所在程序集—— ApplyTo(RouteOptions) 与 Match(HttpContext) 的来源。
        // 三者任一被加载即代表 MVC 可用，本文件刻意保留这些真实框架程序集（不在 ShouldReferenceAssembly 排除之列），
        // 使「真实引用 MVC 的消费者」画像成立，生成的 MvcOptions / RouteOptions 注册代码亦能编译。
        var seedAssemblies = new[]
        {
            typeof(StronglyTypedIdAttribute).Assembly,
            typeof(System.Text.Json.JsonSerializer).Assembly,
            typeof(Microsoft.AspNetCore.Mvc.MvcOptions).Assembly,
            typeof(Microsoft.AspNetCore.Mvc.ModelBinding.IModelBinder).Assembly,
            typeof(Microsoft.AspNetCore.Routing.RouteOptions).Assembly,
            typeof(Microsoft.AspNetCore.Routing.IRouteConstraint).Assembly,
            typeof(Microsoft.AspNetCore.Http.HttpContext).Assembly,
        };

        // 排除会触发无关生成器的门控程序集，避免本用例被 EF Core / Swagger / 内置 OpenApi 生成器的问题带偏；
        // ASP.NET Core 程序集（含 Microsoft.AspNetCore.Mvc 等）刻意保留，因为本测试要验证 MVC 产物可编译。
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
            "Len.StronglyTypedId.AspNetCoreMvc.Test",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static string GetMvcGeneratedCode(string sourceCode, params MetadataReference[] extraReferences)
    {
        var compilation = CreateCompilation(sourceCode, extraReferences);

        var result = CSharpGeneratorDriver
            .Create(new StronglyTypedIdGenerator())
            .RunGenerators(compilation)
            .GetRunResult();

        result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var mvc = result.Results[0].GeneratedSources
            .FirstOrDefault(g => g.HintName == "StronglyTypedIds.AspNetCoreMvc.g.cs");
        return mvc.SourceText?.ToString() ?? string.Empty;
    }

    /// <summary>
    /// 排除与 MVC 无关、且会触发其它生成器的门控程序集；ASP.NET Core 程序集一律保留。
    /// </summary>
    private static bool ShouldReferenceAssembly(Assembly assembly)
        => assembly.GetName().Name is not
            ("Len.StronglyTypedId.TestBase" or "Microsoft.EntityFrameworkCore" or "Swashbuckle.AspNetCore.SwaggerGen" or "Microsoft.OpenApi" or "Microsoft.AspNetCore.OpenApi" or "Newtonsoft.Json");

    #endregion

    #region 顺序属性

    [Fact]
    public void AspNetCoreMvc_Should_HaveOrderSeven()
    {
        AspNetCoreMvcCodeGenerator.Instance.Order.Should().Be(7);
    }

    #endregion

    #region 真实引用下的产物形态

    [Fact]
    public void AspNetCoreMvc_Should_Generate_WhenMvcReferenced()
    {
        var code = """
            using System;

            namespace Len.StronglyTypedId;

            [StronglyTypedId]
            public partial record struct OrderId(Guid Value);
            """;

        var generated = GetMvcGeneratedCode(code);

        generated.Should().NotBeEmpty();
    }

    [Fact]
    public void AspNetCoreMvc_Should_DeclareModelBinderProviderRegistration()
    {
        var code = """
            using System;

            namespace Len.StronglyTypedId;

            [StronglyTypedId]
            public partial record struct OrderId(Guid Value);
            """;

        var generated = GetMvcGeneratedCode(code);

        generated.Should().Contain("public static void ApplyTo(global::Microsoft.AspNetCore.Mvc.MvcOptions options)")
            .And.Contain("options.ModelBinderProviders.Insert(0, new StronglyTypedIdModelBinderProvider());");
    }

    [Fact]
    public void AspNetCoreMvc_Should_DeclareRouteConstraintRegistration()
    {
        var code = """
            using System;

            namespace Len.StronglyTypedId;

            [StronglyTypedId]
            public partial record struct OrderId(Guid Value);
            """;

        var generated = GetMvcGeneratedCode(code);

        generated.Should().Contain("public static void ApplyTo(global::Microsoft.AspNetCore.Routing.RouteOptions options)")
            .And.Contain("options.ConstraintMap[\"OrderId\"] = typeof(global::Len.StronglyTypedId.StronglyTypedIds.StronglyTypedIdRouteConstraint<global::Len.StronglyTypedId.OrderId>);");
    }

    [Fact]
    public void AspNetCoreMvc_Should_DeclareBinderProviderAndConstraints()
    {
        var code = """
            using System;

            namespace Len.StronglyTypedId;

            [StronglyTypedId]
            public partial record struct OrderId(Guid Value);
            """;

        var generated = GetMvcGeneratedCode(code);

        generated.Should().Contain("private sealed class StronglyTypedIdModelBinderProvider : global::Microsoft.AspNetCore.Mvc.ModelBinding.IModelBinderProvider")
            .And.Contain("private sealed class StronglyTypedIdModelBinder<TId> : global::Microsoft.AspNetCore.Mvc.ModelBinding.IModelBinder where TId : global::System.IParsable<TId>")
            .And.Contain("private sealed class StronglyTypedIdRouteConstraint<TId> : global::Microsoft.AspNetCore.Routing.IRouteConstraint where TId : global::System.IParsable<TId>");
    }

    [Fact]
    public void AspNetCoreMvc_Should_RegisterConstraintForEveryId()
    {
        var code = """
            using System;

            namespace Len.StronglyTypedId;

            [StronglyTypedId]
            public partial record struct OrderId(Guid Value);

            [StronglyTypedId]
            public partial record ProductId(int Value);
            """;

        var generated = GetMvcGeneratedCode(code);

        generated.Should().Contain("options.ConstraintMap[\"OrderId\"] = typeof(global::Len.StronglyTypedId.StronglyTypedIds.StronglyTypedIdRouteConstraint<global::Len.StronglyTypedId.OrderId>);")
            .And.Contain("options.ConstraintMap[\"ProductId\"] = typeof(global::Len.StronglyTypedId.StronglyTypedIds.StronglyTypedIdRouteConstraint<global::Len.StronglyTypedId.ProductId>);");
    }

    #endregion

    #region 消费者可编译性：生成文件在真实 ASP.NET Core 引用下必须能编译

    /// <summary>
    /// 回归用例：MVC 产物在「真实引用 ASP.NET Core」的消费者里必须能编译。
    /// </summary>
    /// <remarks>
    /// 生成文件里所有 MVC / 路由符号都用 <c>global::</c> 全限定，不依赖任何隐式 using，
    /// 因此 Web SDK 与非 Web SDK 消费者都通。必须真正 Emit 一次产物（而非只比对文本），
    /// 才能抓住「类型/命名空间不可解析」这类真机缺陷。
    /// </remarks>
    [Fact]
    public void AspNetCoreMvc_Should_Compile_WhenConsumerReferencesMvc()
    {
        var code = """
            using System;

            namespace Len.StronglyTypedId;

            [StronglyTypedId]
            public partial record struct OrderId(Guid Value);
            """;

        var compilation = CreateCompilation(code);

        var result = CSharpGeneratorDriver
            .Create(new StronglyTypedIdGenerator())
            .RunGenerators(compilation)
            .GetRunResult();

        result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        // 编译「全部」生成产物：真实消费者会把所有生成文件一起编译，核心文件
        // Len.StronglyTypedId.OrderId.g.cs 提供的 IParsable<OrderId> 实现正是 MVC 产物里
        // where TId : IParsable<TId> 约束所依赖的。只 Emit MVC 单文件会因 OrderId 未满足约束
        // 而 CS0315；只比对文本又抓不到 CS8926 这类静态抽象接口成员访问缺陷。
        // 本用例刻意排除 EF Core / Swagger / Newtonsoft / 内置 OpenApi，故它们的生成器闸门不触发，
        // 实际只会产出「核心 + MVC」两类文件，均可在此编译。
        var generatedTrees = result.Results[0].GeneratedSources
            .Select(source => CSharpSyntaxTree.ParseText(source.SourceText.ToString(), path: source.HintName))
            .ToArray();

        using var stream = new MemoryStream();
        var emit = compilation
            .AddSyntaxTrees(generatedTrees)
            .Emit(stream);

        var errors = emit.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();

        errors.Should().BeEmpty(string.Join("\n", errors.Select(error => error.ToString())));
    }

    #endregion
}
