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
public partial class DescriptorsTests
{
    /// <summary>
    /// 资源键的形态：<c>&lt;描述符属性名&gt;Title</c> 或 <c>&lt;描述符属性名&gt;Message</c>。
    /// 渲染结果命中该形态即说明本地化未命中、回退到了键本身。
    /// </summary>
    [GeneratedRegex("^[A-Za-z]+(Title|Message)$")]
    private static partial Regex ResourceKeyPattern();

    /// <summary>
    /// 规则 ID 的形态：<c>STIAO</c> + 三位数字。
    /// </summary>
    [GeneratedRegex(@"^STIAO\d{3}$")]
    private static partial Regex RuleIdPattern();

    [Fact]
    public void Descriptors_Should_FollowRuleIdAndSeverityConvention()
    {
        using var scope = new AssertionScope();

        foreach (var descriptor in Descriptors.All)
        {
            descriptor.Id.Should().MatchRegex(RuleIdPattern(), "规则 ID 是对外承诺，不能改号");
            descriptor.Category.Should().Be("StronglyTypedIdAnalyzer");

            // STIAO014（ValidatorCanBeBypassed）默认关闭：public 的 Id 类型是完全正常的用法
            // （API 契约、DTO、跨程序集传递都需要），默认开启会在绝大多数项目上制造噪声，
            // 只应由需要「验证器不可绕过」这一强保证的项目在 .editorconfig 里显式开启。
            // 其余规则一律默认启用。
            if (descriptor.Id == Descriptors.ValidatorCanBeBypassedId)
            {
                descriptor.IsEnabledByDefault.Should().BeFalse("STIAO014 默认关闭，需显式开启");
            }
            else
            {
                descriptor.IsEnabledByDefault.Should().BeTrue();
            }

            // STIAO011（BypassCreate）是「建议性」诊断：仅当 Id 设了 Validator 时提示绕过 Create 的直接构造，
            // 默认 Info 以避免无验证器场景下的噪声（各项目可在 .editorconfig 里调级）。
            // STIAO013（InvalidPrefix）是「告警性」诊断：空前缀没有语义、纯空白前缀几乎必然是笔误，
            // 默认 Warning —— 不阻断构建，但在错误列表里提示（各项目可在 .editorconfig 里调级）。
            // STIAO014（ValidatorCanBeBypassed）同为建议性诊断，默认 Info。
            // STIAO015 / STIAO016 是纯命名风格提示（名字不以 Id 结尾、同命名空间共用基元），
            // 与「声明是否合法」无关，默认 Info。
            // 其余规则一律 Error。
            switch (descriptor.Id)
            {
                case Descriptors.BypassCreateId:
                    descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Info, "STIAO011 是建议性诊断");
                    break;
                case Descriptors.InvalidPrefixId:
                    descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Warning, "STIAO013 是告警性诊断");
                    break;
                case Descriptors.ValidatorCanBeBypassedId:
                    descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Info, "STIAO014 是建议性诊断");
                    break;
                case Descriptors.TypeNameShouldEndWithIdId:
                    descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Info, "STIAO015 是命名风格提示");
                    break;
                case Descriptors.DuplicatePrimitiveTypeId:
                    descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Info, "STIAO016 是命名风格提示");
                    break;
                default:
                    descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Error);
                    break;
            }
        }
    }

    [Fact]
    public void Descriptors_Should_HaveUniqueRuleIds() => Descriptors.All.Select(descriptor => descriptor.Id).Should().OnlyHaveUniqueItems();

    /// <summary>
    /// 标题与消息都必须命中内嵌的本地化资源，而不是回退成资源键。
    /// </summary>
    [Fact]
    public void DescriptorTexts_Should_ResolveToLocalizationResource()
    {
        using var scope = new AssertionScope();

        foreach (var descriptor in Descriptors.All)
        {
            descriptor.Title.ToString().Should().NotMatchRegex(ResourceKeyPattern());
            descriptor.MessageFormat.ToString().Should().NotMatchRegex(ResourceKeyPattern());
        }
    }

    /// <summary>
    /// 每条消息都要保留实参占位符。
    /// </summary>
    /// <remarks>
    /// STIAO007 / STIAO008 曾漏掉 <c>{0}</c>，分析器虽然传入了参数名 / 参数类型，文案却把它们静默丢弃，
    /// 使用者看不到究竟哪个名字、哪个类型有问题。STIAO013 例外：它描述的是「前缀为空或纯空白」这一事实本身，
    /// 可展示的实参就是空 / 空白，回显它只是噪声，故刻意不带占位符。
    /// </remarks>
    [Fact]
    public void DescriptorMessages_Should_KeepArgumentPlaceholder()
    {
        using var scope = new AssertionScope();

        foreach (var descriptor in Descriptors.All)
        {
            if (descriptor.Id == Descriptors.InvalidPrefixId)
            {
                continue;
            }

            descriptor.MessageFormat.ToString().Should().Contain("{0}", $"{descriptor.Id} 的消息必须保留 {0} 占位符");
        }
    }

    [Fact]
    public void SupportedDiagnostics_Should_ExposeEveryDescriptor() => new StronglyTypedIdAnalyzer().SupportedDiagnostics.Should().BeEquivalentTo(Descriptors.All);

    /// <summary>
    /// 生成器侧诊断与分析器侧诊断共用同一套规则 ID 形态与本地化契约。
    /// </summary>
    /// <remarks>
    /// 生成器没有 <c>SupportedDiagnostics</c> 这样的声明入口，规则是否「存在」只由是否被报出体现，
    /// 因此这里手工把同一份契约再过一遍：ID 形态、类别、默认启用、严重级别、本地化命中、占位符保留。
    /// </remarks>
    [Fact]
    public void GeneratorDiagnostics_Should_SatisfyTheSameContract()
    {
        using var scope = new AssertionScope();

        Descriptors.GeneratorDiagnostics.Should().NotBeEmpty();

        foreach (var descriptor in Descriptors.GeneratorDiagnostics)
        {
            descriptor.Id.Should().MatchRegex(RuleIdPattern(), "规则 ID 是对外承诺，不能改号");
            descriptor.Category.Should().Be("StronglyTypedIdGenerator");
            descriptor.IsEnabledByDefault.Should().BeTrue();
            descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Error);
            descriptor.Title.ToString().Should().NotMatchRegex(ResourceKeyPattern());
            descriptor.MessageFormat.ToString().Should().NotMatchRegex(ResourceKeyPattern());
            descriptor.MessageFormat.ToString().Should().Contain("{0}", $"{descriptor.Id} 的消息必须保留 {0} 占位符");
        }
    }

    /// <summary>
    /// 两组诊断的规则 ID 不得重叠：分析器的 <c>SupportedDiagnostics</c> 不该声称支持它从不报出的规则。
    /// </summary>
    [Fact]
    public void GeneratorDiagnostics_Should_NotOverlapAnalyzerDiagnostics()
    {
        Descriptors.GeneratorDiagnostics.Select(descriptor => descriptor.Id)
            .Should().NotIntersectWith(Descriptors.All.Select(descriptor => descriptor.Id));
    }
}
