namespace Len.StronglyTypedId;

/// <summary>
/// 指定强类型 Id 在 JSON 中的序列化形态。
/// </summary>
/// <remarks>
/// <para>
/// 默认形态是 <see cref="Primitive"/>：Id 与其所包装的基元值在 JSON 里占据同一个位置、呈现同一个形状，
/// 因此引入强类型 Id 不会改变既有 API 契约。这在大多数场景下正是想要的 —— 序列化产物与「还没引入 Id
/// 之前」逐字节一致。
/// </para>
/// <para>
/// 但对于<b>已经对外承诺了对象形态</b>的既有契约（例如历史上手写过 <c>{ "value": "…" }</c> 的 DTO），
/// 默认形态会造成破坏性变更。<see cref="Object"/> 让 Id 序列化成 <c>{ "value": … }</c>，从而在不改动
/// 客户端的前提下完成迁移。
/// </para>
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
    /// 属性名固定而不跟随 <c>JsonSerializerOptions.PropertyNamingPolicy</c>：Id 的 JSON 形状是它对外的
    /// 契约，不应随宿主应用的序列化配置漂移，否则同一份数据在不同服务里会得到不同的键名。
    /// </remarks>
    Object = 1,
}
