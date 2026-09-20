namespace Len.StronglyTypedId;

/// <summary>
/// 生成代码时的文本工具。
/// </summary>
internal static class GeneratedCode
{
    /// <summary>
    /// 每个嵌套层级的缩进空格数。
    /// </summary>
    private const int IndentSize = 4;

    /// <summary>
    /// 把 <paramref name="code"/> 的换行符统一为模板自身使用的风格。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 生成模板是原始字符串字面量，其行尾即源文件的行尾 —— 随 git 的 <c>text=auto</c> 在 CRLF（Windows 工作区）
    /// 与 LF（Linux / macOS 检出）之间变化；而按条件拼出的片段一律以 <c>\n</c> 书写。两者直接相接会让同一个
    /// 生成文件出现混排行尾：既破坏确定性构建，也让快照断言隐含依赖平台。
    /// </para>
    /// <para>
    /// 判据取「模板里是否出现过 <c>\r</c>」，而不是 <see cref="System.Environment.NewLine"/>：
    /// 后者是运行生成器的机器属性，与模板所在文件的行尾并无必然关系。
    /// </para>
    /// </remarks>
    public static string NormalizeLineEndings(string code)
        => code.IndexOf('\r') >= 0
            ? code.Replace("\r\n", "\n").Replace("\n", "\r\n")
            : code.Replace("\r\n", "\n");

    /// <summary>
    /// 把在命名空间层级书写的强类型 Id 声明块嵌进各层包含类型里，并按嵌套层数整体缩进。
    /// </summary>
    /// <param name="idInfo">强类型 Id 描述，提供包含类型链。</param>
    /// <param name="declarationBlock">
    /// 自 <c>partial record …</c> 起、到该类型自身的收尾大括号为止的声明块，书写在 0 缩进处；
    /// 附在 Id 上的特性（如 <c>[JsonConverter]</c>）属于签名的一部分，一并含在其中。
    /// </param>
    /// <remarks>
    /// <para>
    /// partial 类型的各段必须处在同一容器内，所以嵌套类型的强类型 Id 无法像顶层类型那样在命名空间层级
    /// 补成员，只能把声明逐层放回原本的包含类型中。顶层类型（<see cref="StronglyTypedIdInfo.NestingDepth"/>
    /// 为 0）原样返回，产物与不支持嵌套时的逐字节一致 —— 既有快照即回归基线。
    /// </para>
    /// <para>
    /// 行尾一律用 <c>\n</c> 拼接，由调用方在最后统一交给 <see cref="NormalizeLineEndings"/>；
    /// 缩进逐行显式写出，不依赖原始字符串字面量的裁剪规则。空行保持为空，不补缩进，
    /// 以免在产物里留下行尾空白。
    /// </para>
    /// </remarks>
    public static string NestInContainingTypes(StronglyTypedIdInfo idInfo, string declarationBlock)
    {
        if (idInfo.NestingDepth == 0)
        {
            return declarationBlock;
        }

        var declarations = idInfo.ContainingTypeDeclarations.Split('\n');
        var bodyIndent = new string(' ', declarations.Length * IndentSize);
        var lines = new List<string>();

        for (var depth = 0; depth < declarations.Length; depth++)
        {
            var indent = new string(' ', depth * IndentSize);

            lines.Add(indent + declarations[depth]);
            lines.Add(indent + "{");
        }

        foreach (var line in declarationBlock.Split('\n'))
        {
            lines.Add(line.Length == 0 ? line : bodyIndent + line);
        }

        for (var depth = declarations.Length - 1; depth >= 0; depth--)
        {
            lines.Add(new string(' ', depth * IndentSize) + "}");
        }

        return string.Join("\n", lines);
    }
}
