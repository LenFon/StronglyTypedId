namespace Len.StronglyTypedId.GraphQL;

/// <summary>
/// 端到端用例专用的 Id 声明：集成生成器按「本编译声明的 <c>[StronglyTypedId]</c> + 本编译的包引用」门控，
/// 故本工程必须自己声明至少一个 Id。
/// </summary>
[StronglyTypedId]
public partial record struct OrderId(Guid Value);

/// <summary>
/// 整数基元：验证 GraphQL 侧按数字而非字符串写出。
/// </summary>
[StronglyTypedId]
public partial record struct ProductId(int Value);

/// <summary>
/// 枚举基元：验证按底层整数（与 STJ 默认口径一致）而非成员名写出。
/// </summary>
[StronglyTypedId]
public partial record struct StatusId(OrderStatus Value);

/// <summary>
/// 带前缀：验证规范文本形（含前缀）贯穿 GraphQL 的输入与输出。
/// </summary>
[StronglyTypedId(Prefix = "ord_")]
public partial record struct PrefixedOrderId(Guid Value);

/// <summary>
/// 带验证器：验证拒绝值时报错，而不是让 <c>ArgumentException</c> 穿透。
/// </summary>
[StronglyTypedId(Validator = nameof(Validate))]
public partial record struct ValidatedOrderId(Guid Value)
{
    private static bool Validate(Guid value) => value != Guid.Empty;
}

/// <summary>
/// 枚举基元的成员集合。
/// </summary>
public enum OrderStatus
{
    Pending = 0,
    Paid = 1,
}
