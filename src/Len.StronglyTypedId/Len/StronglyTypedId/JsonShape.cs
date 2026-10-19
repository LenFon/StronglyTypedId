namespace Len.StronglyTypedId;

/// <summary>
/// 指定强类型 Id 在 JSON 中的序列化形态。
/// </summary>
/// <remarks>
/// <see cref="Primitive"/> keeps the wire contract unchanged: the id occupies the same slot and shape as the
/// primitive it wraps. <see cref="Object"/> exists for contracts that already promise <c>{ "value": … }</c>.
/// </remarks>
public enum JsonShape
{
    /// <summary>
    /// 序列化为被包装的基元值本身，例如 <c>"3f2c…"</c> 或 <c>42</c>。这是默认值。
    /// </summary>
    Primitive = 0,

    /// <summary>
    /// 序列化为单属性对象 <c>{ "value": … }</c>，属性名固定为 <c>value</c>。
    /// </summary>
    /// <remarks>
    /// The property name is fixed and ignores <c>JsonSerializerOptions.PropertyNamingPolicy</c>: the JSON shape is
    /// a wire contract and must not drift with the host application's serializer settings.
    /// </remarks>
    Object = 1,
}
