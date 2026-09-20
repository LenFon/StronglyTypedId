using System.Text.RegularExpressions;
using FluentAssertions;
using FluentAssertions.Execution;
using Len.StronglyTypedId.Analyzers;
using Microsoft.CodeAnalysis;

namespace Len.StronglyTypedId;

/// <summary>
/// <see cref="Descriptors"/> 的契约守卫。
/// </summary>
/// <remarks>
/// <para>
/// 规则 ID 会被写进使用者的 <c>NoWarn</c> / <c>#pragma</c> / 编辑器配置里，属对外承诺，一旦发布就不能改号，
/// 只能改文案，因此在此把 ID 形态、类别、严重级别一并钉死。
/// </para>
/// <para>
/// 文案则与 <c>Locales/en.txt</c> / <c>Locales/zh-CN.txt</c> 按 <b>资源键</b> 关联：键名写错或资源漏配时，
/// <see cref="LocalizedString"/> 会静默回退成键本身（例如显示 <c>TypeMustBePartialTitle</c>），
/// 使用者看到的是一串驼峰标识符而不是人话。这类"静默降级"没有编译期信号，只能靠用例把守。
/// </para>
/// </remarks>
public class DescriptorsTests
{
    /// <summary>
    /// 资源键的形态：<c>&lt;描述符属性名&gt;Title</c> 或 <c>&lt;描述符属性名&gt;Message</c>。
    /// 渲染结果命中该形态即说明本地化未命中、回退到了键本身。
    /// </summary>
    private static readonly Regex _resourceKeyPattern = new("^[A-Za-z]+(Title|Message)$", RegexOptions.Compiled);

    /// <summary>
    /// 规则 ID 的形态：<c>STIAO</c> + 三位数字。
    /// </summary>
    private static readonly Regex _ruleIdPattern = new(@"^STIAO\d{3}$", RegexOptions.Compiled);

    [Fact]
    public void Descriptors_Should_FollowRuleIdAndSeverityConvention()
    {
        using var scope = new AssertionScope();

        foreach (var descriptor in Descriptors.All)
        {
            descriptor.Id.Should().MatchRegex(_ruleIdPattern, "规则 ID 是对外承诺，不能改号");
            descriptor.Category.Should().Be("StronglyTypedIdAnalyzer");
            descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Error);
            descriptor.IsEnabledByDefault.Should().BeTrue();
        }
    }

    [Fact]
    public void Descriptors_Should_HaveUniqueRuleIds()
    {
        Descriptors.All.Select(descriptor => descriptor.Id).Should().OnlyHaveUniqueItems();
    }

    /// <summary>
    /// 标题与消息都必须命中内嵌的本地化资源，而不是回退成资源键。
    /// </summary>
    [Fact]
    public void DescriptorTexts_Should_ResolveToLocalizationResource()
    {
        using var scope = new AssertionScope();

        foreach (var descriptor in Descriptors.All)
        {
            descriptor.Title.ToString().Should().NotMatchRegex(_resourceKeyPattern);
            descriptor.MessageFormat.ToString().Should().NotMatchRegex(_resourceKeyPattern);
        }
    }

    /// <summary>
    /// 每条消息都要保留实参占位符。
    /// </summary>
    /// <remarks>
    /// STIAO007 / STIAO008 曾漏掉 <c>{0}</c>，分析器虽然传入了参数名 / 参数类型，文案却把它们静默丢弃，
    /// 使用者看不到究竟哪个名字、哪个类型有问题。
    /// </remarks>
    [Fact]
    public void DescriptorMessages_Should_KeepArgumentPlaceholder()
    {
        using var scope = new AssertionScope();

        foreach (var descriptor in Descriptors.All)
        {
            descriptor.MessageFormat.ToString().Should().Contain("{0}", $"{descriptor.Id} 的消息必须保留 {0} 占位符");
        }
    }

    [Fact]
    public void SupportedDiagnostics_Should_ExposeEveryDescriptor()
    {
        new StronglyTypedIdAnalyzer().SupportedDiagnostics.Should().BeEquivalentTo(Descriptors.All);
    }
}
