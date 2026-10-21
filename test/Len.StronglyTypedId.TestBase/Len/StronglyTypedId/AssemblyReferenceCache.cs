using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.CodeAnalysis;

namespace Len.StronglyTypedId;

/// <summary>
/// 按文件路径缓存 <see cref="MetadataReference"/>，供测试构造合成编译时复用。
/// </summary>
/// <remarks>
/// <para>
/// <c>MetadataReference.CreateFromFile</c> 每次调用都会打开 PE 文件、解析元数据头并计算 MVID；
/// 而生成器测试的每个用例都要对 AppDomain 中数十个程序集各建一次引用，这部分开销远超被测逻辑本身。
/// </para>
/// <para>
/// 缓存的主要收益不只是省掉重复的文件解析：<see cref="PortableExecutableReference"/> 会把解析后的
/// 元数据挂在实例上，各用例复用同一实例后，整轮测试只需解析一遍元数据，Roslyn 内部的元数据导入与
/// 符号缓存也能跨用例命中。若每次都新建实例，这份缓存永远不可能命中。
/// </para>
/// <para>
/// 与 <see cref="SyntheticAssembly"/> 一样用 <see cref="ConcurrentDictionary{TKey,TValue}"/>：
/// net8.0 与 net10.0 两个测试进程并行跑，且 xunit 会跨测试类并行，必须线程安全。
/// </para>
/// </remarks>
public static class AssemblyReferenceCache
{
    private static readonly ConcurrentDictionary<string, MetadataReference> _references
        = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 取得指定路径程序集的引用；同一路径在整轮测试中只解析一次。
    /// </summary>
    /// <param name="location">程序集的绝对路径。</param>
    public static MetadataReference FromFile(string location)
        => _references.GetOrAdd(location, static path => MetadataReference.CreateFromFile(path));

    /// <summary>
    /// 取得已加载程序集的引用，等价于 <see cref="FromFile(string)"/> 作用于其 <see cref="Assembly.Location"/>。
    /// </summary>
    /// <param name="assembly">已加载的程序集。</param>
    public static MetadataReference FromAssembly(Assembly assembly)
        => FromFile(assembly.Location);
}
