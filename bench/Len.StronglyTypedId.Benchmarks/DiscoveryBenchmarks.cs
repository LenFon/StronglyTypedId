using System.Collections.Immutable;
using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;

namespace Len.StronglyTypedId.Benchmarks;

/// <summary>
/// <see cref="StronglyTypedIdDiscovery.Discover"/> 在真实引用集上的成本，以及「被跑两遍」的放大效应。
/// </summary>
/// <remarks>
/// <para>
/// EfCore 与 Swagger 两个生成器各调用一次 Discover，且拿到的是同一份模块集合、同一个编译
/// （都来自同一个 <c>MetadataReferencesProvider.Collect()</c>），于是「遍历每个引用程序集的全部类型」
/// 在<b>同一次生成器运行中被完整地做了两遍</b>。用例命名对应消费者的两种形态：只引用其中一个包（一次）、
/// 两个都引用（两次）。
/// </para>
/// <para>
/// 本组全部在「符号已实现」的热状态下测量（<see cref="Setup"/> 先做一次全量枚举预热），因为第二次
/// Discover 面对的正是热符号；一次运行中第一次调用的冷启动成本由 <see cref="ColdDiscoverBenchmarks"/> 单独测。
/// </para>
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

    /// <summary>现状：只启用 EF Core 或只启用 Swashbuckle 时的单次开销。</summary>
    [Benchmark(Baseline = true)]
    public int Discover_OneCall() => Discover().Count();

    /// <summary>现状：两个包都引用时，两个生成器各跑一遍，全量枚举做两遍——本例应约等于基准的两倍。</summary>
    [Benchmark]
    public int Discover_TwoCalls() => Discover().Count() + Discover().Count();

    /// <summary>
    /// 反事实（未实现的「加缓存」方案）：把每个模块的类型表缓存下来，同一次运行内的第二次 Discover
    /// 只付查到缓存的成本。
    /// </summary>
    /// <remarks>
    /// 每次调用前清空缓存，模拟「一次生成器运行从空缓存开始」，因此本例量到的是一次运行的<b>总</b>开销，
    /// 可以直接与 <see cref="Discover_TwoCalls"/> 对比：两者的差就是缓存能省下的部分。
    /// </remarks>
    [Benchmark]
    public int Discover_TwoCalls_WithCache()
    {
        _typeCache.Clear();

        return DiscoverWithCache().Count() + DiscoverWithCache().Count();
    }

    private IEnumerable<StronglyTypedIdInfo> Discover()
        => StronglyTypedIdDiscovery.Discover(_declaredIds, _modules);

    /// <summary>
    /// 复刻 <see cref="StronglyTypedIdDiscovery.Discover"/> 的管道，只把「取类型表」换成缓存版本。
    /// 管道必须与产品代码保持一致，否则反事实不再等价——<see cref="Setup"/> 会校验两者结果相同。
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
