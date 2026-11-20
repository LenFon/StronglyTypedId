using System.Reflection;
using FluentAssertions;
using Xunit;

namespace Len.StronglyTypedId.Testing.Tests;

/// <summary>
/// <see cref="StronglyTypedIdConformance.CheckRoundTrip"/>：把「库承诺的往返契约」变成用户能跑的一行调用。
/// </summary>
/// <remarks>
/// 这些契约此前只有库自己的测试在守。用户的 Id（尤其是自定义基元、自定义归一化器）没有任何检查，
/// 违约要到生产环境才暴露。本类锁住「违约能被报出来」与「造不出值不算违约」这两条。
/// </remarks>
public class RoundTripConformanceTests
{
    #region 通过

    /// <summary>
    /// 全部 Id 都合法时返回空列表。
    /// </summary>
    /// <remarks>
    /// 用 <c>Len.StronglyTypedId.Testing</c> 自身的程序集：它不声明任何 Id，因此必然「全部通过」。
    /// 本测试程序集同时含违约 Id，不能用来断言「全好」。
    /// </remarks>
    [Fact]
    public void CheckRoundTrip_Should_ReturnEmpty_WhenAssemblyHasNoIds()
    {
        StronglyTypedIdConformance.CheckRoundTrip(typeof(StronglyTypedIdConformance).Assembly)
            .Should().BeEmpty();
    }

    /// <summary>
    /// 合法的 Id 不产生失败条目。
    /// </summary>
    [Fact]
    public void CheckRoundTrip_Should_NotReportValidId()
    {
        var failures = StronglyTypedIdConformance.CheckRoundTrip(typeof(RoundTripValidId).Assembly);

        failures.Select(failure => failure.IdType).Should().NotContain(typeof(RoundTripValidId));
    }

    /// <summary>
    /// 归一化器幂等的 Id 不产生失败条目。
    /// </summary>
    [Fact]
    public void CheckRoundTrip_Should_NotReportIdempotentNormalizer()
    {
        var failures = StronglyTypedIdConformance.CheckRoundTrip(typeof(RoundTripIdempotentNormalizerId).Assembly);

        failures.Select(failure => failure.IdType).Should().NotContain(typeof(RoundTripIdempotentNormalizerId));
    }

    #endregion

    #region 违约

    /// <summary>
    /// 归一化器不幂等时报告一条失败，消息含归一化器名与原因。
    /// </summary>
    /// <remarks>
    /// 生成器在 <c>Create</c> 里先归一化再校验，因此不幂等的归一化器会让同一个基元值经
    /// <c>Create</c> 与经 <c>Parse(ToString())</c> 得到不同的 Id。它同时会破坏文本往返，
    /// 所以同一个 Id 上可能有多条失败 —— 这里只断言「幂等那条在」。
    /// </remarks>
    [Fact]
    public void CheckRoundTrip_Should_ReportNonIdempotentNormalizer()
    {
        var failures = StronglyTypedIdConformance.CheckRoundTrip(typeof(RoundTripNonIdempotentNormalizerId).Assembly)
            .Where(entry => entry.IdType == typeof(RoundTripNonIdempotentNormalizerId))
            .ToList();

        failures.Should().Contain(entry => entry.Message.Contains("is not idempotent") && entry.Message.Contains("Normalize"));
    }

    /// <summary>
    /// 自定义基元的 <c>ToString()</c> 不是 <c>TryParse</c> 的逆时，文本与 JSON 往返都失败。
    /// </summary>
    /// <remarks>
    /// 这是自定义基元最现实的一类违约：<c>ToString()</c> 面向显示、做了 <c>TryParse</c> 不认的格式化，
    /// 写出去的文本读不回来。生成器的 JSON 转换器对自定义基元正是「写 <c>ToString()</c>、读 <c>TryParse</c>」，
    /// 所以同一条缺陷会同时出现在两条路径上 —— 两条都要报，否则用户修好一条以为完事了。
    /// <para>
    /// 夹具让 <c>TryParse</c> 接受纯数字，<see cref="Any"/> 的启发式因此能造出值，不必依赖
    /// <c>Any.Register</c> 的全局状态（其它用例会 <c>Any.Reset()</c>，并行跑时注册会丢）。
    /// <c>ToString()</c> 输出带货币符号的形态，<c>TryParse</c> 必然拒绝 —— 失败是确定的，
    /// 不会因为恰好生成到某个能往返的值而偶发通过。
    /// </para>
    /// </remarks>
    [Fact]
    public void CheckRoundTrip_Should_ReportBothTextAndJson_WhenCustomPrimitiveToStringIsNotParseable()
    {
        var failures = StronglyTypedIdConformance.CheckRoundTrip(typeof(RoundTripLossyPrimitiveId).Assembly)
            .Where(entry => entry.IdType == typeof(RoundTripLossyPrimitiveId))
            .ToList();

        failures.Should().Contain(entry => entry.Message.Contains("Parse(ToString())"));
        failures.Should().Contain(entry => entry.Message.Contains("JSON round-trip"));
    }

    /// <summary>
    /// 有违约时 <see cref="StronglyTypedIdConformance.AssertRoundTrip"/> 抛出列出全部条目的异常。
    /// </summary>
    [Fact]
    public void AssertRoundTrip_Should_ListAllFailures()
    {
        var act = () => StronglyTypedIdConformance.AssertRoundTrip(typeof(RoundTripNonIdempotentNormalizerId).Assembly);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*failed the round-trip contract*RoundTripNonIdempotentNormalizerId*");
    }

    #endregion

    #region 跳过

    /// <summary>
    /// 造不出值的 Id 记在跳过列表里，不算失败。
    /// </summary>
    /// <remarks>
    /// 严格格式的自定义基元无法从常见文本形态推断，这是 <see cref="Any"/> 的已知边界，
    /// 不是 Id 的缺陷。把它报成失败会让「跑一遍检查」在正常项目上必然变红。
    /// </remarks>
    [Fact]
    public void FindSkippedRoundTrips_Should_ReportUnvaluableId()
    {
        var skipped = StronglyTypedIdConformance.FindSkippedRoundTrips(typeof(RoundTripStrictCodeId).Assembly);

        skipped.Select(entry => entry.IdType).Should().Contain(typeof(RoundTripStrictCodeId));
        skipped.Single(entry => entry.IdType == typeof(RoundTripStrictCodeId))
            .Message.Should().Contain("no value could be generated");
    }

    /// <summary>
    /// 跳过的 Id 不出现在失败列表里 —— 两个列表互斥。
    /// </summary>
    [Fact]
    public void CheckRoundTrip_Should_NotReportUnvaluableIdAsFailure()
    {
        var failures = StronglyTypedIdConformance.CheckRoundTrip(typeof(RoundTripStrictCodeId).Assembly);

        failures.Select(entry => entry.IdType).Should().NotContain(typeof(RoundTripStrictCodeId));
    }

    /// <summary>
    /// 在自己的生成器上注册工厂后，原本跳过的 Id 会被真正检查。
    /// </summary>
    /// <remarks>
    /// 用独立的 <see cref="AnyIdGenerator"/> 而非 <see cref="Any.Register"/>：共享实例的注册是进程级状态，
    /// 并行跑的其它用例会调 <see cref="Any.Reset"/> 把它清掉，那样这条断言会偶发失败。
    /// </remarks>
    [Fact]
    public void FindSkippedRoundTrips_Should_NotReportId_WhenFactoryIsRegistered()
    {
        var generator = new AnyIdGenerator();
        generator.Register(typeof(RoundTripStrictCodeId), () => RoundTripStrictCodeId.Create(new RoundTripStrictCode("ACCT-00000001")));

        StronglyTypedIdConformance.FindSkippedRoundTrips(typeof(RoundTripStrictCodeId).Assembly, generator)
            .Select(entry => entry.IdType).Should().NotContain(typeof(RoundTripStrictCodeId));
    }

    #endregion

    #region 参数校验与容错

    [Fact]
    public void CheckRoundTrip_Should_Throw_WhenAssemblyIsNull()
    {
        var act = () => StronglyTypedIdConformance.CheckRoundTrip(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void FindSkippedRoundTrips_Should_Throw_WhenAssemblyIsNull()
    {
        var act = () => StronglyTypedIdConformance.FindSkippedRoundTrips(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AssertRoundTrip_Should_Throw_WhenAssemblyIsNull()
    {
        var act = () => StronglyTypedIdConformance.AssertRoundTrip(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// 缺依赖的程序集不能让检查入口抛异常，与 <c>FindBrokenInstances</c> 共用同一份保护。
    /// </summary>
    [Fact]
    public void CheckRoundTrip_Should_NotThrow_WhenSomeTypesFailToLoad()
    {
        var assembly = CreateAssemblyWithUnloadableType();

        var act = () => StronglyTypedIdConformance.CheckRoundTrip(assembly);

        act.Should().NotThrow();
    }

    private static System.Reflection.Emit.AssemblyBuilder CreateAssemblyWithUnloadableType()
    {
        var dependency = System.Reflection.Emit.AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("RoundTripMissingDependency"),
            System.Reflection.Emit.AssemblyBuilderAccess.Run);
        var dependencyType = dependency
            .DefineDynamicModule("RoundTripMissingDependency.dll")
            .DefineType("Missing.Widget", TypeAttributes.Public | TypeAttributes.Class);

        // 刻意不 CreateType：依赖类型永远不可解析，消费者类型的基类因此加载失败。
        var consumer = System.Reflection.Emit.AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("RoundTripConsumer"),
            System.Reflection.Emit.AssemblyBuilderAccess.Run);
        var consumerType = consumer
            .DefineDynamicModule("RoundTripConsumer.dll")
            .DefineType("Consumer.Broken", TypeAttributes.Public | TypeAttributes.Class, dependencyType);

        try
        {
            consumerType.CreateType();
        }
        catch (NotSupportedException)
        {
            // 基类未创建时 CreateType 会失败，但类型已进入模块的类型表 —— 这正是我们要的形态。
        }

        return consumer;
    }

    #endregion
}

/// <summary>
/// 完全合法的 Id：往返检查不应报任何条目。
/// </summary>
[StronglyTypedId]
public partial record struct RoundTripValidId(Guid Value);

/// <summary>
/// 归一化器幂等（<c>Trim</c> 再 <c>Trim</c> 结果不变），不应被报。
/// </summary>
[StronglyTypedId(Normalizer = nameof(Normalize))]
public partial record struct RoundTripIdempotentNormalizerId(string Value)
{
    private static string Normalize(string value) => value.Trim();
}

/// <summary>
/// 归一化器不幂等：每次调用都追加一个字符，<c>Create(Normalize(v)) != Create(v)</c>。
/// </summary>
/// <remarks>
/// 这是真实的违约形态 —— 归一化器若依赖外部状态或做了有损转换，同一个值经不同入口会得到不同 Id。
/// </remarks>
[StronglyTypedId(Normalizer = nameof(Normalize))]
public partial record struct RoundTripNonIdempotentNormalizerId(string Value)
{
    private static string Normalize(string value) => value + "x";
}

/// <summary>
/// 严格格式的自定义基元：<see cref="Any"/> 的启发式无法推断，应记为跳过。
/// </summary>
[StronglyTypedId]
public partial record struct RoundTripStrictCodeId(RoundTripStrictCode Value);

/// <summary>
/// 只接受 <c>ACCT-</c> 前缀加 8 位数字：任何随机文本都命中不了。
/// </summary>
public readonly record struct RoundTripStrictCode(string Value)
    : IParsable<RoundTripStrictCode>, IComparable<RoundTripStrictCode>, IComparable
{
    public static RoundTripStrictCode Parse(string s, IFormatProvider? provider)
        => TryParse(s, provider, out var result) ? result : throw new FormatException($"'{s}' is not a valid RoundTripStrictCode.");

    public static bool TryParse(string? s, IFormatProvider? provider, out RoundTripStrictCode result)
    {
        if (s is { Length: 13 }
            && s.StartsWith("ACCT-", StringComparison.Ordinal)
            && IsAllDigits(s.AsSpan(5)))
        {
            result = new RoundTripStrictCode(s);
            return true;
        }

        result = default;
        return false;
    }

    private static bool IsAllDigits(ReadOnlySpan<char> text)
    {
        foreach (var character in text)
        {
            if (!char.IsAsciiDigit(character))
            {
                return false;
            }
        }

        return true;
    }

    public int CompareTo(RoundTripStrictCode other) => string.CompareOrdinal(Value, other.Value);

    public int CompareTo(object? obj)
        => obj is RoundTripStrictCode other ? CompareTo(other) : throw new ArgumentException("not a RoundTripStrictCode", nameof(obj));

    public override string ToString() => Value;
}

/// <summary>
/// 自定义基元的 <c>ToString()</c> 输出 <c>TryParse</c> 不认的形态。
/// </summary>
/// <remarks>
/// <c>TryParse</c> 只认纯数字，<c>ToString()</c> 却输出带货币符号的文本。于是
/// <c>Parse(ToString())</c> 与 JSON 往返必然都失败 —— 这正是这条检查要抓的形态。
/// <para>
/// 接受纯数字是为了让 <see cref="Any"/> 的启发式能造出值；失败由 <c>ToString()</c> 的形态保证，
/// 与生成到哪个值无关。
/// </para>
/// </remarks>
[StronglyTypedId]
public partial record struct RoundTripLossyPrimitiveId(RoundTripLossyPrimitive Value);

/// <summary>
/// 只接受纯数字文本，但 <c>ToString()</c> 输出带货币符号的形态。
/// </summary>
public readonly record struct RoundTripLossyPrimitive(int Cents)
    : IParsable<RoundTripLossyPrimitive>, IComparable<RoundTripLossyPrimitive>, IComparable
{
    public static RoundTripLossyPrimitive Parse(string s, IFormatProvider? provider)
        => TryParse(s, provider, out var result) ? result : throw new FormatException($"'{s}' is not a valid RoundTripLossyPrimitive.");

    public static bool TryParse(string? s, IFormatProvider? provider, out RoundTripLossyPrimitive result)
    {
        if (int.TryParse(s, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var cents))
        {
            result = new RoundTripLossyPrimitive(cents);
            return true;
        }

        result = default;
        return false;
    }

    public int CompareTo(RoundTripLossyPrimitive other) => Cents.CompareTo(other.Cents);

    public int CompareTo(object? obj)
        => obj is RoundTripLossyPrimitive other ? CompareTo(other) : throw new ArgumentException("not a RoundTripLossyPrimitive", nameof(obj));

    // 面向显示：带货币符号，TryParse 不认。写出去的文本因此读不回来。
    public override string ToString()
        => (Cents / 100m).ToString("C", System.Globalization.CultureInfo.InvariantCulture);
}
