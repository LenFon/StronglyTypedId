using Microsoft.CodeAnalysis.Formatting;

namespace Len.StronglyTypedId;

/// <summary>
/// 编辑器内改动（代码修复 / 代码重构）落盘前的文本处理。
/// </summary>
/// <remarks>
/// 产物要写回使用者自己的文件，行尾必须沿用文档风格：写死 CRLF 会在 LF 检出里留下混排行尾。
/// 判据取「文档里是否出现过 <c>\r</c>」，而非 <see cref="System.Environment.NewLine"/>。
/// </remarks>
internal static class DocumentFormatting
{
    /// <summary>
    /// 取文档使用的行尾：出现过 <c>\r\n</c> 就用 CRLF，否则用 LF。
    /// </summary>
    public static string GetEndOfLine(SyntaxNode root)
        => root.GetText().ToString().Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

    /// <summary>
    /// 按文档的行尾风格格式化语法树。
    /// </summary>
    /// <remarks><see cref="Formatter"/> 新造换行时取 <see cref="FormattingOptions.NewLine"/>（缺省为 <see cref="System.Environment.NewLine"/>），故显式传入文档行尾。</remarks>
    public static SyntaxNode Format(Document document, SyntaxNode root, CancellationToken cancellationToken)
    {
        var workspace = document.Project.Solution.Workspace;
        var options = workspace.Options.WithChangedOption(
            FormattingOptions.NewLine, LanguageNames.CSharp, GetEndOfLine(root));

        return Formatter.Format(root, workspace, options, cancellationToken);
    }
}
