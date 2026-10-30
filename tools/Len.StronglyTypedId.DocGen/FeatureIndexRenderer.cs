using System.Globalization;

namespace Len.StronglyTypedId.DocGen;

/// <summary>
/// 渲染「功能索引」表：一行一个功能点，并给出它的示例文件与文档锚点。
/// </summary>
/// <remarks>
/// 这张表存在的理由是<b>可校验</b>：功能名来自生成器登记表与集成枚举，示例列与文档列在渲染时
/// 逐条检查目标是否真实存在，任何一项缺失都会让 <c>--verify</c> 直接失败。
/// <para>
/// 它取代不了 <c>features.md</c>：那一页讲「这个功能是什么、为什么这样设计」，是散文；
/// 这一页只回答「这个功能在哪儿能看到、在哪儿读到」，是索引。两者刻意分开，
/// 因为散文无法自动校验，而索引可以。
/// </para>
/// </remarks>
internal static class FeatureIndexRenderer
{
    /// <summary>
    /// 渲染整张索引表。
    /// </summary>
    /// <param name="root">仓库根目录，用于检查示例文件与文档锚点是否存在。</param>
    /// <param name="culture">目标区域性。</param>
    /// <param name="samplesPrefix">
    /// 从索引页指向仓库根的相对前缀：英文页在 <c>docs/</c> 下用 <c>../</c>，
    /// 中文页在 <c>docs/zh-CN/</c> 下用 <c>../../</c>。文档链接不需要它——两种语言的文档页
    /// 都与索引页同目录。
    /// </param>
    /// <exception cref="InvalidOperationException">某个功能的示例文件或文档锚点不存在。</exception>
    public static string Render(string root, CultureInfo culture, string samplesPrefix)
    {
        var rows = new List<IReadOnlyList<string>>();
        var problems = new List<string>();

        foreach (var feature in Features.Entries)
        {
            var sample = ResolveSample(root, feature, samplesPrefix, problems);
            var document = ResolveDocument(root, feature, problems);

            rows.Add([feature.Name.For(culture), feature.Kind.For(culture), sample, document]);
        }

        // 一次报出全部缺失项，而不是遇到第一个就停：修一处跑一次 CI 太慢。
        return problems.Count > 0
            ? throw new InvalidOperationException(
                "功能索引表引用了不存在的示例文件或文档锚点：" + Environment.NewLine
                + string.Join(Environment.NewLine, problems.Select(static problem => "  " + problem)))
            : MarkdownTable.Render(
                [
                    Localize(culture, "Feature", "功能"),
                    Localize(culture, "Kind", "类型"),
                    Localize(culture, "Sample", "示例"),
                    Localize(culture, "Documentation", "文档"),
                ],
                rows);
    }

    /// <summary>
    /// 把示例列渲染成链接，并检查目标文件确实存在。
    /// </summary>
    private static string ResolveSample(string root, FeatureEntry feature, string samplesPrefix, List<string> problems)
    {
        if (feature.SamplePath is null)
        {
            return "—";
        }

        var fullPath = Path.Combine(root, feature.SamplePath.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(fullPath))
        {
            problems.Add($"「{feature.Name.En}」的示例文件不存在：{feature.SamplePath}");

            return "—";
        }

        // 链接文本用文件名（去掉 .cs），读者一眼能看出是哪个演示。
        var label = Path.GetFileNameWithoutExtension(feature.SamplePath);

        return $"[`{label}`]({samplesPrefix}{feature.SamplePath})";
    }

    /// <summary>
    /// 把文档列渲染成链接，并检查目标文件与锚点确实存在。
    /// </summary>
    private static string ResolveDocument(string root, FeatureEntry feature, List<string> problems)
    {
        var fullPath = Path.Combine(root, feature.DocumentPath.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(fullPath))
        {
            problems.Add($"「{feature.Name.En}」的文档不存在：{feature.DocumentPath}");

            return "—";
        }

        if (feature.DocumentAnchor is { } anchor)
        {
            var text = File.ReadAllText(fullPath);

            if (!text.Contains($"<a id=\"{anchor}\"></a>", StringComparison.Ordinal))
            {
                problems.Add($"「{feature.Name.En}」的文档锚点不存在：{feature.DocumentPath}#{anchor}");

                return "—";
            }
        }

        // 文档页与索引页同目录（docs/ 或 docs/zh-CN/），因此只取文件名即可。
        var fileName = Path.GetFileName(feature.DocumentPath);
        var link = feature.DocumentAnchor is { } target ? $"{fileName}#{target}" : fileName;

        return $"[{feature.DocumentLabel.En}]({link})";
    }

    private static string Localize(CultureInfo culture, string en, string zh)
        => culture.TwoLetterISOLanguageName == "zh" ? zh : en;
}
