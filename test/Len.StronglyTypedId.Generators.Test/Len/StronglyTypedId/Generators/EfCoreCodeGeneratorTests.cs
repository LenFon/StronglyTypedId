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
/// <see cref="EfCoreCodeGenerator"/> 的单元测试：覆盖顺序属性、真实 EF Core 引用下的产物形态
/// （ValueConverter / ModelConfigurationBuilder 约定入口 / ModelBuilder 扫描入口），
/// 多 Id 注册，以及消费者可编译性。
/// </summary>
/// <remarks>
/// <para>
/// 本文件与 <see cref="CodeGeneratorModuleGateTests"/> 的 EF Core 闸门用例互补：后者用合成
/// <c>Microsoft.EntityFrameworkCore</c> 程序集验证「版本门控是否生效」（≥7 才生成），本文件则
/// 在真实 EF Core 引用下验证「生成的 ValueConverter 与注册代码确实能编译、形态正确」。
/// </para>
/// <para>
/// EF Core 集成生成器额外要求源码出现 <c>ConfigureConventions(ModelConfigurationBuilder)</c>
/// 语法（见 <see cref="StronglyTypedIdGenerator"/> 的 <c>HasDbContextConventions</c> 门槛），
/// 故示例源码必须携带一个 DbContext 子类，否则生成器不被选入、反向用例将失去判别力。
/// </para>
/// <para>
/// 本文件刻意排除 Swagger / Dapper / AspNetCoreMvc / AspNetCoreOpenApi 的门控程序集，
/// 只保留真实 EF Core 引用，使「真实引用 EF Core 的消费者」画像成立。
/// </para>
/// </remarks>
public class EfCoreCodeGeneratorTests
{
    #region 辅助：驱动生成器并捕获 EF Core 产物

    /// <summary>
    /// 宿主 bin 里 EF Core 主包与 Abstractions 的绝对路径。
    /// </summary>
    /// <remarks>
    /// <para>
    /// EF Core 8 起 <c>ModelConfigurationBuilder</c> / <c>IModel</c> 等真实定义在
    /// <c>Microsoft.EntityFrameworkCore.Abstractions</c>，主包 <c>Microsoft.EntityFrameworkCore</c> 只做类型转发。
    /// 只把主包喂进合成编译时，编译器无法从中解析转发的 <c>ModelConfigurationBuilder</c>，
    /// 示例源码的 <c>ConfigureConventions(ModelConfigurationBuilder …)</c> 会 CS0246、EF Core 产物不被触发。
    /// 因此这里直接按<b>宿主 bin 里的文件路径</b>把主包与 Abstractions 两个 dll 都喂进引用集，
    /// 绕开宿主 AppDomain 是否已加载这些类型的偶发状态。
    /// </para>
    /// </remarks>
    private static readonly string _efCoreMainDll =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Microsoft.EntityFrameworkCore.dll"));

    private static readonly string _efCoreAbstractionsDll =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Microsoft.EntityFrameworkCore.Abstractions.dll"));

    private static CSharpCompilation CreateCompilation(string sourceCode, params MetadataReference[] extraReferences)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);

        // 种子：强类型 Id 运行时 + System.Text.Json（核心生成器）+ Newtonsoft（门控无关但产物里引用）
        // + System.Linq.Expressions（EF Core 产物里 ValueConverter 的 lambda 被编译为 Expression<Func<…>>，
        // 缺此程序集则产物 CS0012「类型 Expression<> 在未被引用的程序集中定义」）。
        var seedAssemblies = new[]
        {
            typeof(StronglyTypedIdAttribute).Assembly,
            typeof(System.Text.Json.JsonSerializer).Assembly,
            typeof(Newtonsoft.Json.JsonSerializer).Assembly,
            typeof(System.Linq.Expressions.Expression).Assembly,
        };

        // 排除会触发其它集成生成器的门控程序集（Swagger / Dapper / OpenApi / Mvc 等），避免本用例被它们的问题带偏；
        // 真实 EF Core 主包与 Abstractions 不在排除之列（本测试要验证 EF Core 产物可编译）。
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

        // 显式按文件路径补 EF Core 主包 + Abstractions（宿主 bin 里的真实 dll）：
        // 确保 ModelConfigurationBuilder / ValueConverter / ModelBuilder 全部可解析。
        var efCoreReferences = new[]
        {
            MetadataReference.CreateFromFile(_efCoreMainDll),
            MetadataReference.CreateFromFile(_efCoreAbstractionsDll),
        };

        // 去重：同一文件已被 AppDomain 引用时不要重复喂（Roslyn 允许同名重复，但去重更稳）。
        var distinct = references
            .Concat(efCoreReferences)
            .Distinct()
            .ToArray();

        return CSharpCompilation.Create(
            "Len.StronglyTypedId.EfCore.Test",
            [syntaxTree],
            distinct,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static IReadOnlyDictionary<string, string> GetGeneratedCode(string sourceCode, params MetadataReference[] extraReferences)
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
            or "Swashbuckle.AspNetCore.SwaggerGen"
            or "Microsoft.OpenApi"
            or "Microsoft.AspNetCore.Mvc"
            or "Microsoft.AspNetCore.Mvc.Core"
            or "Microsoft.AspNetCore.Mvc.Abstractions"
            or "Microsoft.AspNetCore.OpenApi"
            or "Microsoft.AspNetCore.Routing"
            or "Dapper");

    /// <summary>
    /// EF Core 集成生成器的示例源码：必须出现 <c>ConfigureConventions</c> 语法，否则不被选入。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 必须带 <c>using Microsoft.EntityFrameworkCore;</c>：示例里 <c>TestDbContext : DbContext</c> 的
    /// <c>ConfigureConventions(ModelConfigurationBuilder)</c> 形参 <c>ModelConfigurationBuilder</c> 在
    /// <c>Microsoft.EntityFrameworkCore</c> 命名空间，缺了该 using 会 CS0246、EF Core 产物不被触发。
    /// 这与 <see cref="StronglyTypedIdGeneratorTests"/> 里 EF Core 用例的源码范式一致。
    /// </para>
    /// </remarks>
    private const string EfCoreSource = """
        using System;
        using Microsoft.EntityFrameworkCore;

        namespace Len.StronglyTypedId;

        [StronglyTypedId]
        public partial record struct OrderId(Guid Value);

        public class TestDbContext : DbContext
        {
            protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
            {
            }
        }
        """;

    #endregion

    #region 顺序属性

    [Fact]
    public void EfCore_Should_HaveOrderThree()
    {
        EfCoreCodeGenerator.Instance.Order.Should().Be(3);
    }

    #endregion

    #region 真实引用下的产物形态

    [Fact]
    public void EfCore_Should_GenerateConverterAndRegistrationFiles_WhenEntityFrameworkCoreReferenced()
    {
        var generated = GetGeneratedCode(EfCoreSource);

        // 逐 Id 的转换器文件 + 列举全部 Id 的聚合注册文件。
        generated.Should().ContainKey("Len.StronglyTypedId.OrderId.EntityFrameworkCore.g.cs");
        generated.Should().ContainKey("StronglyTypedIds.EntityFrameworkCore.g.cs");
    }

    [Fact]
    public void EfCore_Should_GenerateValueConverterForId()
    {
        var generated = GetGeneratedCode(EfCoreSource);

        var converter = generated["Len.StronglyTypedId.OrderId.EntityFrameworkCore.g.cs"];

        converter.Should().Contain(
            "class OrderIdConverter : global::Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<global::Len.StronglyTypedId.OrderId, global::System.Guid>")
            .And.Contain(
                "public OrderIdConverter() : base(v => v.Value, val => global::Len.StronglyTypedId.OrderId.Create(val))")
            .And.Contain(
                "public static void ApplyTo(global::Microsoft.EntityFrameworkCore.ModelConfigurationBuilder configurationBuilder) =>")
            .And.Contain(
                "configurationBuilder.Properties<global::Len.StronglyTypedId.OrderId>().HaveConversion(typeof(OrderIdConverter));");
    }

    [Fact]
    public void EfCore_Should_GenerateModelBuilderScanEntry()
    {
        var generated = GetGeneratedCode(EfCoreSource);

        var converter = generated["Len.StronglyTypedId.OrderId.EntityFrameworkCore.g.cs"];

        converter.Should().Contain(
            "public static void ApplyTo(global::Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder)")
            .And.Contain(
                "if (property.ClrType == typeof(global::Len.StronglyTypedId.OrderId))");
    }

    [Fact]
    public void EfCore_Should_RegisterApplyToForBothEntries()
    {
        var generated = GetGeneratedCode(EfCoreSource);

        var registration = generated["StronglyTypedIds.EntityFrameworkCore.g.cs"];

        registration.Should().Contain(
            "public static void ApplyTo(global::Microsoft.EntityFrameworkCore.ModelConfigurationBuilder configurationBuilder)")
            .And.Contain("OrderIdConverter.ApplyTo(configurationBuilder);")
            .And.Contain(
            "public static void ApplyTo(global::Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder)")
            .And.Contain("OrderIdConverter.ApplyTo(modelBuilder);");
    }

    [Fact]
    public void EfCore_Should_GenerateConverterForEveryId()
    {
        var source = """
            using System;
            using Microsoft.EntityFrameworkCore;

            namespace Len.StronglyTypedId;

            [StronglyTypedId]
            public partial record struct OrderId(Guid Value);

            [StronglyTypedId]
            public partial record struct ProductId(int Value);

            public class TestDbContext : DbContext
            {
                protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
                {
                }
            }
            """;

        var generated = GetGeneratedCode(source);

        var registration = generated["StronglyTypedIds.EntityFrameworkCore.g.cs"];

        registration.Should().Contain("OrderIdConverter.ApplyTo(configurationBuilder);")
            .And.Contain("ProductIdConverter.ApplyTo(configurationBuilder);");
    }

    #endregion

    #region 消歧：不同命名空间下的同名 Id 生成互不冲突的转换器

    [Fact]
    public void EfCore_Should_QualifyConverterName_WhenSameNameInDifferentNamespaces()
    {
        // 两个命名空间下各有一个 OrderId：转换器必须用「命名空间_类型名」消歧，避免 CS0102。
        // 两个 Id 都放在「与运行时程序集命名空间 Len.StronglyTypedId 完全无关」的顶层命名空间
        // （Domain.Orders / Domain.Inventory），彻底避开本库命名空间族的匹配歧义；
        // 用块作用域 namespace 确保各自独立落到目标顶层命名空间、互不嵌套。
        var source = """
            using System;
            using Len.StronglyTypedId;
            using Microsoft.EntityFrameworkCore;

            namespace Domain.Orders
            {
                [StronglyTypedId]
                public partial record struct OrderId(Guid Value);
            }

            namespace Domain.Inventory
            {
                [StronglyTypedId]
                public partial record struct OrderId(Guid Value);
            }

            public class TestDbContext : DbContext
            {
                protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
                {
                }
            }
            """;

        var generated = GetGeneratedCode(source);

        generated.Should().ContainKey("Domain.Orders.OrderId.EntityFrameworkCore.g.cs");
        generated.Should().ContainKey("Domain.Inventory.OrderId.EntityFrameworkCore.g.cs");

        // 消歧类名：Domain_Orders_OrderIdConverter / Domain_Inventory_OrderIdConverter。
        generated["Domain.Orders.OrderId.EntityFrameworkCore.g.cs"]
            .Should().Contain("class Domain_Orders_OrderIdConverter");
        generated["Domain.Inventory.OrderId.EntityFrameworkCore.g.cs"]
            .Should().Contain("class Domain_Inventory_OrderIdConverter");
    }

    #endregion

    #region 消费者可编译性：生成文件在真实 EF Core 引用下必须能编译

    /// <summary>
    /// 回归用例：EF Core 产物在「真实引用 Microsoft.EntityFrameworkCore」的消费者里必须能编译。
    /// </summary>
    /// <remarks>
    /// 生成文件里所有 EF Core 符号都用 <c>global::</c> 全限定，不依赖任何隐式 using，
    /// 因此 Web SDK 与非 Web SDK 消费者都通。必须真正 Emit 一次产物（而非只比对文本），
    /// 才能抓住「类型 / 命名空间不可解析」这类真机缺陷。
    /// </remarks>
    [Fact]
    public void EfCore_Should_Compile_WhenConsumerReferencesEntityFrameworkCore()
    {
        var compilation = CreateCompilation(EfCoreSource);

        var result = CSharpGeneratorDriver
            .Create(new StronglyTypedIdGenerator())
            .RunGenerators(compilation)
            .GetRunResult();

        result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        // 编译「全部」生成产物：核心文件提供 OrderId 的 IParsable / Create 实现，
        // 是 EF Core 产物里 ValueConverter 构造 lambda 所依赖的；只 Emit 单文件会缺依赖而误报。
        var generatedTrees = result.Results[0].GeneratedSources
            .Select(source => CSharpSyntaxTree.ParseText(source.SourceText.ToString(), path: source.HintName))
            .ToArray();

        using var stream = new MemoryStream();
        var emit = compilation
            .AddSyntaxTrees(generatedTrees)
            .Emit(stream);

        var errors = emit.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();

        errors.Should().BeEmpty(string.Join("\n", errors.Select(error => error.ToString())));
    }

    #endregion
}
