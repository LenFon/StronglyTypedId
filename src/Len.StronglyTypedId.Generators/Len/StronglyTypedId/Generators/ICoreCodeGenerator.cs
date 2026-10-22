namespace Len.StronglyTypedId.Generators;

/// <summary>
/// 核心代码生成逻辑：为单个强类型 Id 生成源码（运行时实现、System.Text.Json、Newtonsoft.Json）。
/// </summary>
internal interface ICoreCodeGenerator
{
    /// <summary>
    /// 生成顺序，数值较小者先执行。
    /// </summary>
    int Order { get; }

    /// <summary>
    /// 为单个强类型 Id 生成源码。
    /// </summary>
    void Generate(
        StronglyTypedIdInfo stronglyTypedIdInfo,
        ImmutableArray<ModuleInfo> modules,
        SourceProductionContext context,
        string version);
}
