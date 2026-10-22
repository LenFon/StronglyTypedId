using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.CodeAnalysis;

namespace Len.StronglyTypedId;

/// <summary>
/// 按文件路径缓存 <see cref="MetadataReference"/>，供测试构造合成编译时复用。
/// </summary>
/// <remarks>
/// <c>MetadataReference.CreateFromFile</c> 每次调用都要打开 PE 文件、解析元数据头并计算 MVID，这部分开销远超被测逻辑本身。
/// 复用同一实例还能让 Roslyn 的元数据导入与符号缓存跨用例命中；用 <see cref="ConcurrentDictionary{TKey,TValue}"/> 是因为两个 TFM 的测试进程并行跑。
/// </remarks>
public static class AssemblyReferenceCache
{
    private static readonly ConcurrentDictionary<string, MetadataReference> _references
        = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 取得指定路径程序集的引用；同一路径在整轮测试中只解析一次。
    /// </summary>
    public static MetadataReference FromFile(string location)
        => _references.GetOrAdd(location, static path => MetadataReference.CreateFromFile(path));

    /// <summary>
    /// 取得已加载程序集的引用，等价于 <see cref="FromFile(string)"/> 作用于其 <see cref="Assembly.Location"/>。
    /// </summary>
    public static MetadataReference FromAssembly(Assembly assembly)
        => FromFile(assembly.Location);
}
