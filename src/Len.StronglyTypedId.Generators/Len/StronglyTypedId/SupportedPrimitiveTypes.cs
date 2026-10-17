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
/// <c>X.Guid</c>、<c>X.Int32</c> 之类同名类型被当成 BCL 基元类型：分析器不再报 STIAO008，
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

    private static readonly ImmutableHashSet<string> _typeNames = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        nameof(Guid),
        nameof(String),
        nameof(Decimal),
        // 整型基元。
        nameof(Byte),
        nameof(SByte),
        nameof(Int16),
        nameof(Int32),
        nameof(Int64),
        nameof(UInt16),
        nameof(UInt32),
        nameof(UInt64),
        // 以下新基元（.NET 6+ / 7+）的类型在 netstandard2.0（生成器目标框架）里不存在，nameof 无法解析，
        // 故一律用字符串字面量。IsSupported 本就按「类型名 + 命名空间」匹配，不依赖编译器解析出该类型符号，
        // 因此字符串字面量与 nameof 在此语义完全等价。
        // 扩展整型基元（.NET 7+）：实现 IParsable / IComparable / IFormattable / ISpanFormattable，
        // 与现有整型基元完全同构，能力判定无需任何特判。
        "Int128",
        "UInt128",
        // 浮点基元：float / double 实现 IParsable / ISpanParsable / IComparable / IFormattable /
        // ISpanFormattable / IUtf8SpanParsable / IUtf8SpanFormattable，能力面与整型同构。
        nameof(Single),
        nameof(Double),
        // 16 位半精度浮点（.NET 7+）：接口面与 float / double 一致，一并登记。
        "Half",
        // 时间型基元：DateTime / DateTimeOffset / TimeSpan 均实现 IParsable、IComparable、IFormattable
        // 与 ISpanFormattable，生成逻辑（TryParse 经 ISpanParsable、格式化成员按符号判定）对它们天然适配。
        nameof(DateTime),
        nameof(DateTimeOffset),
        nameof(TimeSpan),
        // .NET 6+ 的日期 / 时间基元：实现 IParsable / IComparable / IFormattable，与 DateTime 同构。
        "DateOnly",
        "TimeOnly");

    /// <summary>
    /// 位于 <see cref="NumericsNamespace"/> 下、同样受支持的基元类型名。
    /// </summary>
    /// <remarks>
    /// <c>BigInteger</c>（引用类型）实现 <c>IParsable</c> / <c>IComparable</c> / <c>IFormattable</c> /
    /// <c>ISpanFormattable</c>，能力面与整型同构，只是命名空间为 <c>System.Numerics</c>，故单独登记。
    /// </remarks>
    private static readonly ImmutableHashSet<string> _numericsTypeNames = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "BigInteger");

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
    /// 命名空间必须与类型名成对校验：<c>System</c> 下的 <c>_typeNames</c>，以及 <c>System.Numerics</c>
    /// 下的 <c>_numericsTypeNames</c>（当前仅 <c>BigInteger</c>）。
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
}
