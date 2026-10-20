namespace Len.StronglyTypedId;

/// <summary>
/// 描述一个被引用程序集（模块），用于按程序集名与主版本号决定启用哪些代码生成器。
/// </summary>
internal sealed record ModuleInfo
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

    // 类型表在同一编译里完全稳定，而 4 个集成生成器各会遍历一次；实例级懒缓存把重复枚举收敛为一次。
    //
    // 注意：record 合成的 Equals 会比较「本类型声明的全部实例字段」，这个缓存字段也在其中 —— 于是
    // 「缓存已填充」与「缓存为空」的两个实例并不相等。增量管线不受影响，因为它用的是 ModuleInfo.Comparer
    // （见 StronglyTypedIdGenerator.Initialize 里的 WithComparer），只比程序集名与版本号；
    // 但别处若对 ModuleInfo 直接用默认相等性（Distinct / HashSet / Contains），就会踩到这个坑。
    // Generator_Should_ReuseCachedOutputs_WhenUnrelatedSyntaxChanges 守着管线的这一行为。
    private ImmutableArray<ITypeSymbol>? _typesCache;

    public ImmutableArray<ITypeSymbol> GetTypes()
    {
        if (Assembly is null)
        {
            return [];
        }

        if (_typesCache is { } cached)
        {
            return cached;
        }

        var typeSymbols = new List<ITypeSymbol>();
        var stack = new Stack<INamespaceOrTypeSymbol>(Assembly.GlobalNamespace.GetMembers());

        while (stack.Count > 0)
        {
            var namespaceOrTypeSymbol = stack.Pop();

            foreach (var member in namespaceOrTypeSymbol.GetMembers().OfType<INamespaceOrTypeSymbol>())
            {
                stack.Push(member);
            }

            if (namespaceOrTypeSymbol is ITypeSymbol typeSymbol)
            {
                typeSymbols.Add(typeSymbol);
            }
        }

        _typesCache = [.. typeSymbols];

        return _typesCache.Value;
    }

    /// <summary>
    /// 按「程序集名 + 完整版本号」判定模块等价性，忽略 <see cref="MetadataReference"/>、 <see cref="Assembly"/> 等实例差异。
    /// </summary>
    internal sealed class Comparer : IEqualityComparer<ModuleInfo>
    {
        public bool Equals(ModuleInfo? x, ModuleInfo? y)
            => ReferenceEquals(x, y)
                || (x is not null && y is not null && x.Name == y.Name && x.Version == y.Version);

        public int GetHashCode(ModuleInfo obj)
            => unchecked((obj.Name.GetHashCode() * 397) ^ obj.Version.GetHashCode());
    }
}
