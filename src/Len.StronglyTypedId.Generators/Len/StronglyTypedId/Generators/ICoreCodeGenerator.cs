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
    /// <param name="stronglyTypedIdInfo">当前强类型 Id 的描述。</param>
    /// <param name="modules">当前编译引用的全部模块，用于按依赖程序集决定生成内容。</param>
    /// <param name="context">源码生成上下文。</param>
    /// <param name="version">写入 <c>GeneratedCodeAttribute</c> 的生成器版本号。</param>
    void Generate(
        StronglyTypedIdInfo stronglyTypedIdInfo,
        ImmutableArray<ModuleInfo> modules,
        SourceProductionContext context,
        string version);
}
