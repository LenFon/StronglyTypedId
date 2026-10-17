namespace Len.StronglyTypedId;

/// <summary>
/// 一次遍历算出的强类型 Id 发现结果，同时给出「含嵌套」与「排除嵌套」两份视图。
/// </summary>
/// <remarks>
/// <para>
/// 枚举全部引用程序集的所有类型是发现阶段的主要开销，而此前 5 个集成生成器各调一次发现，一次生成要
/// 枚举 5 遍。把两份视图一次算好后交给各生成器取用，枚举只发生一次。
/// </para>
/// <para>
/// 两份视图必须出自同一次枚举：分别调用会得到内容相同但实例不同的结果，既白白多做一遍枚举，
/// 也让下游按引用比较的增量缓存失去意义。
/// </para>
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
    /// <remarks>
    /// 核心生成器把声明逐层嵌回原本的包含类型，泛型容器同样能重开，故不排除。
    /// </remarks>
    public ImmutableArray<StronglyTypedIdInfo> All { get; }

    /// <summary>
    /// 排除声明在泛型包含类型内部的子集，供集成生成器使用。
    /// </summary>
    /// <remarks>
    /// 集成生成器需要从外部按全名引用 Id 类型，而泛型包含类型无法被这样引用
    /// （开放类型不能用作类型实参），故把这些 Id 排除在外。
    /// </remarks>
    public ImmutableArray<StronglyTypedIdInfo> External { get; }

    /// <summary>
    /// 合并「引用程序集中已实现 <c>IStronglyTypedId</c> 的类型」与「本次编译声明的强类型 Id」并去重，
    /// 一次算出两份视图。
    /// </summary>
    /// <param name="declaredInfos">本次编译中声明的强类型 Id 集合。</param>
    /// <param name="modules">当前编译引用的全部模块。</param>
    /// <remarks>
    /// 去重不能省：同一个强类型 Id 可能既出现在引用程序集的产物里、又出现在本次编译的声明里，
    /// 不去重会让下游为同一个类型产出重复的 <c>AddSource</c>。合并逻辑本身由
    /// <see cref="StronglyTypedIdDiscovery.Discover"/> 承担，这里只负责把结果落成两份视图。
    /// </remarks>
    public static DiscoveredIds Create(
        ImmutableArray<StronglyTypedIdInfo> declaredInfos,
        ImmutableArray<ModuleInfo> modules)
    {
        var all = StronglyTypedIdDiscovery.Discover(declaredInfos, modules).ToImmutableArray();

        return new DiscoveredIds(all, [.. all.Where(static idInfo => !idInfo.IsInGenericContainer)]);
    }
}
