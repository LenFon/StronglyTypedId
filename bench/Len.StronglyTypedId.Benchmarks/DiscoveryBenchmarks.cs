using System.Collections.Immutable;
using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;

namespace Len.StronglyTypedId.Benchmarks;

/// <summary>
/// <see cref="StronglyTypedIdDiscovery.Discover"/> 在真实引用集上的成本，以及「被跑五遍」的放大效应。
/// </summary>
/// <remarks>
/// 改造前五个集成生成器各遍历一次引用集，同一次运行枚举五遍；现由 <c>DiscoveredIds</c> 一次算好共用。
/// 本组测「符号已实现」的热状态，冷启动成本见 <see cref="ColdDiscoverBenchmarks"/>。
/// </remarks>
[MemoryDiagnoser]
public class DiscoveryBenchmarks
{
    private ImmutableArray<ModuleInfo> _modules;
    private ImmutableArray<StronglyTypedIdInfo> _declaredIds;
    private Dictionary<ModuleInfo, ImmutableArray<ITypeSymbol>> _typeCache = null!;

    [GlobalSetup]
    public void Setup()
    {
        var compilation = ConsumerScenario.CreateCompilation(ConsumerScenario.References, ConsumerScenario.CreateSyntaxTrees());
        _modules = ConsumerScenario.ResolveModules(compilation);
        _declaredIds = ConsumerScenario.GetDeclaredIds(compilation);

        _typeCache = new Dictionary<ModuleInfo, ImmutableArray<ITypeSymbol>>(new ModuleInfo.Comparer());

        foreach (var module in _modules)
        {
            _typeCache[module] = module.GetTypes();
        }

        var discovered = Discover().Count();

        // 反事实管道必须与产品代码等价，否则量出来的收益是假的。
        var byProduct = Discover().Select(info => info.FullName).ToArray();
        var byCache = DiscoverWithCache().Select(info => info.FullName).ToArray();

        if (!byProduct.SequenceEqual(byCache, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("反事实管道与 Discover 的结果不一致，基准结论不成立。");
        }

        Console.WriteLine(ConsumerScenario.Describe(_modules.Length, _typeCache.Values.Sum(types => types.Length), discovered));
    }

    /// <summary>
    /// 现状（改造后）：发现结果一次算好、各集成生成器共用，全量枚举只做一遍。
    /// </summary>
    [Benchmark(Baseline = true)]
    public int Discover_OneCall() => Discover().Count();

    /// <summary>
    /// 改造前：五个集成生成器各跑一遍，全量枚举做五遍，本例应约等于基准的五倍。
    /// </summary>
    [Benchmark]
    public int Discover_FiveCalls() => Enumerable.Range(0, 5).Sum(_ => Discover().Count());

    /// <summary>
    /// 反事实（另一条未采纳的路）：把每个模块的类型表缓存下来，同一次运行内的后续 Discover
    /// 只付查到缓存的成本。
    /// </summary>
    /// <remarks>
    /// 每次调用前清空缓存，量到的是一次运行的总开销；与 <see cref="Discover_FiveCalls"/> 之差即缓存收益。
    /// </remarks>
    [Benchmark]
    public int Discover_FiveCalls_WithCache()
    {
        _typeCache.Clear();

        return Enumerable.Range(0, 5).Sum(_ => DiscoverWithCache().Count());
    }

    private IEnumerable<StronglyTypedIdInfo> Discover()
        => StronglyTypedIdDiscovery.Discover(_declaredIds, _modules);

    /// <summary>
    /// 复刻 <see cref="StronglyTypedIdDiscovery.Discover"/> 的管道，只把「取类型表」换成缓存版本。
    /// 管道必须与产品代码保持一致，否则反事实不再等价，<see cref="Setup"/> 会校验两者结果相同。
    /// </summary>
    private IEnumerable<StronglyTypedIdInfo> DiscoverWithCache()
        => _modules
            .SelectMany(module => GetTypes(module))
            .Where(type => type.Interfaces.Any(StronglyTypedIdInfo.IsStronglyTypedIdInterface))
            .Select(type => new StronglyTypedIdInfo(type))
            .Union(_declaredIds)
            .Distinct();

    private ImmutableArray<ITypeSymbol> GetTypes(ModuleInfo module)
    {
        if (!_typeCache.TryGetValue(module, out var types))
        {
            types = module.GetTypes();
            _typeCache[module] = types;
        }

        return types;
    }
}
