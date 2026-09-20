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
    /// 运行时程序集中 <c>StronglyTypedIdAttribute</c> 的显示名（含 <c>global::</c> 前缀）。
    /// </summary>
    internal const string AttributeName = "global::Len.StronglyTypedId.StronglyTypedIdAttribute";

    /// <summary>
    /// attribute 中用于指定验证器方法的命名实参名。
    /// </summary>
    internal const string ValidatorArgumentName = "Validator";

    /// <summary>
    /// <c>System.IFormattable</c> 的显示名（含 <c>global::</c> 前缀）。
    /// </summary>
    private const string FormattableName = "global::System.IFormattable";

    /// <summary>
    /// <c>System.ISpanFormattable</c> 的显示名（含 <c>global::</c> 前缀）。
    /// </summary>
    private const string SpanFormattableName = "global::System.ISpanFormattable";

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
        // 非具名类型（数组、指针、类型参数等）没有 ContainingNamespace，直接解引用会抛
        // NullReferenceException。这里与「基元类型解析失败」保持同一异常语义，便于定位问题。
        var containingNamespace = type.ContainingNamespace
            ?? throw new InvalidOperationException($"无法从类型“{type.ToDisplayString()}”解析强类型 Id 信息：该类型不是具名类型。");

        FullyQualifiedNamespace = containingNamespace.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        Namespace = FullyQualifiedNamespace[GlobalPrefix.Length..];
        Name = type.Name;
        FullyQualifiedName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        FullName = type.ToDisplayString();
        TypeKindSuffix = type.TypeKind == TypeKind.Struct ? " struct" : null;

        var primitiveIdType = GetPrimitiveIdType(type);

        PrimitiveIdTypeName = primitiveIdType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        // 三项能力都由基元类型的符号判定，而非按类型名硬编码分支：与 SupportedPrimitiveTypes 保持同一取向
        // —— 判据落在符号上，类型名只用于展示。
        IsStringPrimitive = primitiveIdType.SpecialType == SpecialType.System_String;
        IsFormattable = ImplementsInterface(primitiveIdType, FormattableName);
        IsSpanFormattable = ImplementsInterface(primitiveIdType, SpanFormattableName);
        ValidatorName = GetValidatorName(type);
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
    /// 基元类型是否为 <c>string</c>。
    /// </summary>
    /// <remarks>
    /// 仅用于选择生成模板的形态（<c>string</c> 基元的解析、格式化与字典键路径都与其它基元不同）。
    /// 判据取自符号的 <see cref="ITypeSymbol.SpecialType"/>，不比较 <see cref="PrimitiveIdTypeName"/> 的字面量。
    /// </remarks>
    public bool IsStringPrimitive { get; }

    /// <summary>
    /// 基元类型是否实现 <c>System.IFormattable</c>。
    /// </summary>
    /// <remarks>
    /// 只有为 <see langword="true"/> 时生成的强类型 Id 才会一并实现 <c>IFormattable</c>：
    /// 该接口要求 <c>ToString(string?, IFormatProvider?)</c>，而受支持基元里的 <c>string</c> 并不具备它。
    /// </remarks>
    public bool IsFormattable { get; }

    /// <summary>
    /// 基元类型是否实现 <c>System.ISpanFormattable</c>。
    /// </summary>
    public bool IsSpanFormattable { get; }

    /// <summary>
    /// <c>[StronglyTypedId]</c> 上 <c>Validator</c> 指定的验证器方法名；未指定时为 <see langword="null"/>。
    /// </summary>
    /// <remarks>
    /// 验证器是强类型 Id 类型自身的静态方法，签名形如 <c>static bool Validate(TPrimitiveId value)</c>，
    /// 返回 <see langword="true"/> 表示取值合法。生成代码只在 <c>Create</c> 与两个 <c>TryParse</c> 重载里调用它，
    /// 因此「直接 new 主构造函数」绕不过验证 —— 该限制由生成器无法改写使用者声明的主构造函数所致。
    /// </remarks>
    public string? ValidatorName { get; }

    /// <summary>
    /// 解析强类型 Id 所包装的基元类型符号。
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
    private static ITypeSymbol GetPrimitiveIdType(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol namedType)
        {
            var stronglyTypedIdInterface = namedType.Interfaces.FirstOrDefault(IsStronglyTypedIdInterface);

            if (stronglyTypedIdInterface is not null)
            {
                return stronglyTypedIdInterface.TypeArguments[1];
            }

            var constructor = namedType.Constructors.FirstOrDefault(candidate => candidate.Parameters.Length == 1);

            if (constructor is not null)
            {
                return constructor.Parameters[0].Type;
            }
        }

        throw new InvalidOperationException($"无法从类型“{type.ToDisplayString()}”解析基元 Id 类型：它既未实现 {InterfaceName}<TSelf, TPrimitiveId>，也不具备单参数构造函数。");
    }

    /// <summary>
    /// 判断给定类型是否实现了指定的接口。
    /// </summary>
    private static bool ImplementsInterface(ITypeSymbol type, string interfaceName)
        => type.AllInterfaces.Any(@interface => @interface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == interfaceName);

    /// <summary>
    /// 读取 <c>[StronglyTypedId]</c> 上 <c>Validator</c> 命名实参指向的方法名。
    /// </summary>
    /// <remarks>
    /// 命名实参会被写入程序集元数据，因此引用程序集里的强类型 Id 与本次编译新声明的走同一条读取路径，
    /// 无需在生成器入口处另行传递。
    /// </remarks>
    private static string? GetValidatorName(ITypeSymbol type)
        => type.GetAttributes()
            .FirstOrDefault(attribute => attribute.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == AttributeName)
            ?.NamedArguments
            .FirstOrDefault(argument => argument.Key == ValidatorArgumentName)
            .Value.Value as string;
}
