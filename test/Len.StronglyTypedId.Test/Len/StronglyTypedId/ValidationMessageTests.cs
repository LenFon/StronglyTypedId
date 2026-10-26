using FluentAssertions;

namespace Len.StronglyTypedId;

/// <summary>
/// 验证失败消息的运行时语义：<c>RedactValidationMessage</c> 是否真的把被拒值挡在消息之外，
/// 以及它与 <c>ExceptionType</c>、<c>TryCreate</c> / <c>TryParse</c> / <c>Parse</c> 三条入口的关系。
/// </summary>
/// <remarks>
/// 生成器侧已有逐字符的文本契约用例，但那只能证明「代码长得对」。这里补的是运行期事实：
/// 消息里到底有没有那个值，它决定了用户输入会不会顺着异常进日志与 APM。
/// </remarks>
public class ValidationMessageTests
{
    /// <summary>
    /// 被验证器拒绝的原文。取一段明显不像 Id 的文本，便于在消息里做「包含 / 不包含」判定。
    /// </summary>
    private const string RejectedValue = "this-value-is-way-too-long";

    /// <summary>
    /// 默认配置（<c>RedactValidationMessage</c> 未开启）：消息内插被拒绝的原文，便于定位问题。
    /// </summary>
    [Fact]
    public void Create_Should_EmbedRejectedValue_WhenRedactionIsOff()
    {
        var exception = Assert.Throws<ArgumentException>(() => VerboseValidatedStringId.Create(RejectedValue));

        exception.Message.Should().Contain(RejectedValue);
        exception.Message.Should().Contain(nameof(VerboseValidatedStringId));
        exception.ParamName.Should().Be("value");
    }

    /// <summary>
    /// 开启脱敏后消息是固定文案：既不内插原文，也不再带 <c>paramName</c> 之外的任何上下文。
    /// </summary>
    /// <remarks>
    /// 这里只能用 <c>StartWith</c>：<see cref="ArgumentException"/> 自己会在 <c>Message</c> 尾部追加
    /// <c> (Parameter 'value')</c>，那不是生成代码写的文本。自定义异常没有这层追加，故下一条用例可以精确比对。
    /// </remarks>
    [Fact]
    public void Create_Should_OmitRejectedValue_WhenRedactionIsOn()
    {
        var exception = Assert.Throws<ArgumentException>(() => RedactedValidatedStringId.Create(RejectedValue));

        exception.Message.Should().StartWith($"The value is not a valid {nameof(RedactedValidatedStringId)}.");
        exception.Message.Should().NotContain(RejectedValue);
    }

    /// <summary>
    /// 脱敏只改消息文本，不改异常契约：仍然是 <see cref="ArgumentException"/>，<c>ParamName</c> 仍是 <c>value</c>。
    /// </summary>
    [Fact]
    public void Create_Should_KeepExceptionContract_WhenRedactionIsOn()
    {
        var exception = Assert.Throws<ArgumentException>(() => RedactedValidatedStringId.Create(RejectedValue));

        exception.ParamName.Should().Be("value");
    }

    /// <summary>
    /// 脱敏与自定义异常类型正交：换成领域异常后同样不泄漏原文，且不再带 <c>paramName</c>（自定义异常未必有双参构造）。
    /// </summary>
    [Fact]
    public void Create_Should_UseConfiguredExceptionType_AndOmitRejectedValue()
    {
        var exception = Assert.Throws<RedactedDomainException>(() => RedactedCustomExceptionId.Create(RejectedValue));

        exception.Message.Should().NotContain(RejectedValue);
        exception.Message.Should().Be($"The value is not a valid {nameof(RedactedCustomExceptionId)}.");
    }

    /// <summary>
    /// <c>TryCreate</c> 是非抛异常的工厂：被拒时返回 <see langword="false"/> 并把结果置为 <c>default</c>，
    /// 压根没有消息可泄漏，这也是「不想让用户输入进日志」时最省事的用法。
    /// </summary>
    [Fact]
    public void TryCreate_Should_ReturnFalse_ForRejectedValue()
    {
        RedactedValidatedStringId.TryCreate(RejectedValue, out var redacted).Should().BeFalse();
        redacted.Should().Be(default);

        VerboseValidatedStringId.TryCreate(RejectedValue, out var verbose).Should().BeFalse();
        verbose.Should().Be(default);
    }

    /// <summary>
    /// 脱敏不影响接受路径：合法值照常构造。
    /// </summary>
    [Fact]
    public void Create_Should_AcceptValidValue_RegardlessOfRedaction()
    {
        RedactedValidatedStringId.Create("ok").Value.Should().Be("ok");
        VerboseValidatedStringId.Create("ok").Value.Should().Be("ok");
    }

    /// <summary>
    /// <c>TryParse</c> 走的是同一条校验分支，但失败只返回 <see langword="false"/>。
    /// </summary>
    [Fact]
    public void TryParse_Should_ReturnFalse_ForRejectedValue()
    {
        RedactedValidatedStringId.TryParse(RejectedValue, null, out var redacted).Should().BeFalse();
        redacted.Should().Be(default);

        VerboseValidatedStringId.TryParse(RejectedValue, null, out var verbose).Should().BeFalse();
        verbose.Should().Be(default);
    }

    /// <summary>
    /// <c>Parse</c> 的失败消息恒为固定文案，与脱敏开关无关，它报的是「文本读不成 Id」，
    /// 而 <c>RedactValidationMessage</c> 只作用于 <c>Create</c> 的验证拒绝分支。
    /// </summary>
    /// <remarks>
    /// 这条断言的价值在于锁住「Parse 不会成为第二条泄漏通道」：即便某个 Id 忘了开脱敏，
    /// 走 <c>Parse</c> 的调用方也不会把原文写进日志。
    /// </remarks>
    [Fact]
    public void Parse_Should_ThrowFormatException_WithoutLeakingValue()
    {
        var redacted = Assert.Throws<FormatException>(() => RedactedValidatedStringId.Parse(RejectedValue, null));
        var verbose = Assert.Throws<FormatException>(() => VerboseValidatedStringId.Parse(RejectedValue, null));

        redacted.Message.Should().NotContain(RejectedValue);
        verbose.Message.Should().NotContain(RejectedValue);
    }
}
