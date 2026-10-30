using System.Globalization;
using Len.StronglyTypedId;

// 程序集级默认验证器：本程序集内所有 Id 都套用它，除非类型自己声明了 Validator。
// 每个 Id 仍须提供同名静态方法，否则 STIA0010 会报错。
[assembly: StronglyTypedIdDefaults(Validator = "Validate")]

namespace Len.StronglyTypedId.SampleIntegrations;

[StronglyTypedId]
public partial record struct OrderId(Guid Value)
{
    private static bool Validate(Guid value) => value != Guid.Empty;
}

[StronglyTypedId]
public partial record struct LineId(int Value)
{
    private static bool Validate(int value) => value > 0;
}

[StronglyTypedId]
public partial record struct Sku(string Value)
{
    private static bool Validate(string value) => !string.IsNullOrWhiteSpace(value);
}

// protobuf-net：surrogate 只能靠特性声明，因此 Id 必须声明在本次编译里。
// 引用程序集里的 Id 拿不到 surrogate —— 这是 protobuf 集成与其余集成最大的不同。
[StronglyTypedId]
public partial record struct LedgerId(Guid Value)
{
    private static bool Validate(Guid value) => value != Guid.Empty;
}

// protobuf-net 没有原生表示的基元：载荷退回规范文本（DateTimeOffset 用 "O" 格式保留 ticks 与 offset）。
[StronglyTypedId]
public partial record struct PostedAtId(DateTimeOffset Value)
{
    private static bool Validate(DateTimeOffset value) => value != default;
}

// 带前缀的 Id：用来证明 protobuf 载荷存的是裸基元值，前缀只属于文本形。
[StronglyTypedId(Prefix = "led_")]
public partial record struct PrefixedLedgerId(Guid Value)
{
    private static bool Validate(Guid value) => value != Guid.Empty;
}

// 引用类型 Id：protobuf 的 null 往返只对 record class 成立，值类型 Id 收到空载荷会抛异常。
[StronglyTypedId]
public partial record SessionId(Guid Value)
{
    private static bool Validate(Guid value) => value != Guid.Empty;
}

// 自定义基元：所有序列化路径都写规范文本，EF Core 的 provider 类型也因此是 string。
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
public partial record struct PriceId(Money Value)
{
    private static bool Validate(Money value) => value.Amount > 0;
}

// 枚举基元：Dapper 的读取路径要处理「驱动返回 long 而不是枚举」的情形，
// EF Core 则按底层整数建列。两者都在下面的演示里实际跑一遍。
public enum LedgerKind
{
    Debit = 0,
    Credit = 1,
}

[StronglyTypedId]
public partial record struct KindId(LedgerKind Value)
{
    private static bool Validate(LedgerKind value) => Enum.IsDefined(value);
}

internal sealed class Order
{
    public OrderId Id { get; set; }

    public LineId Line { get; set; }

    public Sku Code { get; set; }

    public int Quantity { get; set; }
}
