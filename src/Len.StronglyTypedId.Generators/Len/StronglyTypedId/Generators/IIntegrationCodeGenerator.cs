namespace Len.StronglyTypedId.Generators;

/// <summary>
/// 集成代码生成逻辑：为全部强类型 Id 生成聚合产物（EF Core、Swagger、Dapper、内置 OpenAPI、ASP.NET Core MVC）。
/// </summary>
internal interface IIntegrationCodeGenerator
{
    /// <summary>
    /// 生成顺序，数值较小者先执行。
    /// </summary>
    int Order { get; }

    /// <summary>
    /// 为全部强类型 Id 生成源码。
    /// </summary>
    void Generate(
        DiscoveredIds discoveredIds,
        ImmutableArray<ModuleInfo> modules,
        SourceProductionContext context,
        string version);
}
