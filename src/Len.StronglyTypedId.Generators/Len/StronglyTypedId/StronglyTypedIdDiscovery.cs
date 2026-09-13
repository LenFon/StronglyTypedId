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
        ImmutableArray<ModuleInfo> modules)
        => modules
            .SelectMany(module => module.GetTypes())
            .Where(type => type.Interfaces.Any(@interface => @interface.Name == StronglyTypedIdInfo.InterfaceName))
            .Select(type => new StronglyTypedIdInfo(type))
            .Union(declaredInfos)
            .Distinct();
}
