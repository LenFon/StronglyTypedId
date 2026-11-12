using System.Globalization;
using System.Reflection;
using FluentAssertions;
using Newtonsoft.Json;
using JsonException = System.Text.Json.JsonException;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Len.StronglyTypedId;

/// <summary>
/// 自定义基元（N12）：用户自己实现 <c>IParsable&lt;TSelf&gt;</c> 与 <c>IComparable&lt;TSelf&gt;</c> 的类型
/// 作为强类型 Id 的基元。
/// </summary>
/// <remarks>
/// 核心断言是「零配置往返」：自定义基元没有内置转换器，若把读写外包给序列化器，STJ 会按公开属性写出
/// 对象形态、读回 <c>default</c> —— 看起来成功、值已经丢了。本类逐条锁住「一律走规范文本形」这一契约。
/// </remarks>
public class CustomPrimitiveTests
{
    #region 核心成员

    [Fact]
    public void Create_Should_WrapCustomPrimitive()
    {
        var id = MoneyId.Create(new Money(12.5m));

        id.Value.Should().Be(new Money(12.5m));
    }

    [Fact]
    public void ToString_Should_UsePrimitiveCanonicalText()
    {
        MoneyId.Create(new Money(12.5m)).ToString().Should().Be("12.5");
        SlugId.Create(new Slug("abc")).ToString().Should().Be("abc");
    }

    /// <summary>
    /// 基元的两参 <c>ToString</c> 返回 <see langword="null"/> 时，Id 的带格式 <c>ToString</c> 必须返回空串。
    /// </summary>
    /// <remarks>
    /// 缺陷回归：<c>StronglyTypedIdFormatting.Format</c> 与生成代码里的直呼路径都没有兜底，
    /// 而 <c>IFormattable.ToString</c> 的返回类型标注为非可空、Id 的 <c>ToString(string?, IFormatProvider?)</c>
    /// 同样标注为非可空，于是 <see langword="null"/> 被泄漏给调用方，
    /// <c>id.ToString(format, provider).Length</c> 抛 <see cref="NullReferenceException"/>。
    /// </remarks>
    [Fact]
    public void ToString_Should_ReturnEmptyString_WhenPrimitiveFormattedTextIsNull()
    {
        var id = NullReturningFormatId.Create(new NullReturningFormat("abc"));

        id.ToString(null, CultureInfo.InvariantCulture).Should().BeEmpty();
        id.ToString("X", CultureInfo.InvariantCulture).Should().BeEmpty();
        id.ToString(null, CultureInfo.InvariantCulture).Length.Should().Be(0);
    }

    [Fact]
    public void Parse_Should_RoundTripThroughToString()
    {
        var id = MoneyId.Create(new Money(12.5m));

        MoneyId.Parse(id.ToString(), CultureInfo.InvariantCulture).Should().Be(id);
    }

    [Fact]
    public void Parse_Should_ThrowFormatException_WhenPrimitiveRejectsText()
    {
        var act = () => MoneyId.Parse("not-a-money", CultureInfo.InvariantCulture);

        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void TryParse_Should_ReturnFalse_WhenPrimitiveRejectsText()
    {
        MoneyId.TryParse("not-a-money", CultureInfo.InvariantCulture, out var result).Should().BeFalse();

        result.Should().Be(default);
    }

    /// <summary>
    /// span 重载必须可用：自定义基元的契约只保证 string 重载，生成器负责在 span 路径上转成字符串。
    /// </summary>
    [Fact]
    public void TryParse_Should_AcceptSpan_ByConvertingToString()
    {
        MoneyId.TryParse("12.5".AsSpan(), CultureInfo.InvariantCulture, out var result).Should().BeTrue();

        result.Should().Be(MoneyId.Create(new Money(12.5m)));
    }

    [Fact]
    public void CompareTo_Should_DelegateToPrimitive()
    {
        var small = MoneyId.Create(new Money(1m));
        var large = MoneyId.Create(new Money(2m));

        small.CompareTo(large).Should().BeNegative();
        (small < large).Should().BeTrue();
        (small >= large).Should().BeFalse();
    }

    [Fact]
    public void Deconstruct_Should_YieldPrimitiveValue()
    {
        MoneyId.Create(new Money(12.5m)).Deconstruct(out var value);

        value.Should().Be(new Money(12.5m));
    }

    /// <summary>
    /// 能力探测对自定义基元同样生效：<see cref="Money"/> 实现了 <c>IFormattable</c> 与 span 接口，
    /// 故 <c>ToString(format, provider)</c> 与 <c>TryFormat</c> 都应生成。
    /// </summary>
    [Fact]
    public void FormattableMembers_Should_BeEmitted_WhenPrimitiveSupportsThem()
    {
        var id = MoneyId.Create(new Money(12.5m));

        id.ToString("F1", CultureInfo.InvariantCulture).Should().Be("12.5");

        Span<char> destination = stackalloc char[32];
        id.TryFormat(destination, out var written, default, CultureInfo.InvariantCulture).Should().BeTrue();
        destination[..written].ToString().Should().Be("12.5");
    }

    /// <summary>
    /// 反向断言：<see cref="Slug"/> 只实现最低契约，故 <c>ToString(format, provider)</c> 不应存在 ——
    /// 否则产物会 CS0535（接口成员未实现）。
    /// </summary>
    [Fact]
    public void FormattableMembers_Should_BeAbsent_WhenPrimitiveLacksThem()
    {
        typeof(SlugId).GetMethod("ToString", [typeof(string), typeof(IFormatProvider)]).Should().BeNull();
        typeof(SlugId).GetMethod("TryFormat", [typeof(Span<char>), typeof(int).MakeByRefType(), typeof(ReadOnlySpan<char>), typeof(IFormatProvider)]).Should().BeNull();
    }

    /// <summary>
    /// 显式实现 <c>IFormattable</c> 的基元，格式化路径不得装箱。
    /// </summary>
    /// <remarks>
    /// 这类基元只能经接口调用两参重载（按类型名直呼会 CS1501）。写成
    /// <c>((IFormattable)Value).ToString(…)</c> 时 IL 里是一条 <c>box</c>，每次调用一次分配；
    /// 生成代码改调泛型辅助方法后，<c>constrained.</c> 调用让值类型直接命中自己的实现。
    /// <para>
    /// 用 24 字节的基元测：小结构体可能被 JIT 的逃逸分析消除装箱，那样这条用例会失去意义。
    /// </para>
    /// </remarks>
    [Fact]
    public void Formatting_Should_NotBox_WhenPrimitiveImplementsIFormattableExplicitly()
    {
        var id = ExplicitFormattableMoneyId.Create(new ExplicitFormattableMoney("12.5", "CNY", string.Empty));

        // 预热：让 JIT 完成分层编译，避免把编译开销算进分配量。
        for (var index = 0; index < 20_000; index++)
        {
            _ = id.ToString();
            _ = id.ToString("F2", CultureInfo.InvariantCulture);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var index = 0; index < 200_000; index++)
        {
            _ = id.ToString();
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // 每次调用都会产出新字符串（decimal 的文本），故只断言「没有额外的装箱分配」：
        // 装箱会让每次调用多出 24 字节，200k 次即约 4.8 MB，与字符串本身的量级明显不同。
        allocated.Should().BeLessThan(200_000 * 24, "格式化路径不应为基元值装箱");
    }

    #endregion

    #region 序列化

    [Fact]
    public void SystemTextJson_Should_RoundTripAsCanonicalText()
    {
        var id = MoneyId.Create(new Money(12.5m));

        var json = JsonSerializer.Serialize(id);

        json.Should().Be("\"12.5\"");
        JsonSerializer.Deserialize<MoneyId>(json).Should().Be(id);
    }

    [Fact]
    public void NewtonsoftJson_Should_RoundTripAsCanonicalText()
    {
        var id = MoneyId.Create(new Money(12.5m));

        var json = JsonConvert.SerializeObject(id);

        json.Should().Be("\"12.5\"");
        JsonConvert.DeserializeObject<MoneyId>(json).Should().Be(id);
    }

    /// <summary>
    /// 对象形态（<c>JsonShape.Object</c>）下值同样是文本，不是基元的公开属性。
    /// </summary>
    [Fact]
    public void SystemTextJson_Should_RoundTripObjectShape()
    {
        var id = ObjectShapeMoneyId.Create(new Money(12.5m));

        var json = JsonSerializer.Serialize(id);

        json.Should().Be("{\"value\":\"12.5\"}");
        JsonSerializer.Deserialize<ObjectShapeMoneyId>(json).Should().Be(id);
    }

    [Fact]
    public void SystemTextJson_Should_RoundTripAsDictionaryKey()
    {
        var id = MoneyId.Create(new Money(12.5m));
        var dictionary = new Dictionary<MoneyId, int> { [id] = 1 };

        var json = JsonSerializer.Serialize(dictionary);

        json.Should().Be("{\"12.5\":1}");
        JsonSerializer.Deserialize<Dictionary<MoneyId, int>>(json).Should().ContainKey(id);
    }

    /// <summary>
    /// 基元拒绝文本时必须抛 <c>JsonException</c>：STJ 的转换器契约要求失败一律是这个类型，
    /// 抛别的会让按契约 <c>catch (JsonException)</c> 的调用方漏掉这条路径。
    /// </summary>
    [Fact]
    public void SystemTextJson_Should_ThrowJsonException_WhenTextInvalid()
    {
        var act = () => JsonSerializer.Deserialize<MoneyId>("\"not-a-money\"");

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void NewtonsoftJson_Should_ThrowJsonSerializationException_WhenTextInvalid()
    {
        var act = () => JsonConvert.DeserializeObject<MoneyId>("\"not-a-money\"");

        act.Should().Throw<JsonSerializationException>();
    }

    #endregion

    #region 命名实例

    [Fact]
    public void NamedInstance_Should_BeCreatedThroughPrimitiveParse()
    {
        MoneyInstanceId.Zero.Value.Should().Be(new Money(0m));
        MoneyInstanceId.Ten.Value.Should().Be(new Money(10.5m));
    }

    /// <summary>
    /// 值写错时静态构造抛 <see cref="TypeInitializationException"/>，内层是带实例名与原文的
    /// <see cref="InvalidOperationException"/> —— 这是自定义基元唯一可用的定位手段（编译期无法执行用户 Parse）。
    /// </summary>
    [Fact]
    public void NamedInstance_Should_ReportInstanceName_WhenValueInvalid()
    {
        var act = () => _ = BrokenMoneyInstanceId.Broken;

        act.Should().Throw<TypeInitializationException>()
            .WithInnerException<InvalidOperationException>()
            .WithMessage("StronglyTypedId: named instance 'Broken' of 'BrokenMoneyInstanceId' could not be created from 'not-a-money'.*");
    }

    /// <summary>
    /// 内置基元的实例产物必须逐字不变：它们仍走 <c>Create(字面量)</c>，不生成 <c>CreateInstance</c>。
    /// </summary>
    [Fact]
    public void NamedInstance_Should_NotEmitCreateInstance_ForBuiltInPrimitives()
    {
        typeof(GuidId).GetMethod("CreateInstance", BindingFlags.NonPublic | BindingFlags.Static).Should().BeNull();
        typeof(MoneyId).GetMethod("CreateInstance", BindingFlags.NonPublic | BindingFlags.Static).Should().BeNull();
        typeof(MoneyInstanceId).GetMethod("CreateInstance", BindingFlags.NonPublic | BindingFlags.Static).Should().NotBeNull();
    }

    #endregion
}
