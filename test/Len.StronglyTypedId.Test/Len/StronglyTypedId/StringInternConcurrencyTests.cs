using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using FluentAssertions;

namespace Len.StronglyTypedId;

/// <summary>
/// string 基元 Id 的驻留缓存（每个 Id 类型一个 <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey, TValue}"/>）
/// 在并发下的正确性。
/// </summary>
/// <remarks>
/// <para>
/// 驻留的正确性判据只有一条：<b>取值相等的字符串最终共享同一个引用</b>。单线程下由 <c>GetOrAdd</c> 直接保证，
/// 但多线程同时首次写入同一取值时，<c>GetOrAdd</c> 的工厂可能被调用多次、只有一个结果被留下 ——
/// 若实现改成「先查后写」或「TryAdd 后不回读」，同一取值就会对应多个引用，而值相等断言
/// （<c>==</c> / <c>Equals</c>）对此完全无感。故这里只断言引用同一性。
/// </para>
/// <para>
/// 性能侧不在此断言（阈值型断言在不同机器上必然抖动），由
/// <c>bench/Len.StronglyTypedId.Benchmarks/StringInternBenchmarks.cs</c> 量化。
/// </para>
/// </remarks>
public class StringInternConcurrencyTests
{
    private const int ThreadCount = 8;
    private const int RoundsPerThread = 2000;
    private const int SharedValueCount = 32;
    private const int DistinctValueCountPerThread = 500;

    /// <summary>
    /// 多个线程同时对同一批取值反复创建 Id：每个取值最终只能对应一个字符串引用，
    /// 且必须是串行预热时留下的那一个。
    /// </summary>
    [Fact]
    public void Create_Should_ConvergeToOneSharedReference_WhenThreadsRaceOnSameValues()
    {
        var values = Enumerable.Range(0, SharedValueCount)
            .Select(index => $"shared-{index:D2}-{Guid.NewGuid():N}")
            .ToArray();

        // 串行预热：让每个取值先落进缓存，得到一个确定的「胜者」引用。
        // 之后无论哪个线程、哪一轮创建，都必须看到这一个引用。
        var canonical = values.Select(value => StringId.Create(value).Value).ToArray();

        var perThread = RunConcurrently((_, random) =>
        {
            var observed = new HashSet<string>[values.Length];
            for (var index = 0; index < observed.Length; index++)
            {
                observed[index] = new(ReferenceEqualityComparer.Instance);
            }

            for (var round = 0; round < RoundsPerThread; round++)
            {
                var valueIndex = random.Next(values.Length);

                // 每次都用「内容相同、引用不同」的新字符串 —— 直接复用 values[valueIndex] 的话
                // 引用本来就相同，驻留与否无从体现，测试会假通过。
                var id = StringId.Create(new string(values[valueIndex].AsSpan()));
                observed[valueIndex].Add(id.Value);
            }

            return observed;
        });

        for (var valueIndex = 0; valueIndex < values.Length; valueIndex++)
        {
            // 必须先按引用去重：ContainSingle 默认用值相等，8 个线程各贡献一个「值相等」的引用
            // 会被数成 8 项 —— 那正是本测试要抓的缺陷，不能让断言自己先踩进去。
            var references = perThread.SelectMany(observed => observed[valueIndex])
                .Distinct(ReferenceEqualityComparer.Instance);

            references.Should().ContainSingle()
                .Which.Should().BeSameAs(canonical[valueIndex]);
        }
    }

    /// <summary>
    /// 多个线程各自创建互不相同的取值：引用必须两两不同（驻留不能把不同取值折叠到一起），
    /// 且取值本身不能被改写。
    /// </summary>
    [Fact]
    public void Create_Should_KeepReferencesDistinct_WhenThreadsCreateDifferentValues()
    {
        var perThread = RunConcurrently((threadIndex, _) =>
        {
            var created = new (string Expected, string Actual)[DistinctValueCountPerThread];

            for (var index = 0; index < created.Length; index++)
            {
                var value = $"distinct-{threadIndex:D2}-{index:D3}-{Guid.NewGuid():N}";
                created[index] = (value, StringId.Create(new string(value.AsSpan())).Value);
            }

            return created;
        });

        var all = perThread.SelectMany(created => created).ToArray();

        all.Should().OnlyContain(item => item.Actual == item.Expected);
        all.Select(item => item.Actual)
            .Distinct(ReferenceEqualityComparer.Instance)
            .Should().HaveCount(all.Length);
    }

    /// <summary>
    /// 四个构造入口（<c>Create</c> / <c>TryCreate</c> / <c>Parse</c> / <c>TryParse</c>）在生成代码里
    /// 都经过同一个 <c>Intern</c>，故并发混用也必须收敛到同一引用。
    /// </summary>
    [Fact]
    public void AllEntryPoints_Should_ConvergeToOneSharedReference_WhenThreadsRaceOnSameText()
    {
        var text = $"entry-{Guid.NewGuid():N}";
        var canonical = StringId.Create(text).Value;

        var perThread = RunConcurrently((_, random) =>
        {
            var observed = new HashSet<string>(ReferenceEqualityComparer.Instance);

            for (var round = 0; round < RoundsPerThread; round++)
            {
                var fresh = new string(text.AsSpan());

                switch (random.Next(4))
                {
                    case 0:
                        observed.Add(StringId.Create(fresh).Value);
                        break;

                    case 1:
                        StringId.TryCreate(fresh, out var created).Should().BeTrue();
                        observed.Add(created.Value);
                        break;

                    case 2:
                        observed.Add(StringId.Parse(fresh, CultureInfo.InvariantCulture).Value);
                        break;

                    default:
                        StringId.TryParse(fresh, CultureInfo.InvariantCulture, out var parsed).Should().BeTrue();
                        observed.Add(parsed.Value);
                        break;
                }
            }

            return observed;
        });

        perThread.SelectMany(observed => observed)
            .Distinct(ReferenceEqualityComparer.Instance)
            .Should().ContainSingle()
            .Which.Should().BeSameAs(canonical);
    }

    /// <summary>
    /// 让 <see cref="ThreadCount"/> 个线程在同一道闸门后同时起跑，收集各自的返回值。
    /// </summary>
    /// <remarks>
    /// 用显式 <see cref="Thread"/> 而非 <see cref="System.Threading.Tasks.Parallel"/>：后者会按线程池容量
    /// 分批爬坡，线程数达不到 <see cref="ThreadCount"/> 时争用窗口根本不存在，测试会退化成串行。
    /// 任一线程抛出异常都会在汇合后聚合重抛，避免「线程崩了但测试通过」。
    /// </remarks>
    private static T[] RunConcurrently<T>(Func<int, Random, T> body)
    {
        var results = new T[ThreadCount];
        var errors = new Exception?[ThreadCount];
        var threads = new Thread[ThreadCount];

        using var gate = new Barrier(ThreadCount);

        for (var index = 0; index < ThreadCount; index++)
        {
            var threadIndex = index;

            threads[index] = new(() =>
            {
                try
                {
                    // 起跑栅栏：把「同时首次写入同一取值」的窗口压到最小，
                    // 否则线程依次启动，第一个线程早把缓存填好，争用不会发生。
                    gate.SignalAndWait();

                    // 每线程独立种子，让各线程访问取值的顺序不同，扩大交错面。
                    results[threadIndex] = body(threadIndex, new(unchecked((threadIndex + 1) * 7919)));
                }
                catch (Exception ex)
                {
                    errors[threadIndex] = ex;
                }
            })
            {
                IsBackground = true,
            };

            threads[index].Start();
        }

        foreach (var thread in threads)
        {
            thread.Join();
        }

        var failures = errors.OfType<Exception>().ToArray();

        return failures.Length == 0
            ? results
            : throw new AggregateException("并发执行期间有线程抛出异常。", failures);
    }
}
