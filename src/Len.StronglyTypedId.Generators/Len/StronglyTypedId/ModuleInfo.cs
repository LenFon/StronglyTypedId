namespace Len.StronglyTypedId;

/// <summary>
/// 描述一个被引用程序集（模块），用于按程序集名与主版本号决定启用哪些代码生成器。
/// </summary>
internal sealed record ModuleInfo
{
    /// <summary>
    /// <c>IStronglyTypedId&lt;TSelf, TPrimitiveId&gt;</c> 的元数据名（含元数后缀）；声明 Id 的模块必然能看见它。
    /// </summary>
    private const string StronglyTypedIdInterfaceMetadataName = "Len.StronglyTypedId.IStronglyTypedId`2";

    /// <summary>
    /// 运行时程序集的简单名；声明 Id 的模块必然引用它。
    /// </summary>
    private const string RuntimeAssemblyName = "Len.StronglyTypedId";

    public ModuleInfo(string name, Version version, IAssemblySymbol? assemblySymbol)
    {
        Name = name;
        Version = version;
        Assembly = assemblySymbol;
        MayDeclareIds = CanDeclareIds(assemblySymbol);
    }

    public string Name { get; }

    public Version Version { get; }

    public IAssemblySymbol? Assembly { get; }

    /// <summary>
    /// 该模块是否可能声明强类型 Id（即它能看见 <c>IStronglyTypedId&lt;,&gt;</c>）。
    /// </summary>
    public bool MayDeclareIds { get; }

    // 类型表在同一编译里稳定，4 个集成生成器各遍历一次，故用实例级懒缓存收敛为一次。
    // 注意 record 合成的 Equals 会比较缓存字段：「已填充」与「为空」的两个实例不相等；增量管线用 ModuleInfo.Comparer 不受影响，别处直接用默认相等性会踩坑。
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

        // 看不见 IStronglyTypedId<,> 的模块不可能声明 Id，没必要走一遍类型树 —— 光是
        // System.Private.CoreLib 就有上万个类型。判定只查一次元数据字典或一张引用表，比遍历便宜几个数量级。
        if (!MayDeclareIds)
        {
            _typesCache = ImmutableArray<ITypeSymbol>.Empty;

            return _typesCache.Value;
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
    /// 判定模块能否声明 Id。两条信号任一成立即可：模块自己就定义了接口（运行时程序集本身），
    /// 或模块直接引用了运行时程序集。前者顺带覆盖类型转发，后者覆盖「引用进来」的常规情形。
    /// </summary>
    private static bool CanDeclareIds(IAssemblySymbol? assembly)
        => assembly is not null
            && (assembly.GetTypeByMetadataName(StronglyTypedIdInterfaceMetadataName) is not null
                || assembly.Modules.Any(module => module.ReferencedAssemblies.Any(identity => identity.Name == RuntimeAssemblyName)));

    /// <summary>
    /// 按「程序集名 + 完整版本号」判定模块等价性，忽略 <see cref="Assembly"/> 等实例差异。
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
