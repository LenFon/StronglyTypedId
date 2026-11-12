using System.Globalization;
using System.Text.RegularExpressions;
using FluentAssertions;
using Len.StronglyTypedId.CodeFixes;

namespace Len.StronglyTypedId;

/// <summary>
/// 文档里写死的规则条数必须与源码一致。
/// </summary>
/// <remarks>
/// 这些数字是散文，DocGen 不会重写它们，于是新增一条规则后最容易漏改。STIA0028 落地时就有六处需要同步；
/// 这条用例让漏改在 CI 变红，而不是等用户发现文档说 28 条、实际 29 条。
/// </remarks>
public partial class DocumentationCountTests
{
    private static readonly int _totalRuleCount = Descriptors.All.Length + Descriptors.GeneratorDiagnostics.Length;

    private static readonly int _fixableRuleCount = new StronglyTypedIdCodeFixProvider().FixableDiagnosticIds.Length;

    /// <summary>
    /// 六处提到规则条数的文档，都必须写出当前的总数；其中四处还要写出可修复数。
    /// </summary>
    /// <remarks>
    /// <c>docs/packages.md</c> 只讲「关闭分析器后一条都不报」，因此只断言总数。
    /// </remarks>
    [Theory]
    [InlineData("README.md", true)]
    [InlineData("README.zh-CN.md", true)]
    [InlineData("docs/features.md", true)]
    [InlineData("docs/zh-CN/features.md", true)]
    [InlineData("docs/packages.md", false)]
    [InlineData("docs/zh-CN/packages.md", false)]
    public void Documentation_Should_StateCurrentRuleCounts(string fileName, bool mentionsFixableCount)
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot(), fileName));

        // 英文写 `30 rules`，中文写 `30 条规则`。同一个文件里若出现两处，两处都必须是当前值。
        var statedTotals = RuleCountPattern().Matches(text)
            .Select(static match => int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture))
            .ToArray();

        statedTotals.Should().NotBeEmpty($"{fileName} 应当提到规则总数");
        statedTotals.Should().AllSatisfy(
            stated => stated.Should().Be(_totalRuleCount, $"{fileName} 的规则总数已过期，请同步为 {_totalRuleCount}"));

        if (!mentionsFixableCount)
        {
            return;
        }

        // 英文 `22 of its 30 rules`（可能跨行），中文 `30 条规则里有 22 条`。
        var fixablePhrase = fileName.Contains("zh-CN", StringComparison.Ordinal)
            ? $@"{_totalRuleCount}\s*条规则里有\s*{_fixableRuleCount}\s*条"
            : $@"{_fixableRuleCount}\s+of\s+its\s+{_totalRuleCount}\s+rules";

        Regex.IsMatch(text, fixablePhrase).Should().BeTrue(
            $"{fileName} 应当写出「{_fixableRuleCount} / {_totalRuleCount}」这一对数字");
    }

    [GeneratedRegex(@"(\d+)\s*(?:rules|条规则)")]
    private static partial Regex RuleCountPattern();

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Len.StronglyTypedId.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("找不到仓库根目录（未找到 Len.StronglyTypedId.slnx）。");
    }
}
