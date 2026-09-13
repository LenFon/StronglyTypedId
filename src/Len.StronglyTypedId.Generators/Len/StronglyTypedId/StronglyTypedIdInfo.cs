namespace Len.StronglyTypedId;

/// <summary>
/// 描述一个由源码生成器发现的强类型 Id 类型，供各代码生成器拼装文本模板使用。
/// </summary>
internal readonly record struct StronglyTypedIdInfo
{
    private const string GlobalPrefix = "global::";

    /// <summary>
    /// 运行时程序集中 <c>IStronglyTypedId&lt;TSelf, TPrimitiveId&gt;</c> 的简单名。
    /// </summary>
    /// <remarks>
    /// 生成器项目不引用运行时程序集，因此只能以字符串字面量匹配符号名。
    /// </remarks>
    internal const string InterfaceName = "IStronglyTypedId";

    public StronglyTypedIdInfo(ITypeSymbol type)
    {
        FullyQualifiedNamespace = type.ContainingNamespace.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        Namespace = FullyQualifiedNamespace[GlobalPrefix.Length..];
        Name = type.Name;
        FullyQualifiedName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        FullName = type.ToDisplayString();
        TypeKindSuffix = type.TypeKind == TypeKind.Struct ? " struct" : null;
        PrimitiveIdTypeName = type switch
        {
            INamedTypeSymbol { Constructors: var constructors } => constructors
                .First(constructor => constructor.Parameters.Length == 1)
                .Parameters[0]
                .Type
                .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            { Interfaces: var interfaces } => interfaces
                .First(@interface => @interface.Name == InterfaceName)
                .TypeArguments[1]
                .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
        };
    }

    /// <summary>
    /// The namespace of a strongly typed id, without the <c>global::</c> prefix.
    /// </summary>
    /// <remarks>
    /// This is the form emitted right after the generated <c>namespace</c> keyword.
    /// </remarks>
    public string Namespace { get; }

    /// <summary>
    /// The namespace of a strongly typed id, prefixed with <c>global::</c>.
    /// </summary>
    public string FullyQualifiedNamespace { get; }

    /// <summary>
    /// The name of a strongly typed id.
    /// </summary>
    /// <remarks>
    /// This is a short strongly typed id type name that does not include a namespace.
    /// </remarks>
    public string Name { get; }

    /// <summary>
    /// The fully qualified name of a strongly typed id, prefixed with <c>global::</c>.
    /// </summary>
    /// <remarks>
    /// This is the form used when emitting type references into generated code, where the
    /// <c>global::</c> prefix shields the name from any namespace that happens to be in scope.
    /// </remarks>
    public string FullyQualifiedName { get; }

    /// <summary>
    /// The display name of a strongly typed id, also used as the generated hint name.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="FullyQualifiedName"/>, this form carries no <c>global::</c> prefix,
    /// which makes it a legal hint name but unsuitable for use inside generated code.
    /// </remarks>
    public string FullName { get; }

    /// <summary>
    /// The type kind suffix appended right after the <c>record</c> keyword, e.g. <c>" struct"</c>.
    /// </summary>
    /// <remarks>
    /// A <see langword="null"/> value means the strongly typed id is declared as a reference type record.
    /// </remarks>
    public string? TypeKindSuffix { get; }

    /// <summary>
    /// The fully qualified name of the primitive type wrapped by the strongly typed id.
    /// </summary>
    public string PrimitiveIdTypeName { get; }
}
