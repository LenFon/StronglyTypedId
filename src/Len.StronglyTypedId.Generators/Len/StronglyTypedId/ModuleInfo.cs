namespace Len.StronglyTypedId;

/// <summary>
/// 描述一个被引用程序集（模块），用于按程序集名与主版本号决定启用哪些代码生成器。
/// </summary>
/// <remarks>
/// 两处版本语义不同，阅读时不要混淆：
/// 决定启用哪个生成器看的是主版本号（<see cref="ModuleInfoExtensions.HasModule"/>），
/// 而模块集合去重看的是完整版本号（<see cref="Comparer"/>）。
/// </remarks>
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

    // 每个模块的类型表只依赖 Assembly 符号，且在同一编译里完全稳定：
    // 4 个生成器（EF Core / Swagger / Dapper / 内置 OpenAPI）各自调用 Discover 时都会遍历一次，
    // 不加缓存就会把整棵命名空间树重复枚举多遍。用实例级懒缓存把重复枚举收敛为一次。
    // 字段不参与 record 的相等性比较（record 只比对声明的属性），因此缓存的存在不影响去重逻辑。
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
    /// 按「程序集名 + 完整版本号」判定模块等价性，忽略 <see cref="MetadataReference"/>、
    /// <see cref="Assembly"/> 等实例差异。
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
