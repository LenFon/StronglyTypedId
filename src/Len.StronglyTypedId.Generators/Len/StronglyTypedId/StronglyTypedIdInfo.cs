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

    /// <summary>
    /// 运行时程序集中 <c>Len.StronglyTypedId</c> 命名空间的显示名（含 <c>global::</c> 前缀）。
    /// </summary>
    internal const string InterfaceNamespace = "global::Len.StronglyTypedId";

    /// <summary>
    /// 判断给定接口符号是否为运行时的 <c>IStronglyTypedId&lt;TSelf, TPrimitiveId&gt;</c>。
    /// </summary>
    /// <remarks>
    /// 简单名、命名空间与类型实参个数三者必须同时校验。只比简单名会把使用者或第三方程序集里恰好
    /// 同名（例如只带一个类型实参）的接口一并纳入，随后解析基元类型失败并抛出异常，令整个生成器的
    /// 产出被丢弃 —— 而编译器只报一条 CS8785 警告，使用者看到的是「生成代码全部消失」。
    /// </remarks>
    internal static bool IsStronglyTypedIdInterface(INamedTypeSymbol @interface)
        => @interface.Name == InterfaceName
            && @interface.TypeArguments.Length == 2
            && @interface.ContainingNamespace.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == InterfaceNamespace;

    public StronglyTypedIdInfo(ITypeSymbol type)
    {
        FullyQualifiedNamespace = type.ContainingNamespace.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        Namespace = FullyQualifiedNamespace[GlobalPrefix.Length..];
        Name = type.Name;
        FullyQualifiedName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        FullName = type.ToDisplayString();
        TypeKindSuffix = type.TypeKind == TypeKind.Struct ? " struct" : null;
        PrimitiveIdTypeName = GetPrimitiveIdTypeName(type);
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

    /// <summary>
    /// 解析强类型 Id 所包装的基元类型名。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 优先读取 <c>IStronglyTypedId&lt;TSelf, TPrimitiveId&gt;</c> 的第二个类型实参：它是基元类型的权威来源，
    /// 既不需要类型具备任何特定形状的构造函数，也不会被其它构造函数干扰。引用程序集中已生成的强类型 Id
    /// 走这条路径。
    /// </para>
    /// <para>
    /// 本次编译新声明的强类型 Id 尚未实现该接口（实现由生成代码补充），因此回退到按单参数构造函数推断。
    /// 判断顺序不能颠倒：所有具名类型都满足 <see cref="INamedTypeSymbol"/>，若让构造函数分支先行，
    /// 会同时引入两类缺陷——类型没有单参数构造函数时取首个元素直接抛异常；引用类型 <c>record</c> 会额外
    /// 生成 <c>Foo(Foo original)</c> 复制构造函数，主构造函数参数多于一个时会把该记录自身误判为基元类型。
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// 该类型既未实现 <c>IStronglyTypedId&lt;TSelf, TPrimitiveId&gt;</c>，也不具备单参数构造函数。
    /// </exception>
    private static string GetPrimitiveIdTypeName(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol namedType)
        {
            var stronglyTypedIdInterface = namedType.Interfaces.FirstOrDefault(IsStronglyTypedIdInterface);

            if (stronglyTypedIdInterface is not null)
            {
                return stronglyTypedIdInterface.TypeArguments[1].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            }

            var constructor = namedType.Constructors.FirstOrDefault(candidate => candidate.Parameters.Length == 1);

            if (constructor is not null)
            {
                return constructor.Parameters[0].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            }
        }

        throw new InvalidOperationException($"无法从类型“{type.ToDisplayString()}”解析基元 Id 类型：它既未实现 {InterfaceName}<TSelf, TPrimitiveId>，也不具备单参数构造函数。");
    }
}
