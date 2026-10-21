namespace Len.StronglyTypedId;

/// <summary>
/// 一个被引用程序集模块的「身份」：名称、版本与元数据引用，但不含程序集符号。
/// </summary>
/// <remarks>
/// <para>
/// 与 <see cref="ModuleInfo"/> 的唯一差别是不持有 <see cref="IAssemblySymbol"/>。拆出这一层是为了把
/// <see cref="Compilation"/> 依赖从「模块枚举」这一步剥离出去：模块身份只由引用列表决定，而引用在编辑期间
/// 不变，因此这一步在每次按键后不会重跑，元数据解码的成果得以保留。
/// </para>
/// <para>
/// 程序集符号必须经 <see cref="Compilation"/> 解析（<c>Compilation.GetAssemblyOrModuleSymbol</c>），
/// 该依赖被下沉到 <see cref="ResolveAssembly"/>，只在真正需要符号时才执行。
/// </para>
/// </remarks>
internal sealed record ModuleIdentity
{
    public ModuleIdentity(string name, Version version, MetadataReference metadataReference)
    {
        Name = name;
        Version = version;
        MetadataReference = metadataReference;
    }

    public string Name { get; }

    public Version Version { get; }

    public MetadataReference MetadataReference { get; }

    /// <summary>
    /// 解析出程序集符号，补齐成完整的 <see cref="ModuleInfo"/>。
    /// </summary>
    /// <remarks>
    /// 项目引用（<see cref="CompilationReference"/>）的符号取自被引用编译本身，无需外层编译；
    /// 文件引用（<see cref="PortableExecutableReference"/>）则必须经 <see cref="Compilation"/> 解析，
    /// 引用未登记进该编译时结果为 <see langword="null"/>。
    /// </remarks>
    public ModuleInfo ResolveAssembly(Compilation compilation)
        => MetadataReference is CompilationReference compilationReference
            ? new ModuleInfo(Name, Version, MetadataReference, compilationReference.Compilation.Assembly)
            : new ModuleInfo(Name, Version, MetadataReference, compilation.GetAssemblyOrModuleSymbol(MetadataReference) as IAssemblySymbol);

    /// <summary>
    /// 按「程序集名 + 完整版本号」判定模块等价性，与 <see cref="ModuleInfo.Comparer"/> 同一口径。
    /// </summary>
    /// <remarks>
    /// 两处比较器必须保持同一口径：身份阶段的去重与模块阶段的去重若判定不一致，会让增量管道在
    /// 「身份未变、符号变了」时误判为无变化。此处刻意与 <see cref="ModuleInfo.Comparer"/> 逐字一致。
    /// </remarks>
    internal sealed class Comparer : IEqualityComparer<ModuleIdentity>
    {
        public bool Equals(ModuleIdentity? x, ModuleIdentity? y)
            => ReferenceEquals(x, y)
                || (x is not null && y is not null && x.Name == y.Name && x.Version == y.Version);

        public int GetHashCode(ModuleIdentity obj)
            => unchecked((obj.Name.GetHashCode() * 397) ^ obj.Version.GetHashCode());
    }
}
