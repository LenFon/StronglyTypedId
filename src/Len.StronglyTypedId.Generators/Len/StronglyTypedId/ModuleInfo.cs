namespace Len.StronglyTypedId;

internal record ModuleInfo
{
    private readonly static System.Collections.Concurrent.ConcurrentDictionary<string, ImmutableArray<ITypeSymbol>> _cached = new();

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

    public ImmutableArray<ITypeSymbol> GetTypes() => _cached.GetOrAdd(Name, _ =>
    {
        if (Assembly is null)
        {
            return ImmutableArray<ITypeSymbol>.Empty;
        }

        var typeSymbols = new List<ITypeSymbol>();
        var stack = new Stack<INamespaceOrTypeSymbol>(Assembly.GlobalNamespace.GetMembers());

        while (stack.Count > 0)
        {
            var namespaceOrTypeSymbol = stack.Pop();

            foreach (var item in namespaceOrTypeSymbol.GetMembers().OfType<INamespaceOrTypeSymbol>())
            {
                stack.Push(item);
            }

            if (namespaceOrTypeSymbol is ITypeSymbol typeSymbol)
            {
                typeSymbols.Add(typeSymbol);
            }
        }

        return typeSymbols.ToImmutableArray();
    });

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