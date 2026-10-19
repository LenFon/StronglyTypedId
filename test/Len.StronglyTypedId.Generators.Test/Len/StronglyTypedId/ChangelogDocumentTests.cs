using FluentAssertions;
using Len.StronglyTypedId.DocGen.Changelog;

namespace Len.StronglyTypedId;

/// <summary>
/// <c>CHANGELOG.md</c> 解析与结构守卫。
/// </summary>
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

    [Theory]
    [InlineData("CHANGELOG.md")]
    [InlineData("CHANGELOG.zh-CN.md")]
    public void Changelog_Should_StartWithUnreleasedAndLinkEverySection(string fileName)
    {
        var path = Path.Combine(RepositoryRoot(), fileName);

        File.Exists(path).Should().BeTrue($"仓库根目录必须有 {fileName}");

        var text = File.ReadAllText(path);
        var sections = ChangelogDocument.ParseSections(text);
        var links = ChangelogDocument.ParseLinkDefinitions(text);

        sections.Should().NotBeEmpty();
        ChangelogDocument.IsUnreleased(sections[0].Version).Should().BeTrue("第一个小节必须是未发布小节");
        sections.Skip(1).Should().OnlyContain(section => section.Date != null, "已发布小节必须带日期");
        links.Should().Contain(sections.Select(static section => section.Version), "每个小节都要有文末链接定义");
    }

    [Fact]
    public void IsUnreleased_Should_AcceptLocalisedHeading()
    {
        ChangelogDocument.IsUnreleased(ChangelogDocument.Unreleased).Should().BeTrue();
        ChangelogDocument.IsUnreleased("未发布").Should().BeTrue();
        ChangelogDocument.IsUnreleased("1.3.0").Should().BeFalse();
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
