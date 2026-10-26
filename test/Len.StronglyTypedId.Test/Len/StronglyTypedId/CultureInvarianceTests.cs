using System.Globalization;
using FluentAssertions;

namespace Len.StronglyTypedId;

/// <summary>
/// 文化不变性：规范文本形与解析入口都不受 <see cref="CultureInfo.CurrentCulture"/> 影响。
/// </summary>
/// <remarks>
/// 这是「静默错值」最危险的一类缺陷：de-DE 下漏掉 <see cref="CultureInfo.InvariantCulture"/> 会把 <c>"1.5"</c> 读成 <c>15</c>，不抛异常也不改类型，只在业务数据里留下错误金额。
/// </remarks>
public class CultureInvarianceTests
{
    /// <summary>
    /// 覆盖小数分隔符（de-DE / fr-FR）、土耳其语 I 大小写（tr-TR）、非拉丁数字与历法（ar-SA）、
    /// 以及一个与不变文化接近的东亚文化（ja-JP）。
    /// </summary>
    private static readonly string[] _cultureNames = ["de-DE", "fr-FR", "tr-TR", "ar-SA", "ja-JP"];

    private static readonly CultureCase[] _cases =
    [
        CultureCase.For<DecimalId, decimal>(1.5m),
        CultureCase.For<DoubleId, double>(1.5),
        CultureCase.For<SingleId, float>(1.5f),
        CultureCase.For<HalfId, Half>((Half)1.5),
        CultureCase.For<Int32Id, int>(1234),
        CultureCase.For<DateTimeId, DateTime>(new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc)),
        CultureCase.For<DateTimeOffsetId, DateTimeOffset>(new DateTimeOffset(2020, 1, 2, 3, 4, 5, TimeSpan.FromHours(8))),
        CultureCase.For<TimeOnlyId, TimeOnly>(new TimeOnly(3, 4, 0)),
        CultureCase.For<DateOnlyId, DateOnly>(new DateOnly(2020, 1, 2)),
        CultureCase.For<TimeSpanId, TimeSpan>(TimeSpan.FromMinutes(90)),
    ];

    public static TheoryData<string> CultureNames
    {
        get
        {
            var data = new TheoryData<string>();

            foreach (var name in _cultureNames)
            {
                data.Add(name);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(CultureNames))]
    public void ToString_Should_NotDependOnCurrentCulture(string cultureName)
    {
        var culture = new CultureInfo(cultureName);

        foreach (var testCase in _cases)
        {
            var expected = WithCulture(CultureInfo.InvariantCulture, testCase.ToText);
            var actual = WithCulture(culture, testCase.ToText);

            actual.Should().Be(expected, $"{testCase.Label}：ToString 在 {cultureName} 下必须与不变文化一致");
        }
    }

    /// <summary>
    /// 关键路径是「调用方没有传 provider」：此时生成代码必须自己回退到不变文化，
    /// 而不是把 <c>null</c> 一路交给基元的 <c>Parse</c>（那会用当前线程文化）。
    /// </summary>
    [Theory]
    [MemberData(nameof(CultureNames))]
    public void Parse_Should_ReadCanonicalText_WithoutProvider_UnderAnyCulture(string cultureName)
    {
        var culture = new CultureInfo(cultureName);

        foreach (var testCase in _cases)
        {
            var canonical = WithCulture(CultureInfo.InvariantCulture, testCase.ToText);
            var expected = testCase.Create();

            WithCulture(culture, () => testCase.Parse(canonical))
                .Should().Be(expected, $"{testCase.Label}：{cultureName} 下必须能读回规范文本 \"{canonical}\"");

            WithCulture(culture, () => testCase.TryParse(canonical))
                .Should().BeTrue($"{testCase.Label}：{cultureName} 下 TryParse 必须接受规范文本 \"{canonical}\"");
        }
    }

    /// <summary>
    /// 显式传入的 provider 必须被尊重，这是 <c>IParsable</c> 的契约，也是使用者处理本地化输入的出口。
    /// </summary>
    [Fact]
    public void Parse_Should_HonourExplicitProvider()
    {
        var german = new CultureInfo("de-DE");

        WithCulture(german, () => DecimalId.Parse("1,5", german).Value).Should().Be(1.5m);
        WithCulture(german, () => DecimalId.Parse("1.5", CultureInfo.InvariantCulture).Value).Should().Be(1.5m);
    }

    /// <summary>
    /// <c>ToString(format, provider)</c> 是 <see cref="IFormattable"/> 的原样转发：格式串与 provider 都生效，
    /// 与无参 <c>ToString()</c> 的「恒不变文化」是两条不同的约定。
    /// </summary>
    [Fact]
    public void Formattable_Should_ForwardFormatAndProvider()
    {
        var german = new CultureInfo("de-DE");

        WithCulture(german, () => DecimalId.Create(1.5m).ToString("F2", german)).Should().Be("1,50");
        WithCulture(german, () => DecimalId.Create(1.5m).ToString("F2", CultureInfo.InvariantCulture)).Should().Be("1.50");
    }

    /// <summary>
    /// 在指定文化下执行，结束后恢复原文化。
    /// </summary>
    private static T WithCulture<T>(CultureInfo culture, Func<T> action)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;

            return action();
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    /// <summary>
    /// 一个文化敏感基元的断言入口，委托在泛型工厂里闭包捕获具体类型。
    /// </summary>
    private sealed record CultureCase(
        string Label,
        Func<object> Create,
        Func<string> ToText,
        Func<string, object> Parse,
        Func<string, bool> TryParse)
    {
        public static CultureCase For<TId, TPrimitive>(TPrimitive value)
            where TId : IStronglyTypedId<TId, TPrimitive>, IComparable<TId>
            where TPrimitive : notnull, IComparable
        {
            var id = TId.Create(value);

            return new(
                typeof(TId).Name,
                () => TId.Create(value),
                () => id.ToString()!,
                text => TId.Parse(text, null),
                text => TId.TryParse(text, null, out _));
        }
    }
}
