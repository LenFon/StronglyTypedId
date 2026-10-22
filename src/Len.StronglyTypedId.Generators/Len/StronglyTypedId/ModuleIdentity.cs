namespace Len.StronglyTypedId;

/// <summary>
/// 一个被引用程序集模块的「身份」：名称、版本与元数据引用，但不含程序集符号。
/// </summary>
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
    public ModuleInfo ResolveAssembly(Compilation compilation)
        => MetadataReference is CompilationReference compilationReference
            ? new ModuleInfo(Name, Version, MetadataReference, compilationReference.Compilation.Assembly)
            : new ModuleInfo(Name, Version, MetadataReference, compilation.GetAssemblyOrModuleSymbol(MetadataReference) as IAssemblySymbol);

    /// <summary>
    /// 按「程序集名 + 完整版本号」判定模块等价性，与 <see cref="ModuleInfo.Comparer"/> 同一口径。
    /// </summary>
    internal sealed class Comparer : IEqualityComparer<ModuleIdentity>
    {
        public bool Equals(ModuleIdentity? x, ModuleIdentity? y)
            => ReferenceEquals(x, y)
                || (x is not null && y is not null && x.Name == y.Name && x.Version == y.Version);

        public int GetHashCode(ModuleIdentity obj)
            => unchecked((obj.Name.GetHashCode() * 397) ^ obj.Version.GetHashCode());
    }
}
