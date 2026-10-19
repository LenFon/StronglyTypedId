namespace Len.StronglyTypedId.Generators;

/// <summary>
/// 集成代码生成逻辑：为全部强类型 Id 生成聚合产物
/// （EF Core、Swagger、Dapper、内置 OpenAPI、ASP.NET Core MVC）。
/// </summary>
/// <remarks>除逐 Id 的转换器 / 处理器外还要产出聚合注册文件，必须看到全集，无法按单个 Id 拆分输出。</remarks>
internal interface IIntegrationCodeGenerator
{
    /// <summary>
    /// 生成顺序，数值较小者先执行。
    /// </summary>
    int Order { get; }

    /// <summary>
    /// 为全部强类型 Id 生成源码。
    /// </summary>
    /// <param name="discoveredIds">当前编译的强类型 Id 发现结果。</param>
    /// <param name="modules">当前编译引用的全部模块，用于按依赖程序集决定生成内容。</param>
    /// <param name="context">源码生成上下文。</param>
    /// <param name="version">写入 <c>GeneratedCodeAttribute</c> 的生成器版本号。</param>
    void Generate(
        DiscoveredIds discoveredIds,
        ImmutableArray<ModuleInfo> modules,
        SourceProductionContext context,
        string version);
}
