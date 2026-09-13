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
            return assemblyMetadata.GetModules()
                .Select(m => new ModuleInfo(
                              m.Name,
                              m.GetMetadataReader().GetAssemblyDefinition().Version,
                              portable,
                              assemblySymbol));
        }

        return [];
    }
}
