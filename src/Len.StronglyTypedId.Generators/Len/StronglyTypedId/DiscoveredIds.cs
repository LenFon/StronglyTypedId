namespace Len.StronglyTypedId;

/// <summary>
/// 一次遍历算出的强类型 Id 发现结果，同时给出「含嵌套」与「排除嵌套」两份视图。
/// </summary>
internal readonly record struct DiscoveredIds
{
    private DiscoveredIds(ImmutableArray<StronglyTypedIdInfo> all, ImmutableArray<StronglyTypedIdInfo> external)
    {
        All = all;
        External = external;
    }

    /// <summary>
    /// 含声明在泛型包含类型内部的全部强类型 Id，供核心生成器使用。
    /// </summary>
    public ImmutableArray<StronglyTypedIdInfo> All { get; }

    /// <summary>
    /// 排除声明在泛型包含类型内部的子集，供集成生成器使用。
    /// </summary>
    public ImmutableArray<StronglyTypedIdInfo> External { get; }

    /// <summary>
    /// 在 <see cref="External"/> 的基础上再按 <c>Integrations</c> 过滤，只留下选中了该集成的 Id。
    /// </summary>
    /// <param name="integration">单个集成标志，见 <see cref="IntegrationFlags"/>。</param>
    public ImmutableArray<StronglyTypedIdInfo> ExternalFor(int integration)
        => [.. External.Where(idInfo => idInfo.Supports(integration))];

    /// <summary>
    /// 合并「引用程序集中已实现 <c>IStronglyTypedId</c> 的类型」与「本次编译声明的强类型 Id」并去重，一次算出两份视图。
    /// </summary>
    /// <param name="declaredInfos">本次编译中声明的强类型 Id 集合。</param>
    /// <param name="modules">当前编译引用的全部模块。</param>
    public static DiscoveredIds Create(
        ImmutableArray<StronglyTypedIdInfo> declaredInfos,
        ImmutableArray<ModuleInfo> modules)
    {
        var all = StronglyTypedIdDiscovery.Discover(declaredInfos, modules).ToImmutableArray();

        return new DiscoveredIds(all, [.. all.Where(static idInfo => !idInfo.IsInGenericContainer)]);
    }
}
