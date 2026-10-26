using System.Numerics;

namespace Len.StronglyTypedId.Testing;

/// <summary>
/// 为生成器支持的每种基元类型产出合理的值。
/// </summary>
internal sealed class PrimitiveValueFactory(Random random, int stringLength, Func<Type, object?>? customFactory, bool includeBoundaryValues)
{
    /// <summary>
    /// 默认字符集：字母 + 数字 + 常见分隔符。只给字母数字会让「字符集约束」型验证器（邮箱、路径、复合编码）
    /// 在 <see cref="AnyIdOptions.MaxAttempts"/> 次重试内必然全败。
    /// </summary>
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789@.-_/:";

    /// <summary>
    /// 开启 <see cref="AnyIdOptions.IncludeBoundaryValues"/> 后，每次取值有 1/16 的概率返回
    /// 该类型的最小值或最大值。
    /// </summary>
    private const int BoundaryOdds = 16;

    private static readonly long _minDateTimeTicks = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;

    private static readonly long _maxDateTimeTicks = new DateTime(2100, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;

    /// <summary>
    /// 为 <paramref name="type"/> 创建一个值。
    /// </summary>
    public object? Create(Type type)
    {
        if (customFactory?.Invoke(type) is { } customValue)
        {
            return customValue;
        }

        if (type.IsEnum)
        {
            var values = Enum.GetValues(type);

            return values.GetValue(random.Next(values.Length));
        }

        return Type.GetTypeCode(type) switch
        {
            TypeCode.Byte => BoundaryOr(byte.MinValue, byte.MaxValue, () => (byte)random.Next(byte.MinValue, byte.MaxValue + 1)),
            TypeCode.SByte => BoundaryOr(sbyte.MinValue, sbyte.MaxValue, () => (sbyte)random.Next(sbyte.MinValue, sbyte.MaxValue + 1)),
            TypeCode.Int16 => BoundaryOr(short.MinValue, short.MaxValue, () => (short)random.Next(short.MinValue, short.MaxValue + 1)),
            TypeCode.UInt16 => BoundaryOr(ushort.MinValue, ushort.MaxValue, () => (ushort)random.Next(ushort.MinValue, ushort.MaxValue + 1)),
            TypeCode.Int32 => BoundaryOr(int.MinValue, int.MaxValue, NextInt32),
            TypeCode.UInt32 => BoundaryOr(uint.MinValue, uint.MaxValue, () => (uint)random.NextInt64(uint.MinValue, (long)uint.MaxValue + 1)),
            TypeCode.Int64 => BoundaryOr(long.MinValue, long.MaxValue, NextInt64Bits),
            TypeCode.UInt64 => BoundaryOr(ulong.MinValue, ulong.MaxValue, NextUInt64Bits),
            TypeCode.Decimal => BoundaryOr(decimal.MinValue, decimal.MaxValue, CreateDecimal),
            TypeCode.Single => CreateSingle(),
            TypeCode.Double => CreateDouble(),
            TypeCode.DateTime => CreateDateTime(),
            TypeCode.String => CreateString(),
            TypeCode.Char => Alphabet[random.Next(Alphabet.Length)],
            _ => CreateOther(type),
        };
    }

    private object CreateOther(Type type) => type switch
    {
        _ when type == typeof(Guid) => CreateGuid(),
        _ when type == typeof(Int128) => BoundaryOr(Int128.MinValue, Int128.MaxValue, () => new Int128(NextUInt64Bits(), NextUInt64Bits())),
        _ when type == typeof(UInt128) => BoundaryOr(UInt128.MinValue, UInt128.MaxValue, () => new UInt128(NextUInt64Bits(), NextUInt64Bits())),
        _ when type == typeof(Half) => CreateHalf(),
        _ when type == typeof(BigInteger) => new BigInteger(CreateBytes(16)),
        _ when type == typeof(DateTimeOffset) => new DateTimeOffset(CreateDateTime()),
        _ when type == typeof(TimeSpan) => TimeSpan.FromTicks(random.NextInt64(0, TimeSpan.TicksPerDay)),
        _ when type == typeof(DateOnly) => DateOnly.FromDateTime(CreateDateTime()),
        _ when type == typeof(TimeOnly) => new TimeOnly(random.NextInt64(0, TimeOnly.MaxValue.Ticks)),
        _ => throw new NotSupportedException(
            $"'{type}' is not a supported strongly typed id primitive. "
            + $"Provide a value through {nameof(AnyIdOptions)}.{nameof(AnyIdOptions.PrimitiveValueFactory)}."),
    };

    /// <summary>
    /// 按 <see cref="BoundaryOdds"/> 的概率返回区间端点，否则走常规采样。
    /// </summary>
    /// <remarks>
    /// 默认关闭：偶尔蹦出 <c>int.MaxValue</c> 会让 <c>OnlyHaveUniqueItems()</c> 这类断言偶发失败。
    /// </remarks>
    private T BoundaryOr<T>(T min, T max, Func<T> ordinary)
        => includeBoundaryValues && random.Next(BoundaryOdds) == 0
            ? (random.Next(2) == 0 ? min : max)
            : ordinary();

    /// <summary>
    /// <see cref="Random.Next(int, int)"/> 的上界是独占的，直接调用永远取不到 <see cref="int.MaxValue"/>，
    /// 而它正是 <c>ToString</c> 位数最长、<c>Parse</c> 溢出检测的临界点。改用 64 位区间 +1 再收窄。
    /// </summary>
    private int NextInt32() => (int)random.NextInt64(int.MinValue, (long)int.MaxValue + 1);

    /// <summary>
    /// 取满 64 位有符号区间（含 <see cref="long.MaxValue"/>）。<c>NextInt64(min, max)</c> 上界独占，
    /// 而 <c>long.MaxValue + 1</c> 溢出，只能走位重解释。
    /// </summary>
    private long NextInt64Bits() => unchecked((long)NextUInt64Bits());

    /// <summary>
    /// 符号与小数位数都随机，让负数与高精度舍入两条分支都能触发。scale 取满 <c>[0, 28]</c>，
    /// 低位尾数会缩成 <c>0.000…</c> 形态，正好覆盖 <c>ToString</c> 的定点格式化。
    /// </summary>
    private decimal CreateDecimal()
        => new(random.Next(1, int.MaxValue), random.Next(0, int.MaxValue), 0, random.Next(2) == 0, (byte)random.Next(0, 29));

    /// <summary>
    /// <see cref="Half"/> 的最大有限值是 65504，而 <see cref="CreateSingle"/> 落在 <c>[-1_000_000, 1_000_000]</c>，
    /// 直接转型会让约 93% 的样本舍入成 <see cref="Half.PositiveInfinity"/>。这里按 <see cref="Half"/> 的实际量程取值。
    /// </summary>
    private Half CreateHalf() => (Half)(((random.NextDouble() * 2) - 1) * 60_000);

    /// <summary>
    /// 对称区间 <c>[-1_000_000, 1_000_000]</c>，让符号相关的分支（<c>CompareTo</c> 的负号、<c>ToString</c> 的前导 <c>-</c>）
    /// 也能触发。刻意不注入 <c>NaN</c> / <c>±Infinity</c>：它们在 JSON 里非法，会让往返测试随机失败，而重试循环救不了。
    /// </summary>
    private float CreateSingle() => (float)(((random.NextDouble() * 2) - 1) * 1_000_000);

    private double CreateDouble() => ((random.NextDouble() * 2) - 1) * 1_000_000;

    /// <summary>
    /// 取满 64 位无符号区间。<c>Random.NextInt64()</c> 的无参重载只覆盖 <c>[0, long.MaxValue)</c>（63 位），
    /// 直接转型会让 <c>ulong</c> / <c>UInt128</c> 的上半区与 <c>Int128</c> 的负数区永不出现，故取两次低 32 位拼满。
    /// 不写成位移或运算：符号扩展后的按位或会触发 CS0675。
    /// </summary>
    private ulong NextUInt64Bits()
    {
        var high = unchecked((uint)random.NextInt64());
        var low = unchecked((uint)random.NextInt64());

        return ((ulong)high << 32) | low;
    }

    private DateTime CreateDateTime() => new(random.NextInt64(_minDateTimeTicks, _maxDateTimeTicks), DateTimeKind.Utc);

    private string CreateString()
    {
        var chars = new char[stringLength];

        for (var index = 0; index < chars.Length; index++)
        {
            chars[index] = Alphabet[random.Next(Alphabet.Length)];
        }

        return new string(chars);
    }

    private Guid CreateGuid() => new(CreateBytes(16));

    private byte[] CreateBytes(int length)
    {
        var bytes = new byte[length];
        random.NextBytes(bytes);

        return bytes;
    }
}
