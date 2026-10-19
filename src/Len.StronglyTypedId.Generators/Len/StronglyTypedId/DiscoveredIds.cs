namespace Len.StronglyTypedId;

/// <summary>
/// 一次遍历算出的强类型 Id 发现结果，同时给出「含嵌套」与「排除嵌套」两份视图。
/// </summary>
/// <remarks>
/// 枚举全部引用程序集的类型是发现阶段的主要开销，此前 5 个集成生成器各调一次。两份视图一次算好，
/// 且必须出自同一次枚举 —— 分别调用会得到实例不同的结果，让下游按引用比较的增量缓存失效。
/// </remarks>
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
    /// <remarks>核心生成器能把声明逐层嵌回包含类型，泛型容器同样能重开，故不排除。</remarks>
    public ImmutableArray<StronglyTypedIdInfo> All { get; }

    /// <summary>
    /// 排除声明在泛型包含类型内部的子集，供集成生成器使用。
    /// </summary>
    /// <remarks>集成生成器要从外部按全名引用 Id，而泛型包含类型无法这样引用（开放类型不能作类型实参）。</remarks>
    public ImmutableArray<StronglyTypedIdInfo> External { get; }

    /// <summary>
    /// 合并「引用程序集中已实现 <c>IStronglyTypedId</c> 的类型」与「本次编译声明的强类型 Id」并去重，
    /// 一次算出两份视图。
    /// </summary>
    /// <param name="declaredInfos">本次编译中声明的强类型 Id 集合。</param>
    /// <param name="modules">当前编译引用的全部模块。</param>
    /// <remarks>去重不能省：同一个 Id 可能既在引用程序集里又在本次编译里，不去重会让下游重复 <c>AddSource</c>。</remarks>
    public static DiscoveredIds Create(
        ImmutableArray<StronglyTypedIdInfo> declaredInfos,
        ImmutableArray<ModuleInfo> modules)
    {
        var all = StronglyTypedIdDiscovery.Discover(declaredInfos, modules).ToImmutableArray();

        return new DiscoveredIds(all, [.. all.Where(static idInfo => !idInfo.IsInGenericContainer)]);
    }
}
