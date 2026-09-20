namespace Len.StronglyTypedId;

/// <summary>
/// 生成代码时的文本工具。
/// </summary>
internal static class GeneratedCode
{
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
}
