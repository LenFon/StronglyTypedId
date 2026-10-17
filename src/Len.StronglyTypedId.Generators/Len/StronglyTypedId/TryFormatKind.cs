namespace Len.StronglyTypedId;

/// <summary>
/// 基元 <c>TryFormat</c> 的直呼形态，决定生成代码如何调用它。
/// </summary>
/// <remarks>
/// <para>
/// 值类型基元经接口调用（<c>((ISpanFormattable)Value).TryFormat(...)</c>）会把 <c>Value</c> 装箱一次。
/// 只要基元暴露了公开的实例 <c>TryFormat</c> 重载，就可以按具体类型直呼，彻底省掉这次装箱。
/// </para>
/// <para>
/// 之所以还要区分「带 <c>IFormatProvider</c>」与「不带」，是因为 <c>Guid</c> 把带 provider 的重载实现为
/// <b>显式接口实现</b>：按具体类型名调用 4 参重载会 CS1501，只能退到公开的 3 参重载。
/// <c>Guid</c> 的格式化本就与文化无关，省略 provider 语义不变。
/// </para>
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
