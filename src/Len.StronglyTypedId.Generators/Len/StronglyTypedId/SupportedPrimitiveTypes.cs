namespace Len.StronglyTypedId;

/// <summary>
/// 源码生成器与诊断分析器共同认可的强类型 Id 基元类型集合。
/// </summary>
/// <remarks>
/// <para>
/// 两处判定必须保持一致：分析器负责在编辑期报错，生成器负责在编译期决定是否产出代码，
/// 因此集合集中在此，避免各自维护一份而产生漂移。
/// </para>
/// <para>
/// 判据同时校验<b>简单名</b>与<b>所属命名空间</b>。只比简单名会让使用者自定义的
/// <c>X.Guid</c>、<c>X.Int32</c> 之类同名类型被当成 BCL 基元类型：分析器不再报 STIA0008，
/// 生成器却把它们嵌进生成代码，最终在使用者项目里报出若干条 CS0315（该类型不满足
/// <c>IComparable</c> 等泛型约束），错误指向生成文件而非使用者自己的源码。
/// </para>
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
    /// <remarks>
    /// <c>BigInteger</c> 是唯一不在 <c>System</c> 下的受支持基元，单独按其命名空间登记，
    /// 以免把 <c>System</c> 命名空间的判据放宽到整个 BCL。
    /// </remarks>
    private const string NumericsNamespace = "System.Numerics";

    /// <summary>
    /// 受支持基元类型的登记表，顺序即 README「Supported primitive types」表的行序。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 这张表是<b>唯一</b>的事实来源：<see cref="IsSupported"/> 由它构建匹配集合，README 的基元表由
    /// <c>tools/Len.StronglyTypedId.DocGen</c> 从它生成。新增一个基元只改这一处，判定与文档不可能再各说一套。
    /// </para>
    /// <para>
    /// 类型名一律用 <c>nameof</c>（netstandard2.0 里存在的那些）或字符串字面量（.NET 6+ 才有的那些，
    /// 生成器目标框架里解析不到符号）。<see cref="IsSupported"/> 本就按「类型名 + 命名空间」匹配，
    /// 不依赖编译器解析出该类型符号，因此两者语义完全等价。
    /// </para>
    /// </remarks>
    internal static readonly ImmutableArray<PrimitiveTypeEntry> DocumentedTypes = ImmutableArray.Create(
        new PrimitiveTypeEntry("Guid", nameof(Guid), PrimitiveNamespace, false),
        // 唯一的引用类型基元：承载它的 Id 只能是 record（不能是 record struct）。
        new PrimitiveTypeEntry("string", nameof(String), PrimitiveNamespace, true),
        // 整型基元。
        new PrimitiveTypeEntry("byte", nameof(Byte), PrimitiveNamespace, false),
        new PrimitiveTypeEntry("sbyte", nameof(SByte), PrimitiveNamespace, false),
        new PrimitiveTypeEntry("short", nameof(Int16), PrimitiveNamespace, false),
        new PrimitiveTypeEntry("ushort", nameof(UInt16), PrimitiveNamespace, false),
        new PrimitiveTypeEntry("int", nameof(Int32), PrimitiveNamespace, false),
        new PrimitiveTypeEntry("uint", nameof(UInt32), PrimitiveNamespace, false),
        new PrimitiveTypeEntry("long", nameof(Int64), PrimitiveNamespace, false),
        new PrimitiveTypeEntry("ulong", nameof(UInt64), PrimitiveNamespace, false),
        // 扩展整型基元（.NET 7+）：实现 IParsable / IComparable / IFormattable / ISpanFormattable，
        // 与现有整型基元完全同构，能力判定无需任何特判。
        new PrimitiveTypeEntry("Int128", "Int128", PrimitiveNamespace, false),
        new PrimitiveTypeEntry("UInt128", "UInt128", PrimitiveNamespace, false),
        new PrimitiveTypeEntry("decimal", nameof(Decimal), PrimitiveNamespace, false),
        // 浮点基元：float / double 实现 IParsable / ISpanParsable / IComparable / IFormattable /
        // ISpanFormattable / IUtf8SpanParsable / IUtf8SpanFormattable，能力面与整型同构。
        // Half 是 16 位半精度浮点（.NET 7+），接口面与 float / double 一致，一并登记。
        new PrimitiveTypeEntry("Half", "Half", PrimitiveNamespace, false),
        new PrimitiveTypeEntry("float", nameof(Single), PrimitiveNamespace, false),
        new PrimitiveTypeEntry("double", nameof(Double), PrimitiveNamespace, false),
        // 时间型基元：DateTime / DateTimeOffset / TimeSpan 均实现 IParsable、IComparable、IFormattable
        // 与 ISpanFormattable，生成逻辑（TryParse 经 ISpanParsable、格式化成员按符号判定）对它们天然适配。
        new PrimitiveTypeEntry("DateTime", nameof(DateTime), PrimitiveNamespace, false),
        new PrimitiveTypeEntry("DateTimeOffset", nameof(DateTimeOffset), PrimitiveNamespace, false),
        new PrimitiveTypeEntry("TimeSpan", nameof(TimeSpan), PrimitiveNamespace, false),
        // .NET 6+ 的日期 / 时间基元：实现 IParsable / IComparable / IFormattable，与 DateTime 同构。
        new PrimitiveTypeEntry("DateOnly", "DateOnly", PrimitiveNamespace, false),
        new PrimitiveTypeEntry("TimeOnly", "TimeOnly", PrimitiveNamespace, false),
        // 引用类型之外的唯一一个「非 System 命名空间」基元：实现 IParsable / IComparable /
        // IFormattable / ISpanFormattable，能力面与整型同构，只是命名空间为 System.Numerics。
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
    /// 非具名类型（数组、指针、类型参数等）不是 <see cref="INamedTypeSymbol"/>，直接判为不支持，
    /// 因此此处不会走到 <see cref="ISymbol.ContainingNamespace"/> 上。
    /// <para>
    /// 枚举（<see cref="TypeKind.Enum"/>）不受命名空间与简单名约束：枚举可能定义在任意命名空间，
    /// 且一律派生自 <c>System.Enum</c>（具备 <c>IComparable</c> / <c>IFormattable</c>），其解析走
    /// <c>Enum.TryParse&lt;TEnum&gt;</c>，与「按符号判定能力」的设计一致，故无条件支持。
    /// </para>
    /// <para>
    /// 命名空间必须与类型名成对校验：<c>System</c> 下的 <see cref="_typeNames"/>，以及 <c>System.Numerics</c>
    /// 下的 <see cref="_numericsTypeNames"/>（当前仅 <c>BigInteger</c>）。
    /// </para>
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
    /// <remarks>
    /// <see cref="CSharpName"/> 与 <see cref="ClrName"/> 分开记录：判定按元数据里的 CLR 名（<c>Int32</c>），
    /// 文档按 C# 关键字（<c>int</c>）。两者只有别名关系，合在一起就只能二选一。
    /// </remarks>
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
