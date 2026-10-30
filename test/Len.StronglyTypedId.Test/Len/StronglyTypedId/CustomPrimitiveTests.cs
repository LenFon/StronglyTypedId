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
