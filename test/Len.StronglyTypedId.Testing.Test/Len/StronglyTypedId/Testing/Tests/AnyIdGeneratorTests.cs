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
        any.Id<DecimalId>().Value.Should().BeGreaterThan(0);
        any.Id<HalfId>().Value.Should().BeGreaterThan(Half.Zero);
        any.Id<SingleId>().Value.Should().BeGreaterThan(0);
        any.Id<DoubleId>().Value.Should().BeGreaterThan(0);
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
    /// 重试循环若只认 <see cref="ArgumentException"/>，<c>MaxAttempts</c> 就形同虚设——第一次被拒即冒泡。
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
