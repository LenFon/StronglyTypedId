using Microsoft.CodeAnalysis.Formatting;

namespace Len.StronglyTypedId;

/// <summary>
/// 编辑器内改动（代码修复 / 代码重构）落盘前的文本处理。
/// </summary>
/// <remarks>
/// 与分析器、生成器不同，这两类改动的产物要写回使用者自己的文件，因此行尾必须沿用被编辑文档的风格：
/// 写死 CRLF 会在 LF 检出（Linux / macOS，或 <c>core.autocrlf=false</c>）的文件里留下混排行尾，
/// 反之亦然。判据取「文档里是否出现过 <c>\r</c>」，而不是 <see cref="System.Environment.NewLine"/> ——
/// 后者是运行编辑器的机器属性，与文档自身的行尾并无必然关系（同 <see cref="GeneratedCode.NormalizeLineEndings"/> 的取向）。
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
    /// <remarks>
    /// <see cref="Formatter"/> 只在「自己新造换行」时取 <see cref="FormattingOptions.NewLine"/>，
    /// 其缺省值是 <see cref="System.Environment.NewLine"/>；显式传入文档的行尾，新造的换行才会与文档一致。
    /// </remarks>
    public static SyntaxNode Format(Document document, SyntaxNode root, CancellationToken cancellationToken)
    {
        var workspace = document.Project.Solution.Workspace;
        var options = workspace.Options.WithChangedOption(
            FormattingOptions.NewLine, LanguageNames.CSharp, GetEndOfLine(root));

        return Formatter.Format(root, workspace, options, cancellationToken);
    }
}
