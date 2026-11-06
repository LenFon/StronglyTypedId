namespace Len.StronglyTypedId.Generators;

/// <summary>
/// 集成生成器共用的命名规则：重名判定与「生成在 <c>StronglyTypedIds</c> 里的类型」的类名。
/// </summary>
/// <remarks>
/// EF Core / Dapper / MessagePack / MongoDB / HotChocolate / Newtonsoft 六个集成都要把每个 Id 的
/// 辅助类型放进同一个 <c>StronglyTypedIds</c> 类，因此都需要「哪些短名撞了」与「撞名时怎么消歧」。
/// 规则集中在这里，避免六份实现逐渐漂移。
/// </remarks>
internal static class IntegrationNaming
{
    /// <summary>
    /// 取在多个 Id 之间重名的短名集合。
    /// </summary>
    /// <remarks>
    /// 分组键用 <see cref="StronglyTypedIdInfo.Name"/> 而非 <see cref="StronglyTypedIdInfo.EscapedName"/>：
    /// 这里只比是否重名，名字不落进生成代码，两者又是 1:1 映射（仅保留关键字差一个 <c>@</c> 前缀）。
    /// </remarks>
    public static ImmutableHashSet<string> GetAmbiguousNames(IEnumerable<StronglyTypedIdInfo> stronglyTypedIds)
        => stronglyTypedIds
            .GroupBy(static idInfo => idInfo.Name, StringComparer.Ordinal)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToImmutableHashSet(StringComparer.Ordinal);

    /// <summary>
    /// 取生成在 <c>StronglyTypedIds</c> 里的辅助类型名。
    /// </summary>
    /// <param name="idInfo">当前 Id 的描述信息。</param>
    /// <param name="requiresNamespaceQualifier">该短名是否在本次编译里撞名。</param>
    /// <param name="suffix">类型名后缀，如 <c>Converter</c> / <c>TypeHandler</c>。</param>
    /// <param name="collidingNames">
    /// 本次编译里「最终类名彼此相同」的集合（见 <see cref="GetCollidingNames"/>）；
    /// <see langword="null"/> 表示调用方不需要处理这种塌缩。
    /// </param>
    /// <remarks>
    /// 不撞名时用 Id 的短名，产物与加入消歧规则之前逐字符一致；撞名时改用净化后的 hint 名
    /// （<c>Ns_Outer_Inner</c> 形），它已含命名空间与包含类型链，足以区分同名 Id。
    /// <para>
    /// 把 <c>.</c> 换成 <c>_</c> 本身会塌缩：<c>A.B.OrderId</c> 与 <c>A_B.OrderId</c> 都变成
    /// <c>A_B_OrderId</c>，两个 Id 于是生成同一个类名（CS0102）。因此这里额外接受
    /// <paramref name="collidingNames"/>：只有「最终类名与别的 Id 相同」时才追加原始全名的稳定哈希。
    /// 无冲突场景（绝大多数）的产物名保持不变。
    /// </para>
    /// <para>
    /// 撞名判定必须覆盖「短名」与「消歧名」两类：短名唯一的 Id 若恰好叫 <c>A_B_OrderId</c>，
    /// 它的短名产物 <c>A_B_OrderIdConverter</c> 会与 <c>A.B.OrderId</c> 的消歧名逐字符相同。
    /// 只比较消歧名之间是否相同会漏掉这一档，产物仍撞 CS0102。
    /// </para>
    /// </remarks>
    public static string GetTypeName(
        StronglyTypedIdInfo idInfo,
        bool requiresNamespaceQualifier,
        string suffix,
        ImmutableHashSet<string>? collidingNames = null)
    {
        var name = GetBaseName(idInfo, requiresNamespaceQualifier, suffix);

        return collidingNames is not null && collidingNames.Contains(Unescape(name))
            ? $"{name}_{IdentifierEscaping.StableHash(idInfo.FullName)}"
            : name;
    }

    /// <summary>
    /// 取未加哈希后缀的类名：撞名时用净化后的 hint 名，否则用短名。
    /// </summary>
    private static string GetBaseName(StronglyTypedIdInfo idInfo, bool requiresNamespaceQualifier, string suffix)
        => requiresNamespaceQualifier
            ? $"{idInfo.HintName.Replace('.', '_')}{suffix}"
            : $"{idInfo.EscapedName}{suffix}";

    /// <summary>
    /// 去掉标识符的 <c>@</c> 前缀，得到它在元数据里的真实名字。
    /// </summary>
    /// <remarks>
    /// 短名产物用 <see cref="StronglyTypedIdInfo.EscapedName"/>，关键字 Id 会带 <c>@</c>；
    /// 消歧名来自已净化的 hint 名，永远不带。两者比较前必须归一，否则 <c>@class</c> 与 <c>class</c>
    /// 会被当成两个不同的名字，漏掉真实撞名。
    /// </remarks>
    private static string Unescape(string name)
        => name.Length > 0 && name[0] == '@' ? name[1..] : name;

    /// <summary>
    /// 取生成在 <c>StronglyTypedIds</c> 里的辅助类型名，撞名判定由 <paramref name="ambiguousNames"/> 承担。
    /// </summary>
    /// <param name="idInfo">当前 Id 的描述信息。</param>
    /// <param name="ambiguousNames">本次编译里重名的短名集合（见 <see cref="GetAmbiguousNames"/>）。</param>
    /// <param name="suffix">类型名后缀，如 <c>Converter</c> / <c>TypeHandler</c>。</param>
    /// <param name="collidingNames">
    /// 本次编译里「最终类名彼此相同」的集合（见 <see cref="GetCollidingNames"/>）；
    /// <see langword="null"/> 表示调用方不需要处理这种塌缩。
    /// </param>
    public static string GetTypeName(
        StronglyTypedIdInfo idInfo,
        ImmutableHashSet<string> ambiguousNames,
        string suffix,
        ImmutableHashSet<string>? collidingNames = null)
        => GetTypeName(idInfo, ambiguousNames.Contains(idInfo.Name), suffix, collidingNames);

    /// <summary>
    /// 取「最终类名彼此相同」的集合：这类 Id 需要追加稳定哈希才能区分。
    /// </summary>
    /// <param name="stronglyTypedIds">参与本次生成的 Id。</param>
    /// <param name="ambiguousNames">本次编译里重名的短名集合。</param>
    /// <param name="suffix">类型名后缀，如 <c>Converter</c> / <c>TypeHandler</c>。</param>
    /// <remarks>
    /// 必须对全部 Id 计算，不能只看「需要消歧」的那批：把 <c>.</c> 换成 <c>_</c> 会让
    /// <c>A.B.OrderId</c> 与 <c>A_B.OrderId</c> 塌缩成同一个消歧名，而短名唯一的 <c>A_B_OrderId</c>
    /// 也会产出同一个短名类名。两类撞车都落在同一个 <c>StronglyTypedIds</c> 类里，都会 CS0102。
    /// </remarks>
    public static ImmutableHashSet<string> GetCollidingNames(
        IEnumerable<StronglyTypedIdInfo> stronglyTypedIds,
        ImmutableHashSet<string> ambiguousNames,
        string suffix)
        => stronglyTypedIds
            .GroupBy(idInfo => Unescape(GetBaseName(idInfo, ambiguousNames.Contains(idInfo.Name), suffix)), StringComparer.Ordinal)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToImmutableHashSet(StringComparer.Ordinal);

    /// <summary>
    /// 取 GraphQL 类型名：短名唯一时用短名，撞名时用净化后的 hint 名（<c>Ns_Outer_Inner</c> 形）。
    /// </summary>
    /// <remarks>
    /// GraphQL 的类型名有自己的一张全局表，与 C# 的命名空间无关：两个不同命名空间的 <c>OrderId</c>
    /// 即使 C# 类名已消歧，注册到 schema 时仍会撞成同一个名字，构建期报
    /// <c>An item with the same key has already been added. Key: OrderId</c>。
    /// <para>
    /// 消歧名取 <see cref="StronglyTypedIdInfo.HintName"/> 并把 <c>.</c> 换成 <c>_</c>：hint 名已按全名净化
    /// 且带稳定哈希，既唯一又满足 GraphQL 标识符的字符集（字母、数字、下划线）。
    /// </para>
    /// <para>
    /// <paramref name="collidingNames"/> 处理「短名唯一的 Id 恰好叫 <c>A_B_OrderId</c>」这一档：
    /// 它的短名与 <c>A.B.OrderId</c> 的消歧名逐字符相同，只比较消歧名之间是否相同会漏掉。
    /// </para>
    /// </remarks>
    public static string GetGraphQlTypeName(
        StronglyTypedIdInfo idInfo,
        ImmutableHashSet<string> ambiguousNames,
        ImmutableHashSet<string>? collidingNames = null)
    {
        var name = ambiguousNames.Contains(idInfo.Name)
            ? idInfo.HintName.Replace('.', '_')
            : idInfo.Name;

        return collidingNames is not null && collidingNames.Contains(name)
            ? $"{name}_{IdentifierEscaping.StableHash(idInfo.FullName)}"
            : name;
    }

    /// <summary>
    /// 取「最终 GraphQL 类型名彼此相同」的集合：这类 Id 需要追加稳定哈希才能区分。
    /// </summary>
    /// <param name="stronglyTypedIds">参与本次生成的 Id。</param>
    /// <param name="ambiguousNames">本次编译里重名的短名集合。</param>
    /// <remarks>
    /// 与 <see cref="GetCollidingNames"/> 同一类塌缩：短名唯一的 Id 若恰好叫 <c>A_B_OrderId</c>，
    /// 它的 GraphQL 名会与 <c>A.B.OrderId</c> 的消歧名逐字符相同。只比较消歧名之间是否相同会漏掉这一档。
    /// </remarks>
    public static ImmutableHashSet<string> GetCollidingGraphQlTypeNames(
        IEnumerable<StronglyTypedIdInfo> stronglyTypedIds,
        ImmutableHashSet<string> ambiguousNames)
        => stronglyTypedIds
            .GroupBy(idInfo => GetGraphQlTypeName(idInfo, ambiguousNames), StringComparer.Ordinal)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToImmutableHashSet(StringComparer.Ordinal);
}
