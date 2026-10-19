namespace Len.StronglyTypedId;

/// <summary>
/// 一个被引用程序集模块的「身份」：名称、版本与元数据引用，但不含程序集符号。
/// </summary>
/// <remarks>
/// 与 <see cref="ModuleInfo"/> 的差别是不持有 <see cref="IAssemblySymbol"/>：模块身份只由引用列表决定，
/// 而引用在编辑期间不变，这一步因此可脱离 <see cref="Compilation"/> 缓存；符号解析下沉到 <see cref="ResolveAssembly"/>。
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
    /// <remarks>项目引用取自被引用编译本身；文件引用须经 <see cref="Compilation"/> 解析，未登记时为 <see langword="null"/>。</remarks>
    public ModuleInfo ResolveAssembly(Compilation compilation)
        => MetadataReference is CompilationReference compilationReference
            ? new ModuleInfo(Name, Version, MetadataReference, compilationReference.Compilation.Assembly)
            : new ModuleInfo(Name, Version, MetadataReference, compilation.GetAssemblyOrModuleSymbol(MetadataReference) as IAssemblySymbol);

    /// <summary>
    /// 按「程序集名 + 完整版本号」判定模块等价性，与 <see cref="ModuleInfo.Comparer"/> 同一口径。
    /// </summary>
    /// <remarks>必须与 <see cref="ModuleInfo.Comparer"/> 同一口径，否则增量管道会在「身份未变、符号变了」时误判无变化。</remarks>
    internal sealed class Comparer : IEqualityComparer<ModuleIdentity>
    {
        public bool Equals(ModuleIdentity? x, ModuleIdentity? y)
            => ReferenceEquals(x, y)
                || (x is not null && y is not null && x.Name == y.Name && x.Version == y.Version);

        public int GetHashCode(ModuleIdentity obj)
            => unchecked((obj.Name.GetHashCode() * 397) ^ obj.Version.GetHashCode());
    }
}
