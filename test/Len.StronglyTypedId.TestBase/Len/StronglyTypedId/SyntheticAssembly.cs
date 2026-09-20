using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Len.StronglyTypedId;

/// <summary>
/// 生成「只带程序集标识与一个占位类型、不含任何真实 API」的合成程序集引用。
/// </summary>
/// <remarks>
/// <para>
/// 生成器按「程序集名 + 主版本号」决定启用哪些代码生成器，而测试宿主的 AppDomain 里只会加载被引用
/// 包的真实版本（恰好都是满足闸门的版本）。要验证「引用低于阈值时不生成」，就必须让编译引用中出现
/// 一个只有名字和版本、却没有真实 API 的替身，故在此 emit 临时 DLL 作为 <see cref="MetadataReference"/>。
/// </para>
/// <para>
/// 同一个「程序集名 + 版本」只 emit 一次并缓存。合成文件落在临时目录，若每条用例都重写同一路径，
/// 并行执行的测试类会互相争用文件句柄。
/// </para>
/// </remarks>
public static class SyntheticAssembly
{
    private static readonly ConcurrentDictionary<string, Lazy<MetadataReference>> _references
        = new(StringComparer.Ordinal);

    /// <summary>
    /// 取得指定程序集名与版本的合成引用。
    /// </summary>
    /// <param name="assemblyName">
    /// 不含 <c>.dll</c> 后缀的程序集名，须与生成器判据里的字面量一致
    /// （判据比较的是模块名，即带 <c>.dll</c> 后缀的形式）。
    /// </param>
    /// <param name="version">程序集版本，形如 <c>6.0.0.0</c>；生成器只读取其主版本号。</param>
    public static MetadataReference GetOrCreate(string assemblyName, string version)
        => _references.GetOrAdd(
            assemblyName + "|" + version,
            _ => new Lazy<MetadataReference>(
                () => Emit(assemblyName, version),
                LazyThreadSafetyMode.ExecutionAndPublication)).Value;

    private static MetadataReference Emit(string assemblyName, string version)
    {
        var source = $$"""
            using System.Reflection;

            [assembly: AssemblyVersion("{{version}}")]

            /// <summary>Stand-in for {{assemblyName}}; intentionally carries no API surface.</summary>
            public class Placeholder
            {
            }
            """;

        var compilation = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source)],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var stream = new MemoryStream();
        var emit = compilation.Emit(stream);

        if (!emit.Success)
        {
            throw new InvalidOperationException(
                $"合成程序集 {assemblyName} {version} 生成失败：{string.Join("\n", emit.Diagnostics)}");
        }

        var directory = Path.Combine(Path.GetTempPath(), "StiSyntheticAssemblies");
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, $"{assemblyName}.{version}.dll");
        File.WriteAllBytes(path, stream.ToArray());

        return MetadataReference.CreateFromFile(path);
    }
}
