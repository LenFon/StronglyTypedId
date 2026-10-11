namespace Len.StronglyTypedId;

/// <summary>
/// 汇集需要生成代码的强类型 Id：既有本次编译声明的，也有已引用程序集中已实现接口的。
/// </summary>
internal static class StronglyTypedIdDiscovery
{
    /// <summary>
    /// 合并「引用程序集中已实现 <c>IStronglyTypedId</c> 的类型」与「本次编译声明的强类型 Id」并去重。
    /// </summary>
    /// <param name="declaredInfos">本次编译中声明的强类型 Id 集合。</param>
    /// <param name="modules">当前编译引用的且已启用相关生成器的全部模块。</param>
    /// <param name="includeNested">
    /// 是否包含声明在泛型包含类型内部的强类型 Id。集成生成器（EF Core / Swagger / Dapper / 内置 OpenAPI）
    /// 需要以外部引用 Id 类型，而泛型包含类型无法从外部按全名引用（开放类型不能用作类型实参），故传
    /// <see langword="false"/> 将其排除；核心生成器仍可逐层嵌回包含类型，默认 <see langword="true"/> 不排除。
    /// </param>
    public static IEnumerable<StronglyTypedIdInfo> Discover(
        ImmutableArray<StronglyTypedIdInfo> declaredInfos,
        ImmutableArray<ModuleInfo> modules,
        bool includeNested = true)
    {
        var all = modules
            .SelectMany(module => module.GetTypes())
            .Where(type => type.Interfaces.Any(StronglyTypedIdInfo.IsStronglyTypedIdInterface))
            .Select(type => new StronglyTypedIdInfo(type))
            .Union(declaredInfos)
            .Distinct();

        return includeNested ? all : all.Where(static idInfo => !idInfo.IsInGenericContainer);
    }
}
