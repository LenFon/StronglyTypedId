namespace Len.StronglyTypedId;

internal static class MetadataReferenceExtensions
{
    public static IEnumerable<ModuleInfo> GetModules(this MetadataReference metadataReference, Compilation compilation)
    {
        // Project reference (ISymbol)
        if (metadataReference is CompilationReference compilationReference)
        {
            return compilationReference.Compilation.Assembly.Modules
                      .Select(s => new ModuleInfo(
                                      s.Name,
                                      compilationReference.Compilation.Assembly.Identity.Version,
                                      compilationReference,
                                      compilationReference.Compilation.Assembly));
        }

        // DLL
        if (metadataReference is PortableExecutableReference portable
            && portable.GetMetadata() is AssemblyMetadata assemblyMetadata)
        {
            if (compilation.GetAssemblyOrModuleSymbol(portable) is IAssemblySymbol assemblySymbol)
            {
                return assemblyMetadata.GetModules()
                    .Select(m => new ModuleInfo(
                                  m.Name,
                                  m.GetMetadataReader().GetAssemblyDefinition().Version,
                                  portable,
                                  assemblySymbol));
            }
            else
            {
                return assemblyMetadata.GetModules()
                    .Select(m => new ModuleInfo(
                                  m.Name,
                                  m.GetMetadataReader().GetAssemblyDefinition().Version,
                                  portable,
                                  null));
            }
        }

        return Array.Empty<ModuleInfo>();
    }
}
