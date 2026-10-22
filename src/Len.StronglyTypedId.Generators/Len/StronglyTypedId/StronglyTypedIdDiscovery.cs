namespace Len.StronglyTypedId;

/// <summary>
/// 汇集需要生成代码的强类型 Id：既有本次编译声明的，也有已引用程序集中已实现接口的。
/// </summary>
internal static class StronglyTypedIdDiscovery
{
    /// <summary>
    /// 合并「引用程序集中已实现 <c>IStronglyTypedId</c> 的类型」与「本次编译声明的强类型 Id」并去重。
    /// </summary>
    public static IEnumerable<StronglyTypedIdInfo> Discover(
        ImmutableArray<StronglyTypedIdInfo> declaredInfos,
        ImmutableArray<ModuleInfo> modules,
        bool includeNested = true)
    {
        var all = modules
            .SelectMany(module => module.GetTypes())
            .Where(type => type.Interfaces.Any(StronglyTypedIdInfo.IsStronglyTypedIdInterface))
            // 外部 Id 的集成开关取自它自己声明程序集的默认值：库作者写了 Integrations = None，
            // 消费者就不该再为它生成集成代码。逐类型特性由构造函数自行读取。
            .Select(type => new StronglyTypedIdInfo(
                type,
                defaultIntegrations: StronglyTypedIdInfo.GetAssemblyDefaultIntegrations(type.ContainingAssembly)))
            .Union(declaredInfos)
            .Distinct();

        return includeNested ? all : all.Where(static idInfo => !idInfo.IsInGenericContainer);
    }
}
