namespace Len.StronglyTypedId;

internal static class MetadataReferenceExtensions
{
    /// <summary>
    /// 枚举引用所代表的全部模块「身份」（名称 + 版本 + 元数据引用），不解析程序集符号。
    /// </summary>
    /// <remarks>
    /// 这一步不接触 <see cref="Compilation"/>，因此可以脱离编译独立求值：引用列表在编辑期间不变，
    /// 增量管道据此缓存，元数据解码不会在每次按键后重跑。程序集符号的解析下沉到
    /// <see cref="ModuleIdentity.ResolveAssembly"/>。
    /// </remarks>
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
                // GetMetadata() 对模块级（netmodule）引用也会返回被包裹的 AssemblyMetadata，
                // 但其模块不含程序集清单，GetAssemblyDefinition() 会抛异常（主流运行时为
                // InvalidOperationException，部分环境下为 BadImageFormatException）。
                // 仅保留真正含程序集清单的模块；netmodule 引用因此安全跳过而非崩溃。
                Version version;
                try
                {
                    version = module.GetMetadataReader().GetAssemblyDefinition().Version;
                }
                catch (BadImageFormatException)
                {
                    // 模块级（netmodule）引用不含程序集清单。不同运行时下 GetAssemblyDefinition()
                    // 可能抛 BadImageFormatException 或 InvalidOperationException，统一按「无清单」跳过。
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
    /// <remarks>
    /// 由 <see cref="GetModuleIdentities"/> 与 <see cref="ModuleIdentity.ResolveAssembly"/> 组合而成；
    /// 保留该方法是为了让「一次拿到完整 <see cref="ModuleInfo"/>」的调用方（测试与基准）不必自行组合两步。
    /// </remarks>
    public static IEnumerable<ModuleInfo> GetModules(this MetadataReference metadataReference, Compilation compilation)
        => metadataReference.GetModuleIdentities().Select(identity => identity.ResolveAssembly(compilation));
}
