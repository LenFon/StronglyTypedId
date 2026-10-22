using System.Text.RegularExpressions;

namespace Len.StronglyTypedId.DocGen.Changelog;

/// <summary>
/// <c>CHANGELOG.md</c> 的一个版本小节。
/// </summary>
internal sealed record ChangelogSection(string Version, string? Date, int LineIndex);

/// <summary>
/// <c>CHANGELOG.md</c> 的解析：只认标题与链接定义两类结构，不关心正文措辞。
/// </summary>
/// <remarks>
/// 正文由人写，工具只负责「每个发布都有小节、日期对得上、链接不缺」这些可机械判定的部分。
/// </remarks>
internal static partial class ChangelogDocument
{
    /// <summary>
    /// 未发布小节的固定名字。
    /// </summary>
    public const string Unreleased = "Unreleased";

    /// <summary>
    /// 未发布小节可接受的标题。中文版沿用 Keep a Changelog 的本地化写法，校验时视作同一个小节。
    /// </summary>
    private static readonly HashSet<string> _unreleasedNames = new(StringComparer.Ordinal)
    {
        Unreleased,
        "未发布",
    };

    /// <summary>
    /// 判断小节标题是否代表「未发布」。
    /// </summary>
    public static bool IsUnreleased(string version) => _unreleasedNames.Contains(version);

    /// <summary>
    /// 匹配 <c>## [1.3.0] - 2026-10-07</c> 与 <c>## [Unreleased]</c>。
    /// </summary>
    [GeneratedRegex(@"^## \[(?<version>[^\]]+)\](?: - (?<date>\d{4}-\d{2}-\d{2}))?\s*$")]
    private static partial Regex HeadingPattern();

    /// <summary>
    /// 匹配文末的 <c>[1.3.0]: https://…</c>。
    /// </summary>
    [GeneratedRegex(@"^\[(?<version>[^\]]+)\]:\s*\S+")]
    private static partial Regex LinkDefinitionPattern();

    /// <summary>
    /// 按出现顺序取出全部版本小节。
    /// </summary>
    public static IReadOnlyList<ChangelogSection> ParseSections(string text)
    {
        var sections = new List<ChangelogSection>();
        var lines = SplitLines(text);

        for (var index = 0; index < lines.Length; index++)
        {
            var match = HeadingPattern().Match(lines[index]);

            if (match.Success)
            {
                var date = match.Groups["date"].Success ? match.Groups["date"].Value : null;
                sections.Add(new ChangelogSection(match.Groups["version"].Value, date, index));
            }
        }

        return sections;
    }

    /// <summary>
    /// 取出文末声明的链接标签集合（不含方括号）。
    /// </summary>
    public static IReadOnlySet<string> ParseLinkDefinitions(string text)
    {
        var versions = new HashSet<string>(StringComparer.Ordinal);

        foreach (var line in SplitLines(text))
        {
            var match = LinkDefinitionPattern().Match(line);

            if (match.Success)
            {
                versions.Add(match.Groups["version"].Value);
            }
        }

        return versions;
    }

    /// <summary>
    /// 判断小节正文是否为空：下一个标题之前的行里，至少有一行非空白内容。
    /// </summary>
    public static bool HasContent(string text, ChangelogSection section, int nextHeadingLineIndex)
    {
        var lines = SplitLines(text);
        var end = nextHeadingLineIndex < 0 ? lines.Length : nextHeadingLineIndex;

        for (var index = section.LineIndex + 1; index < end; index++)
        {
            if (!string.IsNullOrWhiteSpace(lines[index]))
            {
                return true;
            }
        }

        return false;
    }

    private static string[] SplitLines(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
}
