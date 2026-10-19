namespace Len.StronglyTypedId;

/// <summary>
/// 源码生成器与诊断分析器共同认可的强类型 Id 基元类型集合。
/// </summary>
/// <remarks>
/// 分析器（编辑期报错）与生成器（编译期决定产出）共用此表，避免两处判定漂移。
/// 判据同时校验简单名与命名空间：只比简单名会把使用者自定义的 <c>X.Guid</c> 当成 BCL 基元，
/// 生成代码后在调用方报 CS0315，错误却指向生成文件。
/// </remarks>
internal static class SupportedPrimitiveTypes
{
    /// <summary>
    /// BCL 基元类型所属的命名空间。
    /// </summary>
    private const string PrimitiveNamespace = "System";

    /// <summary>
    /// 少数基元类型所在的次级命名空间（<c>System.Numerics</c>）。
    /// </summary>
    /// <remarks><c>BigInteger</c> 是唯一不在 <c>System</c> 下的受支持基元，单列以免放宽整体判据。</remarks>
    private const string NumericsNamespace = "System.Numerics";

    /// <summary>
    /// 受支持基元类型的登记表，顺序即 README「Supported primitive types」表的行序。
    /// </summary>
    /// <remarks>
    /// 唯一事实来源：<see cref="IsSupported"/> 与 README 的基元表都由它派生，新增基元只改这一处。
    /// 类型名用 <c>nameof</c> 或字面量均可 —— 匹配按「类型名 + 命名空间」，不依赖符号解析。
    /// </remarks>
    internal static readonly ImmutableArray<PrimitiveTypeEntry> DocumentedTypes = ImmutableArray.Create(
        new PrimitiveTypeEntry("Guid", nameof(Guid), PrimitiveNamespace, false),
        // 唯一的引用类型基元：承载它的 Id 只能是 record（不能是 record struct）。
        new PrimitiveTypeEntry("string", nameof(String), PrimitiveNamespace, true),
        new PrimitiveTypeEntry("byte", nameof(Byte), PrimitiveNamespace, false),
        new PrimitiveTypeEntry("sbyte", nameof(SByte), PrimitiveNamespace, false),
        new PrimitiveTypeEntry("short", nameof(Int16), PrimitiveNamespace, false),
        new PrimitiveTypeEntry("ushort", nameof(UInt16), PrimitiveNamespace, false),
        new PrimitiveTypeEntry("int", nameof(Int32), PrimitiveNamespace, false),
        new PrimitiveTypeEntry("uint", nameof(UInt32), PrimitiveNamespace, false),
        new PrimitiveTypeEntry("long", nameof(Int64), PrimitiveNamespace, false),
        new PrimitiveTypeEntry("ulong", nameof(UInt64), PrimitiveNamespace, false),
        // .NET 7+ 扩展整型：接口面与整型同构。
        new PrimitiveTypeEntry("Int128", "Int128", PrimitiveNamespace, false),
        new PrimitiveTypeEntry("UInt128", "UInt128", PrimitiveNamespace, false),
        new PrimitiveTypeEntry("decimal", nameof(Decimal), PrimitiveNamespace, false),
        // 浮点基元：接口面与整型同构；Half 为 .NET 7+ 的 16 位半精度。
        new PrimitiveTypeEntry("Half", "Half", PrimitiveNamespace, false),
        new PrimitiveTypeEntry("float", nameof(Single), PrimitiveNamespace, false),
        new PrimitiveTypeEntry("double", nameof(Double), PrimitiveNamespace, false),
        // 时间型基元：均实现 IParsable / IComparable / IFormattable / ISpanFormattable。
        new PrimitiveTypeEntry("DateTime", nameof(DateTime), PrimitiveNamespace, false),
        new PrimitiveTypeEntry("DateTimeOffset", nameof(DateTimeOffset), PrimitiveNamespace, false),
        new PrimitiveTypeEntry("TimeSpan", nameof(TimeSpan), PrimitiveNamespace, false),
        // .NET 6+ 日期 / 时间基元，与 DateTime 同构。
        new PrimitiveTypeEntry("DateOnly", "DateOnly", PrimitiveNamespace, false),
        new PrimitiveTypeEntry("TimeOnly", "TimeOnly", PrimitiveNamespace, false),
        // 唯一非 System 命名空间的基元，能力面与整型同构。
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
    /// 非具名类型（数组、指针、类型参数）直接判为不支持。枚举不受命名空间与简单名约束：
    /// 可定义在任意命名空间，解析走 <c>Enum.TryParse&lt;TEnum&gt;</c>，故无条件支持。
    /// </remarks>
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
    /// <remarks><see cref="CSharpName"/> 用于文档（<c>int</c>），<see cref="ClrName"/> 用于元数据匹配（<c>Int32</c>）。</remarks>
    internal sealed class PrimitiveTypeEntry(string csharpName, string clrName, string @namespace, bool isReferenceType)
    {
        /// <summary>C# 里书写该类型的关键字或类型名（<c>int</c> / <c>Guid</c>）。</summary>
        public string CSharpName { get; } = csharpName;

        /// <summary>元数据里的类型名，即 <c>IsSupported</c> 用来匹配的名字（<c>Int32</c>）。</summary>
        public string ClrName { get; } = clrName;

        /// <summary>该类型所属的命名空间。</summary>
        public string Namespace { get; } = @namespace;

        /// <summary>是否为引用类型（决定 README 示例用 <c>record</c> 还是 <c>record struct</c>）。</summary>
        public bool IsReferenceType { get; } = isReferenceType;

        /// <summary>README 表格「Example declaration」列的示例声明。</summary>
        public string ExampleDeclaration
            => IsReferenceType
                ? $"record {ClrName}Id({CSharpName} Value)"
                : $"record struct {ClrName}Id({CSharpName} Value)";
    }
}
