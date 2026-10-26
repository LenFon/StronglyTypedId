using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using FluentAssertions;
using Xunit;

namespace Len.StronglyTypedId.Testing.Tests;

/// <summary>
/// <see cref="AnyIdGenerator"/> 与 <see cref="Any"/> 的行为。
/// </summary>
public class AnyIdGeneratorTests
{
    [Fact]
    public void Id_Should_CreateValue_ForEverySupportedPrimitive()
    {
        // 固定种子：断言「非 default」这类判据必须确定，否则随机值偶尔为 0 会让用例偶发失败。
        var any = new AnyIdGenerator(new AnyIdOptions { Seed = 20261019 });

        any.Id<GuidId>().Value.Should().NotBe(Guid.Empty);
        any.Id<StringId>().Value.Should().HaveLength(8);
        any.Id<CharId>().Value.Should().NotBe(default);
        any.Id<ByteId>().Value.Should().NotBe(default);
        any.Id<SByteId>().Value.Should().NotBe(default);
        any.Id<Int16Id>().Value.Should().NotBe(default);
        any.Id<UInt16Id>().Value.Should().NotBe(default);
        any.Id<Int32Id>().Value.Should().NotBe(default);
        any.Id<UInt32Id>().Value.Should().NotBe(default);
        any.Id<Int64Id>().Value.Should().NotBe(default);
        any.Id<UInt64Id>().Value.Should().NotBe(default);
        any.Id<Int128Id>().Value.Should().NotBe(Int128.Zero);
        any.Id<UInt128Id>().Value.Should().NotBe(UInt128.Zero);
        any.Id<DecimalId>().Value.Should().NotBe(0m);
        Half.IsFinite(any.Id<HalfId>().Value).Should().BeTrue();
        any.Id<SingleId>().Value.Should().NotBe(0f);
        any.Id<DoubleId>().Value.Should().NotBe(0d);
        any.Id<DateTimeId>().Value.Year.Should().BeInRange(1970, 2100);
        any.Id<DateTimeOffsetId>().Value.Year.Should().BeInRange(1970, 2100);
        any.Id<TimeSpanId>().Value.Should().BeLessThan(TimeSpan.FromDays(1));
        any.Id<DateOnlyId>().Value.Year.Should().BeInRange(1970, 2100);
        any.Id<TimeOnlyId>().Value.Ticks.Should().BeLessThan(TimeOnly.MaxValue.Ticks);
        any.Id<BigIntegerId>().Value.Should().NotBe(BigInteger.Zero);
        any.Id<ColorId>().Value.Should().BeOneOf(Enum.GetValues<Color>());
    }

    [Fact]
    public void Ids_Should_ReturnRequestedCount() => Any.Ids<GuidId>(25).Should().HaveCount(25);

    /// <summary>
    /// D5：<c>int</c> / <c>long</c> / <c>ulong</c> 的采样必须覆盖整个取值范围。负数与 <c>ulong</c>
    /// 上半区若永不出现，负数格式化、排序、DB 往返这些分支在 <c>Any.Id&lt;T&gt;()</c> 生成的样本里
    /// 就永远测不到，测试工具的价值恰恰在于覆盖边界。
    /// </summary>
    [Fact]
    public void Ids_Should_CoverFullPrimitiveRange()
    {
        const int Count = 200;

        var int32Values = Any.Ids<Int32Id>(Count, seed: 20261021).Select(id => id.Value).ToArray();
        int32Values.Should().Contain(value => value < 0);
        int32Values.Should().Contain(value => value > 0);

        var int64Values = Any.Ids<Int64Id>(Count, seed: 20261021).Select(id => id.Value).ToArray();
        int64Values.Should().Contain(value => value < 0);

        var uint64Values = Any.Ids<UInt64Id>(Count, seed: 20261021).Select(id => id.Value).ToArray();
        uint64Values.Should().Contain(value => value > long.MaxValue);
    }

    /// <summary>
    /// D3：<c>Half</c> 的量程只有 ±65504，而通用浮点采样落在 <c>[1, 1_000_001]</c>
    /// 直接转型会让约 93% 的样本按 IEEE 舍入成 <c>±Infinity</c>，序列化往返测试就变成了在测无穷大。
    /// <c>Int128</c> / <c>UInt128</c> 同族：<c>NextInt64()</c> 的无参重载只覆盖
    /// <c>[0, long.MaxValue)</c>，高低两个字都取它会让 <c>Int128</c> 永不为负、
    /// <c>UInt128</c> 永远落在下半区。
    /// </summary>
    [Fact]
    public void Ids_Should_StayWithinPrimitiveRange_ForHalfAndWideIntegers()
    {
        const int Count = 200;

        var halfValues = Any.Ids<HalfId>(Count, seed: 20261021).Select(id => id.Value).ToArray();
        halfValues.Should().OnlyContain(value => Half.IsFinite(value));
        halfValues.Should().Contain(value => value < Half.Zero);
        halfValues.Should().Contain(value => value > Half.Zero);

        var int128Values = Any.Ids<Int128Id>(Count, seed: 20261021).Select(id => id.Value).ToArray();
        int128Values.Should().Contain(value => value < Int128.Zero);

        var uint128Values = Any.Ids<UInt128Id>(Count, seed: 20261021).Select(id => id.Value).ToArray();
        uint128Values.Should().Contain(value => value > (UInt128)long.MaxValue);
    }

    /// <summary>
    /// D4：浮点与 <c>decimal</c> 样本必须覆盖负数。只取正数会让符号相关的分支
    /// （<c>CompareTo</c> 的负号、<c>ToString</c> 的前导 <c>-</c>、JSON 的负数形式）
    /// 在 <c>Any.Id&lt;T&gt;()</c> 下永不触发。
    /// </summary>
    [Fact]
    public void Ids_Should_CoverNegativeFloatingPointAndDecimalValues()
    {
        const int Count = 200;

        Any.Ids<SingleId>(Count, seed: 20261021).Select(id => id.Value).Should().Contain(value => value < 0);
        Any.Ids<DoubleId>(Count, seed: 20261021).Select(id => id.Value).Should().Contain(value => value < 0);
        Any.Ids<DecimalId>(Count, seed: 20261021).Select(id => id.Value).Should().Contain(value => value < 0);
    }

    /// <summary>
    /// D5：默认字符集里掺了常见分隔符。只给字母数字时，任何「字符集约束」型验证器
    /// （邮箱、路径、复合编码）都会在 <c>MaxAttempts</c> 次重试内全败。
    /// </summary>
    [Fact]
    public void StringValues_Should_OccasionallyContainSeparators()
    {
        var values = Any.Ids<StringId>(200, seed: 20261021).Select(id => id.Value).ToArray();

        values.Should().Contain(value => value.Any(character => "@.-_/:".Contains(character)));
    }

    /// <summary>
    /// R7-2：均匀采样下 <c>int.MaxValue</c> / <c>long.MaxValue</c> 实际不可能出现
    /// （2^32 / 2^64 分之一），而它们正是位数最长、<c>Parse</c> 溢出的临界点。
    /// 打开 <c>IncludeBoundaryValues</c> 后按 1/16 概率注入两端。
    /// </summary>
    [Fact]
    public void IncludeBoundaryValues_Should_ProducePrimitiveExtremes()
    {
        const int Count = 200;

        var generator = new AnyIdGenerator(new AnyIdOptions { Seed = 20261021, IncludeBoundaryValues = true });

        var int32Values = generator.Ids<Int32Id>(Count).Select(id => id.Value).ToArray();
        int32Values.Should().Contain(int.MinValue);
        int32Values.Should().Contain(int.MaxValue);

        var int64Values = generator.Ids<Int64Id>(Count).Select(id => id.Value).ToArray();
        int64Values.Should().Contain(long.MinValue);
        int64Values.Should().Contain(long.MaxValue);
    }

    /// <summary>
    /// 边界注入默认关闭：它对「断言唯一性」的测试是毒药，两个端点各占 1/32，
    /// 100 个样本里撞上同一端点的概率约 22%。
    /// </summary>
    [Fact]
    public void IncludeBoundaryValues_Should_BeOffByDefault() => new AnyIdOptions().IncludeBoundaryValues.Should().BeFalse();

    /// <summary>
    /// 边界注入覆盖所有「有明确端点」的基元，而不只是 <c>int</c> / <c>long</c>：窄整型的溢出、
    /// <c>ulong</c> 上半区的格式化、<c>decimal</c> 的 96 位尾数、<c>Int128</c> / <c>UInt128</c> 的高低位，
    /// 都只有在端点样本上才会走到。
    /// </summary>
    [Fact]
    public void IncludeBoundaryValues_Should_CoverEveryBoundedPrimitive()
    {
        // 1/16 的注入概率下，400 个样本里漏掉某一端的概率约 6e-12。
        const int Count = 400;

        var generator = new AnyIdGenerator(new AnyIdOptions { Seed = 20261021, IncludeBoundaryValues = true });

        var bytes = generator.Ids<ByteId>(Count).Select(id => id.Value).ToArray();
        bytes.Should().Contain(byte.MinValue).And.Contain(byte.MaxValue);

        var sbytes = generator.Ids<SByteId>(Count).Select(id => id.Value).ToArray();
        sbytes.Should().Contain(sbyte.MinValue).And.Contain(sbyte.MaxValue);

        var int16Values = generator.Ids<Int16Id>(Count).Select(id => id.Value).ToArray();
        int16Values.Should().Contain(short.MinValue).And.Contain(short.MaxValue);

        var uint16Values = generator.Ids<UInt16Id>(Count).Select(id => id.Value).ToArray();
        uint16Values.Should().Contain(ushort.MinValue).And.Contain(ushort.MaxValue);

        var uint32Values = generator.Ids<UInt32Id>(Count).Select(id => id.Value).ToArray();
        uint32Values.Should().Contain(uint.MinValue).And.Contain(uint.MaxValue);

        var uint64Values = generator.Ids<UInt64Id>(Count).Select(id => id.Value).ToArray();
        uint64Values.Should().Contain(ulong.MinValue).And.Contain(ulong.MaxValue);

        var decimals = generator.Ids<DecimalId>(Count).Select(id => id.Value).ToArray();
        decimals.Should().Contain(decimal.MinValue).And.Contain(decimal.MaxValue);

        var int128Values = generator.Ids<Int128Id>(Count).Select(id => id.Value).ToArray();
        int128Values.Should().Contain(Int128.MinValue).And.Contain(Int128.MaxValue);

        var uint128Values = generator.Ids<UInt128Id>(Count).Select(id => id.Value).ToArray();
        uint128Values.Should().Contain(UInt128.MinValue).And.Contain(UInt128.MaxValue);
    }

    /// <summary>
    /// 浮点刻意不注入端点：<c>NaN</c> / <c>±Infinity</c> 在 JSON 里非法，会让序列化往返测试随机失败，
    /// 而重试循环救不了（验证器会接受它们，不触发重试）。这条把「刻意排除」钉住，免得日后顺手补上。
    /// </summary>
    [Fact]
    public void IncludeBoundaryValues_Should_NotInjectNonFiniteFloatingPoint()
    {
        const int Count = 400;

        var generator = new AnyIdGenerator(new AnyIdOptions { Seed = 20261021, IncludeBoundaryValues = true });

        generator.Ids<SingleId>(Count).Select(id => id.Value).Should().OnlyContain(value => float.IsFinite(value));
        generator.Ids<DoubleId>(Count).Select(id => id.Value).Should().OnlyContain(value => double.IsFinite(value));
        generator.Ids<HalfId>(Count).Select(id => id.Value).Should().OnlyContain(value => Half.IsFinite(value));
    }

    /// <summary>
    /// 计数 0 合法（只有负数才抛）：批量生成空集合是「今天没有样本」的正常表达。
    /// </summary>
    [Fact]
    public void Ids_WithZeroCount_Should_ReturnEmpty() => Any.Ids<GuidId>(0).Should().BeEmpty();

    /// <summary>
    /// <c>StringLength</c> 的下界是 1（0 在构造时就被拒，见 <c>Options_Should_RejectInvalidValues</c>），
    /// 且只影响 <c>string</c> 基元，调小它不该改变其它基元的形态。
    /// </summary>
    [Fact]
    public void StringLength_Should_AffectOnlyStringPrimitives()
    {
        var generator = new AnyIdGenerator(new AnyIdOptions { StringLength = 1, Seed = 20261021 });

        generator.Id<StringId>().Value.Should().HaveLength(1);

        // 非 string 基元不受影响：Guid 仍是 16 字节。
        generator.Id<GuidId>().Value.ToByteArray().Should().HaveCount(16);
    }

    [Fact]
    public void Ids_Should_ProduceDistinctValues() => Any.Ids<GuidId>(100).Select(id => id.Value).Should().OnlyHaveUniqueItems();

    [Fact]
    public void Ids_WithNegativeCount_Should_Throw()
    {
        var generator = new AnyIdGenerator();

        var act = () => generator.Ids<GuidId>(-1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Ids_WithSameSeed_Should_BeReproducible() => Any.Ids<GuidId>(10, seed: 42).Should().Equal(Any.Ids<GuidId>(10, seed: 42));

    [Fact]
    public void Ids_WithDifferentSeeds_Should_Differ() => Any.Ids<GuidId>(10, seed: 1).Should().NotEqual(Any.Ids<GuidId>(10, seed: 2));

    [Fact]
    public void Id_WithSameSeed_Should_ReturnSameValue() => Any.Id<GuidId>(seed: 7).Should().Be(Any.Id<GuidId>(seed: 7));

    [Fact]
    public void StringLength_Should_BeHonored()
    {
        var generator = new AnyIdGenerator(new AnyIdOptions { StringLength = 12 });

        generator.Id<StringId>().Value.Should().HaveLength(12);
    }

    [Fact]
    public void Validator_Should_BeSatisfied_ByRetrying()
    {
        // 约一半的随机 int 是负数，100 次预算足以拿到正数。
        var generator = new AnyIdGenerator();

        generator.Id<PositiveIntId>().Value.Should().BePositive();
    }

    [Fact]
    public void Validator_ThatAlwaysRejects_Should_FailWithActionableMessage()
    {
        var generator = new AnyIdGenerator(new AnyIdOptions { MaxAttempts = 3 });

        var act = () => generator.Id<ImpossibleId>();

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*ImpossibleId*Register*");
    }

    /// <summary>
    /// 配了 <c>ExceptionType</c> 后，验证器拒绝时抛的是自定义异常而不是 <see cref="ArgumentException"/>。
    /// 重试循环若只认 <see cref="ArgumentException"/>，<c>MaxAttempts</c> 就形同虚设，第一次被拒即冒泡。
    /// </summary>
    [Fact]
    public void Validator_WithCustomExceptionType_Should_StillRetry()
    {
        // 前两个值都被拒（负数），第三个才通过。若重试循环不认自定义异常，
        // 第一个值就会把 DomainRuleException 抛出来，拿不到 42。
        var values = new Queue<int>(new[] { -1, -2, 42 });
        var generator = new AnyIdGenerator(new AnyIdOptions
        {
            PrimitiveValueFactory = type => type == typeof(int) ? values.Dequeue() : null,
        });

        generator.Id<CustomExceptionPositiveIntId>().Value.Should().Be(42);
        values.Should().BeEmpty("被拒之后必须重新取值，而不是直接放弃");
    }

    [Fact]
    public void Validator_WithCustomExceptionType_ThatAlwaysRejects_Should_FailWithActionableMessage()
    {
        var generator = new AnyIdGenerator(new AnyIdOptions { MaxAttempts = 3 });

        var act = () => generator.Id<CustomExceptionImpossibleId>();

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*CustomExceptionImpossibleId*Register*");
    }

    /// <summary>
    /// 重试只针对「值被验证器拒绝」。验证器自身抛出的异常是真 bug，必须原样冒泡，
    /// 不能被当成拒绝吞进重试循环。
    /// </summary>
    [Fact]
    public void Validator_ThrowingUnexpectedException_Should_Bubble()
    {
        var generator = new AnyIdGenerator(new AnyIdOptions { MaxAttempts = 3 });

        var act = () => generator.Id<ExplodingValidatorId>();

        act.Should().Throw<InvalidOperationException>().WithMessage("validator exploded");
    }

    [Fact]
    public void Register_Should_OverrideGeneration()
    {
        var known = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var generator = new AnyIdGenerator();

        generator.Register<GuidId>(() => GuidId.Create(known));

        generator.Id<GuidId>().Value.Should().Be(known);
    }

    [Fact]
    public void Register_Should_NotLeakBetweenInstances()
    {
        var known = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var registered = new AnyIdGenerator();
        var untouched = new AnyIdGenerator();

        registered.Register<GuidId>(() => GuidId.Create(known));

        untouched.Id<GuidId>().Value.Should().NotBe(known);
    }

    [Fact]
    public void Any_Reset_Should_DropRegistrations()
    {
        var known = Guid.Parse("11111111-2222-3333-4444-555555555555");

        try
        {
            Any.Register<GuidId>(() => GuidId.Create(known));
            Any.Id<GuidId>().Value.Should().Be(known);

            Any.Reset();

            Any.Id<GuidId>().Value.Should().NotBe(known);
        }
        finally
        {
            Any.Reset();
        }
    }

    [Fact]
    public void Any_Register_Should_AllowReentrantUseInsideFactory()
    {
        var known = Guid.Parse("11111111-2222-3333-4444-555555555555");

        try
        {
            Any.Register<GuidId>(() =>
            {
                // 工厂内部再进 Any：注册表的锁不能跨越用户代码，否则这里会死锁。
                _ = Any.Id<StringId>();

                return GuidId.Create(known);
            });

            Any.Id<GuidId>().Value.Should().Be(known);
        }
        finally
        {
            Any.Reset();
        }
    }

    [Fact]
    public void Any_Register_Should_NotLoseRegistration_WhenRacingWithReset()
    {
        // Register 与 Reset 竞争时，注册可能落在刚被丢弃的实例上，那样之后 Any.Id 会静默不生效。
        // 这里断言「不死锁、不抛异常，且竞争结束后注册仍落在当前实例上」。
        var known = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var barrier = new Barrier(2);
        Exception? failure = null;

        var registrar = new Thread(() =>
        {
            barrier.SignalAndWait();

            try
            {
                for (var index = 0; index < 500; index++)
                {
                    Any.Register<GuidId>(() => GuidId.Create(known));
                }
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });

        var resetter = new Thread(() =>
        {
            barrier.SignalAndWait();

            for (var index = 0; index < 500; index++)
            {
                Any.Reset();
            }
        });

        try
        {
            registrar.Start();
            resetter.Start();
            registrar.Join();
            resetter.Join();

            failure.Should().BeNull();

            Any.Reset();
            Any.Register<GuidId>(() => GuidId.Create(known));
            Any.Id<GuidId>().Value.Should().Be(known);
        }
        finally
        {
            Any.Reset();
        }
    }

    [Fact]
    public void Create_Should_RejectNonIdType()
    {
        var generator = new AnyIdGenerator();

        var act = () => generator.Create(typeof(int));

        act.Should().Throw<ArgumentException>().WithParameterName("idType");
    }

    [Fact]
    public void Create_Should_RejectNullType()
    {
        var generator = new AnyIdGenerator();

        var act = () => generator.Create(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(8, 0)]
    public void Options_Should_RejectInvalidValues(int stringLength, int maxAttempts)
    {
        var act = () => new AnyIdGenerator(new AnyIdOptions { StringLength = stringLength, MaxAttempts = maxAttempts });

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void PrimitiveValueFactory_Should_Override()
    {
        var known = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var generator = new AnyIdGenerator(new AnyIdOptions
        {
            PrimitiveValueFactory = type => type == typeof(Guid) ? known : null,
        });

        generator.Id<GuidId>().Value.Should().Be(known);
    }

    [Fact]
    public void PrimitiveValueFactory_ReturningNull_Should_FallBack()
    {
        var generator = new AnyIdGenerator(new AnyIdOptions { PrimitiveValueFactory = _ => null });

        generator.Id<GuidId>().Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Id_Should_BeThreadSafe()
    {
        const int threadCount = 8;
        const int idsPerThread = 100;

        var generator = new AnyIdGenerator();
        var barrier = new Barrier(threadCount);
        var results = new Guid[threadCount][];
        var threads = new Thread[threadCount];

        for (var index = 0; index < threadCount; index++)
        {
            var slot = index;

            threads[slot] = new Thread(() =>
            {
                barrier.SignalAndWait();
                results[slot] = [.. Enumerable.Range(0, idsPerThread).Select(_ => generator.Id<GuidId>().Value)];
            });

            threads[slot].Start();
        }

        foreach (var thread in threads)
        {
            thread.Join();
        }

        results.SelectMany(values => values).Should().OnlyHaveUniqueItems();
    }
}
