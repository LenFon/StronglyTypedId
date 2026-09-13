namespace Len.StronglyTypedId;

/// <summary>
/// 源码生成器与诊断分析器共同认可的强类型 Id 基元类型集合。
/// </summary>
/// <remarks>
/// 两处判定必须保持一致：分析器负责在编辑期报错，生成器负责在编译期决定是否产出代码，
/// 因此集合集中在此，避免各自维护一份而产生漂移。
/// </remarks>
internal static class SupportedPrimitiveTypes
{
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
    /// 判断给定的类型简单名是否为受支持的强类型 Id 基元类型。
    /// </summary>
    public static bool IsSupported(string typeName) => _typeNames.Contains(typeName);
}
