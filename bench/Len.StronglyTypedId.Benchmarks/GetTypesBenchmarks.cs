using BenchmarkDotNet.Attributes;

namespace Len.StronglyTypedId.Benchmarks;

/// <summary>
/// 单个引用程序集的<b>全量递归类型枚举</b>成本——<see cref="ModuleInfo.GetTypes"/> 就是这段代码。
/// </summary>
/// <remarks>
/// 被测程序集不硬编码，而是取引用集里类型最多的前几个：这一项是 Discover 的成本构成单元，
/// 整个引用集的开销正是这些程序集逐个累加后再乘以调用次数。
/// </remarks>
[MemoryDiagnoser]
public class GetTypesBenchmarks
{
    private static readonly Lazy<string[]> _assemblies = new(() => [.. ConsumerScenario.LargestAssemblies(5)]);

    private ModuleInfo _module = null!;

    /// <summary>参数源：引用集里类型最多的前 5 个程序集。</summary>
    /// <remarks>
    /// 用参数源而不是 <c>[Params(...)]</c> 硬编码程序集名：程序集名随目标框架版本变化，
    /// 写死会在换代时静默失配；这里直接由引用集自身排名决定。
    /// </remarks>
    public IEnumerable<string> Assemblies => _assemblies.Value;

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
