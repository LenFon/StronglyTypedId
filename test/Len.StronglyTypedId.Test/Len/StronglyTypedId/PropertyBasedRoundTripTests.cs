using System.Globalization;
using System.Numerics;
using CsCheck;

namespace Len.StronglyTypedId;

/// <summary>
/// K1：属性测试 —— 用随机输入验证「对所有取值都成立」的不变式。
/// </summary>
/// <remarks>
/// <para>
/// 手写用例覆盖的是「写测试时想得到的输入」；这里的每条 property 由 CsCheck 随机生成数百组输入，
/// 失败时自动收缩到最小反例并打印可复现的 seed（<c>seed:</c> 参数或 <c>CsCheck_Seed</c> 环境变量）。
/// </para>
/// <para>
/// 断言分两类，区别来自<b>规范文本形是否有损</b>：无参 <c>ToString()</c> 对 <see cref="IFormattable"/>
/// 基元走 <c>Value.ToString(null, InvariantCulture)</c>，即默认格式串 —— <see cref="DateTime"/> /
/// <see cref="DateTimeOffset"/> 的 "G" 丢亚秒、<see cref="TimeOnly"/> 的 "t" 丢秒。对这些基元断言「值往返」
/// 只会测出格式串的选择，故改为断言「文本幂等」：<c>Parse(id.ToString()).ToString() == id.ToString()</c>，
/// 这才是显示形应当满足的契约。
/// </para>
/// <para>
/// 字符串基元刻意排除空串：生成器的 <c>TryParse</c> 以「非空即合法」表达，而 <c>Create("")</c> 不校验，
/// 于是空串的文本形无法再解析回来。这是既有行为，不在本类里断言，避免把可疑语义固化成契约。
/// </para>
/// </remarks>
public class PropertyBasedRoundTripTests
{
    /// <summary>每个 property 的迭代次数。</summary>
    /// <remarks>
    /// 默认 100 已能覆盖各类边界；取 200 是为了让 23 个基元的组合更容易撞到极端值，
    /// 同时保持整个类在 1 秒内跑完。
    /// </remarks>
    private const int Iterations = 200;

    /// <summary><c>Int128</c> 没有内置生成器，用高低两段拼出来。</summary>
    private static readonly Gen<Int128> _genInt128 =
        Gen.Select(Gen.Long, Gen.ULong, static (upper, lower) => ToInt128(upper, lower));

    /// <summary><c>UInt128</c> 的内置生成器是专用的 <c>GenUInt128</c> 类型，不满足 <c>Gen&lt;UInt128&gt;</c> 形参，故同样拼出来。</summary>
    private static readonly Gen<UInt128> _genUInt128 =
        Gen.Select(Gen.ULong, Gen.ULong, static (upper, lower) => ToUInt128(upper, lower));

    /// <summary><c>Half</c> 没有内置生成器，按位模式从 <c>short</c> 转过来（含 NaN / 无穷）。</summary>
    private static readonly Gen<Half> _genHalf = Gen.Short.Select(BitConverter.Int16BitsToHalf);

    /// <summary><c>BigInteger</c> 没有内置生成器，用字节数组按补码解释。</summary>
    private static readonly Gen<BigInteger> _genBigInteger =
        Gen.Byte.Array.Select(static bytes => ToBigInteger(bytes));

    [Fact]
    public void ValueRoundTrip_Should_Hold_ForIntegralPrimitives()
    {
        AssertValueRoundTrip<ByteId, byte>(Gen.Byte, nameof(ByteId));
        AssertValueRoundTrip<SByteId, sbyte>(Gen.SByte, nameof(SByteId));
        AssertValueRoundTrip<Int16Id, short>(Gen.Short, nameof(Int16Id));
        AssertValueRoundTrip<UInt16Id, ushort>(Gen.UShort, nameof(UInt16Id));
        AssertValueRoundTrip<Int32Id, int>(Gen.Int, nameof(Int32Id));
        AssertValueRoundTrip<UInt32Id, uint>(Gen.UInt32, nameof(UInt32Id));
        AssertValueRoundTrip<Int64Id, long>(Gen.Long, nameof(Int64Id));
        AssertValueRoundTrip<UInt64Id, ulong>(Gen.ULong, nameof(UInt64Id));
        AssertValueRoundTrip<Int128Id, Int128>(_genInt128, nameof(Int128Id));
        AssertValueRoundTrip<UInt128Id, UInt128>(_genUInt128, nameof(UInt128Id));
    }

    [Fact]
    public void ValueRoundTrip_Should_Hold_ForFloatingPointPrimitives()
    {
        // 浮点的默认格式串自 .NET Core 3.0 起是「最短可往返」形，因此值往返成立；
        // NaN 也能往返 —— 记录的相等性走 EqualityComparer<double>.Default，它把 NaN 视作等于自身。
        AssertValueRoundTrip<SingleId, float>(Gen.Float, nameof(SingleId));
        AssertValueRoundTrip<DoubleId, double>(Gen.Double, nameof(DoubleId));
        AssertValueRoundTrip<HalfId, Half>(_genHalf, nameof(HalfId));
        AssertValueRoundTrip<DecimalId, decimal>(Gen.Decimal, nameof(DecimalId));
    }

    [Fact]
    public void ValueRoundTrip_Should_Hold_ForLosslessTemporalPrimitives()
    {
        // DateOnly 的默认 "d" 不含时间分量、TimeSpan 的默认 "c" 精确到 tick，两者都无损。
        AssertValueRoundTrip<DateOnlyId, DateOnly>(Gen.DateOnly, nameof(DateOnlyId));
        AssertValueRoundTrip<TimeSpanId, TimeSpan>(Gen.TimeSpan, nameof(TimeSpanId));
    }

    [Fact]
    public void ValueRoundTrip_Should_Hold_ForGuidStringAndBigInteger()
    {
        AssertValueRoundTrip<GuidId, Guid>(Gen.Guid, nameof(GuidId));
        AssertValueRoundTrip<StringId, string>(Gen.String.Where(static text => text.Length > 0), nameof(StringId));
        AssertValueRoundTrip<BigIntegerId, BigInteger>(_genBigInteger, nameof(BigIntegerId));
    }

    [Fact]
    public void TextRoundTrip_Should_BeIdempotent_ForLossyTemporalPrimitives()
    {
        // 这三个基元的无参 ToString() 用的是短格式，值本身不可还原；能守的是「文本形稳定」：
        // 解析回来的 Id 再写一次，必须得到逐字符相同的文本。
        AssertTextRoundTrip<DateTimeId, DateTime>(Gen.DateTime, nameof(DateTimeId));
        AssertTextRoundTrip<DateTimeOffsetId, DateTimeOffset>(Gen.DateTimeOffset, nameof(DateTimeOffsetId));
        AssertTextRoundTrip<TimeOnlyId, TimeOnly>(Gen.TimeOnly, nameof(TimeOnlyId));
    }

    [Fact]
    public void TryParse_Should_AgreeWithParse_ForArbitraryText()
    {
        // 任意文本（含空串、非法字符、超长数字）下，两个入口的结论必须一致：
        // TryParse 为 true ⟺ Parse 不抛，且两者给出的值相同。
        AssertTryParseAgreesWithParse<Int32Id, int>(nameof(Int32Id));
        AssertTryParseAgreesWithParse<GuidId, Guid>(nameof(GuidId));
        AssertTryParseAgreesWithParse<DecimalId, decimal>(nameof(DecimalId));
        AssertTryParseAgreesWithParse<DateOnlyId, DateOnly>(nameof(DateOnlyId));
        AssertTryParseAgreesWithParse<StringId, string>(nameof(StringId));
        AssertTryParseAgreesWithParse<PrefixedGuidId, Guid>(nameof(PrefixedGuidId));
        AssertTryParseAgreesWithParse<StrictPrefixedGuidId, Guid>(nameof(StrictPrefixedGuidId));
    }

    /// <summary>
    /// 断言「值往返」：<c>Parse(Create(value).ToString()) == Create(value)</c>。
    /// </summary>
    private static void AssertValueRoundTrip<TId, TPrimitive>(Gen<TPrimitive> gen, string label)
        where TId : IStronglyTypedId<TId, TPrimitive>
        where TPrimitive : notnull, IComparable
    {
        gen.Sample(
            value =>
            {
                var id = TId.Create(value);
                var text = id.ToString()!;

                return TId.Parse(text, CultureInfo.InvariantCulture) == id;
            },
            iter: Iterations,
            print: value => $"{label}: {value}");
    }

    /// <summary>
    /// 断言「文本幂等」：<c>Parse(Create(value).ToString()).ToString()</c> 与原文本逐字符相同。
    /// </summary>
    private static void AssertTextRoundTrip<TId, TPrimitive>(Gen<TPrimitive> gen, string label)
        where TId : IStronglyTypedId<TId, TPrimitive>
        where TPrimitive : notnull, IComparable
    {
        gen.Sample(
            value =>
            {
                var text = TId.Create(value).ToString()!;

                return TId.Parse(text, CultureInfo.InvariantCulture).ToString() == text;
            },
            iter: Iterations,
            print: value => $"{label}: {value}");
    }

    /// <summary>
    /// 断言 <c>TryParse</c> 与 <c>Parse</c> 对任意文本给出一致结论。
    /// </summary>
    /// <remarks>
    /// 生成的 <c>Parse</c> 在解析失败时抛 <see cref="ArgumentException"/>（不是 <c>FormatException</c>），
    /// 因为它先调 <c>TryParse</c> 再统一抛出。
    /// </remarks>
    private static void AssertTryParseAgreesWithParse<TId, TPrimitive>(string label)
        where TId : IStronglyTypedId<TId, TPrimitive>
        where TPrimitive : notnull, IComparable
    {
        Gen.String.Sample(
            text =>
            {
                var succeeded = TId.TryParse(text, CultureInfo.InvariantCulture, out var parsed);

                try
                {
                    // 必须先调用 Parse 再与 succeeded 合取：写成 succeeded && Parse(...) 会在失败输入上
                    // 短路掉 Parse，异常分支永远进不来，property 反而把「两个入口一致」判成不一致。
                    var thrown = TId.Parse(text, CultureInfo.InvariantCulture);

                    return succeeded && thrown == parsed!;
                }
                catch (ArgumentException)
                {
                    return !succeeded;
                }
            },
            iter: Iterations,
            print: text => $"{label}: \"{text}\"");
    }

    private static Int128 ToInt128(long upper, ulong lower) => new((ulong)upper, lower);

    private static UInt128 ToUInt128(ulong upper, ulong lower) => new(upper, lower);

    private static BigInteger ToBigInteger(byte[] bytes) => new(bytes);
}
