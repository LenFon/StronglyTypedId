namespace Len.StronglyTypedId.Generators;

/// <summary>
/// 核心代码生成逻辑：为单个强类型 Id 生成源码（运行时实现、System.Text.Json、Newtonsoft.Json）。
/// </summary>
/// <remarks>
/// 与 <see cref="IIntegrationCodeGenerator"/> 分开，是因为两者的输入粒度本就不同：核心生成器的产物是
/// 「每个 Id 一份文件」，按单个 Id 注册输出才能保住增量性 —— 任一 Id 改动只重跑它自己那一次输出，
/// 而不是把全部 Id 的产物重算一遍；集成生成器的产物里有一份「列举全部 Id」的聚合文件，必须看到全集。
/// </remarks>
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
