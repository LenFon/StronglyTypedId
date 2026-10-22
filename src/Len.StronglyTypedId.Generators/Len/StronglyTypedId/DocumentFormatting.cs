using Microsoft.CodeAnalysis.Formatting;

namespace Len.StronglyTypedId;

/// <summary>
/// 编辑器内改动（代码修复 / 代码重构）落盘前的文本处理。
/// </summary>
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
    public static SyntaxNode Format(Document document, SyntaxNode root, CancellationToken cancellationToken)
    {
        var workspace = document.Project.Solution.Workspace;
        var options = workspace.Options.WithChangedOption(
            FormattingOptions.NewLine, LanguageNames.CSharp, GetEndOfLine(root));

        return Formatter.Format(root, workspace, options, cancellationToken);
    }
}
