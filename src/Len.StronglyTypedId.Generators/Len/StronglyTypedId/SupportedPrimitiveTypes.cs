namespace Len.StronglyTypedId;

/// <summary>
/// 源码生成器与诊断分析器共同认可的强类型 Id 基元类型集合。
/// </summary>
internal static class SupportedPrimitiveTypes
{
    /// <summary>
    /// BCL 基元类型所属的命名空间。
    /// </summary>
    private const string PrimitiveNamespace = "System";

    /// <summary>
    /// 少数基元类型所在的次级命名空间（<c>System.Numerics</c>）。
    /// </summary>
    private const string NumericsNamespace = "System.Numerics";

    /// <summary>
    /// BCL 接口所属命名空间的显示名（含 <c>global::</c> 前缀）。
    /// </summary>
    private const string SystemNamespace = "global::System";

    /// <summary>
    /// <c>System.IFormatProvider</c> 的显示名（含 <c>global::</c> 前缀）。
    /// </summary>
    private const string FormatProviderName = "global::System.IFormatProvider";

    /// <summary>
    /// <c>System.IParsable&lt;TSelf&gt;</c> 的简单名。
    /// </summary>
    private const string ParsableInterfaceName = "IParsable";

    /// <summary>
    /// <c>System.IComparable&lt;TSelf&gt;</c> 的简单名。
    /// </summary>
    private const string ComparableInterfaceName = "IComparable";

    /// <summary>
    /// <c>System.Uri</c> 的显示名：它满足自定义基元契约，但相等语义与 Id 契约冲突，故显式排除。
    /// </summary>
    private const string UriTypeName = "System.Uri";

    /// <summary>
    /// 受支持基元类型的登记表，顺序即 README「Supported primitive types」表的行序。
    /// </summary>
    internal static readonly ImmutableArray<PrimitiveTypeEntry> DocumentedTypes = ImmutableArray.Create(
        new PrimitiveTypeEntry("Guid", nameof(Guid), PrimitiveNamespace, false),
        new PrimitiveTypeEntry("string", nameof(String), PrimitiveNamespace, true),
        // 字符基元：CLR 里是 16 位无符号整数，接口面与整型同构，但 IParsable / IFormattable 的成员
        // 全是显式接口实现，按类型名直呼会 CS1501，故生成器为它单列分支（见 StronglyTypedIdInfo.IsCharPrimitive）。
        new PrimitiveTypeEntry("char", nameof(Char), PrimitiveNamespace, false),
        new PrimitiveTypeEntry("byte", nameof(Byte), PrimitiveNamespace, false),
        new PrimitiveTypeEntry("sbyte", nameof(SByte), PrimitiveNamespace, false),
        new PrimitiveTypeEntry("short", nameof(Int16), PrimitiveNamespace, false),
        new PrimitiveTypeEntry("ushort", nameof(UInt16), PrimitiveNamespace, false),
        new PrimitiveTypeEntry("int", nameof(Int32), PrimitiveNamespace, false),
        new PrimitiveTypeEntry("uint", nameof(UInt32), PrimitiveNamespace, false),
        new PrimitiveTypeEntry("long", nameof(Int64), PrimitiveNamespace, false),
        new PrimitiveTypeEntry("ulong", nameof(UInt64), PrimitiveNamespace, false),
        new PrimitiveTypeEntry("Int128", "Int128", PrimitiveNamespace, false),
        new PrimitiveTypeEntry("UInt128", "UInt128", PrimitiveNamespace, false),
        new PrimitiveTypeEntry("decimal", nameof(Decimal), PrimitiveNamespace, false),
        new PrimitiveTypeEntry("Half", "Half", PrimitiveNamespace, false),
        new PrimitiveTypeEntry("float", nameof(Single), PrimitiveNamespace, false),
        new PrimitiveTypeEntry("double", nameof(Double), PrimitiveNamespace, false),
        new PrimitiveTypeEntry("DateTime", nameof(DateTime), PrimitiveNamespace, false),
        new PrimitiveTypeEntry("DateTimeOffset", nameof(DateTimeOffset), PrimitiveNamespace, false),
        new PrimitiveTypeEntry("TimeSpan", nameof(TimeSpan), PrimitiveNamespace, false),
        new PrimitiveTypeEntry("DateOnly", "DateOnly", PrimitiveNamespace, false),
        new PrimitiveTypeEntry("TimeOnly", "TimeOnly", PrimitiveNamespace, false),
        new PrimitiveTypeEntry("BigInteger", "BigInteger", NumericsNamespace, false));

    /// <summary>
    /// <c>System</c> 命名空间下受支持的基元类型名集合（由 <see cref="DocumentedTypes"/> 派生）。
    /// </summary>
    private static readonly ImmutableHashSet<string> _typeNames = ImmutableHashSet.CreateRange(
        StringComparer.Ordinal,
        DocumentedTypes.Where(static type => type.Namespace == PrimitiveNamespace).Select(static type => type.ClrName));

    /// <summary>
    /// <c>System.Numerics</c> 命名空间下受支持的基元类型名集合（由 <see cref="DocumentedTypes"/> 派生）。
    /// </summary>
    private static readonly ImmutableHashSet<string> _numericsTypeNames = ImmutableHashSet.CreateRange(
        StringComparer.Ordinal,
        DocumentedTypes.Where(static type => type.Namespace == NumericsNamespace).Select(static type => type.ClrName));

    /// <summary>
    /// 判断给定类型是否为受支持的强类型 Id 基元类型。
    /// </summary>
    /// <remarks>
    /// 三段式，顺序即优先级：内置白名单 → 枚举 → 自定义基元契约。前两段与加入自定义基元之前逐字一致，
    /// 因此 24 种内置基元与枚举的产物不受影响。
    /// </remarks>
    public static bool IsSupported(ITypeSymbol type)
        => type is INamedTypeSymbol namedType
            && (namedType.TypeKind == TypeKind.Enum || IsBuiltIn(namedType) || IsCustomPrimitive(namedType));

    /// <summary>
    /// 判断给定类型是否落在内置白名单里（<c>System</c> / <c>System.Numerics</c> 下的 24 种基元）。
    /// </summary>
    internal static bool IsBuiltIn(INamedTypeSymbol namedType)
    {
        var containingNamespace = namedType.ContainingNamespace.ToDisplayString();

        return (containingNamespace == PrimitiveNamespace && _typeNames.Contains(namedType.Name))
            || (containingNamespace == NumericsNamespace && _numericsTypeNames.Contains(namedType.Name));
    }

    /// <summary>
    /// 判断给定类型是否满足自定义基元契约：实现 <c>IParsable&lt;TSelf&gt;</c>、<c>IComparable&lt;TSelf&gt;</c>
    /// 与非泛型 <c>IComparable</c>，并暴露可公开调用的静态 <c>Parse</c> / <c>TryParse</c>。
    /// </summary>
    /// <remarks>
    /// 内置基元与枚举一律返回 <see langword="false"/>：它们由前两段判定接管，产物走各自既有的分支。
    /// 要求「类型实参就是自身」是为了排除 <c>class MyEmail : Email</c> —— 它的 <c>AllInterfaces</c> 里是
    /// <c>IParsable&lt;Email&gt;</c>，按它生成会让 <c>MyEmail.Parse</c> 返回 <c>Email</c> 而编译不过。
    /// <para>
    /// 非泛型 <c>IComparable</c> 是硬要求而非可选：<c>IStronglyTypedId&lt;TSelf, TPrimitiveId&gt;</c> 的约束就是
    /// <c>where TPrimitiveId : notnull, IComparable</c>，而 <c>IComparable&lt;T&gt;</c> 并不蕴含它 —— 只实现泛型版本
    /// 的类型无法作为 <c>TPrimitiveId</c>（CS0315）。泛型版本另行要求，是为了让生成的 <c>CompareTo</c> 不装箱。
    /// </para>
    /// <para>
    /// 要求公开静态成员是因为 <c>IParsable&lt;T&gt;</c> 允许显式接口实现（<c>static Email IParsable&lt;Email&gt;.Parse(…)</c>），
    /// 此时按类型名直呼会 CS1501。本库已有先例：<c>BigInteger</c> 的 <c>IUtf8SpanParsable.TryParse</c> 就是显式实现。
    /// </para>
    /// </remarks>
    internal static bool IsCustomPrimitive(ITypeSymbol type)
        => type is INamedTypeSymbol namedType
            && namedType.TypeKind is TypeKind.Struct or TypeKind.Class
            && !namedType.IsAbstract
            && !namedType.IsStatic
            && namedType.TypeParameters.Length == 0
            && !IsNullable(namedType)
            && !IsSystemUri(namedType)
            && !IsBuiltIn(namedType)
            && ImplementsSelfReferentialInterface(namedType, ParsableInterfaceName)
            && ImplementsSelfReferentialInterface(namedType, ComparableInterfaceName)
            && ImplementsNonGenericComparable(namedType)
            && HasPublicStringParse(namedType)
            && HasPublicStringTryParse(namedType);

    /// <summary>
    /// 描述该类型不满足自定义基元契约的原因，供 <c>STIA0008</c> 的消息使用。
    /// </summary>
    /// <remarks>
    /// 返回一句以大写字母开头、以句号结尾的英文短句；类型本身合法时返回空串。
    /// <c>System.Uri</c> 单独给一句解释：它在 .NET 7+ 实现了 <c>IParsable&lt;Uri&gt;</c>，放开契约后会「自动」变成合法基元，
    /// 而它的相等语义（忽略 fragment、归一化大小写与默认端口）会让两个不同的 URI 塌缩成同一个 Id。
    /// </remarks>
    internal static string DescribeMissingContracts(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol namedType)
        {
            return string.Empty;
        }

        if (IsSystemUri(namedType))
        {
            return "System.Uri is not supported: its equality ignores the fragment and normalizes case and the default port, so two different URIs would compare equal.";
        }

        if (namedType.TypeKind is not (TypeKind.Struct or TypeKind.Class))
        {
            return "It is not a struct or class.";
        }

        if (namedType.IsAbstract || namedType.IsStatic)
        {
            return "It is abstract or static.";
        }

        if (namedType.TypeParameters.Length > 0)
        {
            return "It is an open generic type.";
        }

        if (IsNullable(namedType))
        {
            return "It is Nullable<T>.";
        }

        var missing = new List<string>(4);

        if (!ImplementsSelfReferentialInterface(namedType, ParsableInterfaceName))
        {
            missing.Add($"does not implement `IParsable<{namedType.Name}>`");
        }

        if (!ImplementsSelfReferentialInterface(namedType, ComparableInterfaceName))
        {
            missing.Add($"does not implement `IComparable<{namedType.Name}>`");
        }

        if (!ImplementsNonGenericComparable(namedType))
        {
            missing.Add("does not implement non-generic `System.IComparable`");
        }

        if (!HasPublicStringParse(namedType))
        {
            missing.Add($"does not expose `public static {namedType.Name} Parse(string, IFormatProvider)`");
        }

        if (!HasPublicStringTryParse(namedType))
        {
            missing.Add($"does not expose `public static bool TryParse(string, IFormatProvider, out {namedType.Name})`");
        }

        return missing.Count == 0 ? string.Empty : "It " + string.Join(", ", missing) + ".";
    }

    /// <summary>
    /// 判断类型是否实现了指定的一元 BCL 接口，且类型实参就是自身。
    /// </summary>
    private static bool ImplementsSelfReferentialInterface(INamedTypeSymbol type, string interfaceName)
        => type.AllInterfaces.Any(@interface =>
            @interface.Name == interfaceName
            && @interface.ContainingNamespace.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == SystemNamespace
            && @interface.TypeArguments.Length == 1
            && SymbolEqualityComparer.Default.Equals(@interface.TypeArguments[0], type));

    /// <summary>
    /// 判断类型是否实现了非泛型 <c>System.IComparable</c>。
    /// </summary>
    /// <remarks>
    /// 这是 <c>IStronglyTypedId&lt;TSelf, TPrimitiveId&gt;</c> 对 <c>TPrimitiveId</c> 的实际约束；
    /// <c>IComparable&lt;T&gt;</c> 不蕴含它，两者必须分别检查。
    /// </remarks>
    private static bool ImplementsNonGenericComparable(INamedTypeSymbol type)
        => type.AllInterfaces.Any(@interface =>
            @interface.Name == ComparableInterfaceName
            && @interface.TypeArguments.Length == 0
            && @interface.ContainingNamespace.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == SystemNamespace);

    /// <summary>
    /// 判断类型是否暴露公开的静态 <c>Parse(string, IFormatProvider)</c>，且返回自身。
    /// </summary>
    private static bool HasPublicStringParse(INamedTypeSymbol type)
        => type.GetMembers("Parse").Any(member =>
            member is IMethodSymbol { IsStatic: true, DeclaredAccessibility: Accessibility.Public, Parameters.Length: 2 } method
            && method.Parameters[0].Type.SpecialType == SpecialType.System_String
            && IsFormatProvider(method.Parameters[1].Type)
            && SymbolEqualityComparer.Default.Equals(method.ReturnType, type));

    /// <summary>
    /// 判断类型是否暴露公开的静态 <c>TryParse(string, IFormatProvider, out TSelf)</c>。
    /// </summary>
    private static bool HasPublicStringTryParse(INamedTypeSymbol type)
        => type.GetMembers("TryParse").Any(member =>
            member is IMethodSymbol { IsStatic: true, DeclaredAccessibility: Accessibility.Public, Parameters.Length: 3 } method
            && method.Parameters[0].Type.SpecialType == SpecialType.System_String
            && IsFormatProvider(method.Parameters[1].Type)
            && method.Parameters[2].RefKind == RefKind.Out
            && SymbolEqualityComparer.Default.Equals(method.Parameters[2].Type, type)
            && method.ReturnType.SpecialType == SpecialType.System_Boolean);

    private static bool IsFormatProvider(ITypeSymbol type)
        => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == FormatProviderName;

    private static bool IsNullable(INamedTypeSymbol type)
        => type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;

    private static bool IsSystemUri(INamedTypeSymbol type)
        => type.ToDisplayString() == UriTypeName;

    /// <summary>
    /// 单个受支持基元类型的登记项。
    /// </summary>
    internal sealed class PrimitiveTypeEntry(string csharpName, string clrName, string @namespace, bool isReferenceType)
    {
        /// <summary>
        /// C# 里书写该类型的关键字或类型名（<c>int</c> / <c>Guid</c>）。
        /// </summary>
        public string CSharpName { get; } = csharpName;

        /// <summary>
        /// 元数据里的类型名，即 <c>IsSupported</c> 用来匹配的名字（<c>Int32</c>）。
        /// </summary>
        public string ClrName { get; } = clrName;

        /// <summary>
        /// 该类型所属的命名空间。
        /// </summary>
        public string Namespace { get; } = @namespace;

        /// <summary>
        /// 是否为引用类型（决定 README 示例用 <c>record</c> 还是 <c>record struct</c>）。
        /// </summary>
        public bool IsReferenceType { get; } = isReferenceType;

        /// <summary>
        /// README 表格「Example declaration」列的示例声明。
        /// </summary>
        public string ExampleDeclaration
            => IsReferenceType
                ? $"record {ClrName}Id({CSharpName} Value)"
                : $"record struct {ClrName}Id({CSharpName} Value)";
    }
}
