namespace Len.StronglyTypedId.Testing;

/// <summary>
/// 与运行时版本无关的伪随机源，供 <see cref="PrimitiveValueFactory"/> 取值。
/// </summary>
/// <remarks>
/// 不用 <see cref="Random"/>：它的算法属实现细节，.NET 各版本之间可能变，于是「同一个种子得到同一串值」
/// 只在给定运行时内成立。快照测试与黄金文件需要跨版本、跨机器完全一致，故自带 SplitMix64。
/// <para>
/// 选 SplitMix64 而不是 xoshiro256**：状态只有一个 <see cref="ulong"/>，实现短、无初始化约束，
/// 且分布足够好——这里只需要「可复现的均匀采样」，不需要密码学强度或极长周期。
/// </para>
/// </remarks>
internal sealed class DeterministicRandom
{
    private const ulong Gamma = 0x9E3779B97F4A7C15UL;

    private ulong _state;

    /// <summary>
    /// 用指定种子初始化。
    /// </summary>
    /// <remarks>
    /// 种子先经一次 SplitMix64 混合再作为状态：相邻种子（0、1、2…）若直接当状态，
    /// 前几个输出会高度相关，而测试里连号种子很常见。
    /// </remarks>
    public DeterministicRandom(int seed)
    {
        _state = unchecked((ulong)(uint)seed);
        _ = NextUInt64();
    }

    /// <summary>
    /// 返回 <c>[0, maxValue)</c> 内的整数。
    /// </summary>
    public int Next(int maxValue)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxValue);

        return maxValue == 0 ? 0 : (int)NextBounded((ulong)maxValue);
    }

    /// <summary>
    /// 返回 <c>[minValue, maxValue)</c> 内的整数。
    /// </summary>
    public int Next(int minValue, int maxValue)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minValue, maxValue);

        var range = (ulong)((long)maxValue - minValue);

        return range == 0 ? minValue : (int)(minValue + (long)NextBounded(range));
    }

    /// <summary>
    /// 返回任意 64 位有符号整数。
    /// </summary>
    public long NextInt64() => unchecked((long)NextUInt64());

    /// <summary>
    /// 返回 <c>[minValue, maxValue)</c> 内的 64 位整数。
    /// </summary>
    public long NextInt64(long minValue, long maxValue)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minValue, maxValue);

        // 满区间时 maxValue - minValue 溢出成 0，此时整个 ulong 都是合法取值。
        var range = unchecked((ulong)(maxValue - minValue));

        return range == 0 ? unchecked((long)NextUInt64()) : unchecked(minValue + (long)NextBounded(range));
    }

    /// <summary>
    /// 返回 <c>[0, 1)</c> 内的双精度浮点数。
    /// </summary>
    /// <remarks>
    /// 取高 53 位再除以 2^53：这是 IEEE 754 双精度能精确表示的最大整数位数，
    /// 也是 <see cref="Random.NextDouble"/> 的取值口径。
    /// </remarks>
    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));

    /// <summary>
    /// 用随机字节填满 <paramref name="buffer"/>。
    /// </summary>
    public void NextBytes(byte[] buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        var index = 0;

        while (index < buffer.Length)
        {
            var bits = NextUInt64();

            for (var offset = 0; offset < sizeof(ulong) && index < buffer.Length; offset++)
            {
                buffer[index++] = (byte)(bits >> (offset * 8));
            }
        }
    }

    /// <summary>
    /// 返回 <c>[0, bound)</c> 内的无符号整数，无模偏差。
    /// </summary>
    /// <remarks>
    /// 直接取模会让较小的余数区间概率略高；这里用「拒绝采样」把落在偏置区间的样本丢掉重取。
    /// 偏置区间长度是 <c>2^64 mod bound</c>，bound 远小于 2^64 时命中概率可忽略。
    /// </remarks>
    private ulong NextBounded(ulong bound)
    {
        var threshold = unchecked(0UL - bound) % bound;

        while (true)
        {
            var value = NextUInt64();

            if (value >= threshold)
            {
                return value % bound;
            }
        }
    }

    /// <summary>
    /// SplitMix64 的单步输出。
    /// </summary>
    private ulong NextUInt64()
    {
        unchecked
        {
            _state += Gamma;

            var value = _state;
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;

            return value ^ (value >> 31);
        }
    }
}
