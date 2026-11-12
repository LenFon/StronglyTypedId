using FluentAssertions;

namespace Len.StronglyTypedId;

/// <summary>
/// 富验证结果（<see cref="StronglyTypedIdValidationResult"/>）的运行时语义：拒绝原因与错误码
/// 能否从 <c>Create</c> 的异常和 <c>TryCreate</c> 的三参重载拿到，以及它与脱敏、自定义异常、
/// 归一化的组合行为。
/// </summary>
/// <remarks>
/// 生成器侧的快照只能证明「代码长得对」；这里补的是运行期事实：原因到底传没传出来。
/// </remarks>
public class ValidationResultTests
{
    /// <summary>
    /// <see cref="StronglyTypedIdValidationResult"/> 的 <see langword="default"/> 必须是「通过」，
    /// 否则验证器写 <c>return default;</c> 会被当成拒绝。
    /// </summary>
    [Fact]
    public void Default_Should_BeSuccess()
    {
        StronglyTypedIdValidationResult result = default;

        result.IsValid.Should().BeTrue();
        result.Message.Should().BeNull();
        result.Code.Should().BeNull();
        StronglyTypedIdValidationResult.Success.IsValid.Should().BeTrue();
    }

    /// <summary>
    /// <see cref="StronglyTypedIdValidationResult.Failure"/> 带回原因与错误码。
    /// </summary>
    [Fact]
    public void Failure_Should_CarryMessageAndCode()
    {
        var result = StronglyTypedIdValidationResult.Failure("nope", "E_NOPE");

        result.IsValid.Should().BeFalse();
        result.Message.Should().Be("nope");
        result.Code.Should().Be("E_NOPE");
    }

    /// <summary>
    /// 错误码是可选的。
    /// </summary>
    [Fact]
    public void Failure_Should_AllowMissingCode()
    {
        var result = StronglyTypedIdValidationResult.Failure("nope");

        result.IsValid.Should().BeFalse();
        result.Message.Should().Be("nope");
        result.Code.Should().BeNull();
    }

    /// <summary>
    /// 消息为 <see langword="null"/> 是调用方错误，必须立刻抛，而不是留到生成代码里变成空消息。
    /// </summary>
    [Fact]
    public void Failure_Should_RejectNullMessage()
        => FluentActions.Invoking(static () => StronglyTypedIdValidationResult.Failure(null!))
            .Should().Throw<ArgumentNullException>();

    /// <summary>
    /// <c>Create</c> 被拒绝时，异常消息就是验证器给出的原因。
    /// </summary>
    [Fact]
    public void Create_Should_UseValidatorMessage_WhenResultValidatorRejects()
    {
        var act = static () => ResultValidatedGuidId.Create(Guid.Empty);

        act.Should().Throw<ArgumentException>()
            .WithMessage("An id cannot be empty.*");
    }

    /// <summary>
    /// <c>Create</c> 接受时正常构造。
    /// </summary>
    [Fact]
    public void Create_Should_Succeed_WhenResultValidatorAccepts()
    {
        var value = Guid.NewGuid();

        ResultValidatedGuidId.Create(value).Value.Should().Be(value);
    }

    /// <summary>
    /// 三参 <c>TryCreate</c> 在拒绝时带回原因与错误码，而不是只给一个 <see langword="false"/>。
    /// </summary>
    [Fact]
    public void TryCreate_Should_ReturnValidationResult_WhenResultValidatorRejects()
    {
        var succeeded = ResultValidatedGuidId.TryCreate(Guid.Empty, out var result, out var validation);

        succeeded.Should().BeFalse();
        result.Should().Be(default);
        validation.IsValid.Should().BeFalse();
        validation.Message.Should().Be("An id cannot be empty.");
        validation.Code.Should().Be("E_EMPTY");
    }

    /// <summary>
    /// 三参 <c>TryCreate</c> 在通过时给出 <see cref="StronglyTypedIdValidationResult.Success"/>。
    /// </summary>
    [Fact]
    public void TryCreate_Should_ReturnSuccess_WhenResultValidatorAccepts()
    {
        var value = Guid.NewGuid();

        var succeeded = ResultValidatedGuidId.TryCreate(value, out var result, out var validation);

        succeeded.Should().BeTrue();
        result.Value.Should().Be(value);
        validation.IsValid.Should().BeTrue();
        validation.Message.Should().BeNull();
    }

    /// <summary>
    /// 两参重载仍然可用，且与三参重载结论一致。
    /// </summary>
    [Fact]
    public void TryCreate_Should_KeepTwoArgumentOverload()
    {
        ResultValidatedGuidId.TryCreate(Guid.Empty, out _).Should().BeFalse();
        ResultValidatedGuidId.TryCreate(Guid.NewGuid(), out _).Should().BeTrue();
    }

    /// <summary>
    /// 返回富结果的验证器同样要拦住 <c>TryParse</c> 与 <c>Parse</c>。
    /// </summary>
    /// <remarks>
    /// 缺陷回归：验证器返回结构体时无法并入解析条件，校验被挪进「解析成功」块；但那个块只在存在归一化时
    /// 才写校验，于是无归一化的 Id 三个 <c>TryParse</c> 重载全部放行被拒的值，<c>Parse</c> 也跟着成功。
    /// 文档承诺「每个 <c>TryParse</c> 重载都会调用验证器」，这里逐入口锁住。
    /// </remarks>
    [Fact]
    public void TryParse_Should_Reject_WhenResultValidatorRejects()
    {
        var rejected = Guid.Empty.ToString();

        ResultValidatedGuidId.TryParse(rejected, null, out _).Should().BeFalse();
        ResultValidatedGuidId.TryParse(rejected.AsSpan(), null, out _).Should().BeFalse();

        // Guid 自 .NET 10 起才实现 IUtf8SpanParsable<Guid>，UTF-8 重载在 net8.0 下不存在。
#if NET10_0_OR_GREATER
        ResultValidatedGuidId.TryParse(System.Text.Encoding.UTF8.GetBytes(rejected).AsSpan(), null, out _).Should().BeFalse();
#endif

        FluentActions.Invoking(() => ResultValidatedGuidId.Parse(rejected, null))
            .Should().Throw<FormatException>();

        // 合法值仍照常通过，避免「一律拒绝」也能让上面的断言变绿。
        var accepted = Guid.NewGuid().ToString();

        ResultValidatedGuidId.TryParse(accepted, null, out var parsed).Should().BeTrue();
        parsed.Value.Should().Be(Guid.Parse(accepted));
    }

    /// <summary>
    /// 字符串基元的富结果验证器同样拦住 <c>TryParse</c>：这条路径的「解析」只是长度判定，
    /// 更容易漏掉校验。
    /// </summary>
    [Fact]
    public void TryParse_Should_Reject_WhenResultValidatorRejectsStringPrimitive()
    {
        const string TooLong = "this-value-is-way-too-long";

        ResultValidatedRedactedId.TryParse(TooLong, null, out _).Should().BeFalse();
        ResultValidatedRedactedId.TryParse(TooLong.AsSpan(), null, out _).Should().BeFalse();

        FluentActions.Invoking(() => ResultValidatedRedactedId.Parse(TooLong, null))
            .Should().Throw<FormatException>();
    }

    /// <summary>
    /// 带前缀时读入走 <c>TryParse</c>，验证器同样要生效。
    /// </summary>
    /// <remarks>
    /// 前缀路径的 <c>TryParse</c> 与无前缀路径共用同一份成功块，但它是 JSON / Newtonsoft / MVC 绑定
    /// 读入带前缀 Id 的唯一入口，单独锁一条以免将来只修好其中一条。
    /// </remarks>
    [Fact]
    public void TryParse_Should_Reject_WhenPrefixedResultValidatorRejects()
    {
        var rejected = "ord_" + Guid.Empty.ToString();

        ResultValidatedPrefixedId.TryParse(rejected, null, out _).Should().BeFalse();

        FluentActions.Invoking(() => ResultValidatedPrefixedId.Parse(rejected, null))
            .Should().Throw<FormatException>();
    }

    /// <summary>
    /// <see langword="bool"/> 验证器的 Id 同样有三参重载：<see cref="StronglyTypedIdValidationResult.Message"/>
    /// 是一条库生成的通用消息，<see cref="StronglyTypedIdValidationResult.Code"/> 为 <see langword="null"/>。
    /// </summary>
    /// <remarks>
    /// 这样约束到 <c>IStronglyTypedId&lt;TSelf, TPrimitiveId&gt;</c> 的泛型代码不必区分两种验证器。
    /// 文案与 <c>Create</c> 的异常消息同源，改一处就会同时影响两边，故用精确断言锁住。
    /// </remarks>
    [Fact]
    public void TryCreate_Should_ExposeValidationResult_ForBoolValidator()
    {
        var rejected = NonEmptyGuidId.TryCreate(Guid.Empty, out _, out var failure);

        rejected.Should().BeFalse();
        failure.IsValid.Should().BeFalse();
        failure.Message.Should().Be($"The value is not a valid {nameof(NonEmptyGuidId)}.");
        failure.Code.Should().BeNull();

        var value = Guid.NewGuid();
        var accepted = NonEmptyGuidId.TryCreate(value, out var result, out var success);

        accepted.Should().BeTrue();
        result.Value.Should().Be(value);
        success.IsValid.Should().BeTrue();
        success.Message.Should().BeNull();
        success.Code.Should().BeNull();
    }

    /// <summary>
    /// 无验证器的 Id 也有三参重载，且恒为通过。
    /// </summary>
    [Fact]
    public void TryCreate_Should_AlwaysSucceed_WhenNoValidator()
    {
        var value = Guid.NewGuid();

        var succeeded = GuidId.TryCreate(value, out var result, out var validation);

        succeeded.Should().BeTrue();
        result.Value.Should().Be(value);
        validation.IsValid.Should().BeTrue();
    }

    /// <summary>
    /// 自定义异常类型：拒绝原因进入该异常的消息。
    /// </summary>
    [Fact]
    public void Create_Should_UseValidatorMessage_WithCustomExceptionType()
    {
        var act = static () => ResultValidatedDomainExceptionId.Create(Guid.Empty);

        act.Should().Throw<DomainRuleViolationException>()
            .WithMessage("An id cannot be empty.");
    }

    /// <summary>
    /// 脱敏打开后忽略验证器文案：验证器消息可能含用户输入，脱敏是安全策略。
    /// </summary>
    [Fact]
    public void Create_Should_IgnoreValidatorMessage_WhenRedacted()
    {
        var act = static () => ResultValidatedRedactedId.Create("this-value-is-way-too-long");

        act.Should().Throw<ArgumentException>()
            .WithMessage("The value is not a valid ResultValidatedRedactedId.*")
            .Which.Message.Should().NotContain("this-value-is-way-too-long");
    }

    /// <summary>
    /// 脱敏只作用于 <c>Create</c> 的异常消息；<c>TryCreate</c> 仍把验证器原因交给调用方，
    /// 因为那是程序内部的返回值，不经过日志与 APM。
    /// </summary>
    [Fact]
    public void TryCreate_Should_StillExposeValidatorMessage_WhenRedacted()
    {
        var succeeded = ResultValidatedRedactedId.TryCreate("this-value-is-way-too-long", out _, out var validation);

        succeeded.Should().BeFalse();
        validation.Message.Should().Be("'this-value-is-way-too-long' is too long.");
        validation.Code.Should().Be("E_TOO_LONG");
    }

    /// <summary>
    /// 归一化在验证之前执行：先 trim 再判空。
    /// </summary>
    [Fact]
    public void Create_Should_NormalizeBeforeValidating()
    {
        ResultValidatedNormalizedId.Create("  abc  ").Value.Should().Be("abc");

        var act = static () => ResultValidatedNormalizedId.Create("   ");

        act.Should().Throw<ArgumentException>()
            .WithMessage("The value cannot be blank.*");
    }

    /// <summary>
    /// 三参 <c>TryCreate</c> 同样先归一化再验证。
    /// </summary>
    [Fact]
    public void TryCreate_Should_NormalizeBeforeValidating()
    {
        var succeeded = ResultValidatedNormalizedId.TryCreate("  abc  ", out var result, out var validation);

        succeeded.Should().BeTrue();
        result.Value.Should().Be("abc");
        validation.IsValid.Should().BeTrue();
    }

    /// <summary>
    /// 约束到接口的泛型代码能调三参重载并拿到错误码 —— 这正是把该重载放到接口上的理由。
    /// </summary>
    [Fact]
    public void GenericCode_Should_ReadValidationResult_ThroughInterface()
    {
        var (succeeded, code) = TryCreateThroughInterface<ResultValidatedGuidId, Guid>(Guid.Empty);

        succeeded.Should().BeFalse();
        code.Should().Be("E_EMPTY");
    }

    /// <summary>
    /// 验证器重载时按实参类型绑定：只有 <c>Guid</c> 那个重载会被调用，故走富结果模板。
    /// </summary>
    /// <remarks>
    /// 缺陷回归：判据曾是「任一静态同名方法返回 <c>StronglyTypedIdValidationResult</c>」，于是
    /// <c>bool Validate(Guid)</c> 与 <c>Result Validate(string)</c> 并存时生成代码按 <c>Guid</c> 绑到
    /// <c>bool</c> 重载，却仍按富结果模板访问 <c>.IsValid</c>，产物直接编译不过。
    /// </remarks>
    [Fact]
    public void Create_Should_BindToOverloadMatchingPrimitive_WhenValidatorIsOverloaded()
    {
        var act = static () => OverloadedValidatorId.Create(Guid.Empty);

        act.Should().Throw<ArgumentException>()
            .WithMessage("An id cannot be empty.*");

        OverloadedValidatorId.Create(Guid.NewGuid()).Value.Should().NotBe(Guid.Empty);
    }

    private static (bool Succeeded, string? Code) TryCreateThroughInterface<TSelf, TPrimitive>(TPrimitive value)
        where TSelf : IStronglyTypedId<TSelf, TPrimitive>, IParsable<TSelf>
        where TPrimitive : notnull, IComparable
    {
        var succeeded = TSelf.TryCreate(value, out _, out var validation);

        return (succeeded, validation.Code);
    }
}
