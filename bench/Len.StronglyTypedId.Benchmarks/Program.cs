using BenchmarkDotNet.Running;

namespace Len.StronglyTypedId.Benchmarks;

internal static class Program
{
    private static int Main(string[] args)
    {
        // --gate 走「固定轮次的确定性门槛」，不经过 BenchmarkDotNet：CI 上要的是可比对的阻断式判据，
        // 不是统计报告。其余参数原样交给 BDN（本地量化用）。
        if (args.Length > 0 && args[0] == "--gate")
        {
            return PerformanceGate.Run(args);
        }

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        return 0;
    }
}
