using System.Globalization;

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

// N2：命名实例。值以基元的不变文化文本声明，编译期校验并翻译成字面量，运行期一律经 Create 构造，
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

// N2 的反例：实例值被验证器拒绝时，类型初始化即失败，宁可第一次触碰就炸，
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
// 对 string 基元尤其重要，那段文本往往就是用户输入的邮箱、手机号或 token 片段，
// 而被拒绝的原因恰恰是它不合规，却仍然是个人信息。默认关闭（消息里带原文）。
[StronglyTypedId(Validator = nameof(IsEmail), RedactValidationMessage = true)]
public partial record struct RedactedEmailId(string Value)
{
    private static bool IsEmail(string value) => value.Contains('@');
}

// 引用类型 Id（record class）：可以为 null，default 是空引用而不是全零值。
// 比较运算符把 null 视为最小，JSON 的 null 也能往返。
[StronglyTypedId]
public partial record SessionId(Guid Value);

// 自定义基元：只要实现 IParsable<TSelf>、IComparable<TSelf> 与非泛型 IComparable，
// 并暴露公开静态的 Parse / TryParse，就能直接当基元用，本库不需要为它加任何代码。
// 它的规范文本是 "USD 12.50" 这样的字符串，因此所有文本路径（JSON、ToString、字典键）都写这个形态。
public readonly record struct Money(decimal Amount, string Currency)
    : IParsable<Money>, IComparable<Money>, IComparable
{
    public static Money Parse(string s, IFormatProvider? provider)
        => TryParse(s, provider, out var result) ? result : throw new FormatException($"'{s}' is not a valid Money.");

    public static bool TryParse(string? s, IFormatProvider? provider, out Money result)
    {
        var parts = s?.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);

        if (parts is { Length: 2 }
            && decimal.TryParse(parts[1], NumberStyles.Number, provider ?? CultureInfo.InvariantCulture, out var amount))
        {
            result = new Money(amount, parts[0]);
            return true;
        }

        result = default;
        return false;
    }

    public int CompareTo(Money other)
    {
        var byCurrency = string.CompareOrdinal(Currency, other.Currency);

        return byCurrency != 0 ? byCurrency : Amount.CompareTo(other.Amount);
    }

    public int CompareTo(object? obj)
        => obj is Money other ? CompareTo(other) : throw new ArgumentException("Not a Money value.", nameof(obj));

    public override string ToString() => $"{Currency} {Amount.ToString(CultureInfo.InvariantCulture)}";
}

[StronglyTypedId]
public partial record struct MoneyId(Money Value);

// 第三方自定义基元：ULID 满足同一份契约（IParsable + IComparable + 非泛型 IComparable），
// 本库对它零改动 —— 引用 Ulid 包、声明一行即可。规范文本是 26 字符的 Crockford Base32。
[StronglyTypedId]
public partial record struct UlidId(Ulid Value);

// 带前缀的 ULID：文本形是 "ord_" + 26 字符。
[StronglyTypedId(Prefix = "ord_")]
public partial record struct PrefixedUlidId(Ulid Value);

// ULID 的命名实例：值在运行期经 Ulid.Parse 还原（生成器跑在 netstandard2.0 上，无法执行它）。
[StronglyTypedId]
[StronglyTypedIdInstance("Nil", "00000000000000000000000000")]
public partial record struct InstanceUlidId(Ulid Value);

// 集成白名单：只保留两个 JSON 序列化器，其余集成（EF Core / Swagger / MessagePack……）都不生成。
// 与 None 的区别是「精确列出要什么」，适合宿主引了一堆包、但某个 Id 只想参与其中几类的场景。
// 注意白名单是过滤器：列出未引用其包的集成会报 STIA0019，不会静默失效。
[StronglyTypedId(Integrations = StronglyTypedIdIntegrations.SystemTextJson | StronglyTypedIdIntegrations.NewtonsoftJson)]
public partial record struct WhitelistedId(Guid Value);
