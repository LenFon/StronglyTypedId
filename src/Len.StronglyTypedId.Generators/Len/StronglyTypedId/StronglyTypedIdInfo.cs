using System.Net.Security;

namespace Len.StronglyTypedId;

internal readonly record struct StronglyTypedIdTypeInfo
{
    public StronglyTypedIdTypeInfo(ITypeSymbol type)
    {
        FullyQualifiedNamespace = type.ContainingNamespace.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        Namespace = FullyQualifiedNamespace["global::".Length..];
        Name = type.Name;
        FullyQualifiedName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        FullName = type.ToDisplayString();
        TypeKindName = type.TypeKind == TypeKind.Struct ? " struct" : null;
        PrimitiveIdTypeName = type switch
        {
            INamedTypeSymbol { Constructors: var constructors } => constructors
                .First(w => w.Parameters.Length == 1)
                .Parameters
                .First()
                .Type
                .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            { Interfaces: var interfaces } => interfaces
                .First(w => w.Name == "IStronglyTypedId")
                .TypeArguments[1]
                .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
        };
    }

    /// <summary>
    /// The namespace of a strongly typed id.
    /// </summary>
    public string Namespace { get; }

    /// <summary>
    /// The fully qualified namespace of a strongly typed id.
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
    /// The fully qualified name of a strongly typed id.
    /// </summary>
    public string FullyQualifiedName { get; }

    /// <summary>
    /// The fully qualified name of a strongly typed id.
    /// </summary>
    /// <remarks>
    /// The fully qualified name of a strongly typed id does not include the word “global::”.
    /// </remarks>
    public string FullName { get; }

    public string? TypeKindName { get; }

    public string PrimitiveIdTypeName { get; }
}
