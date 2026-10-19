namespace Len.StronglyTypedId;

internal static class MetadataReferenceExtensions
{
    /// <summary>
    /// 枚举引用所代表的全部模块「身份」（名称 + 版本 + 元数据引用），不解析程序集符号。
    /// </summary>
    /// <remarks>不接触 <see cref="Compilation"/>，可脱离编译求值并被增量管道缓存；符号解析见 <see cref="ModuleIdentity.ResolveAssembly"/>。</remarks>
    public static IEnumerable<ModuleIdentity> GetModuleIdentities(this MetadataReference metadataReference)
    {
        // 项目引用（CompilationReference）：模块名与版本取自被引用编译的程序集标识。
        if (metadataReference is CompilationReference compilationReference)
        {
            var assembly = compilationReference.Compilation.Assembly;

            return assembly.Modules
                           .Select(s => new ModuleIdentity(s.Name, assembly.Identity.Version, metadataReference));
        }

        // 程序集文件引用（PortableExecutableReference）：从元数据读取器逐个枚举其中的模块。
        if (metadataReference is PortableExecutableReference portable
            && portable.GetMetadata() is AssemblyMetadata assemblyMetadata)
        {
            var identities = new List<ModuleIdentity>();

            foreach (var module in assemblyMetadata.GetModules())
            {
                // netmodule 引用不含程序集清单，GetAssemblyDefinition() 会抛异常；按「无清单」跳过而非崩溃。
                Version version;
                try
                {
                    version = module.GetMetadataReader().GetAssemblyDefinition().Version;
                }
                catch (BadImageFormatException)
                {
                    // 不同运行时抛的异常类型不同，统一按「无清单」跳过。
                    continue;
                }
                catch (InvalidOperationException)
                {
                    continue;
                }

                identities.Add(new ModuleIdentity(module.Name, version, metadataReference));
            }

            return identities;
        }

        return [];
    }

    /// <summary>
    /// 枚举引用所代表的全部模块，并解析出各自的程序集符号。
    /// </summary>
    /// <remarks>由 <see cref="GetModuleIdentities"/> 与 <see cref="ModuleIdentity.ResolveAssembly"/> 组合而成，省去调用方两步拼接。</remarks>
    public static IEnumerable<ModuleInfo> GetModules(this MetadataReference metadataReference, Compilation compilation)
        => metadataReference.GetModuleIdentities().Select(identity => identity.ResolveAssembly(compilation));
}
