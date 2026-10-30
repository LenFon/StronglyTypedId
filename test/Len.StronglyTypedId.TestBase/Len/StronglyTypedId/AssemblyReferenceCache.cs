using System;
using System.Collections.Concurrent;
using System.IO;
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
    /// 按程序集缓存 <see cref="TryResolveReference"/> 的结果（含 <c>null</c>）。
    /// </summary>
    /// <remarks>
    /// 生成器用例每次构造合成编译都会遍历整个 AppDomain 的程序集；解析一个程序集要走
    /// <see cref="File.Exists(string)"/> 与 TPA 列表扫描，几百个用例 × 上百个程序集累积起来是秒级开销，
    /// 而同一进程内程序集的解析结果不会变。缓存 null 同样重要：解析不出来的程序集占相当比例。
    /// </remarks>
    private static readonly ConcurrentDictionary<Assembly, MetadataReference?> _resolvedByAssembly = new();

    /// <summary>
    /// 受信平台程序集（TPA）的文件名 → 完整路径索引，进程内只建一次。
    /// </summary>
    private static readonly Lazy<Dictionary<string, string>> _trustedPlatformAssemblies = new(BuildTrustedPlatformAssemblies);

    /// <summary>
    /// 取得指定路径程序集的引用；同一路径在整轮测试中只解析一次。
    /// </summary>
    public static MetadataReference FromFile(string location)
        => _references.GetOrAdd(location, static path => MetadataReference.CreateFromFile(path));

    /// <summary>
    /// 取得已加载程序集的引用（非空契约）；无法解析时抛 <see cref="InvalidOperationException"/>。
    /// 用于调用方明确持有可用程序集（如 <c>typeof(T).Assembly</c>）的场景。
    /// </summary>
    public static MetadataReference FromAssembly(Assembly assembly)
        => TryResolveReference(assembly)
            ?? throw new InvalidOperationException(
                $"无法为程序集“{assembly.FullName}”解析元数据引用路径：Location、模块路径与名称均不可用。");

    /// <summary>
    /// 尝试取得已加载程序集的引用；无法解析（路径为空 / 身份为 «Unknown» / 文件不存在）时返回 <c>null</c>，交由调用方跳过，而非抛异常中断整批引用收集。
    /// </summary>
    /// <remarks>
    /// net10.0 测试宿主下，部分程序集的 <see cref="Assembly.Location"/> 为空串、<see cref="Assembly.GetName"/> 为 «Unknown»。
    /// 兜底链：① Location；② 模块完全限定名；③ 受信平台程序集（TPA）列表按文件名匹配；④ 测试输出目录下的模块名。
    /// 任一环节文件不存在即返回 <c>null</c>，由调用方跳过。结果按程序集缓存，见 <see cref="_resolvedByAssembly"/>。
    /// </remarks>
    public static MetadataReference? TryResolveReference(Assembly assembly)
        => _resolvedByAssembly.GetOrAdd(assembly, static target => ResolveReference(target));

    private static MetadataReference? ResolveReference(Assembly assembly)
    {
        if (!string.IsNullOrEmpty(assembly.Location) && File.Exists(assembly.Location))
        {
            return FromFile(assembly.Location);
        }

        var module = assembly.Modules.FirstOrDefault();
        var moduleFileName = module?.Name;
        if (!string.IsNullOrEmpty(moduleFileName))
        {
            var resolved = ResolveFromTrustedPlatformAssemblies(moduleFileName);
            if (resolved is not null && File.Exists(resolved))
            {
                return FromFile(resolved);
            }

            var inBaseDirectory = Path.Combine(AppContext.BaseDirectory, moduleFileName);
            if (File.Exists(inBaseDirectory))
            {
                return FromFile(inBaseDirectory);
            }
        }

        var simpleName = assembly.GetName().Name;
        if (!string.IsNullOrEmpty(simpleName) && simpleName != "<Unknown>")
        {
            var resolved = ResolveFromTrustedPlatformAssemblies(simpleName + ".dll");
            if (resolved is not null && File.Exists(resolved))
            {
                return FromFile(resolved);
            }

            var inBaseDirectory = Path.Combine(AppContext.BaseDirectory, simpleName + ".dll");
            if (File.Exists(inBaseDirectory))
            {
                return FromFile(inBaseDirectory);
            }
        }

        return null;
    }

    /// <summary>
    /// 在 <c>TRUSTED_PLATFORM_ASSEMBLIES</c> 列表中按文件名（含或不含扩展名）匹配程序集路径。
    /// </summary>
    private static string? ResolveFromTrustedPlatformAssemblies(string fileName)
    {
        var index = _trustedPlatformAssemblies.Value;

        return index.TryGetValue(fileName, out var path)
            || index.TryGetValue(Path.GetFileNameWithoutExtension(fileName), out path)
            ? path
            : null;
    }

    /// <summary>
    /// 把 <c>TRUSTED_PLATFORM_ASSEMBLIES</c> 拆成「文件名 → 路径」与「无扩展名 → 路径」两张表。
    /// </summary>
    /// <remarks>
    /// 原先每次调用都 <c>Split</c> 一遍整条 TPA 串（约 200 项）并逐项 <c>Path.GetFileName</c>，
    /// 而它在每个程序集的解析里最多被调用两次。建一次索引后单次查找降为字典命中。
    /// </remarks>
    private static Dictionary<string, string> BuildTrustedPlatformAssemblies()
    {
        var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is not string trustedPlatformAssemblies)
        {
            return index;
        }

        foreach (var path in trustedPlatformAssemblies.Split(Path.PathSeparator))
        {
            index.TryAdd(Path.GetFileName(path), path);
            index.TryAdd(Path.GetFileNameWithoutExtension(path), path);
        }

        return index;
    }
}
