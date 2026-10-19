using System.Globalization;
using Len.StronglyTypedId.Generators;

namespace Len.StronglyTypedId.DocGen;

/// <summary>
/// 从生成器内部的判定数据渲染 README 的三张表。
/// </summary>
/// <remarks>
/// <para>
/// 每张表的事实来源都在生成器工程里，且都是「判定逻辑本身」而非另抄一份的文档副本：
/// </para>
/// <list type="bullet">
///   <item>基元表 ← <see cref="SupportedPrimitiveTypes.DocumentedTypes"/>（<c>IsSupported</c> 的匹配集合由它派生）；</item>
///   <item>集成触发表 ← <see cref="StronglyTypedIdGenerator.DocumentedGenerators"/>（模块闸门登记表）；</item>
///   <item>诊断表 ← <see cref="Descriptors"/>（规则 ID / 严重级别 / 标题）+ <see cref="DiagnosticDocumentation"/>（代码修复文案）。</item>
/// </list>
/// </remarks>
internal static class DocumentationRenderer
{
    /// <summary>渲染「Supported primitive types」表。</summary>
    public static string RenderPrimitiveTypes(CultureInfo culture)
    {
        var rows = new List<IReadOnlyList<string>>();

        foreach (var type in SupportedPrimitiveTypes.DocumentedTypes)
        {
            rows.Add([$"`{type.CSharpName}`", $"`{type.ExampleDeclaration}`"]);
        }

        // 枚举不在登记表里（它按 TypeKind 无条件支持，与类型名无关），单独补一行。
        rows.Add([Localize(culture, "any `enum`", "任意 `enum`"), "`record StatusId(Status Value)`"]);

        return MarkdownTable.Render(
            [Localize(culture, "C# type", "C# 类型"), Localize(culture, "Example declaration", "声明示例")],
            rows);
    }

    /// <summary>渲染「What gets generated」表。</summary>
    public static string RenderGeneratedCode(CultureInfo culture)
    {
        var rows = new List<IReadOnlyList<string>>();

        foreach (var registration in StronglyTypedIdGenerator.DocumentedGenerators)
        {
            rows.Add([registration.Condition.For(culture), registration.Documentation.For(culture)]);
        }

        return MarkdownTable.Render(
            [Localize(culture, "Condition in the compilation", "编译中的条件"), Localize(culture, "Generated code", "生成的代码")],
            rows);
    }

    /// <summary>渲染「Diagnostics」表。</summary>
    public static string RenderDiagnostics(CultureInfo culture)
    {
        var fixes = DiagnosticDocumentation.Entries.ToDictionary(static entry => entry.RuleId, StringComparer.Ordinal);
        var rows = new List<IReadOnlyList<string>>();

        // 按规则 ID 排序：描述符数组是按语义分组排的（分析器规则一组、生成器规则一组），
        // 直接照搬会让 STIA0012 夹在中间，读者按 ID 查找时反而费劲。
        var descriptors = Descriptors.All
            .Concat(Descriptors.GeneratorDiagnostics)
            .OrderBy(static descriptor => descriptor.Id, StringComparer.Ordinal);

        foreach (var descriptor in descriptors)
        {
            var fix = fixes.TryGetValue(descriptor.Id, out var entry) && !entry.Fix.IsEmpty
                ? entry.Fix.For(culture)
                : "—";

            rows.Add([descriptor.Id, descriptor.Title.ToString(culture), fix]);
        }

        return MarkdownTable.Render(
            [
                Localize(culture, "Rule ID", "规则 ID"),
                Localize(culture, "Description", "说明"),
                Localize(culture, "Code fix", "代码修复"),
            ],
            rows);
    }

    private static string Localize(CultureInfo culture, string en, string zh)
        => culture.TwoLetterISOLanguageName == "zh" ? zh : en;
}
