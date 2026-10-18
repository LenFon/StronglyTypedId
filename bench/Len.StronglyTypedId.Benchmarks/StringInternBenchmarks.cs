using BenchmarkDotNet.Attributes;
using Len.StronglyTypedId.Domain;

namespace Len.StronglyTypedId.Benchmarks;

/// <summary>
/// string 基元 Id 的驻留缓存（生成代码里的 <c>Intern</c>）在不同并发度下的吞吐。
/// </summary>
/// <remarks>
/// <para>
/// 被测对象是<b>真实生成代码</b>：<see cref="UserId"/> 是 <c>samples/Len.StronglyTypedId.Domain</c> 里
/// 声明的 <c>partial record struct UserId(string Value)</c>，其 <c>Create</c> 会把取值送进每类型独立的
/// <c>ConcurrentDictionary&lt;string, string&gt;</c> 再构造实例。这里不另写一份等价实现，
/// 否则量到的就不是使用者实际跑的那条路径。
/// </para>
/// <para>
/// 负载取「稳态命中」：同一批取值被反复创建，这是 Id 在真实业务里的主导形态（同一个 Id 在
/// 反序列化、路由绑定、EF 物化里被反复重建）。全新增键的极端负载另行量化过，结论见
/// <c>ROADMAP-ANALYSIS.md</c> 的 A5 条目。
/// </para>
/// <para>
/// 用 <see cref="Parallel.For(int, int, ParallelOptions, Action{int})"/> 而不是手工建线程：
/// BenchmarkDotNet 的预热阶段会把线程池养到所需规模，正式测量时不再有爬坡开销。
/// </para>
/// </remarks>
public class StringInternBenchmarks
{
    /// <summary>取值池大小。够大以避免「全命中同一批缓存行」把争用测没，又不至于撑爆 L2。</summary>
    private const int KeyCount = 65_536;

    private static readonly string[] _values = CreateValues();

    /// <summary>并发度。取 1 / 4 / 8 / 24，覆盖单线程到本机逻辑核数上限。</summary>
    [Params(1, 4, 8, 24)]
    public int Threads { get; set; }

    /// <summary>并发创建 string 基元 Id 的总吞吐。</summary>
    /// <returns>所有线程读到的字符数之和，仅用于阻止调用被优化掉。</returns>
    [Benchmark]
    public int Create()
    {
        var sink = 0;

        Parallel.For(
            0,
            Threads,
            new ParallelOptions { MaxDegreeOfParallelism = Threads },
            thread =>
            {
                var values = _values;
                var index = (thread * 997) % values.Length;
                var local = 0;

                for (var i = 0; i < values.Length; i++)
                {
                    local += UserId.Create(values[index]).Value.Length;

                    if (++index == values.Length)
                    {
                        index = 0;
                    }
                }

                Interlocked.Add(ref sink, local);
            });

        return sink;
    }

    private static string[] CreateValues()
    {
        var values = new string[KeyCount];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = "usr_" + i.ToString("x8");
        }

        return values;
    }
}
