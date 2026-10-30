namespace Len.StronglyTypedId.SampleGraphQL;

/// <summary>
/// 示例用的 Id 声明。覆盖三种表示口径：文本（Guid）、整数（int）、枚举（按底层数值）。
/// </summary>
[StronglyTypedId]
public partial record struct OrderId(Guid Value);

/// <summary>
/// 整数基元：GraphQL 里是 <c>Int</c>，与 JSON 形态一致。
/// </summary>
[StronglyTypedId]
public partial record struct LineId(int Value);

/// <summary>
/// 枚举基元：按底层整数写出，不按成员名。
/// </summary>
[StronglyTypedId]
public partial record struct StatusId(OrderStatus Value);

/// <summary>
/// 带前缀：对外是规范文本形，前缀贯穿 GraphQL 的输入与输出。
/// </summary>
[StronglyTypedId(Prefix = "sku_")]
public partial record struct Sku(string Value);

/// <summary>
/// 订单状态。
/// </summary>
public enum OrderStatus
{
    Pending = 0,
    Paid = 1,
    Shipped = 2,
}

/// <summary>
/// 示例领域对象。
/// </summary>
internal sealed class Order
{
    public OrderId Id { get; init; }

    public LineId Line { get; init; }

    public StatusId Status { get; init; }

    public Sku Code { get; init; }
}
