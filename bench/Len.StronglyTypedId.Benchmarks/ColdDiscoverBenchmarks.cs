using System.Collections.Immutable;
using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;

namespace Len.StronglyTypedId.Benchmarks;

/// <summary>
/// 一次生成器运行中<b>第一次</b>调用 <see cref="StronglyTypedIdDiscovery.Discover"/> 的成本，
/// 也就是冷状态下的成本：符号由元数据惰性实现，第一次枚举要为「实现这些符号」额外付费。
/// </summary>
/// <remarks>
/// 每次迭代重建编译，从「符号尚未实现」起步；PE 解析不在被测范围内（引用实例进程内复用）。
/// </remarks>
[MemoryDiagnoser]
public class ColdDiscoverBenchmarks
{
    private ImmutableArray<MetadataReference> _references;
    private ImmutableArray<SyntaxTree> _syntaxTrees;
    private ImmutableArray<ModuleInfo> _modules;
    private ImmutableArray<StronglyTypedIdInfo> _declaredIds;

    [GlobalSetup]
    public void Setup()
    {
        // 只准备引用与语法树，绝不触碰符号：一旦枚举过一次，符号就「热」了，冷启动成本也就测不出来了。
        _references = ConsumerScenario.References;
        _syntaxTrees = ConsumerScenario.CreateSyntaxTrees();
    }

    [IterationSetup]
    public void SetupIteration()
    {
        var compilation = ConsumerScenario.CreateCompilation(_references, _syntaxTrees);
        _modules = ConsumerScenario.ResolveModules(compilation);
        _declaredIds = ConsumerScenario.GetDeclaredIds(compilation);
    }

    [Benchmark]
    public int Discover_FirstCallOfRun() => StronglyTypedIdDiscovery.Discover(_declaredIds, _modules).Count();
}
