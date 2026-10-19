using System.Text;

namespace Len.StronglyTypedId.DocGen;

/// <summary>
/// 把二维数据渲染成与仓库现有风格一致的 Markdown 表格（列宽对齐、左对齐）。
/// </summary>
/// <remarks>
/// 对齐宽度按<b>显示宽度</b>而非字符数计算：中日韩字符在等宽字体下占两格，按字符数补齐会让
/// 中文表格的竖线错位。Markdown 渲染器本身不在意这点，但对齐后的纯文本 diff 更容易读。
/// </remarks>
internal static class MarkdownTable
{
    /// <summary>
    /// 渲染表格，行间以 <c>\n</c> 分隔且末尾不带换行 —— 换行符由调用方按目标文件统一转换。
    /// </summary>
    public static string Render(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var widths = new int[headers.Count];

        for (var index = 0; index < headers.Count; index++)
        {
            widths[index] = DisplayWidth(headers[index]);
        }

        foreach (var row in rows)
        {
            for (var index = 0; index < headers.Count; index++)
            {
                widths[index] = Math.Max(widths[index], DisplayWidth(row[index]));
            }
        }

        var builder = new StringBuilder();

        AppendRow(builder, headers, widths);

        builder.Append('|');

        foreach (var width in widths)
        {
            builder.Append(' ').Append('-', width).Append(' ').Append('|');
        }

        builder.Append('\n');

        foreach (var row in rows)
        {
            AppendRow(builder, row, widths);
        }

        return builder.ToString().TrimEnd('\n');
    }

    private static void AppendRow(StringBuilder builder, IReadOnlyList<string> cells, int[] widths)
    {
        builder.Append('|');

        for (var index = 0; index < widths.Length; index++)
        {
            // 尾部空格单独补一格：单元格内容恰好占满列宽时，padding 为 0，
            // 少了这一格就会拼成 `|` 紧贴内容（`| `DateTimeOffset`|`），表格看起来像坏了。
            builder.Append(' ')
                .Append(cells[index])
                .Append(' ', widths[index] - DisplayWidth(cells[index]) + 1)
                .Append('|');
        }

        builder.Append('\n');
    }

    private static int DisplayWidth(string text)
    {
        var width = 0;

        foreach (var character in text)
        {
            width += IsWide(character) ? 2 : 1;
        }

        return width;
    }

    /// <summary>
    /// 判断字符是否占据两个等宽字符位（CJK 统一表意文字、假名、谚文、全角标点等）。
    /// </summary>
    /// <remarks>
    /// 采用 <c>wcwidth</c> 的经典近似区间。刻意<b>不</b>把东亚「模糊宽度」字符（如 <c>→</c> U+2192）
    /// 计入宽字符：它们在多数西文终端与编辑器里按一格显示，算成两格反而会错位。
    /// </remarks>
    private static bool IsWide(char character)
        => character is >= '\u1100' and <= '\u115F'
            or >= '\u2E80' and <= '\u303E'
            or >= '\u3041' and <= '\u33FF'
            or >= '\u3400' and <= '\u4DBF'
            or >= '\u4E00' and <= '\u9FFF'
            or >= '\uA000' and <= '\uA4CF'
            or >= '\uAC00' and <= '\uD7A3'
            or >= '\uF900' and <= '\uFAFF'
            or >= '\uFE30' and <= '\uFE6F'
            or >= '\uFF00' and <= '\uFF60'
            or >= '\uFFE0' and <= '\uFFE6';
}
