namespace Len.StronglyTypedId;

/// <summary>
/// 汇集需要生成代码的强类型 Id：既有本次编译声明的，也有已引用程序集中已实现接口的。
/// </summary>
internal static class StronglyTypedIdDiscovery
{
    /// <summary>
    /// 合并「引用程序集中已实现 <c>IStronglyTypedId</c> 的类型」与「本次编译声明的强类型 Id」并去重。
    /// </summary>
    /// <remarks>
    /// 这里只负责合并去重。排除声明在泛型包含类型内部的 Id 是集成生成器的需求，由
    /// <see cref="DiscoveredIds"/> 在合并结果上再筛一次，核心生成器需要包含它们。
    /// </remarks>
    public static IEnumerable<StronglyTypedIdInfo> Discover(
        ImmutableArray<StronglyTypedIdInfo> declaredInfos,
        ImmutableArray<ModuleInfo> modules)
        => modules
            .SelectMany(module => module.GetTypes())
            .Where(type => type.Interfaces.Any(StronglyTypedIdInfo.IsStronglyTypedIdInterface))
            // 外部 Id 的集成开关取自它自己声明程序集的默认值：库作者写了 Integrations = None，
            // 消费者就不该再为它生成集成代码。逐类型特性由构造函数自行读取。
            .Select(type => new StronglyTypedIdInfo(
                type,
                defaultIntegrations: StronglyTypedIdInfo.GetAssemblyDefaultIntegrations(type.ContainingAssembly)))
            .Union(declaredInfos)
            .Distinct();
}
