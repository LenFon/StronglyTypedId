namespace Len.StronglyTypedId;

/// <summary>
/// 指定强类型 Id 在 JSON 中的序列化形态。
/// </summary>
public enum JsonShape
{
    /// <summary>
    /// 序列化为被包装的基元值本身，例如 <c>"3f2c…"</c> 或 <c>42</c>。这是默认值。
    /// </summary>
    Primitive = 0,

    /// <summary>
    /// 序列化为单属性对象 <c>{ "value": … }</c>，属性名固定为 <c>value</c>。
    /// </summary>
    Object = 1,
}
