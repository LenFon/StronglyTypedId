using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;
using Len.StronglyTypedId.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Len.StronglyTypedId.Benchmarks;

/// <summary>
/// 生成器性能回归门槛：跑固定工作量，把「每次完整生成器运行的分配字节数」与「耗时」同入库基线比对。
/// </summary>
/// <remarks>
/// 不用 BenchmarkDotNet 做 CI 门槛：单次跑几分钟，且共享 runner 的墙钟抖动常超 ±20%，只会制造假红。
/// 改判确定性指标：分配字节数为主判据（逐字节可复现），耗时只作数量级兜底（容忍 10 倍）。
/// </remarks>
internal static class PerformanceGate
{
    private const int WarmupRuns = 3;
    private const int MeasuredRuns = 10;

    /// <summary>
    /// 写基线时用缩进格式，便于 diff 与人工核对。实例复用：<see cref="JsonSerializerOptions"/>
    /// 首次使用后会冻结并缓存元数据，每次序列化都新建等于把这份缓存重建一遍。
    /// </summary>
    private static readonly JsonSerializerOptions _baselineJsonOptions = new() { WriteIndented = true };

    /// <summary>
    /// 分配量的容忍比例：超出基线 10% 即判为退化。
    /// </summary>
    /// <remarks>
    /// 10% 是量出来的：同机连续跑分配量相差约 0.005%，已留足跨机器余量。
    /// 不能放宽到 20%：把「一次枚举」改回「五次枚举」只涨 16.3%，会被放过。
    /// </remarks>
    private const double AllocatedBytesTolerance = 0.10;

    /// <summary>
    /// 耗时的容忍比例：只在数量级退化时报警（跨机器、跨 runner 的墙钟不可比）。
    /// </summary>
    /// <remarks>
    /// 取 9.0（上限 10 倍）：耗时跨机器不可比（实测 13.97 ms vs 59.75 ms），更紧的值会因 runner 慢而假红。
    /// 放宽不会放过真正的退化：O(n)→O(n²) 远超一个数量级，常规退化由分配量那条 10% 判据负责。
    /// </remarks>
    private const double ElapsedTolerance = 9.0;

    private const string BaselineFileName = "generator-run.baseline.json";

    /// <summary>
    /// 执行门槛检查。<paramref name="args"/> 里出现 <c>--update-baseline</c> 时改为重写基线。
    /// </summary>
    public static int Run(string[] args)
    {
        var updateBaseline = args.Contains("--update-baseline", StringComparer.Ordinal);
        var baselinePath = Path.Combine(
            FindRepositoryRoot(),
            "bench",
            "Len.StronglyTypedId.Benchmarks",
            "baselines",
            BaselineFileName);

        var measurement = Measure();

        if (updateBaseline)
        {
            PrintReport(measurement, baseline: null);
            WriteBaseline(baselinePath, measurement);
            Console.WriteLine($"基线已写入 {baselinePath}");
            return 0;
        }

        var baseline = ReadBaseline(baselinePath);
        PrintReport(measurement, baseline);

        return Evaluate(measurement, baseline);
    }

    private static Measurement Measure()
    {
        var references = ConsumerScenario.References;
        var syntaxTrees = ConsumerScenario.CreateSyntaxTrees();

        // 场景自检：产出齐全（EfCore / Swagger 都被触发）且生成后编译零错误，否则量到的不是一次真实构建。
        ConsumerScenario.VerifyScenario(references, syntaxTrees);

        // 首轮兼作「产出数量的期望值」：不硬编码数字，避免生成器增减产物时门槛跟着失准。
        var expectedSourceCount = RunOnce(references, syntaxTrees);

        for (var run = 0; run < WarmupRuns; run++)
        {
            RunOnce(references, syntaxTrees);
        }

        var allocations = new long[MeasuredRuns];
        var elapsed = new double[MeasuredRuns];

        for (var run = 0; run < MeasuredRuns; run++)
        {
            // 编译与驱动器每轮重建：增量驱动器的状态随驱动器保存，复用一次就会命中缓存，
            // 量到的就不再是「一次构建」的成本。
            var compilation = ConsumerScenario.CreateCompilation(references, syntaxTrees);
            var driver = CSharpGeneratorDriver.Create(new StronglyTypedIdGenerator());

            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var stopwatch = Stopwatch.StartNew();

            var generated = driver
                .RunGenerators(compilation)
                .GetRunResult()
                .Results
                .Sum(result => result.GeneratedSources.Length);

            stopwatch.Stop();
            allocations[run] = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            elapsed[run] = stopwatch.Elapsed.TotalMilliseconds;

            if (generated != expectedSourceCount)
            {
                throw new InvalidOperationException(
                    $"第 {run} 轮产出 {generated} 个文件，与首轮 {expectedSourceCount} 个不一致 —— 门槛不可比。");
            }
        }

        return new Measurement(Median(allocations), Median(elapsed), expectedSourceCount);
    }

    private static int RunOnce(ImmutableArray<MetadataReference> references, ImmutableArray<SyntaxTree> syntaxTrees)
    {
        var compilation = ConsumerScenario.CreateCompilation(references, syntaxTrees);
        var driver = CSharpGeneratorDriver.Create(new StronglyTypedIdGenerator());

        return driver
            .RunGenerators(compilation)
            .GetRunResult()
            .Results
            .Sum(result => result.GeneratedSources.Length);
    }

    private static int Evaluate(Measurement measurement, Baseline baseline)
    {
        var problems = new List<string>();

        var allocatedLimit = baseline.AllocatedBytesPerRun * (1 + baseline.AllocatedBytesTolerance);
        if (measurement.AllocatedBytesPerRun > allocatedLimit)
        {
            problems.Add(
                $"分配量退化：{measurement.AllocatedBytesPerRun:N0} B/次 > 上限 {allocatedLimit:N0} B/次"
                + $"（基线 {baseline.AllocatedBytesPerRun:N0} B，容忍 +{baseline.AllocatedBytesTolerance:P0}）。");
        }

        var elapsedLimit = baseline.ElapsedMillisecondsPerRun * (1 + baseline.ElapsedTolerance);
        if (measurement.ElapsedMillisecondsPerRun > elapsedLimit)
        {
            problems.Add(
                $"耗时退化：{measurement.ElapsedMillisecondsPerRun:F2} ms/次 > 上限 {elapsedLimit:F2} ms/次"
                + $"（基线 {baseline.ElapsedMillisecondsPerRun:F2} ms，容忍 +{baseline.ElapsedTolerance:P0}）。");
        }

        if (problems.Count == 0)
        {
            Console.WriteLine("性能门槛：通过。");
            return 0;
        }

        foreach (var problem in problems)
        {
            Console.Error.WriteLine(problem);
        }

        Console.Error.WriteLine("若退化是有意为之，用 --update-baseline 重写基线并连同改动一起提交。");
        return 1;
    }

    private static void PrintReport(Measurement measurement, Baseline? baseline)
    {
        Console.WriteLine($"生成器性能门槛 · {MeasuredRuns} 轮中位数（预热 {WarmupRuns} 轮）");
        Console.WriteLine($"  生成文件数/次 : {measurement.GeneratedSourceCount}");
        Console.WriteLine($"  分配量/次     : {measurement.AllocatedBytesPerRun:N0} B");
        Console.WriteLine($"  耗时/次       : {measurement.ElapsedMillisecondsPerRun:F2} ms");

        if (baseline is null)
        {
            return;
        }

        Console.WriteLine($"  基线分配量/次 : {baseline.AllocatedBytesPerRun:N0} B（运行时 {baseline.RuntimeVersion}）");
        Console.WriteLine($"  基线耗时/次   : {baseline.ElapsedMillisecondsPerRun:F2} ms");
    }

    private static Baseline ReadBaseline(string path)
    {
        var baseline = File.Exists(path)
            ? JsonSerializer.Deserialize<Baseline>(File.ReadAllText(path))
            : throw new FileNotFoundException(
                $"未找到性能基线 {path}：先跑一次 --update-baseline 生成并提交。",
                path);

        return baseline ?? throw new InvalidOperationException($"性能基线 {path} 反序列化失败。");
    }

    private static void WriteBaseline(string path, Measurement measurement)
    {
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);

        var baseline = new Baseline(
            Scenario: "GeneratorRunBenchmarks.FullRun",
            RuntimeVersion: Environment.Version.ToString(),
            MeasuredRuns: MeasuredRuns,
            AllocatedBytesPerRun: measurement.AllocatedBytesPerRun,
            ElapsedMillisecondsPerRun: Math.Round(measurement.ElapsedMillisecondsPerRun, 3),
            AllocatedBytesTolerance: AllocatedBytesTolerance,
            ElapsedTolerance: ElapsedTolerance);

        File.WriteAllText(path, JsonSerializer.Serialize(baseline, _baselineJsonOptions));
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Len.StronglyTypedId.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("未能从输出目录向上找到 Len.StronglyTypedId.slnx。");
    }

    private static long Median(long[] values) => values.Order().ElementAt(values.Length / 2);

    private static double Median(double[] values) => values.Order().ElementAt(values.Length / 2);

    private sealed record Measurement(long AllocatedBytesPerRun, double ElapsedMillisecondsPerRun, int GeneratedSourceCount);

    private sealed record Baseline(
        string Scenario,
        string RuntimeVersion,
        int MeasuredRuns,
        long AllocatedBytesPerRun,
        double ElapsedMillisecondsPerRun,
        double AllocatedBytesTolerance,
        double ElapsedTolerance);
}
