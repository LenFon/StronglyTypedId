namespace Len.StronglyTypedId;

internal static class ModuleInfoExtensions
{
    /// <summary>
    /// 判断模块集合中是否存在指定名称、且主版本号不低于 <paramref name="minMajorVersion"/> 的程序集。
    /// </summary>
    /// <remarks>
    /// 程序集名比较忽略大小写：模块名取自元数据读取器，实际大小写可能与 NuGet 包名不完全一致。
    /// </remarks>
    public static bool HasModule(this ImmutableArray<ModuleInfo> modules, string assemblyName, int minMajorVersion)
        => modules.Any(module => string.Equals(module.Name, assemblyName, StringComparison.OrdinalIgnoreCase)
            && module.Version.Major >= minMajorVersion);
}
