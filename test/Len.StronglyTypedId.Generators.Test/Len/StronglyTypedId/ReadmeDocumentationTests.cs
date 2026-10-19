using System.Globalization;
using FluentAssertions;
using Len.StronglyTypedId.CodeFixes;
using Len.StronglyTypedId.DocGen;
using Len.StronglyTypedId.Generators;

namespace Len.StronglyTypedId;

/// <summary>
/// README 三张表与源码的一致性守卫。
/// </summary>
/// <remarks>
/// <para>
/// 三张表（基元类型 / 集成触发条件 / 诊断）都由 <c>tools/Len.StronglyTypedId.DocGen</c> 从生成器内部的
/// 判定数据渲染，README 里只保留 <c>&lt;!-- BEGIN GENERATED:… --&gt;</c> 标记之间的内容。
/// 这个用例把「忘了重新生成」变成一次本地可见的失败，而不是等读者发现表格与代码对不上。
/// </para>
/// <para>
/// 表外的正文（段落、示例、FAQ）不受本用例约束 —— 那些是手写的，改了就改了。
/// </para>
/// </remarks>
public class ReadmeDocumentationTests
{
    [Theory]
    [InlineData("README.md", "en")]
    [InlineData("README.zh-CN.md", "zh-CN")]
    public void Readme_Should_MatchGeneratedTables(string fileName, string cultureName)
    {
        var path = Path.Combine(RepositoryRoot(), fileName);

        File.Exists(path).Should().BeTrue($"测试前提：{fileName} 必须位于仓库根目录");

        var text = File.ReadAllText(path);
        var newLine = ReadmeBlocks.DetectNewLine(text);
        var culture = CultureInfo.GetCultureInfo(cultureName);

        var expected = ReadmeBlocks.Replace(text, "primitive-types", DocumentationRenderer.RenderPrimitiveTypes(culture), newLine);
        expected = ReadmeBlocks.Replace(expected, "generated-code", DocumentationRenderer.RenderGeneratedCode(culture), newLine);
        expected = ReadmeBlocks.Replace(expected, "diagnostics", DocumentationRenderer.RenderDiagnostics(culture), newLine);

        ShouldMatch(text, expected, $"{fileName} 的表格已过期，请运行 `dotnet run --project tools/Len.StronglyTypedId.DocGen`");
    }

    /// <summary>
    /// 每条诊断都必须有文档条目，且不得登记描述符里不存在的规则 ID。
    /// </summary>
    /// <remarks>
    /// 新增诊断却忘了登记时，README 的诊断表会静默少一行 —— 没有人会去数行数。
    /// </remarks>
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
    /// <remarks>
    /// 这一列无法从 <c>DiagnosticDescriptor</c> 推出，只能手工登记；正因如此才需要把两边钉在一起 ——
    /// 新增一个修复却忘了改文档，读者会以为那条规则只能手改。
    /// </remarks>
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
    /// <remarks>
    /// <c>README.zh-CN.md</c> 与英文版共用同一份数据，只按区域性取不同字段。漏填中文不会报错，
    /// 只会让中文表格出现空白单元格 —— 那是最容易被忽略的一种退化。
    /// </remarks>
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
