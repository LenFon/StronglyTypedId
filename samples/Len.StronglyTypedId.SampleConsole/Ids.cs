namespace Len.StronglyTypedId.SampleConsole;

// 最常见的三种基元：Guid / int / string。
[StronglyTypedId]
public partial record struct OrderId(Guid Value);

[StronglyTypedId]
public partial record struct ProductId(int Value);

[StronglyTypedId]
public partial record struct CustomerId(string Value);

// 验证器：Create / TryCreate / TryParse 共用同一条规则。
[StronglyTypedId(Validator = nameof(IsPositive))]
public partial record struct QuantityId(int Value)
{
    private static bool IsPositive(int value) => value > 0;
}

// 文本前缀：ToString 写出前缀，Parse 读回时剥离；读取默认宽松。
[StronglyTypedId(Prefix = "inv_")]
public partial record struct InvoiceId(long Value);

// 严格前缀：裸值不再被接受。
[StronglyTypedId(Prefix = "sku_", PrefixReadMode = PrefixReadMode.Strict)]
public partial record struct SkuId(string Value);

// JSON 对象形状：{"value":"..."} 而不是裸值。
[StronglyTypedId(JsonShape = JsonShape.Object)]
public partial record struct TicketId(Guid Value);

// TypeConverter：供 TypeDescriptor、配置绑定与 Newtonsoft 字典键使用。
[StronglyTypedId(TypeConverter = true)]
public partial record struct WarehouseId(Guid Value);

// enum 基元。
public enum Region
{
    North,
    South,
    East,
    West,
}

[StronglyTypedId]
public partial record struct RegionId(Region Value);

// 嵌套类型：每一层容器都必须 partial。
public partial class OrderAggregate
{
    [StronglyTypedId]
    public partial record struct AggregateId(Guid Value);
}

// 泛型容器：核心能力齐全，外部集成（EF Core / Swagger / Dapper / MVC / OpenAPI）会跳过。
public partial class Box<T>
{
    [StronglyTypedId]
    public partial record struct BoxedId(Guid Value);
}
