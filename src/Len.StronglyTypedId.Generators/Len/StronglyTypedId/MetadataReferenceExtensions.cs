namespace Len.StronglyTypedId;

internal static class MetadataReferenceExtensions
{
    public static IEnumerable<ModuleInfo> GetModules(this MetadataReference metadataReference, Compilation compilation)
    {
        // 项目引用（CompilationReference）：模块名与版本取自被引用编译的程序集标识。
        if (metadataReference is CompilationReference compilationReference)
        {
            return compilationReference.Compilation.Assembly.Modules
                      .Select(s => new ModuleInfo(
                                      s.Name,
                                      compilationReference.Compilation.Assembly.Identity.Version,
                                      compilationReference,
                                      compilationReference.Compilation.Assembly));
        }

        // 程序集文件引用（PortableExecutableReference）：从元数据读取器逐个枚举其中的模块。
        if (metadataReference is PortableExecutableReference portable
            && portable.GetMetadata() is AssemblyMetadata assemblyMetadata)
        {
            var assemblySymbol = compilation.GetAssemblyOrModuleSymbol(portable) as IAssemblySymbol;
            var modules = new List<ModuleInfo>();

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

                modules.Add(new ModuleInfo(module.Name, version, portable, assemblySymbol));
            }

            return modules;
        }

        return [];
    }
}
