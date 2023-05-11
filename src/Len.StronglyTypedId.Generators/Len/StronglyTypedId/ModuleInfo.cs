namespace Len.StronglyTypedId;

internal record ModuleInfo
{
    public ModuleInfo(string name, Version version, MetadataReference metadataReference, IAssemblySymbol? assemblySymbol)
    {
        Name = name;
        Version = version;
        MetadataReference = metadataReference;
        Assembly = assemblySymbol;
    }

    public string Name { get; }

    public Version Version { get; }

    public MetadataReference MetadataReference { get; }

    public IAssemblySymbol? Assembly { get; }

    public ImmutableArray<ITypeSymbol> GetTypes()
    {
        if (Assembly is null)
        {
            return ImmutableArray<ITypeSymbol>.Empty;
        }

        List<ITypeSymbol> typeSymbols = new();

        GetTypeSymbols(Assembly.GlobalNamespace, typeSymbols);

        return typeSymbols.ToImmutableArray();
    }

    private static void GetTypeSymbols(INamespaceOrTypeSymbol symbol, List<ITypeSymbol> typeSymbols)
    {
        if (symbol is ITypeSymbol typeSymbol)
        {
            typeSymbols.Add(typeSymbol);
        }

        foreach (var memberSymbol in symbol.GetMembers().OfType<INamespaceOrTypeSymbol>())
        {
            GetTypeSymbols(memberSymbol, typeSymbols);
        }
    }

    internal class Comparer : IEqualityComparer<ModuleInfo>
    {
        public bool Equals(ModuleInfo x, ModuleInfo y)
        {
            return x.Name == y.Name && x.Version == y.Version;
        }

        public int GetHashCode(ModuleInfo obj)
        {
            return obj.Name.GetHashCode();
        }
    }
}