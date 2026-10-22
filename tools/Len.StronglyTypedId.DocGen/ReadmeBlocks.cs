namespace Len.StronglyTypedId.DocGen;

/// <summary>
/// README 里由生成器维护的区块：<c>&lt;!-- BEGIN GENERATED:id --&gt;</c> 与
/// <c>&lt;!-- END GENERATED:id --&gt;</c> 之间的内容整体替换。
/// </summary>
/// <remarks>
/// 用 HTML 注释作标记：GitHub 渲染时不可见，也不会被 Markdown 当作正文；标记缺失时<b>直接报错</b>而不是静默跳过。
/// </remarks>
internal static class ReadmeBlocks
{
    /// <summary>
    /// 探测文件使用的换行符：出现过 <c>\r\n</c> 即视为 CRLF，否则 LF。
    /// </summary>
    /// <remarks>
    /// 不按平台猜：仓库的 <c>.gitattributes</c> 是 <c>* text=auto</c>，检出时行尾取决于各人的 <c>core.autocrlf</c>，按文件自身现状写回才不会制造整文件的行尾噪音。
    /// </remarks>
    public static string DetectNewLine(string text)
        => text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

    /// <summary>
    /// 用 <paramref name="body"/> 替换指定区块的内容（保留两侧标记行本身）。
    /// </summary>
    public static string Replace(string text, string blockId, string body, string newLine)
    {
        var begin = $"<!-- BEGIN GENERATED:{blockId} -->";
        var end = $"<!-- END GENERATED:{blockId} -->";

        var beginIndex = IndexOfOrThrow(text, begin, 0, $"找不到起始标记「{begin}」。");
        var endIndex = IndexOfOrThrow(
            text,
            end,
            beginIndex + begin.Length,
            $"找不到结束标记「{end}」（起始标记位于偏移 {beginIndex}）。");

        return text[..(beginIndex + begin.Length)]
            + newLine
            + body.Replace("\n", newLine, StringComparison.Ordinal)
            + newLine
            + text[endIndex..];
    }

    private static int IndexOfOrThrow(string text, string marker, int startIndex, string message)
    {
        var index = text.IndexOf(marker, startIndex, StringComparison.Ordinal);

        return index >= 0 ? index : throw new InvalidOperationException(message);
    }
}
