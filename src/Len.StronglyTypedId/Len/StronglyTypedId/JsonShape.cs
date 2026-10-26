namespace Len.StronglyTypedId;

/// <summary>
/// 指定强类型 Id 在 JSON 中的形态。
/// </summary>
public enum JsonShape
{
    /// <summary>
    /// 直接序列化为所包装的基元值，例如 <c>"3f2c…"</c> 或 <c>42</c>。默认值。
    /// </summary>
    Primitive = 0,

    /// <summary>
    /// 序列化为单属性对象 <c>{ "value": … }</c>，属性名为 <c>value</c>。
    /// </summary>
    Object = 1,
}
