using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using FluentAssertions;

namespace Len.StronglyTypedId;

/// <summary>
/// 生成成员里此前没有任何用例触及的部分：<c>Deconstruct</c>、<c>[DebuggerDisplay]</c>、
/// <c>TryFormat</c> 的失败路径、UTF-8 读入的边界输入，以及 <c>TypeConverter</c> 的拒绝分支。
/// </summary>
/// <remarks>
/// 这些成员的共同点是：成功路径早就在别处验过了，而失败路径一旦写错（失败时仍报告已写入的长度、
/// 把非字符串源静默转型、把 null 当成空串），调用方拿到的是「看起来成功」的错误结果，比抛异常更难发现。
/// </remarks>
public class GeneratedMemberTests
{
    /// <summary>
    /// 固定取值：断言里要拼出规范文本与前缀文本，随机值会让失败信息难以复现。
    /// </summary>
    private static readonly Guid _value = new("d8ac85d4-ed76-4974-b055-8ef3508743f3");

    /// <summary>
    /// 基元的规范文本形（不含前缀）。
    /// </summary>
    private static string CanonicalText => _value.ToString("D", CultureInfo.InvariantCulture);

    #region Deconstruct

    [Fact]
    public void Deconstruct_Should_YieldPrimitiveValue()
    {
        GuidId.Create(_value).Deconstruct(out var value);

        value.Should().Be(_value);
    }

    /// <summary>
    /// <c>record</c>（引用类型）形态同样生成 <c>Deconstruct</c>，不只是 <c>record struct</c>。
    /// </summary>
    [Fact]
    public void Deconstruct_Should_YieldPrimitiveValue_ForReferenceTypeRecord()
    {
        GuidIdV2.Create(_value).Deconstruct(out var value);

        value.Should().Be(_value);
    }

    /// <summary>
    /// 解构给的是基元值，不是 <c>ToString()</c> 的规范文本形，前缀只属于文本，不属于值。
    /// </summary>
    [Fact]
    public void Deconstruct_Should_YieldRawPrimitive_NotCanonicalText()
    {
        PrefixedGuidId.Create(_value).Deconstruct(out var guid);
        PrefixedStringId.Create("abc").Deconstruct(out var text);

        guid.Should().Be(_value);
        text.Should().Be("abc");

        // 对照：同一个 Id 的文本形确实带前缀，说明上面拿到的不是文本。
        PrefixedGuidId.Create(_value).ToString().Should().Be("ord_" + CanonicalText);
    }

    #endregion

    #region [DebuggerDisplay]

    /// <summary>
    /// 特性无条件附加：与是否配置前缀 / 验证器 / <c>TypeConverter</c> 无关。
    /// </summary>
    [Fact]
    public void DebuggerDisplay_Should_BeAttachedToEveryGeneratedType()
    {
        Type[] idTypes =
        [
            typeof(GuidId),
            typeof(Int32Id),
            typeof(StringId),
            typeof(GuidIdV2),
            typeof(PrefixedGuidId),
            typeof(ConvertibleGuidId),
        ];

        foreach (var idType in idTypes)
        {
            var attribute = idType.GetCustomAttribute<DebuggerDisplayAttribute>();

            attribute.Should().NotBeNull($"{idType.Name} 应当带 [DebuggerDisplay]");
            attribute!.Value.Should().Be("{Value}");

            // 表达式指向的属性必须真的存在，否则调试器里显示的是一句错误提示而不是值。
            idType.GetProperty("Value").Should().NotBeNull();
        }
    }

    #endregion

    #region TryFormat 失败路径

    /// <summary>
    /// 无前缀时直接把缓冲区交给基元的 <c>TryFormat</c>：写不下必须返回 <see langword="false"/> 且报告 0。
    /// </summary>
    [Fact]
    public void TryFormat_Should_ReportZero_WhenDestinationIsTooSmall()
    {
        var id = Int32Id.Create(255);
        Span<char> destination = stackalloc char[2];

        id.TryFormat(destination, out var charsWritten, "D8", CultureInfo.InvariantCulture).Should().BeFalse();

        // 报 2 会让调用方把被截断的内容当成完整文本。
        charsWritten.Should().Be(0);
    }

    /// <summary>
    /// 有前缀时的关键分支：前缀先写、基元后写。前缀写得下而基元写不下时，缓冲区里已经有前缀了，
    /// 但报告的长度仍必须是 0，否则调用方会把 <c>"ord_"</c> 当成完整结果。
    /// </summary>
    [Fact]
    public void TryFormat_Should_ReportZero_WhenPrefixFitsButPrimitiveDoesNot()
    {
        var id = PrefixedGuidId.Create(_value);

        // 4 个字符刚好容下前缀 "ord_"，36 字符的 Guid 写不下。
        Span<char> destination = stackalloc char[4];

        id.TryFormat(destination, out var charsWritten, default, CultureInfo.InvariantCulture).Should().BeFalse();

        charsWritten.Should().Be(0);
    }

    /// <summary>
    /// 把「刚好够」的边界钉死：4 + 36 成功，少一个字符就失败。
    /// </summary>
    [Fact]
    public void TryFormat_Should_SucceedWithExactlySizedDestination()
    {
        var id = PrefixedGuidId.Create(_value);

        Span<char> destination = stackalloc char[40];

        id.TryFormat(destination, out var charsWritten, default, CultureInfo.InvariantCulture).Should().BeTrue();
        charsWritten.Should().Be(40);
        destination[..charsWritten].ToString().Should().Be("ord_" + CanonicalText);

        Span<char> tight = stackalloc char[39];

        id.TryFormat(tight, out var tightWritten, default, CultureInfo.InvariantCulture).Should().BeFalse();
        tightWritten.Should().Be(0);
    }

    [Fact]
    public void TryFormatUtf8_Should_ReportZero_WhenDestinationIsTooSmall()
    {
        var id = Int32Id.Create(255);
        Span<byte> destination = stackalloc byte[2];

        id.TryFormat(destination, out var bytesWritten, "D8", CultureInfo.InvariantCulture).Should().BeFalse();

        bytesWritten.Should().Be(0);
    }

    /// <summary>
    /// UTF-8 侧与 UTF-16 同构："ord_" 是 4 个 ASCII 字节，刚好写满，Guid 的 36 个字节写不下。
    /// </summary>
    [Fact]
    public void TryFormatUtf8_Should_ReportZero_WhenPrefixFitsButPrimitiveDoesNot()
    {
        var id = PrefixedGuidId.Create(_value);
        Span<byte> destination = stackalloc byte[4];

        id.TryFormat(destination, out var bytesWritten, default, CultureInfo.InvariantCulture).Should().BeFalse();

        bytesWritten.Should().Be(0);
    }

    #endregion

    #region UTF-8 读入边界

    /// <summary>
    /// 空输入与非法 UTF-8 都必须落到 <see langword="false"/>，而不是抛异常或解析出垃圾值。
    /// </summary>
    [Fact]
    public void TryParseUtf8_Should_RejectEmptyAndMalformedInput()
    {
        Int32Id.TryParse(ReadOnlySpan<byte>.Empty, CultureInfo.InvariantCulture, out _).Should().BeFalse();

        // 0xFF 在 UTF-8 里永远非法；0xC3 0x28 是残缺的双字节序列（缺续字节）。
        byte[] invalidByte = [0xFF];
        byte[] truncatedSequence = [0xC3, 0x28];

        Int32Id.TryParse(invalidByte, CultureInfo.InvariantCulture, out _).Should().BeFalse();
        Int32Id.TryParse(truncatedSequence, CultureInfo.InvariantCulture, out _).Should().BeFalse();

        // 合法 UTF-8，但不是数字。
        Int32Id.TryParse("abc"u8, CultureInfo.InvariantCulture, out _).Should().BeFalse();
    }

    [Fact]
    public void TryParseUtf8_Should_AcceptValidInput()
    {
        Int32Id.TryParse("42"u8, CultureInfo.InvariantCulture, out var parsed).Should().BeTrue();

        parsed.Value.Should().Be(42);
    }

    /// <summary>
    /// <c>Parse</c> 的 UTF-8 重载与 UTF-16 一样抛 <see cref="FormatException"/>，而不是 <see cref="ArgumentException"/>。
    /// </summary>
    [Fact]
    public void ParseUtf8_Should_ThrowFormatException_OnMalformedInput()
    {
        byte[] invalidByte = [0xFF];

        var act = () => Int32Id.Parse(invalidByte, CultureInfo.InvariantCulture);

        act.Should().Throw<FormatException>();
    }

    /// <summary>
    /// 验证器在 UTF-8 路径上同样生效：解析成功但被业务规则拒绝时返回 <see langword="false"/>。
    /// </summary>
    [Fact]
    public void TryParseUtf8_Should_HonorValidator()
    {
        NonEmptyInt32Id.TryParse("0"u8, CultureInfo.InvariantCulture, out _).Should().BeFalse();
        NonEmptyInt32Id.TryParse("7"u8, CultureInfo.InvariantCulture, out var parsed).Should().BeTrue();

        // out 形参带 [MaybeNullWhen(false)]，编译器不认 Should().BeTrue() 的收敛，故直接比值而不是读 .Value。
        parsed.Should().Be(NonEmptyInt32Id.Create(7));
    }

    /// <summary>
    /// 归一化必须发生在校验之前：<c>-5</c> 过不了「必须为正」的验证器，取绝对值后是 5 才通过。
    /// 顺序反了这里就会被拒，<c>Create</c> 与两条读入路径都得守这个顺序。
    /// </summary>
    [Fact]
    public void Normalizer_Should_RunBeforeValidator_OnEveryEntryPoint()
    {
        NormalizedValidatedInt32Id.Create(-5).Value.Should().Be(5);

        NormalizedValidatedInt32Id.TryParse("-5", CultureInfo.InvariantCulture, out var textParsed).Should().BeTrue();
        textParsed.Value.Should().Be(5);

        NormalizedValidatedInt32Id.TryParse("-5"u8, CultureInfo.InvariantCulture, out var utf8Parsed).Should().BeTrue();
        utf8Parsed.Value.Should().Be(5);
    }

#if NET10_0_OR_GREATER
    /// <summary>
    /// <c>Guid</c> 自 .NET 10 起才实现 <c>IUtf8SpanParsable&lt;Guid&gt;</c>，故这条只在 net10.0 下编译。
    /// </summary>
    [Fact]
    public void TryParseUtf8_Should_StripPrefix()
    {
        var prefixed = Encoding.UTF8.GetBytes("ord_" + CanonicalText);

        PrefixedGuidId.TryParse(prefixed, CultureInfo.InvariantCulture, out var parsed).Should().BeTrue();
        parsed.Value.Should().Be(_value);

        // 严格模式下缺前缀的裸值必须被拒。
        var bare = Encoding.UTF8.GetBytes(CanonicalText);

        StrictPrefixedGuidId.TryParse(bare, CultureInfo.InvariantCulture, out _).Should().BeFalse();
    }
#endif

    #endregion

    #region TypeConverter 拒绝分支

    /// <summary>
    /// <c>CanConvertFrom</c> 收窄到 <see cref="string"/>；其它源类型在 <c>ConvertFrom</c> 里抛
    /// <see cref="NotSupportedException"/>，而不是尝试 <c>Convert.ChangeType</c> 之类的隐式转换。
    /// </summary>
    [Fact]
    public void TypeConverter_Should_OnlyAcceptStringsAsSource()
    {
        var converter = TypeDescriptor.GetConverter(typeof(ConvertibleGuidId));

        converter.CanConvertFrom(typeof(string)).Should().BeTrue();
        converter.CanConvertFrom(typeof(Guid)).Should().BeFalse();
        converter.CanConvertFrom(typeof(int)).Should().BeFalse();

        var act = () => converter.ConvertFrom(null, CultureInfo.InvariantCulture, 42);

        act.Should().Throw<NotSupportedException>().WithMessage("Cannot convert from System.Int32.");
    }

    /// <summary>
    /// <see langword="null"/> 源同样被拒：消息里没有类型名可写，只剩一句 <c>Cannot convert from .</c>。
    /// </summary>
    [Fact]
    public void TypeConverter_Should_RejectNullSource()
    {
        var converter = TypeDescriptor.GetConverter(typeof(ConvertibleGuidId));

        var act = () => converter.ConvertFrom(null, CultureInfo.InvariantCulture, null!);

        act.Should().Throw<NotSupportedException>().WithMessage("Cannot convert from .");
    }

    [Fact]
    public void TypeConverter_Should_OnlyConvertToString()
    {
        var converter = TypeDescriptor.GetConverter(typeof(ConvertibleGuidId));
        var id = ConvertibleGuidId.Create(_value);

        converter.CanConvertTo(typeof(string)).Should().BeTrue();
        converter.CanConvertTo(typeof(Guid)).Should().BeFalse();

        var act = () => converter.ConvertTo(null, CultureInfo.InvariantCulture, id, typeof(Guid));

        act.Should().Throw<NotSupportedException>().WithMessage("Cannot convert to System.Guid.");
    }

    /// <summary>
    /// <see langword="null"/> 值转字符串返回 <see langword="null"/>（<c>value?.ToString()</c>），
    /// 而不是抛异常，配置绑定与 XAML 都可能喂进空值。
    /// </summary>
    [Fact]
    public void TypeConverter_Should_ConvertNullToNull()
    {
        var converter = TypeDescriptor.GetConverter(typeof(ConvertibleGuidId));

        converter.ConvertTo(null, CultureInfo.InvariantCulture, null, typeof(string)).Should().BeNull();
    }

    /// <summary>
    /// 文本非法时透出 <see cref="FormatException"/>（来自 <c>Parse</c>），不包装成 <see cref="NotSupportedException"/>。
    /// </summary>
    [Fact]
    public void TypeConverter_Should_PropagateFormatException_OnInvalidText()
    {
        var converter = TypeDescriptor.GetConverter(typeof(ConvertibleGuidId));

        var act = () => converter.ConvertFromString("not-a-guid");

        act.Should().Throw<FormatException>();
    }

    #endregion
}
