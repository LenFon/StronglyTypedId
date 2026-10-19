namespace Len.StronglyTypedId;

/// <summary>
/// 基元 <c>TryFormat</c> 的直呼形态，决定生成代码如何调用它。
/// </summary>
/// <remarks>
/// 值类型基元经接口调用 <c>TryFormat</c> 会装箱一次；基元暴露公开实例重载时可直呼省掉装箱。
/// 区分是否带 <c>IFormatProvider</c>，是因为 <c>Guid</c> 把 4 参重载做成显式接口实现，直呼会 CS1501。
/// </remarks>
internal enum TryFormatKind
{
    /// <summary>
    /// 只能经接口调用，值类型会装箱一次。仅在基元既无 4 参也无 3 参公开重载时使用。
    /// </summary>
    Interface,

    /// <summary>
    /// 存在公开的 4 参重载（含 <c>IFormatProvider</c>），按具体类型直呼。
    /// </summary>
    Direct,

    /// <summary>
    /// 只存在公开的 3 参重载（无 <c>IFormatProvider</c>），按具体类型直呼并省略 provider。
    /// </summary>
    DirectWithoutProvider,
}
