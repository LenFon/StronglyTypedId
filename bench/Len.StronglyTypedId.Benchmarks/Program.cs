using BenchmarkDotNet.Running;

namespace Len.StronglyTypedId.Benchmarks;

internal static class Program
{
    private static void Main(string[] args)
        => BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}
