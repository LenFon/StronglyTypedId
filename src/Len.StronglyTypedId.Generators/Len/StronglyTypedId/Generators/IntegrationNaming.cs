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
    /// <remarks>
    /// 不撞名时用 Id 的短名，产物与加入消歧规则之前逐字符一致；撞名时改用净化后的 hint 名
    /// （<c>Ns_Outer_Inner</c> 形），它已含命名空间与包含类型链，足以区分同名 Id。
    /// </remarks>
    public static string GetTypeName(StronglyTypedIdInfo idInfo, bool requiresNamespaceQualifier, string suffix)
        => requiresNamespaceQualifier
            ? $"{idInfo.HintName.Replace('.', '_')}{suffix}"
            : $"{idInfo.EscapedName}{suffix}";

    /// <summary>
    /// 取生成在 <c>StronglyTypedIds</c> 里的辅助类型名，撞名判定由 <paramref name="ambiguousNames"/> 承担。
    /// </summary>
    /// <param name="idInfo">当前 Id 的描述信息。</param>
    /// <param name="ambiguousNames">本次编译里重名的短名集合（见 <see cref="GetAmbiguousNames"/>）。</param>
    /// <param name="suffix">类型名后缀，如 <c>Converter</c> / <c>TypeHandler</c>。</param>
    public static string GetTypeName(StronglyTypedIdInfo idInfo, ImmutableHashSet<string> ambiguousNames, string suffix)
        => GetTypeName(idInfo, ambiguousNames.Contains(idInfo.Name), suffix);
}
