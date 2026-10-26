using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Len.StronglyTypedId.Generators;

namespace Len.StronglyTypedId.DocGen;

/// <summary>
/// 校验 <c>PACKAGE.md</c>（NuGet 详情页正文）与源码的一致性。
/// </summary>
/// <remarks>
/// <c>PACKAGE.md</c> 是手写的，不参与 README 那套区块重写，改基元 / 集成标志 / 目标框架时不会被 <c>--verify</c> 拦住，而它随包发布后无法回滚。
/// 这里只校验三段纯枚举事实（基元清单、集成标志清单、目标框架清单），不校验散文措辞。
/// </remarks>
internal static partial class PackageReadmeVerifier
{
    private const string FileName = "PACKAGE.md";
    private const string PrimitiveSentence = "Supported primitive types:";
    private const string IntegrationHeading = "## Integrations";
    private const string TargetFrameworkSentence = "target framework";
    private const string RuntimeProject = "src/Len.StronglyTypedId/Len.StronglyTypedId.csproj";

    /// <summary>
    /// 打印全部漂移并以退出码表示结果：<c>0</c> 一致，<c>1</c> 不一致。
    /// </summary>
    public static int Verify(string root)
    {
        var problems = FindDrift(root);

        if (problems.Count == 0)
        {
            // 一致时也要出一行：CI 日志里「没输出」与「step 没跑」看起来一样。
            Console.WriteLine($"{FileName}: 与源码一致。");

            return 0;
        }

        foreach (var problem in problems)
        {
            Console.WriteLine(problem);
        }

        return 1;
    }

    /// <summary>
    /// 找出 <c>PACKAGE.md</c> 与源码的全部不一致之处；空集合表示一致。
    /// </summary>
    public static IReadOnlyList<string> FindDrift(string root)
    {
        var path = Path.Combine(root, FileName);

        if (!File.Exists(path))
        {
            return [$"{FileName} 不存在（它是 NuGet 详情页正文，由 common.props 的 PackageReadmeFile 引用）。"];
        }

        var text = File.ReadAllText(path);
        var problems = new List<string>();

        problems.AddRange(Diff(
            "基元类型",
            [.. SupportedPrimitiveTypes.DocumentedTypes.Select(static type => type.CSharpName), "enum"],
            BacktickedTokens(Sentence(text, PrimitiveSentence))));

        problems.AddRange(Diff(
            "目标框架",
            ReadTargetFrameworks(root),
            [.. TargetFrameworkPattern().Matches(Sentence(text, TargetFrameworkSentence))
                .Select(static match => match.Value)
                .Distinct(StringComparer.Ordinal)]));

        var integrations = Section(text, IntegrationHeading);

        foreach (var flag in IntegrationFlagNames().Where(flag => !integrations.Contains($"`{flag}`", StringComparison.Ordinal)))
        {
            problems.Add($"{FileName} 的「Integrations」一节没有提到标志 `{flag}`。");
        }

        return problems;
    }

    /// <summary>
    /// 取集成标志的常量名（排除 <c>Default</c> / <c>None</c> / <c>All</c> 这三个非具体集成的取值）。
    /// </summary>
    private static IEnumerable<string> IntegrationFlagNames()
        => typeof(IntegrationFlags)
            .GetFields(BindingFlags.NonPublic | BindingFlags.Static)
            .Where(static field => field.IsLiteral && field.FieldType == typeof(int))
            .Select(static field => field.Name)
            .Where(static name => name is not ("Default" or "None" or "All"));

    /// <summary>
    /// 读运行时工程声明的目标框架。
    /// </summary>
    private static IReadOnlyList<string> ReadTargetFrameworks(string root)
    {
        var document = XDocument.Load(Path.Combine(root, RuntimeProject.Replace('/', Path.DirectorySeparatorChar)));

        var element = document.Descendants()
            .FirstOrDefault(static candidate => candidate.Name.LocalName is "TargetFrameworks" or "TargetFramework");

        return element is null
            ? []
            : [.. element.Value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
    }

    /// <summary>
    /// 取以 <paramref name="marker"/> 开头的那一句（到第一个句号加空格为止）。
    /// </summary>
    /// <remarks>
    /// 不能整段取：基元那句后面紧跟「Use `record struct` ...」，整段扫反引号会把 <c>record struct</c> / <c>record</c> 当成多余的基元类型报出来。
    /// </remarks>
    private static string Sentence(string text, string marker)
    {
        var paragraph = Paragraph(text, marker);
        var end = paragraph.IndexOf(". ", StringComparison.Ordinal);

        return end < 0 ? paragraph : paragraph[..end];
    }

    /// <summary>
    /// 取以 <paramref name="marker"/> 开头的那一段（到下一个空行为止）。
    /// </summary>
    private static string Paragraph(string text, string marker)
    {
        var start = text.IndexOf(marker, StringComparison.Ordinal);

        if (start < 0)
        {
            return string.Empty;
        }

        var end = text.IndexOf("\n\n", start, StringComparison.Ordinal);

        return end < 0 ? text[start..] : text[start..end];
    }

    /// <summary>
    /// 取某个二级标题下的整节内容（到下一个二级标题为止）。
    /// </summary>
    private static string Section(string text, string heading)
    {
        var start = text.IndexOf(heading, StringComparison.Ordinal);

        if (start < 0)
        {
            return string.Empty;
        }

        var end = text.IndexOf("\n## ", start + heading.Length, StringComparison.Ordinal);

        return end < 0 ? text[start..] : text[start..end];
    }

    /// <summary>
    /// 取出文中所有反引号包裹的记号（去重）。
    /// </summary>
    private static IReadOnlyList<string> BacktickedTokens(string text)
        => [.. BacktickPattern().Matches(text)
            .Select(static match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)];

    /// <summary>
    /// 目标框架记号（<c>net8.0</c> 这种形态）。
    /// </summary>
    [GeneratedRegex(@"net\d+\.\d+")]
    private static partial Regex TargetFrameworkPattern();

    /// <summary>
    /// 反引号包裹的行内代码记号。
    /// </summary>
    [GeneratedRegex("`([^`\\r\\n]+)`")]
    private static partial Regex BacktickPattern();

    private static IEnumerable<string> Diff(string label, IReadOnlyCollection<string> expected, IReadOnlyCollection<string> actual)
    {
        foreach (var missing in expected.Except(actual, StringComparer.Ordinal))
        {
            yield return $"{FileName} 缺少{label} `{missing}`。";
        }

        foreach (var extra in actual.Except(expected, StringComparer.Ordinal))
        {
            yield return $"{FileName} 多出了源码里不存在的{label} `{extra}`。";
        }
    }
}
