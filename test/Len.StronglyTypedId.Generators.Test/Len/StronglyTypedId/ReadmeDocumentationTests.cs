using System.Globalization;
using FluentAssertions;
using Len.StronglyTypedId.CodeFixes;
using Len.StronglyTypedId.DocGen;
using Len.StronglyTypedId.Generators;

namespace Len.StronglyTypedId;

/// <summary>
/// 文档里由 DocGen 维护的表格与源码的一致性守卫。
/// </summary>
/// <remarks>
/// 区块按文件分配：README 只留导览级两张表，诊断表在 <c>docs/diagnostics.md</c>，生成文件表在 <c>docs/generated-code.md</c>。
/// 新增或搬动区块时，这里的 <c>InlineData</c> 与 <see cref="Program"/> 的 targets 必须一起改。
/// </remarks>
public class ReadmeDocumentationTests
{
    [Theory]
    [InlineData("README.md", "en", "primitive-types,generated-code")]
    [InlineData("README.zh-CN.md", "zh-CN", "primitive-types,generated-code")]
    [InlineData("docs/generated-code.md", "en", "generated-files")]
    [InlineData("docs/zh-CN/generated-code.md", "zh-CN", "generated-files")]
    [InlineData("docs/diagnostics.md", "en", "diagnostics")]
    [InlineData("docs/zh-CN/diagnostics.md", "zh-CN", "diagnostics")]
    public void Document_Should_MatchGeneratedTables(string fileName, string cultureName, string blockIds)
    {
        var path = Path.Combine(RepositoryRoot(), fileName);

        File.Exists(path).Should().BeTrue($"测试前提：{fileName} 必须存在于仓库中");

        var text = File.ReadAllText(path);
        var newLine = ReadmeBlocks.DetectNewLine(text);
        var culture = CultureInfo.GetCultureInfo(cultureName);

        var expected = text;

        foreach (var blockId in blockIds.Split(','))
        {
            expected = ReadmeBlocks.Replace(expected, blockId, Program.Render(blockId, culture), newLine);
        }

        ShouldMatch(text, expected, $"{fileName} 的表格已过期，请运行 `dotnet run --project tools/Len.StronglyTypedId.DocGen`");
    }

    /// <summary>
    /// <c>PACKAGE.md</c> 是 NuGet 详情页正文，随包发布后无法回滚，而它不参与 README 那套区块重写 ——
    /// 基元清单、集成标志、目标框架这三段纯枚举事实必须单独守。
    /// </summary>
    [Fact]
    public void PackageReadme_Should_MatchSourceOfTruth()
    {
        var drift = PackageReadmeVerifier.FindDrift(RepositoryRoot());

        drift.Should().BeEmpty(
            "PACKAGE.md 与源码不一致；它不在 DocGen 的重写范围内，需要手工同步");
    }

    /// <summary>
    /// 每条诊断都必须有文档条目，且不得登记描述符里不存在的规则 ID。
    /// </summary>
    [Fact]
    public void DiagnosticDocumentation_Should_CoverEveryDescriptorExactly()
    {
        var documented = DiagnosticDocumentation.Entries.Select(static entry => entry.RuleId);
        var descriptors = Descriptors.All
            .Concat(Descriptors.GeneratorDiagnostics)
            .Select(static descriptor => descriptor.Id);

        DiagnosticDocumentation.Entries.Select(static entry => entry.RuleId).Should().OnlyHaveUniqueItems(
            "同一条规则只能有一行文档");
        documented.Should().BeEquivalentTo(descriptors, "文档条目与诊断描述符必须一一对应");
    }

    /// <summary>
    /// 「代码修复」列有内容的规则，必须恰好等于 <see cref="StronglyTypedIdCodeFixProvider"/> 声明可修复的那些。
    /// </summary>
    [Fact]
    public void DiagnosticDocumentation_Should_MarkExactlyTheFixableRules()
    {
        var documentedAsFixable = DiagnosticDocumentation.Entries
            .Where(static entry => !entry.Fix.IsEmpty)
            .Select(static entry => entry.RuleId);

        documentedAsFixable.Should().BeEquivalentTo(
            new StronglyTypedIdCodeFixProvider().FixableDiagnosticIds,
            "「代码修复」列有内容的规则必须与 CodeFixProvider 的可修复集合完全一致");
    }

    /// <summary>
    /// 所有参与渲染的文案都必须有中文版本。
    /// </summary>
    [Fact]
    public void Documentation_Should_ProvideChineseTextForEveryCell()
    {
        foreach (var entry in DiagnosticDocumentation.Entries.Where(static entry => !entry.Fix.IsEmpty))
        {
            entry.Fix.Zh.Should().NotBeNullOrWhiteSpace($"{entry.RuleId} 的代码修复文案缺少中文");
        }

        foreach (var registration in StronglyTypedIdGenerator.DocumentedGenerators)
        {
            registration.Condition.Zh.Should().NotBeNullOrWhiteSpace($"{registration.AssemblyName} 的触发条件缺少中文");
            registration.Documentation.Zh.Should().NotBeNullOrWhiteSpace($"{registration.AssemblyName} 的产物说明缺少中文");
            registration.FileNames.Zh.Should().NotBeNullOrWhiteSpace($"{registration.AssemblyName} 的产物文件名缺少中文");
        }
    }

    /// <summary>
    /// 每个成行的登记项都必须登记文件名：<c>docs/generated-code.md</c> 的「Generated files」表
    /// 与 README 的表共用同一批行，漏填会让那一格变成空白而不是报错。
    /// </summary>
    [Fact]
    public void DocumentedGenerators_Should_RegisterFileNames()
    {
        foreach (var registration in StronglyTypedIdGenerator.DocumentedGenerators)
        {
            registration.FileNames.IsEmpty.Should().BeFalse(
                $"{registration.AssemblyName} 已在 README 表中成行，必须同时登记产物文件名");
        }
    }

    /// <summary>
    /// 基元登记表不得出现重复项：同名条目会让 <c>IsSupported</c> 的集合构建与表格行数悄悄错位。
    /// </summary>
    [Fact]
    public void SupportedPrimitiveTypes_Should_NotRegisterDuplicates()
    {
        SupportedPrimitiveTypes.DocumentedTypes.Select(static type => type.ClrName).Should().OnlyHaveUniqueItems();
        SupportedPrimitiveTypes.DocumentedTypes.Select(static type => type.CSharpName).Should().OnlyHaveUniqueItems();
    }

    /// <summary>
    /// 逐行比对并只报首个差异行：整篇 README 直接 <c>Should().Be(...)</c> 会把近千行文本全打进失败信息里。
    /// </summary>
    private static void ShouldMatch(string actual, string expected, string because)
    {
        var actualLines = actual.Split('\n');
        var expectedLines = expected.Split('\n');
        var count = Math.Min(actualLines.Length, expectedLines.Length);

        for (var index = 0; index < count; index++)
        {
            if (!string.Equals(actualLines[index], expectedLines[index], StringComparison.Ordinal))
            {
                actualLines[index].Should().Be(expectedLines[index], $"{because}（首个差异在第 {index + 1} 行）");
            }
        }

        actualLines.Length.Should().Be(expectedLines.Length, because);
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
