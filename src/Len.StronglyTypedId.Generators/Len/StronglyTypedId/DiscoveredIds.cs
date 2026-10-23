namespace Len.StronglyTypedId;

/// <summary>
/// 一次遍历算出的强类型 Id 发现结果，供集成生成器取用。
/// </summary>
/// <remarks>
/// 核心生成器不走这里：它按 <c>[StronglyTypedId]</c> 逐个触发，拿到的是单个 <see cref="StronglyTypedIdInfo"/>，
/// 因此不需要「含嵌套」的视图。
/// </remarks>
internal readonly record struct DiscoveredIds
{
    private DiscoveredIds(ImmutableArray<StronglyTypedIdInfo> external)
    {
        External = external;
    }

    /// <summary>
    /// 排除声明在泛型包含类型内部的子集，供集成生成器使用。
    /// </summary>
    public ImmutableArray<StronglyTypedIdInfo> External { get; }

    /// <summary>
    /// 在 <see cref="External"/> 的基础上再按 <c>Integrations</c> 过滤，只留下选中了该集成的 Id。
    /// </summary>
    public ImmutableArray<StronglyTypedIdInfo> ExternalFor(int integration)
        => [.. External.Where(idInfo => idInfo.Supports(integration))];

    /// <summary>
    /// 合并「引用程序集中已实现 <c>IStronglyTypedId</c> 的类型」与「本次编译声明的强类型 Id」并去重，再排除嵌套在泛型容器里的。
    /// </summary>
    public static DiscoveredIds Create(
        ImmutableArray<StronglyTypedIdInfo> declaredInfos,
        ImmutableArray<ModuleInfo> modules)
        => new([.. StronglyTypedIdDiscovery.Discover(declaredInfos, modules)
            .Where(static idInfo => !idInfo.IsInGenericContainer)]);
}
