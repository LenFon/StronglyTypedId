using System.Collections.Immutable;
using BenchmarkDotNet.Attributes;
using Len.StronglyTypedId.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Len.StronglyTypedId.Benchmarks;

/// <summary>
/// 端到端锚点：完整跑一次生成器（核心 + System.Text.Json + Newtonsoft.Json + EfCore + Swagger 全部启用）。
/// </summary>
/// <remarks>
/// <para>
/// 前几组用例给出「重复枚举值多少」，本组给出分母——一次真实生成器运行总共要花多少，两者相除才知道
/// 这件事值不值得动。分母里包含符号收集、两个生成器各自的 Discover、以及所有生成文本的拼装。
/// </para>
/// <para>
/// 每次迭代重建编译与驱动器：增量驱动器的状态随驱动器保存，复用一次就会命中缓存、量到的就不再是
/// 一次构建的成本。
/// </para>
/// </remarks>
[MemoryDiagnoser]
public class GeneratorRunBenchmarks
{
    private ImmutableArray<MetadataReference> _references;
    private ImmutableArray<SyntaxTree> _syntaxTrees;
    private CSharpGeneratorDriver _driver = null!;
    private CSharpCompilation _compilation = null!;

    [GlobalSetup]
    public void Setup()
    {
        _references = ConsumerScenario.References;
        _syntaxTrees = ConsumerScenario.CreateSyntaxTrees();

        // 场景自检：产出齐全（EfCore 与 Swagger 都被触发）且生成后的编译零错误，否则量到的不是一次真实构建。
        ConsumerScenario.VerifyScenario(_references, _syntaxTrees);
    }

    [IterationSetup]
    public void SetupIteration()
    {
        _compilation = ConsumerScenario.CreateCompilation(_references, _syntaxTrees);
        _driver = CSharpGeneratorDriver.Create(new StronglyTypedIdGenerator());
    }

    [Benchmark]
    public int FullRun()
        => _driver.RunGenerators(_compilation).GetRunResult().Results.Sum(result => result.GeneratedSources.Length);
}
