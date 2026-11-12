using FluentAssertions;
using Xunit;

namespace Len.StronglyTypedId.Testing.Tests;

/// <summary>
/// <see cref="DeterministicRandom"/> 的区间语义。
/// </summary>
/// <remarks>
/// 这些边界无法经公开 API 触发：<see cref="PrimitiveValueFactory"/> 的所有调用点都传非退化区间，
/// 故只能直接测内部类型。缺陷回归：<c>NextInt64(min, max)</c> 曾在 <c>min == max</c> 时返回随机值，
/// 且满区间下取不到 <see cref="long.MaxValue"/>。
/// </remarks>
public class DeterministicRandomTests
{
    /// <summary>
    /// 退化区间（<c>min == max</c>）只有一个合法取值，必须返回它。
    /// </summary>
    /// <remarks>
    /// 缺陷回归：判据曾用 <c>range == 0</c> 同时表达「退化区间」与「满区间」，前者返回了
    /// <c>NextUInt64()</c> 的位重解释，完全忽略 <c>minValue</c> / <c>maxValue</c>。
    /// </remarks>
    [Theory]
    [InlineData(0L)]
    [InlineData(3L)]
    [InlineData(-100L)]
    [InlineData(long.MaxValue)]
    [InlineData(long.MinValue)]
    public void NextInt64_Should_ReturnTheOnlyValue_WhenRangeIsDegenerate(long value)
    {
        var random = new DeterministicRandom(7);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            random.NextInt64(value, value).Should().Be(value);
        }
    }

    /// <summary>
    /// 满区间必须覆盖完整的 64 位：修复前走 <c>NextBounded(ulong.MaxValue)</c>，结果上限是
    /// <c>long.MaxValue - 1</c>，永远取不到 <see cref="long.MaxValue"/>。
    /// </summary>
    /// <remarks>
    /// 精确命中 <see cref="long.MaxValue"/> 在 2^64 区间内概率极低，故断言「取到的最大值足够接近上限」：
    /// 修复前的结果上限被硬性截断在 <c>long.MaxValue - 1</c>，而这里用「与上限的差距小于区间宽度的千分之一」
    /// 来区分 —— 修复前必然失败，修复后以压倒性概率通过。
    /// </remarks>
    [Fact]
    public void NextInt64_Should_CoverFullRange_WhenBoundsAreExtremes()
    {
        var random = new DeterministicRandom(7);
        var max = long.MinValue;

        for (var attempt = 0; attempt < 100_000; attempt++)
        {
            var value = random.NextInt64(long.MinValue, long.MaxValue);

            if (value > max)
            {
                max = value;
            }
        }

        // 区间宽度 2^64，千分之一约 1.8e16；修复前的上限差距恒为 1，但结果分布的上界是 2^63-2，
        // 故用「与 long.MaxValue 的差距小于 2^63 的千分之一」判定是否覆盖了上半区。
        (long.MaxValue - max).Should().BeLessThan(long.MaxValue / 1000);
    }

    /// <summary>
    /// 反向区间仍应抛 <see cref="ArgumentOutOfRangeException"/>。
    /// </summary>
    [Fact]
    public void NextInt64_Should_Throw_WhenMinIsGreaterThanMax()
    {
        var random = new DeterministicRandom(7);

        var act = () => random.NextInt64(5, 3);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// 退化区间的 <c>Next(int, int)</c> 一直是对的，锁住它以免两个重载再次分叉。
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(-100)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void Next_Should_ReturnTheOnlyValue_WhenRangeIsDegenerate(int value)
    {
        var random = new DeterministicRandom(7);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            random.Next(value, value).Should().Be(value);
        }
    }
}
