using System;
using System.Reflection;
using FluentAssertions;

namespace Len.StronglyTypedId;

/// <summary>
/// 驻留缓存的硬上限：取值集合无界时缓存必须停止增长，同时不能破坏「已缓存取值收敛到同一引用」。
/// </summary>
/// <remarks>
/// 缓存是每 Id 类型一份的静态状态、生命周期等于进程，故上限本身就是契约的一部分：
/// 没有它，把外部输入的 id 逐个建成 Id 就能把内存推到 OOM。
/// </remarks>
public class StringInternCapacityTests
{
    /// <summary>
    /// 灌入远超上限的不同取值后，缓存条目数停在上限，且溢出取值不再被驻留、已缓存取值仍收敛。
    /// </summary>
    [Fact]
    public void Create_Should_StopCachingNewValues_WhenDistinctValuesExceedCapacity()
    {
        var capacity = ReadCapacity();
        capacity.Should().BePositive();

        for (var index = 0; index < capacity * 3; index++)
        {
            InternCapacityId.Create($"capacity-{index:D6}");
        }

        // 关键断言：条目数跟的是上限，不是「见过的不同取值数」。
        ReadCachedCount().Should().Be(capacity);

        // 溢出后不再驻留：两次用内容相同、引用不同的字符串创建，必须拿到两个不同引用。
        var firstOverflow = InternCapacityId.Create(new string("overflow-probe".AsSpan())).Value;
        var secondOverflow = InternCapacityId.Create(new string("overflow-probe".AsSpan())).Value;
        firstOverflow.Should().NotBeSameAs(secondOverflow);

        // 上限之内仍然驻留：命中的是灌入阶段留下的那个实例，而不是本次传入的字符串。
        var cached = InternCapacityId.Create(new string("capacity-000000".AsSpan())).Value;
        cached.Should().BeSameAs(InternCapacityId.Create("capacity-000000").Value);
    }

    /// <summary>
    /// 读取生成代码里的上限常量。
    /// </summary>
    private static int ReadCapacity()
        => (int)typeof(InternCapacityId)
            .GetField("InternCapacity", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetRawConstantValue()!;

    /// <summary>
    /// 读取生成代码里记录的当前条目数。
    /// </summary>
    private static int ReadCachedCount()
        => (int)typeof(InternCapacityId)
            .GetField("_internedCount", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;
}
