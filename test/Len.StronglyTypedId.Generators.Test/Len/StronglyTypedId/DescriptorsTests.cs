using System.Text.RegularExpressions;
using FluentAssertions;
using FluentAssertions.Execution;
using Len.StronglyTypedId.Analyzers;
using Microsoft.CodeAnalysis;

namespace Len.StronglyTypedId;

/// <summary>
/// <see cref="Descriptors"/> 的契约守卫。
/// </summary>
public partial class DescriptorsTests
{
    /// <summary>
    /// 资源键的形态：<c>&lt;描述符属性名&gt;Title</c> 或 <c>&lt;描述符属性名&gt;Message</c>。渲染结果命中该形态即说明本地化未命中、回退到了键本身。
    /// </summary>
    [GeneratedRegex("^[A-Za-z]+(Title|Message)$")]
    private static partial Regex ResourceKeyPattern();

    /// <summary>
    /// 规则 ID 的形态：<c>STIA0</c> + 三位数字。
    /// </summary>
    [GeneratedRegex(@"^STIA0\d{3}$")]
    private static partial Regex RuleIdPattern();

    [Fact]
    public void Descriptors_Should_FollowRuleIdAndSeverityConvention()
    {
        using var scope = new AssertionScope();

        foreach (var descriptor in Descriptors.All)
        {
            descriptor.Id.Should().MatchRegex(RuleIdPattern(), "规则 ID 是对外承诺，不能改号");
            descriptor.Category.Should().Be("StronglyTypedIdAnalyzer");

            // STIA0014（ValidatorCanBeBypassed）默认关闭：public 的 Id 类型是完全正常的用法
            // （API 契约、DTO、跨程序集传递都需要），默认开启会在绝大多数项目上制造噪声，
            // 只应由需要「验证器不可绕过」这一强保证的项目在 .editorconfig 里显式开启。
            // 其余规则一律默认启用。
            if (descriptor.Id == Descriptors.ValidatorCanBeBypassedId)
            {
                descriptor.IsEnabledByDefault.Should().BeFalse("STIA0014 默认关闭，需显式开启");
            }
            else
            {
                descriptor.IsEnabledByDefault.Should().BeTrue();
            }

            // STIA0011（BypassCreate）是「告警性」诊断：仅当 Id 设了 Validator 时提示绕过 Create 的构造，
            // 默认 Warning —— 不阻断构建，但 build 与 CI 都能看见（各项目可在 .editorconfig 里调级）。
            // STIA0013（InvalidPrefix）同样是「告警性」诊断：空前缀没有语义、纯空白前缀几乎必然是笔误。
            // STIA0019（IntegrationUnavailable）也是告警：显式 opt-in 却因缺包静默失效，必须可见但不阻断构建。
            // STIA0014（ValidatorCanBeBypassed）为建议性诊断，默认 Info。
            // STIA0015 / STIA0016 是纯命名风格提示（名字不以 Id 结尾、同命名空间共用基元），
            // 与「声明是否合法」无关，默认 Info。
            // 其余规则一律 Error。
            switch (descriptor.Id)
            {
                case Descriptors.BypassCreateId:
                    descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Warning, "STIA0011 是告警性诊断");
                    break;
                case Descriptors.InvalidPrefixId:
                    descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Warning, "STIA0013 是告警性诊断");
                    break;
                case Descriptors.IntegrationUnavailableId:
                    descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Warning, "STIA0019 是告警性诊断");
                    break;
                case Descriptors.ValidatorCanBeBypassedId:
                    descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Info, "STIA0014 是建议性诊断");
                    break;
                case Descriptors.TypeNameShouldEndWithIdId:
                    descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Info, "STIA0015 是命名风格提示");
                    break;
                case Descriptors.DuplicatePrimitiveTypeId:
                    descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Info, "STIA0016 是命名风格提示");
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
