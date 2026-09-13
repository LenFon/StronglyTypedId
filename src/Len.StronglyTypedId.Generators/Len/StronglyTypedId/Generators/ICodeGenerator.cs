namespace Len.StronglyTypedId.Generators;

/// <summary>
/// 单项代码生成逻辑（核心实现、System.Text.Json、Newtonsoft.Json、EF Core、Swagger 各一份）。
/// </summary>
internal interface ICodeGenerator
{
    /// <summary>
    /// 生成顺序，数值较小者先执行。
    /// </summary>
    int Order { get; }

    /// <summary>
    /// 为给定的强类型 Id 集合生成源码。
    /// </summary>
    /// <param name="stronglyTypedIdInfos">当前编译中的强类型 Id 集合。</param>
    /// <param name="modules">当前编译引用的全部模块，用于按依赖程序集决定生成内容。</param>
    /// <param name="context">源码生成上下文。</param>
    /// <param name="version">写入 <c>GeneratedCodeAttribute</c> 的生成器版本号。</param>
    void Generate(
        ImmutableArray<StronglyTypedIdInfo> stronglyTypedIdInfos,
        ImmutableArray<ModuleInfo> modules,
        SourceProductionContext context,
        string version);
}
