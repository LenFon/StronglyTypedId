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

    private static readonly ImmutableHashSet<string> _typeNames = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        nameof(Guid),
        nameof(String),
        nameof(Byte),
        nameof(SByte),
        nameof(Int16),
        nameof(Int32),
        nameof(Int64),
        nameof(UInt16),
        nameof(UInt32),
        nameof(UInt64));

    /// <summary>
    /// 判断给定类型是否为受支持的强类型 Id 基元类型。
    /// </summary>
    /// <remarks>
    /// 非具名类型（数组、指针、类型参数等）不是 <see cref="INamedTypeSymbol"/>，直接判为不支持，
    /// 因此此处不会走到 <see cref="ISymbol.ContainingNamespace"/> 上。
    /// </remarks>
    public static bool IsSupported(ITypeSymbol type)
        => type is INamedTypeSymbol namedType
            && namedType.ContainingNamespace.ToDisplayString() == PrimitiveNamespace
            && _typeNames.Contains(namedType.Name);
}
