using BenchmarkDotNet.Attributes;

namespace Len.StronglyTypedId.Benchmarks;

/// <summary>
/// 单个引用程序集的<b>全量递归类型枚举</b>成本——<see cref="ModuleInfo.GetTypes"/> 就是这段代码。
/// </summary>
/// <remarks>
/// 被测程序集取引用集里类型最多的前几个 —— 它们是 Discover 的成本构成单元。
/// </remarks>
[MemoryDiagnoser]
public class GetTypesBenchmarks
{
    private static readonly Lazy<string[]> _assemblies = new(() => [.. ConsumerScenario.LargestAssemblies(5)]);

    private ModuleInfo _module = null!;

    /// <summary>
    /// 参数源：引用集里类型最多的前 5 个程序集。
    /// </summary>
    /// <remarks>
    /// 用参数源而非 <c>[Params]</c>：程序集名随目标框架变化，写死会静默失配。
    /// 标 <see langword="static"/> 是因为只读静态字段，不依赖实例。
    /// </remarks>
    public static IEnumerable<string> Assemblies => _assemblies.Value;

    [ParamsSource(nameof(Assemblies))]
    public string AssemblyName { get; set; } = "";

    [GlobalSetup]
    public void Setup()
    {
        var compilation = ConsumerScenario.CreateCompilation(ConsumerScenario.References, ConsumerScenario.CreateSyntaxTrees());
        _module = ConsumerScenario.ResolveModules(compilation).Single(module => module.Name == AssemblyName);

        // 预热：本组量的是「同一编译内的重复枚举」，不是首次符号实现。后者见 ColdDiscoverBenchmarks。
        _module.GetTypes();
    }

    [Benchmark]
    public int GetTypes() => _module.GetTypes().Length;
}
