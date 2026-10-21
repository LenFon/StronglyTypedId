using FluentAssertions;
using Len.StronglyTypedId.DocGen.Changelog;

namespace Len.StronglyTypedId;

/// <summary>
/// <c>CHANGELOG.md</c> 解析与结构守卫。
/// </summary>
/// <remarks>
/// 工具侧的 tag 比对需要 git（见 <c>--verify-changelog</c>）；这里只钉住不依赖 git 的部分：
/// 标题能被解析、正文非空、链接定义齐全。这样本地改坏格式时不必等 CI 才发现。
/// </remarks>
public class ChangelogDocumentTests
{
    private const string Sample = """
        # Changelog

        ## [Unreleased]

        ### Added

        - something new

        ## [1.2.0] - 2026-10-07

        ### Fixed

        - a bug

        ## [1.1.0] - 2026-09-13

        _Retag; no content changes._

        [Unreleased]: https://example.test/compare/1.2.0...HEAD
        [1.2.0]: https://example.test/compare/1.1.0...1.2.0
        [1.1.0]: https://example.test/releases/tag/1.1.0
        """;

    [Fact]
    public void ParseSections_Should_ReadVersionDateAndOrder()
    {
        var sections = ChangelogDocument.ParseSections(Sample);

        sections.Select(static section => section.Version)
            .Should().Equal("Unreleased", "1.2.0", "1.1.0");
        sections[0].Date.Should().BeNull("Unreleased 没有发布日期");
        sections[1].Date.Should().Be("2026-10-07");
        sections.Select(static section => section.LineIndex).Should().BeInAscendingOrder();
    }

    [Fact]
    public void ParseSections_Should_IgnoreSubheadingsAndProse()
    {
        var sections = ChangelogDocument.ParseSections(Sample);

        sections.Should().HaveCount(3, "### Added / ### Fixed 不是版本标题");
    }

    [Fact]
    public void ParseLinkDefinitions_Should_CollectEveryLabel()
    {
        var links = ChangelogDocument.ParseLinkDefinitions(Sample);

        links.Should().BeEquivalentTo(["Unreleased", "1.2.0", "1.1.0"]);
    }

    [Fact]
    public void HasContent_Should_ReportEmptySection()
    {
        const string text = """
            ## [2.0.0] - 2026-01-01

            ## [1.0.0] - 2025-01-01

            - something
            """;

        var sections = ChangelogDocument.ParseSections(text);

        ChangelogDocument.HasContent(text, sections[0], sections[1].LineIndex).Should().BeFalse();
        ChangelogDocument.HasContent(text, sections[1], -1).Should().BeTrue();
    }

    [Fact]
    public void Changelog_Should_StartWithUnreleasedAndLinkEverySection()
    {
        var path = Path.Combine(RepositoryRoot(), "CHANGELOG.md");

        File.Exists(path).Should().BeTrue("仓库根目录必须有 CHANGELOG.md");

        var text = File.ReadAllText(path);
        var sections = ChangelogDocument.ParseSections(text);
        var links = ChangelogDocument.ParseLinkDefinitions(text);

        sections.Should().NotBeEmpty();
        sections[0].Version.Should().Be(ChangelogDocument.Unreleased);
        sections.Skip(1).Should().OnlyContain(section => section.Date != null, "已发布小节必须带日期");
        links.Should().Contain(sections.Select(static section => section.Version), "每个小节都要有文末链接定义");
    }

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
