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
    public static bool IsSupported(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol namedType)
        {
            return false;
        }

        if (namedType.TypeKind == TypeKind.Enum)
        {
            return true;
        }

        var containingNamespace = namedType.ContainingNamespace.ToDisplayString();

        return (containingNamespace == PrimitiveNamespace && _typeNames.Contains(namedType.Name))
            || (containingNamespace == NumericsNamespace && _numericsTypeNames.Contains(namedType.Name));
    }

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
