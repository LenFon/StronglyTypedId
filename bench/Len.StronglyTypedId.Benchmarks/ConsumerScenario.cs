using System.Collections.Immutable;
using Len.StronglyTypedId.Benchmarks.Consumer;
using Len.StronglyTypedId.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Len.StronglyTypedId.Benchmarks;

/// <summary>
/// 基准所模拟的「真实消费者编译」：引用集取自 <see cref="ConsumerProfile"/>，源码取自 <see cref="Source"/>。
/// </summary>
/// <remarks>
/// 引用集取自编译器真实的 ReferencePath 清单，不做目录扫描；一次生成器运行 = 一个全新编译，冷热成本分开测。
/// </remarks>
internal static class ConsumerScenario
{
    /// <summary>
    /// 消费者源码：一个顶层 Id、一个嵌套 Id，以及 EF Core 生成器的闸门（<c>protected override ConfigureConventions</c>）。
    /// </summary>
    /// <remarks>
    /// 方法体刻意留空：EF Core 生成器只看方法是否存在，写进产物名会在改名后失效。
    /// </remarks>
    public const string Source = """
        using System;
        using Len.StronglyTypedId;
        using Microsoft.EntityFrameworkCore;

        namespace Consumer
        {
            [StronglyTypedId]
            public partial record CustomerId(Guid Value);

            public partial class OrderAggregate
            {
                [StronglyTypedId]
                public partial record struct OrderId(int Value);
            }

            public partial class ConsumerDbContext : DbContext
            {
                protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
                {
                }
            }
        }
        """;

    /// <summary>
    /// 本次编译声明的两个 Id，用元数据名直接取；嵌套类型的分隔符是 <c>+</c>。
    /// </summary>
    private static readonly string[] _declaredTypeNames =
    [
        "Consumer.CustomerId",
        "Consumer.OrderAggregate+OrderId",
    ];

    /// <summary>
    /// 场景自检的期望产出：清单里出现 EfCore 与 Swagger 的生成文件，才说明 Discover 会被跑两遍。
    /// </summary>
    private static readonly string[] _expectedHintNames =
    [
        "Consumer.CustomerId.g.cs",
        "Consumer.CustomerId.SystemTextJson.g.cs",
        "Consumer.CustomerId.NewtonsoftJson.g.cs",
        "Consumer.CustomerId.EntityFrameworkCore.g.cs",
        "Consumer.OrderAggregate.OrderId.g.cs",
        "StronglyTypedIds.EntityFrameworkCore.g.cs",
        "StronglyTypedIds.Swagger.g.cs",
    ];

    /// <summary>
    /// 本次编译的引用集，由 <see cref="ConsumerProfile"/> 给出的路径清单一次性解析而来。
    /// </summary>
    public static ImmutableArray<MetadataReference> References { get; } = CreateReferences();

    public static ImmutableArray<SyntaxTree> CreateSyntaxTrees() => [CSharpSyntaxTree.ParseText(Source)];

    public static CSharpCompilation CreateCompilation(
        ImmutableArray<MetadataReference> references,
        ImmutableArray<SyntaxTree> syntaxTrees)
        => CSharpCompilation.Create(
            assemblyName: "Consumer",
            syntaxTrees: syntaxTrees,
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    /// <summary>
    /// 复刻增量生成器里 <c>MetadataReferencesProvider.Combine(CompilationProvider).SelectMany(GetModules)</c> 那一步，
    /// 并按 <see cref="ModuleInfo.Comparer"/> 去重后收集，与生成器拿到的模块集合一致。
    /// </summary>
    public static ImmutableArray<ModuleInfo> ResolveModules(CSharpCompilation compilation)
        => [.. References
            .SelectMany(reference => GetModules(reference, compilation))
            .Distinct(new ModuleInfo.Comparer())];

    /// <summary>
    /// 本次编译声明的强类型 Id。
    /// </summary>
    /// <remarks>
    /// 数量对成本无实质影响，但能让 <c>Union</c>/<c>Distinct</c> 段不空转。
    /// </remarks>
    public static ImmutableArray<StronglyTypedIdInfo> GetDeclaredIds(CSharpCompilation compilation)
        => [.. _declaredTypeNames
            .Select(name => compilation.GetTypeByMetadataName(name))
            .OfType<INamedTypeSymbol>()
            .Select(symbol => new StronglyTypedIdInfo(symbol))];

    /// <summary>
    /// 引用集里类型最多的前 <paramref name="count"/> 个程序集，用来把「成本集中在哪几个程序集」
    /// 直接排进结果表，而不必在参数里硬编码程序集名（名随框架版本变化，硬编码会过期）。
    /// </summary>
    public static IEnumerable<string> LargestAssemblies(int count)
    {
        var compilation = CreateCompilation(References, CreateSyntaxTrees());

        return ResolveModules(compilation)
            .Select(module => (module.Name, Count: module.GetTypes().Length))
            .OrderByDescending(entry => entry.Count)
            .ThenBy(entry => entry.Name, StringComparer.Ordinal)
            .Take(count)
            .Select(entry => entry.Name);
    }

    /// <summary>
    /// 跑一遍完整流程，确认基准场景没有失效：生成的产出齐全（EfCore 与 Swagger 都被触发），
    /// 且生成后的编译零错误。
    /// </summary>
    /// <remarks>
    /// 两个条件都要断言：只验编译通过会放过「生成器没跑」，只验产出齐全会放过源码写错。
    /// </remarks>
    public static void VerifyScenario(
        ImmutableArray<MetadataReference> references,
        ImmutableArray<SyntaxTree> syntaxTrees)
    {
        var compilation = CreateCompilation(references, syntaxTrees);

        var driver = CSharpGeneratorDriver
            .Create(new StronglyTypedIdGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);

        var hintNames = driver
            .GetRunResult()
            .Results
            .SelectMany(result => result.GeneratedSources)
            .Select(source => source.HintName)
            .ToHashSet(StringComparer.Ordinal);

        var missing = _expectedHintNames.Where(name => !hintNames.Contains(name)).ToArray();

        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                $"基准场景已失效：未生成 {string.Join("、", missing)}；实际生成 {string.Join("、", hintNames.Order(StringComparer.Ordinal))}。");
        }

        var errors = outputCompilation
            .GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        if (errors.Length > 0)
        {
            // 列出前几条而不是只报第一条：多个集成同时失效时，只看第一条会让人以为只有一个问题。
            throw new InvalidOperationException(
                $"基准场景已失效：生成后的编译有 {errors.Length} 个错误：\n{string.Join("\n", errors.Take(5).Select(error => "  " + error))}");
        }
    }

    /// <summary>
    /// 把基准场景的规模写进运行日志，让结果自带自变量。
    /// </summary>
    public static string Describe(int assemblyCount, int typeCount, int discoveredIdCount)
        => $"[scenario] assemblies={assemblyCount} types={typeCount} discoveredIds={discoveredIdCount}";

    private static IEnumerable<ModuleInfo> GetModules(MetadataReference reference, Compilation compilation)
    {
        try
        {
            // 立刻物化：项目引用分支返回的是惰性查询，异常会逃出 try 之外。
            return [.. reference.GetModuleIdentities().Select(identity => identity.ResolveAssembly(compilation))];
        }
        catch (BadImageFormatException)
        {
            // 引用集里混入非托管 dll 时，与编译器一致地跳过，而不是让整个基准崩掉。
            return [];
        }
    }

    private static ImmutableArray<MetadataReference> CreateReferences()
    {
        var paths = ConsumerProfile.ReferencePaths()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var missing = paths.Where(path => !File.Exists(path)).ToArray();

        if (missing.Length > 0)
        {
            // 宁可响亮地失败：静默少掉几个引用，量到的是一个比真实消费者更小的引用集。
            throw new InvalidOperationException(
                $"引用集里有 {missing.Length} 个路径不存在（例如 {string.Join("、", missing.Take(3))}）：重新生成一次基准工程即可。");
        }

        return [.. paths.Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))];
    }
}
