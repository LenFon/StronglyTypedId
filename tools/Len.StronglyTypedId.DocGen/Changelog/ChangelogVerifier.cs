namespace Len.StronglyTypedId.DocGen.Changelog;

/// <summary>
/// <c>CHANGELOG.md</c> 与 <c>CHANGELOG.zh-CN.md</c> 的结构校验：每个 tag 都有小节、日期与 tag 提交日期一致、链接定义齐全。
/// </summary>
/// <remarks>
/// 只校验可机械判定的部分；两份文件各自独立校验，中文版漏了某个版本小节同样会报错。
/// </remarks>
internal static class ChangelogVerifier
{
    /// <summary>
    /// 需要校验的变更日志；顺序即报告顺序。
    /// </summary>
    private static readonly string[] _files = ["CHANGELOG.md", "CHANGELOG.zh-CN.md"];

    /// <summary>
    /// 校验并输出结果；返回进程退出码（0 通过，1 有问题）。
    /// </summary>
    public static int Verify(string root)
    {
        var tags = GitRepository.ReadTags(root);

        if (tags.Count == 0)
        {
            Console.WriteLine("CHANGELOG.md: 仓库里读不到任何 tag —— 浅克隆下无法校验，请先 `git fetch --tags`。");

            return 1;
        }

        var failed = false;

        foreach (var file in _files)
        {
            if (VerifyFile(root, file, tags) != 0)
            {
                failed = true;
            }
        }

        return failed ? 1 : 0;
    }

    private static int VerifyFile(string root, string file, IReadOnlyList<GitTag> tags)
    {
        var path = Path.Combine(root, file);

        if (!File.Exists(path))
        {
            Console.WriteLine($"{file}: 文件不存在。");

            return 1;
        }

        var text = File.ReadAllText(path);
        var sections = ChangelogDocument.ParseSections(text);
        var links = ChangelogDocument.ParseLinkDefinitions(text);
        var problems = new List<string>();

        CheckUnreleased(sections, problems);
        CheckReleasedSections(text, sections, tags, links, problems);

        if (problems.Count == 0)
        {
            Console.WriteLine($"{file}: 已是最新（{tags.Count} 个 tag 均有小节）。");

            return 0;
        }

        foreach (var problem in problems)
        {
            Console.WriteLine($"{file}: {problem}");
        }

        Console.WriteLine();
        Console.WriteLine("补全缺失小节后重跑；草稿可用 `--draft-changelog` 生成。");

        return 1;
    }

    private static void CheckUnreleased(IReadOnlyList<ChangelogSection> sections, List<string> problems)
    {
        if (sections.Count == 0)
        {
            problems.Add("没有任何版本小节。");

            return;
        }

        if (!ChangelogDocument.IsUnreleased(sections[0].Version))
        {
            problems.Add($"第一个小节应为 [{ChangelogDocument.Unreleased}]，实际是 [{sections[0].Version}]（第 {sections[0].LineIndex + 1} 行）。");
        }
    }

    private static void CheckReleasedSections(
        string text,
        IReadOnlyList<ChangelogSection> sections,
        IReadOnlyList<GitTag> tags,
        IReadOnlySet<string> links,
        List<string> problems)
    {
        var byVersion = new Dictionary<string, ChangelogSection>(StringComparer.Ordinal);

        for (var index = 0; index < sections.Count; index++)
        {
            var section = sections[index];

            if (ChangelogDocument.IsUnreleased(section.Version))
            {
                continue;
            }

            if (!byVersion.TryAdd(section.Version, section))
            {
                problems.Add($"[{section.Version}] 出现了不止一次（第 {section.LineIndex + 1} 行）。");
            }

            if (section.Date is null)
            {
                problems.Add($"[{section.Version}] 缺少发布日期（应为 `## [{section.Version}] - yyyy-MM-dd`）。");
            }

            var nextLine = index + 1 < sections.Count ? sections[index + 1].LineIndex : -1;

            if (!ChangelogDocument.HasContent(text, section, nextLine))
            {
                problems.Add($"[{section.Version}] 小节正文为空。");
            }
        }

        foreach (var tag in tags)
        {
            if (!byVersion.TryGetValue(tag.Version, out var section))
            {
                problems.Add($"tag `{tag.Tag}`（{tag.Date}）没有对应小节。");

                continue;
            }

            if (section.Date is not null && !string.Equals(section.Date, tag.Date, StringComparison.Ordinal))
            {
                problems.Add($"[{tag.Version}] 的日期是 {section.Date}，但 tag `{tag.Tag}` 指向的提交日期是 {tag.Date}。");
            }
        }

        foreach (var version in byVersion.Keys)
        {
            if (!tags.Any(tag => string.Equals(tag.Version, version, StringComparison.Ordinal)))
            {
                problems.Add($"[{version}] 没有对应的 git tag（版本号写错了？）。");
            }
        }

        foreach (var section in sections)
        {
            if (!links.Contains(section.Version))
            {
                problems.Add($"[{section.Version}] 缺少文末链接定义 `[{section.Version}]: …`。");
            }
        }
    }
}
