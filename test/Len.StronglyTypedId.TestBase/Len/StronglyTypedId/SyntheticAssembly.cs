using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Len.StronglyTypedId;

/// <summary>
/// 生成「只带程序集标识与一个占位类型、不含任何真实 API」的合成程序集引用。
/// </summary>
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

    private static PortableExecutableReference Emit(string assemblyName, string version)
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
