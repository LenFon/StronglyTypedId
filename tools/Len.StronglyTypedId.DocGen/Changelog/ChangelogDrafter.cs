using System.Text.RegularExpressions;

namespace Len.StronglyTypedId.DocGen.Changelog;

/// <summary>
/// 按 Conventional Commits 把「最近一个 tag 之后的提交」草拟成 <c>[Unreleased]</c> 小节。
/// </summary>
/// <remarks>
/// 输出只是草稿：提交标题是给开发者看的，发布说明要写给使用者看，工具负责分类与不漏条目，措辞仍由人改。
/// </remarks>
internal static partial class ChangelogDrafter
{
    /// <summary>
    /// 类别到小节的映射，顺序即输出顺序。
    /// </summary>
    private static readonly (string Type, string Section)[] _categories =
    [
        ("feat", "Added"),
        ("fix", "Fixed"),
        ("revert", "Fixed"),
        ("perf", "Changed"),
        ("refactor", "Changed"),
    ];

    /// <summary>
    /// 不进入变更日志的类别：它们对使用者没有可观察的影响。
    /// </summary>
    private static readonly HashSet<string> _skipped = new(StringComparer.OrdinalIgnoreCase)
    {
        "docs",
        "test",
        "tests",
        "build",
        "ci",
        "chore",
        "style",
    };

    /// <summary>
    /// 小节输出顺序；<c>Other</c> 收没有约定前缀的提交，避免静默丢条目。
    /// </summary>
    private static readonly string[] _sectionOrder = ["Added", "Changed", "Fixed", "Removed", "Security", "Other"];

    /// <summary>
    /// 匹配 <c>type(scope)!: subject</c>，兼容全角冒号。
    /// </summary>
    [GeneratedRegex(@"^(?<type>[A-Za-z]+)(?:\((?<scope>[^)]*)\))?(?<breaking>!)?[:：]\s*(?<text>.+)$")]
    private static partial Regex SubjectPattern();

    /// <summary>
    /// 打印草稿并返回退出码。
    /// </summary>
    public static int Draft(string root)
    {
        var latestTag = GitRepository.FindLatestTag(root);
        var range = latestTag is null ? null : $"{latestTag}..HEAD";
        var subjects = GitRepository.ReadSubjects(root, range);
        var buckets = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var subject in subjects)
        {
            var (section, line) = Classify(subject);

            if (section is null)
            {
                continue;
            }

            if (!buckets.TryGetValue(section, out var bucket))
            {
                bucket = [];
                buckets[section] = bucket;
            }

            bucket.Add(line);
        }

        Console.WriteLine($"## [{ChangelogDocument.Unreleased}]");
        Console.WriteLine();

        if (buckets.Count == 0)
        {
            Console.WriteLine("_区间内没有需要记录的提交。_");
        }

        foreach (var section in _sectionOrder)
        {
            if (!buckets.TryGetValue(section, out var bucket))
            {
                continue;
            }

            Console.WriteLine($"### {section}");
            Console.WriteLine();

            foreach (var line in bucket)
            {
                Console.WriteLine($"- {line}");
            }

            Console.WriteLine();
        }

        Console.Error.WriteLine(range is null
            ? "（仓库里没有 tag，草稿覆盖全部历史。）"
            : $"（区间 {range}，共 {subjects.Count} 个提交。）");

        return 0;
    }

    /// <summary>
    /// 把一条提交标题映射到小节与条目文本；返回 <see langword="null"/> 表示不记录。
    /// </summary>
    private static (string? Section, string Line) Classify(string subject)
    {
        var match = SubjectPattern().Match(subject);

        if (!match.Success)
        {
            return ("Other", subject);
        }

        var type = match.Groups["type"].Value;

        if (_skipped.Contains(type))
        {
            return (null, string.Empty);
        }

        var section = _categories.FirstOrDefault(category => string.Equals(category.Type, type, StringComparison.OrdinalIgnoreCase)).Section
            ?? "Other";

        var scope = match.Groups["scope"].Success && match.Groups["scope"].Value.Length > 0
            ? $"**{match.Groups["scope"].Value}:** "
            : string.Empty;

        var breaking = match.Groups["breaking"].Success ? "**Breaking:** " : string.Empty;

        return (section, $"{breaking}{scope}{match.Groups["text"].Value}");
    }
}
