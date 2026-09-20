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
/// <para>
/// 引用集特意不按「当前进程加载到的程序集」来拼。基准进程由 BenchmarkDotNet 在另一个目录下托管，
/// 其输出目录里混着基准框架与 Roslyn 自身的程序集，按目录扫描就只能靠黑名单再把它们剔掉；
/// 而 <see cref="ConsumerProfile"/> 交付的是编译器真正吃进去的那份 @(ReferencePath) 清单，
/// 既准确又无需维护黑名单。
/// </para>
/// <para>
/// 一次「生成器运行」= 一个全新编译。符号是惰性实现的，同一个编译里第二次枚举会明显快于第一次，
/// 因此冷热两种成本分开测量：热枚举见 <see cref="DiscoveryBenchmarks"/>，
/// 冷枚举见 <see cref="ColdDiscoverBenchmarks"/>。
/// </para>
/// </remarks>
internal static class ConsumerScenario
{
    /// <summary>
    /// 消费者源码：一个顶层 Id、一个嵌套 Id，以及 EF Core 生成器的闸门（<c>protected override ConfigureConventions</c>）。
    /// </summary>
    /// <remarks>
    /// 约定配置的方法体刻意留空：EF Core 生成器只看「是否存在这个方法」，而方法体里能写的是生成产物
    /// （转换器类名），一旦生成器改了命名，基准就会因为虚构的编译错误而失效。
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

    /// <summary>本次编译声明的两个 Id，用元数据名直接取；嵌套类型的分隔符是 <c>+</c>。</summary>
    private static readonly string[] _declaredTypeNames =
    [
        "Consumer.CustomerId",
        "Consumer.OrderAggregate+OrderId",
    ];

    /// <summary>场景自检的期望产出：清单里出现 EfCore 与 Swagger 的生成文件，才说明 Discover 会被跑两遍。</summary>
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

    private static readonly ImmutableArray<MetadataReference> _references = CreateReferences();

    public static ImmutableArray<MetadataReference> References => _references;

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
    /// 并按 <see cref="ModuleInfo.Comparer"/> 去重后收集——与生成器拿到的模块集合一致。
    /// </summary>
    public static ImmutableArray<ModuleInfo> ResolveModules(CSharpCompilation compilation)
        => [.. References
            .SelectMany(reference => GetModules(reference, compilation))
            .Distinct(new ModuleInfo.Comparer())];

    /// <summary>
    /// 本次编译声明的强类型 Id。
    /// </summary>
    /// <remarks>
    /// 数量对成本没有实质影响（相对整个引用集的类型总数可以忽略），但带上它 <c>Discover</c> 的
    /// <c>Union</c>/<c>Distinct</c> 段才不是空转，场景也更接近真实调用。
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
    /// 两个条件都必须成立，基准才量的是「一次真实构建」。若只断言编译通过，生成器一个都没跑也会「通过」；
    /// 若只断言产出齐全，源码写错（例如 EF Core 的 API 用法变了）则不会被发现。
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
            throw new InvalidOperationException(
                $"基准场景已失效：生成后的编译有 {errors.Length} 个错误，例如 {errors[0]}。");
        }
    }

    /// <summary>把基准场景的规模写进运行日志，让结果自带自变量。</summary>
    public static string Describe(int assemblyCount, int typeCount, int discoveredIdCount)
        => $"[scenario] assemblies={assemblyCount} types={typeCount} discoveredIds={discoveredIdCount}";

    private static IEnumerable<ModuleInfo> GetModules(MetadataReference reference, Compilation compilation)
    {
        try
        {
            // 立刻物化：GetModules 对项目引用分支返回的是惰性查询，异常会逃出 try 之外。
            return [.. reference.GetModules(compilation)];
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
