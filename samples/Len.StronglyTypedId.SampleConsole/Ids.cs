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

// 归一化 + 校验：先修正取值，再判合法性，最后构造。
[StronglyTypedId(Normalizer = nameof(Normalize), Validator = nameof(IsEmail))]
public partial record struct EmailAddress(string Value)
{
    private static string Normalize(string value) => value.Trim().ToLowerInvariant();

    private static bool IsEmail(string value) => value.Contains('@');
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

// N2：命名实例。值以基元的不变文化文本声明，编译期校验并翻译成字面量，运行期一律经 Create 构造 ——
// 因此实例必然满足验证器，Parse(id.ToString()) 一定读得回来。
[StronglyTypedId]
[StronglyTypedIdInstance("Unspecified", "-1")]
[StronglyTypedIdInstance("Anonymous", "0")]
public partial record struct TenantId(int Value);

// N3：验证失败抛自定义异常。只影响 Create 的验证器拒绝分支；Parse 的失败仍是 FormatException。
public sealed class StockLevelOutOfRangeException(string message) : Exception(message);

[StronglyTypedId(Validator = nameof(IsPositive), ExceptionType = typeof(StockLevelOutOfRangeException))]
public partial record struct StockLevelId(int Value)
{
    private static bool IsPositive(int value) => value > 0;
}

// N2 的反例：实例值被验证器拒绝时，类型初始化即失败 —— 宁可第一次触碰就炸，
// 也不让一个「不可能存在」的 Id 悄悄流进领域模型。
[StronglyTypedId(Validator = nameof(IsPositive))]
[StronglyTypedIdInstance("Zero", "0")]
public partial record struct RejectedInstanceId(int Value)
{
    private static bool IsPositive(int value) => value > 0;
}

// [Flags] 枚举基元：位组合本身就是合法取值，含未定义位的数值（如 999）被拒。
[Flags]
public enum Permission
{
    None = 0,
    Read = 1,
    Write = 2,
    Delete = 4,
}

[StronglyTypedId]
public partial record struct PermissionId(Permission Value);

// N10：集成过滤。None 只关集成产物，核心能力（Create / Parse / 比较 / 格式化）不受影响。
[StronglyTypedId(Integrations = StronglyTypedIdIntegrations.None)]
public partial record struct PlainGuidId(Guid Value);

// 文化敏感基元：无参 ToString 与 Parse 恒按不变文化，与当前区域无关。
[StronglyTypedId]
public partial record struct PriceId(decimal Value);

// N3 的隐私开关：验证器拒绝时不把被拒的原文写进异常消息。
// 对 string 基元尤其重要 —— 那段文本往往就是用户输入的邮箱、手机号或 token 片段，
// 而被拒绝的原因恰恰是它不合规，却仍然是个人信息。默认关闭（消息里带原文）。
[StronglyTypedId(Validator = nameof(IsEmail), RedactValidationMessage = true)]
public partial record struct RedactedEmailId(string Value)
{
    private static bool IsEmail(string value) => value.Contains('@');
}
