namespace Len.StronglyTypedId;

/// <summary>
/// 把符号名转成可安全写进生成源码的文本：标识符转义与 hint 名净化。
/// </summary>
/// <remarks>
/// 这两件事都只依赖名字本身、与具体 Id 无关，故从 <see cref="StronglyTypedIdInfo"/> 里独立出来，
/// 让「读符号 → 算属性」与「名字 → 合法源码文本」两个变化原因分开。
/// </remarks>
internal static class IdentifierEscaping
{
    /// <summary>
    /// 把标识符转成可安全写入 C# 源码的形式：C# 关键字（保留与上下文关键字）加 <c>@</c> 前缀，其余原样返回。
    /// </summary>
    /// <remarks>
    /// 判据走 Roslyn 的关键字表，避免自维护清单遗漏。上下文关键字也转义：产物里有 <c>out var id</c> 这类局部变量，
    /// Id 若叫 <c>var</c> 会被当成隐式类型声明（CS1503 / CS8508）。
    /// <para>
    /// <see cref="IsKeywordMissingFromRoslynTables"/> 补的是规范列为上下文关键字、但 Roslyn 两张表都不收录的词，
    /// 其中 <c>value</c> 有实证危害：STJ 读取路径的局部变量 <c>value</c> 会遮蔽同名类型（CS1061）。
    /// </para>
    /// </remarks>
    public static string Escape(string name)
        => SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None
            || SyntaxFacts.GetContextualKeywordKind(name) != SyntaxKind.None
            || IsKeywordMissingFromRoslynTables(name)
                ? "@" + name
                : name;

    /// <summary>
    /// 把类型显示名净化成合法的 hint 名：只保留 Unicode 字母、数字与 <c>_</c> <c>.</c>，其余一律剔除。
    /// </summary>
    /// <remarks>
    /// 用白名单而非黑名单：显示名可能带尖括号、逗号、空格、<c>@</c>（关键字转义）甚至 <c>[]</c>，
    /// 而 <c>AddSource</c> 遇到 <c>@</c> 会抛 <see cref="ArgumentException"/>，表现为 STIA0012 且该类型全部输出被丢弃。
    /// <para>
    /// 剔除会让不同全名塌陷到同一 hint 名，撞名同样降级成 STIA0012，因此只要剔掉了字符就追加原始全名的稳定哈希。
    /// </para>
    /// </remarks>
    public static string SanitizeHintName(string fullName)
    {
        var sanitized = string.Concat(fullName.Where(static c => char.IsLetterOrDigit(c) || c is '_' or '.'));

        // 净化只会删字符，长度不变即代表一个字符都没动，此时保持 hint 名原样。
        return sanitized.Length == fullName.Length
            ? sanitized
            : $"{sanitized}_{StableHash(fullName)}";
    }

    /// <summary>
    /// C# 规范列为上下文关键字、但 Roslyn 5.9 的 <see cref="SyntaxFacts"/> 两张关键字表均不收录的词。
    /// </summary>
    /// <remarks>
    /// 清单来自规范的 contextual_keyword 规则（词法结构 §6.4.5），逐词对照 Roslyn 两张表实测得出。
    /// 将来 Roslyn 若收录其中某个词，这里只是变得冗余。
    /// </remarks>
    private static bool IsKeywordMissingFromRoslynTables(string name)
        => name is "Cdecl" or "Stdcall" or "Thiscall"  // 函数指针调用约定
            or "dynamic" or "nint" or "nuint"          // 预定义类型一类的伪关键字
            or "value" or "notnull";                   // 访问器 / 泛型约束位置的上下文化标识符

    /// <summary>
    /// 构造可安全写进生成源码的完全限定名，形如 <c>global::Ns.Outer&lt;T&gt;.Inner</c>。
    /// </summary>
    /// <remarks>
    /// 不能直接用 <c>type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)</c>：Roslyn 4.8 只转义
    /// 命名空间段、不转义类型名本身，关键字命名的 Id（<c>@return</c>）会渲染成 <c>global::@int.return</c>，
    /// 拼进产物即 CS1001；5.9 才修成 <c>global::@int.@return</c>。生成器要同时支持两个版本，
    /// 故这里自己拼，输出与 Roslyn 版本无关。非关键字名的结果与 <c>FullyQualifiedFormat</c> 逐字节相同。
    /// </remarks>
    public static string FullyQualifiedName(ITypeSymbol type)
    {
        var parts = new List<string>();

        for (var current = type as INamedTypeSymbol; current is not null; current = current.ContainingType)
        {
            var typeParameterList = current.TypeParameters.Length > 0
                ? "<" + string.Join(", ", current.TypeParameters.Select(static parameter => Escape(parameter.Name))) + ">"
                : string.Empty;

            parts.Add(Escape(current.Name) + typeParameterList);
        }

        parts.Reverse();

        var containingNamespace = type.ContainingNamespace;
        var namespaceText = containingNamespace is null || containingNamespace.IsGlobalNamespace
            ? string.Empty
            : containingNamespace.ToDisplayString() + ".";

        return "global::" + namespaceText + string.Join(".", parts);
    }

    /// <summary>
    /// 计算与进程无关的 32 位 FNV-1a 哈希，用作 hint 名与消歧类名的后缀。
    /// </summary>
    /// <remarks>
    /// 不能用 <see cref="string.GetHashCode()"/>：它按进程随机化，会让同一份源码产出不同的 hint 名。
    /// </remarks>
    internal static string StableHash(string value)
    {
        const uint OffsetBasis = 2166136261;
        const uint Prime = 16777619;

        var hash = OffsetBasis;

        foreach (var character in value)
        {
            hash ^= character;
            hash *= Prime;
        }

        return hash.ToString("x8", System.Globalization.CultureInfo.InvariantCulture);
    }
}
