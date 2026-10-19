namespace Len.StronglyTypedId;

/// <summary>
/// 描述一个由源码生成器发现的强类型 Id 类型，供各代码生成器拼装文本模板使用。
/// </summary>
internal readonly record struct StronglyTypedIdInfo
{
    private const string GlobalPrefix = "global::";

    /// <summary>
    /// 运行时 <c>IStronglyTypedId&lt;TSelf, TPrimitiveId&gt;</c> 的简单名（生成器不引用运行时程序集，只能按名字匹配）。
    /// </summary>
    internal const string InterfaceName = "IStronglyTypedId";

    /// <summary>
    /// 运行时 <c>Len.StronglyTypedId</c> 命名空间的显示名（含 <c>global::</c> 前缀）。
    /// </summary>
    internal const string InterfaceNamespace = "global::Len.StronglyTypedId";

    /// <summary>
    /// System.Text.Json 转换器的内嵌类型名。
    /// </summary>
    private const string SystemTextJsonConverterTypeName = "SystemTextJsonConverter";

    /// <summary>
    /// Newtonsoft.Json 转换器的内嵌类型名。
    /// </summary>
    private const string NewtonsoftJsonConverterTypeName = "NewtonsoftJsonConverter";

    /// <summary>
    /// attribute 中验证器方法的命名实参名。
    /// </summary>
    internal const string ValidatorArgumentName = "Validator";

    /// <summary>
    /// attribute 中归一化方法的命名实参名。
    /// </summary>
    internal const string NormalizerArgumentName = "Normalizer";

    /// <summary>
    /// attribute 中 <c>TypeConverter</c> 开关的命名实参名。
    /// </summary>
    internal const string TypeConverterArgumentName = "TypeConverter";

    /// <summary>
    /// attribute 中验证失败异常类型的命名实参名。
    /// </summary>
    internal const string ExceptionTypeArgumentName = "ExceptionType";

    /// <summary>
    /// attribute 中 JSON 形态的命名实参名。
    /// </summary>
    internal const string JsonShapeArgumentName = "JsonShape";

    /// <summary>
    /// attribute 中规范文本形前缀的命名实参名。
    /// </summary>
    internal const string PrefixArgumentName = "Prefix";

    /// <summary>
    /// attribute 中前缀读入策略的命名实参名。
    /// </summary>
    internal const string PrefixReadModeArgumentName = "PrefixReadMode";

    /// <summary>
    /// <c>PrefixReadMode.Strict</c> 的底层值；命名实参在元数据里存的是枚举值而非成员名。
    /// </summary>
    private const int StrictPrefixReadModeValue = 1;

    /// <summary>
    /// <c>JsonShape.Object</c> 的底层值；命名实参在元数据里存的是枚举值而非成员名。
    /// </summary>
    private const int ObjectJsonShapeValue = 1;

    /// <summary>
    /// BCL 接口所属命名空间的显示名（含 <c>global::</c> 前缀）。
    /// </summary>
    private const string SystemNamespace = "global::System";

    /// <summary>
    /// <c>System.IFormattable</c> 的简单名。
    /// </summary>
    private const string FormattableName = "IFormattable";

    /// <summary>
    /// <c>System.ISpanFormattable</c> 的简单名。
    /// </summary>
    private const string SpanFormattableName = "ISpanFormattable";

    /// <summary>
    /// <c>System.IUtf8SpanParsable&lt;TSelf&gt;</c> 的简单名。
    /// </summary>
    private const string Utf8SpanParsableName = "IUtf8SpanParsable";

    /// <summary>
    /// <c>System.IUtf8SpanFormattable</c> 的简单名。
    /// </summary>
    private const string Utf8SpanFormattableName = "IUtf8SpanFormattable";

    /// <summary>
    /// 判断给定接口符号是否为运行时的 <c>IStronglyTypedId&lt;TSelf, TPrimitiveId&gt;</c>。
    /// </summary>
    internal static bool IsStronglyTypedIdInterface(INamedTypeSymbol @interface)
        => @interface.Name == InterfaceName
            && @interface.TypeArguments.Length == 2
            && @interface.ContainingNamespace.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == InterfaceNamespace;

    public StronglyTypedIdInfo(ITypeSymbol type, string? defaultValidatorName = null, string? defaultNormalizerName = null, string? defaultExceptionTypeName = null)
    {
        // 非具名类型（数组、指针、类型参数等）没有 ContainingNamespace。
        var containingNamespace = type.ContainingNamespace
            ?? throw new InvalidOperationException($"无法从类型“{type.ToDisplayString()}”解析强类型 Id 信息：该类型不是具名类型。");

        FullyQualifiedNamespace = containingNamespace.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        Namespace = FullyQualifiedNamespace[GlobalPrefix.Length..];
        Name = type.Name;
        FullyQualifiedName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        FullName = type.ToDisplayString();
        // readonly 必须写在 record 关键字之前，且各 partial 段必须一致，否则合并后编译失败。
        TypeKindSuffix = type.TypeKind == TypeKind.Struct ? " struct" : null;
        ReadOnlyRecordModifier = type.TypeKind == TypeKind.Struct && type.IsReadOnly ? "readonly " : string.Empty;

        var containingTypes = GetContainingTypeDeclarations(type);
        ContainingTypeDeclarations = containingTypes.Declarations;
        NestingDepth = containingTypes.Depth;
        IsInGenericContainer = containingTypes.IsInGenericContainer;
        HintName = SanitizeHintName(FullName);

        var primitiveIdType = GetPrimitiveIdType(type);

        PrimitiveIdTypeName = primitiveIdType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        IsStringPrimitive = primitiveIdType.SpecialType == SpecialType.System_String;
        IsCharPrimitive = primitiveIdType.SpecialType == SpecialType.System_Char;
        IsEnum = primitiveIdType.TypeKind == TypeKind.Enum;
        // 枚举继承的 IFormattable / ISpanFormattable 已被标记 [Obsolete]：透出会 CS0618，不透出又会 CS0535。
        IsFormattable = ImplementsInterface(primitiveIdType, FormattableName) && !IsEnum;
        IsSpanFormattable = ImplementsInterface(primitiveIdType, SpanFormattableName) && !IsEnum;
        // BigInteger 实现了 IUtf8SpanParsable 却把 TryParse 做成显式接口实现，按类型名调用会 CS1501。
        IsUtf8SpanParsable = ImplementsInterface(primitiveIdType, Utf8SpanParsableName)
            && HasPublicStaticTryParse(primitiveIdType, SpecialType.System_Byte);
        IsUtf8SpanFormattable = ImplementsInterface(primitiveIdType, Utf8SpanFormattableName);
        CharTryFormatKind = GetTryFormatKind(primitiveIdType, SpecialType.System_Char);
        Utf8TryFormatKind = GetTryFormatKind(primitiveIdType, SpecialType.System_Byte);
        // 属性只查一次：下面五项都要读命名实参，逐项重查会把 GetAttributes 与显示名格式化各做五遍。
        var attribute = AttributeReader.Find(type, AttributeReader.StronglyTypedIdAttributeName);
        ValidatorName = AttributeReader.GetString(attribute, ValidatorArgumentName) ?? defaultValidatorName;
        NormalizerName = AttributeReader.GetString(attribute, NormalizerArgumentName) ?? defaultNormalizerName;
        // 存显示名而非符号：本类型是增量缓存的键，符号跨编译不稳定，显示名才是稳定值。
        ExceptionTypeName = AttributeReader.GetType(attribute, ExceptionTypeArgumentName)
                ?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            ?? defaultExceptionTypeName;
        HasTypeConverter = AttributeReader.GetBoolean(attribute, TypeConverterArgumentName);
        IsObjectJsonShape = AttributeReader.GetEnumFlag(attribute, JsonShapeArgumentName, ObjectJsonShapeValue);
        Prefix = NormalizePrefix(AttributeReader.GetString(attribute, PrefixArgumentName));
        IsStrictPrefixRead = AttributeReader.GetEnumFlag(attribute, PrefixReadModeArgumentName, StrictPrefixReadModeValue);
        HasSystemTextJsonConverter = WillHaveNestedType(type, SystemTextJsonConverterTypeName);
        HasNewtonsoftJsonConverter = WillHaveNestedType(type, NewtonsoftJsonConverterTypeName);
    }

    /// <summary>
    /// 强类型 Id 的命名空间，不含 <c>global::</c> 前缀。
    /// </summary>
    public string Namespace { get; }

    /// <summary>
    /// 强类型 Id 的命名空间，含 <c>global::</c> 前缀。
    /// </summary>
    public string FullyQualifiedNamespace { get; }

    /// <summary>
    /// 强类型 Id 的类型名，不含命名空间。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 强类型 Id 的全限定名，含 <c>global::</c> 前缀。
    /// </summary>
    public string FullyQualifiedName { get; }

    /// <summary>
    /// 强类型 Id 的显示名，同时用作生成源的 hint 名。
    /// </summary>
    public string FullName { get; }

    /// <summary>
    /// 包含类型链的重开声明，最外层在前、每层一行；顶层类型为空串。
    /// </summary>
    public string ContainingTypeDeclarations { get; }

    /// <summary>
    /// 包含类型链的层数；顶层类型为 0。
    /// </summary>
    public int NestingDepth { get; }

    /// <summary>
    /// 可作为生成源 hint 名的类型显示名：剔除尖括号、逗号与空格。
    /// </summary>
    public string HintName { get; }

    /// <summary>
    /// 强类型 Id 是否声明在泛型包含类型内部（外部集成生成器会跳过此类 Id）。
    /// </summary>
    public bool IsInGenericContainer { get; }

    /// <summary>
    /// 该 Id 是否带有（或本次生成即将产出）内嵌的 <c>SystemTextJsonConverter</c>。
    /// </summary>
    public bool HasSystemTextJsonConverter { get; }

    /// <summary>
    /// 该 Id 是否带有（或本次生成即将产出）内嵌的 <c>NewtonsoftJsonConverter</c>。
    /// </summary>
    public bool HasNewtonsoftJsonConverter { get; }

    /// <summary>
    /// 紧跟在 <c>record</c> 关键字之后的类型种类后缀，如 <c>" struct"</c>；<see langword="null"/> 表示引用类型 record。
    /// </summary>
    public string? TypeKindSuffix { get; }

    /// <summary>
    /// <c>readonly record struct</c> 时插在 <c>partial</c> 与 <c>record</c> 之间的修饰符；否则为空串。
    /// </summary>
    public string ReadOnlyRecordModifier { get; }

    /// <summary>
    /// 强类型 Id 所包装基元类型的全限定名。
    /// </summary>
    public string PrimitiveIdTypeName { get; }

    /// <summary>
    /// 基元类型是否为 <c>string</c>。
    /// </summary>
    public bool IsStringPrimitive { get; }

    /// <summary>
    /// 基元类型是否为 <c>char</c>。
    /// </summary>
    public bool IsCharPrimitive { get; }

    /// <summary>
    /// 基元类型是否为枚举（枚举没有带 <c>IFormatProvider</c> 的静态 <c>TryParse</c>）。
    /// </summary>
    public bool IsEnum { get; }

    /// <summary>
    /// 基元类型是否实现 <c>System.IFormattable</c>。
    /// </summary>
    public bool IsFormattable { get; }

    /// <summary>
    /// 基元类型是否实现 <c>System.ISpanFormattable</c>。
    /// </summary>
    public bool IsSpanFormattable { get; }

    /// <summary>
    /// 基元类型是否实现 <c>IUtf8SpanParsable&lt;TSelf&gt;</c> 且暴露公开的 <c>TryParse(ReadOnlySpan&lt;byte&gt;, …)</c>。
    /// </summary>
    public bool IsUtf8SpanParsable { get; }

    /// <summary>
    /// 基元类型是否实现 <c>System.IUtf8SpanFormattable</c>。
    /// </summary>
    public bool IsUtf8SpanFormattable { get; }

    /// <summary>
    /// 基元 UTF-16 <c>TryFormat</c> 的直呼形态（决定是否经接口调用、是否装箱）。
    /// </summary>
    public TryFormatKind CharTryFormatKind { get; }

    /// <summary>
    /// 基元 UTF-8 <c>TryFormat</c> 的直呼形态（决定是否经接口调用、是否装箱）。
    /// </summary>
    public TryFormatKind Utf8TryFormatKind { get; }

    /// <summary>
    /// <c>[StronglyTypedId]</c> 上 <c>Validator</c> 指定的验证器方法名；未指定时为 <see langword="null"/>。
    /// </summary>
    public string? ValidatorName { get; }

    /// <summary>
    /// <c>[StronglyTypedId]</c> 上 <c>Normalizer</c> 指定的归一化方法名；未指定时为 <see langword="null"/>。
    /// </summary>
    public string? NormalizerName { get; }

    /// <summary>
    /// 验证失败时抛出的异常类型全限定名（含 <c>global::</c> 前缀）；未配置时为 <see langword="null"/>，
    /// 表示沿用 <c>System.ArgumentException</c>。
    /// </summary>
    public string? ExceptionTypeName { get; }

    /// <summary>
    /// <c>[StronglyTypedId]</c> 是否要求生成 <c>System.ComponentModel.TypeConverter</c>。
    /// </summary>
    public bool HasTypeConverter { get; }

    /// <summary>
    /// <c>[StronglyTypedId]</c> 是否要求 JSON 采用对象形态（<c>{ "value": … }</c>）。
    /// </summary>
    public bool IsObjectJsonShape { get; }

    /// <summary>
    /// <c>[StronglyTypedId]</c> 上 <c>Prefix</c> 指定的前缀；未指定或为空 / 纯空白时为 <see langword="null"/>。
    /// </summary>
    public string? Prefix { get; }

    /// <summary>
    /// 强类型 Id 的规范文本形是否携带前缀。
    /// </summary>
    public bool HasPrefix => Prefix is { Length: > 0 };

    /// <summary>
    /// 读回带前缀的文本时是否要求前缀必须存在（<c>PrefixReadMode.Strict</c>）。
    /// </summary>
    public bool IsStrictPrefixRead { get; }

    /// <summary>
    /// 解析强类型 Id 所包装的基元类型符号。
    /// </summary>
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
    /// 自内向外取出包含类型链，反转成最外层在前，并逐层渲染为可重开该类型的 partial 声明。
    /// </summary>
    private static (string Declarations, int Depth, bool IsInGenericContainer) GetContainingTypeDeclarations(ITypeSymbol type)
    {
        var levels = new List<string>();
        var isInGenericContainer = false;

        for (var container = type.ContainingType; container is not null; container = container.ContainingType)
        {
            if (container.TypeParameters.Length > 0)
            {
                isInGenericContainer = true;
            }

            levels.Add(GetContainingTypeDeclaration(container));
        }

        levels.Reverse();

        return (string.Join("\n", levels), levels.Count, isInGenericContainer);
    }

    /// <summary>
    /// 把包含类型渲染成可重开它的 partial 声明，形如 <c>public partial class Outer</c>。
    /// </summary>
    private static string GetContainingTypeDeclaration(INamedTypeSymbol type)
    {
        var keyword = type switch
        {
            { IsRecord: true, TypeKind: TypeKind.Struct } => "record struct",
            { IsRecord: true } => "record",
            { TypeKind: TypeKind.Struct } => "struct",
            { TypeKind: TypeKind.Interface } => "interface",
            _ => "class",
        };

        // 必须复现类型参数表（否则 CS0693），但约束子句只需在任一 partial 上声明一次，故省略 where。
        var typeParameterList = type.TypeParameters.Length > 0
            ? "<" + string.Join(", ", type.TypeParameters.Select(parameter => parameter.Name)) + ">"
            : string.Empty;

        return $"{GetAccessibilityKeyword(type.DeclaredAccessibility)} partial {keyword} {type.Name}{typeParameterList}";
    }

    /// <summary>
    /// 判断该 Id 是否带有（或本次生成即将产出）指定的内嵌转换器类型。
    /// </summary>
    private static bool WillHaveNestedType(ITypeSymbol type, string nestedTypeName)
        => type is INamedTypeSymbol namedType
            && (namedType.GetTypeMembers(nestedTypeName).Length > 0
                || !namedType.Interfaces.Any(IsStronglyTypedIdInterface));

    /// <summary>
    /// 把基元类型的显示名归一化为不含命名空间限定的短名。
    /// </summary>
    internal static string GetPrimitiveIdTypeShortName(string primitiveIdTypeName)
    {
        var separatorIndex = primitiveIdTypeName.LastIndexOf('.');

        return separatorIndex < 0 ? primitiveIdTypeName : primitiveIdTypeName[(separatorIndex + 1)..];
    }

    /// <summary>
    /// 把类型显示名净化成合法的 hint 名：剔除尖括号、逗号与空格。
    /// </summary>
    private static string SanitizeHintName(string fullName)
        => fullName
            .Replace("<", string.Empty)
            .Replace(">", string.Empty)
            .Replace(",", string.Empty)
            .Replace(" ", string.Empty);

    /// <summary>
    /// 把符号的可访问性渲染成 C# 关键字。
    /// </summary>
    private static string GetAccessibilityKeyword(Accessibility accessibility)
        => accessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            Accessibility.Protected => "protected",
            Accessibility.ProtectedOrInternal => "protected internal",
            Accessibility.ProtectedAndInternal => "private protected",
            _ => "private",
        };

    /// <summary>
    /// 判断给定类型是否实现了指定的 BCL 接口。
    /// </summary>
    private static bool ImplementsInterface(ITypeSymbol type, string interfaceName)
        => type.AllInterfaces.Any(@interface => @interface.Name == interfaceName
            && @interface.ContainingNamespace.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == SystemNamespace);

    /// <summary>
    /// 判断给定类型是否暴露了公开的静态 <c>TryParse(ReadOnlySpan&lt;TElement&gt;, IFormatProvider, out T)</c> 重载。
    /// </summary>
    internal static bool HasPublicStaticTryParse(ITypeSymbol type, SpecialType spanElementType)
        => type.GetMembers("TryParse").Any(member =>
            member is IMethodSymbol { IsStatic: true, DeclaredAccessibility: Accessibility.Public, Parameters.Length: 3 } method
            && method.Parameters[0].Type is INamedTypeSymbol spanType
            && spanType.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.ReadOnlySpan<T>"
            && spanType.TypeArguments.Length == 1
            && spanType.TypeArguments[0].SpecialType == spanElementType
            && method.Parameters[1].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.IFormatProvider"
            && method.Parameters[2].RefKind == RefKind.Out);

    /// <summary>
    /// 判定基元 <c>TryFormat</c> 的直呼形态，用于避免值类型基元在格式化时装箱。
    /// </summary>
    /// <param name="type">基元类型。</param>
    /// <param name="spanElementType">目标缓冲区元素类型：UTF-16 为 <c>char</c>，UTF-8 为 <c>byte</c>。</param>
    internal static TryFormatKind GetTryFormatKind(ITypeSymbol type, SpecialType spanElementType)
    {
        var hasProviderOverload = false;
        var hasPlainOverload = false;

        foreach (var member in type.GetMembers("TryFormat"))
        {
            if (member is not IMethodSymbol { IsStatic: false, DeclaredAccessibility: Accessibility.Public, Parameters.Length: 3 or 4 } method
                || method.Parameters[0].Type is not INamedTypeSymbol spanType
                || spanType.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) != "global::System.Span<T>"
                || spanType.TypeArguments.Length != 1
                || spanType.TypeArguments[0].SpecialType != spanElementType
                || method.Parameters[1].RefKind != RefKind.Out
                || method.Parameters[2].Type is not INamedTypeSymbol formatType
                || formatType.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) != "global::System.ReadOnlySpan<T>"
                || formatType.TypeArguments.Length != 1
                || formatType.TypeArguments[0].SpecialType != SpecialType.System_Char)
            {
                continue;
            }

            if (method.Parameters.Length == 4)
            {
                hasProviderOverload = true;
            }
            else
            {
                hasPlainOverload = true;
            }
        }

        return hasProviderOverload
            ? TryFormatKind.Direct
            : hasPlainOverload
                ? TryFormatKind.DirectWithoutProvider
                : TryFormatKind.Interface;
    }

    /// <summary>
    /// 把前缀归一化：空串与纯空白一律视为「未设置」（<see langword="null"/>）。
    /// </summary>
    private static string? NormalizePrefix(string? prefix)
        => string.IsNullOrWhiteSpace(prefix) ? null : prefix;
}
