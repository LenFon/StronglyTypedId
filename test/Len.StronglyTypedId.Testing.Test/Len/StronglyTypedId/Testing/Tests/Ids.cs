// STIA0011：测试宿主类型的属性必须先有个初值，而 Id 的初值只能是 default，
// 但那正是「绕过 Create 与校验」的做法，分析器会报。测试宿主不参与生产代码，这里局部关掉。
#pragma warning disable STIA0011
using System.Numerics;
using Len.StronglyTypedId;

namespace Len.StronglyTypedId.Testing.Tests;

// 覆盖 SupportedPrimitiveTypes.DocumentedTypes 的全部 24 种基元（含 enum）。
// 每个 Id 都只声明一行，生成器负责其余部分，这正是本包要造值的对象。
[StronglyTypedId]
public partial record struct GuidId(Guid Value);

[StronglyTypedId]
public partial record StringId(string Value);

[StronglyTypedId]
public partial record struct CharId(char Value);

[StronglyTypedId]
public partial record struct ByteId(byte Value);

[StronglyTypedId]
public partial record struct SByteId(sbyte Value);

[StronglyTypedId]
public partial record struct Int16Id(short Value);

[StronglyTypedId]
public partial record struct UInt16Id(ushort Value);

[StronglyTypedId]
public partial record struct Int32Id(int Value);

[StronglyTypedId]
public partial record struct UInt32Id(uint Value);

[StronglyTypedId]
public partial record struct Int64Id(long Value);

[StronglyTypedId]
public partial record struct UInt64Id(ulong Value);

[StronglyTypedId]
public partial record struct Int128Id(Int128 Value);

[StronglyTypedId]
public partial record struct UInt128Id(UInt128 Value);

[StronglyTypedId]
public partial record struct DecimalId(decimal Value);

[StronglyTypedId]
public partial record struct HalfId(Half Value);

[StronglyTypedId]
public partial record struct SingleId(float Value);

[StronglyTypedId]
public partial record struct DoubleId(double Value);

[StronglyTypedId]
public partial record struct DateTimeId(DateTime Value);

[StronglyTypedId]
public partial record struct DateTimeOffsetId(DateTimeOffset Value);

[StronglyTypedId]
public partial record struct TimeSpanId(TimeSpan Value);

[StronglyTypedId]
public partial record struct DateOnlyId(DateOnly Value);

[StronglyTypedId]
public partial record struct TimeOnlyId(TimeOnly Value);

[StronglyTypedId]
public partial record struct BigIntegerId(BigInteger Value);

[StronglyTypedId]
public partial record struct ColorId(Color Value);

public enum Color
{
    Red,
    Green,
    Blue,
}

/// <summary>
/// 没有任何成员的空枚举：合法的 C#，但造不出取值。
/// </summary>
public enum EmptyEnum
{
}

[StronglyTypedId]
public partial record struct EmptyEnumId(EmptyEnum Value);

/// <summary>
/// 自定义基元：没有内置生成规则，但 <c>TryParse</c> 只要求含 <c>@</c>，启发式候选能命中。
/// </summary>
/// <remarks>
/// 基元类型 <c>Email</c> 由 <c>StronglyTypedIdConformanceTests</c> 声明（同一命名空间），此处只补 Id。
/// </remarks>
[StronglyTypedId]
public partial record struct EmailId(Email Value);

/// <summary>
/// 第三方自定义基元：只接受 26 位 Crockford Base32 文本，启发式第 2 种候选命中。
/// </summary>
[StronglyTypedId]
public partial record struct UlidId(Ulid Value);

/// <summary>
/// 严格格式的自定义基元：只接受 <c>ACCT-</c> 前缀加固定长度，任何随机文本都无法命中。
/// </summary>
[StronglyTypedId]
public partial record struct StrictCodeId(StrictCode Value);

/// <summary>
/// 只接受 <c>ACCT-</c> 前缀加 8 位数字：启发式无法推断出这个格式。
/// </summary>
public readonly record struct StrictCode(string Value) : IParsable<StrictCode>, IComparable<StrictCode>, IComparable
{
    public static StrictCode Parse(string s, IFormatProvider? provider)
        => TryParse(s, provider, out var result) ? result : throw new FormatException($"'{s}' is not a valid StrictCode.");

    public static bool TryParse(string? s, IFormatProvider? provider, out StrictCode result)
    {
        if (s is { Length: 13 } && s.StartsWith("ACCT-", StringComparison.Ordinal) && IsAllDigits(s.AsSpan(5)))
        {
            result = new StrictCode(s);
            return true;
        }

        result = default;
        return false;
    }

    private static bool IsAllDigits(ReadOnlySpan<char> text)
    {
        foreach (var character in text)
        {
            if (!char.IsAsciiDigit(character))
            {
                return false;
            }
        }

        return true;
    }

    public int CompareTo(StrictCode other) => string.CompareOrdinal(Value, other.Value);

    public int CompareTo(object? obj) => obj is StrictCode other ? CompareTo(other) : throw new ArgumentException("not a StrictCode", nameof(obj));

    public override string ToString() => Value;
}

/// <summary>
/// 验证器只接受正数，随机值约有一半会被拒，用来验证重试路径。
/// </summary>
[StronglyTypedId(Validator = nameof(IsPositive))]
public partial record struct PositiveIntId(int Value)
{
    private static bool IsPositive(int value) => value > 0;
}

/// <summary>
/// 验证器恒为 false，用来验证重试预算耗尽后的失败提示。
/// </summary>
[StronglyTypedId(Validator = nameof(AlwaysRejects))]
public partial record struct ImpossibleId(int Value)
{
    private static bool AlwaysRejects(int _) => false;
}

/// <summary>
/// 验证失败时抛出的自定义异常：<c>ExceptionType</c> 的典型用法。
/// </summary>
public sealed class DomainRuleException(string message) : Exception(message);

/// <summary>
/// 配了 <c>ExceptionType</c> 且验证器只接受正数，重试循环必须认得自定义异常，
/// 否则第一次被拒就直接冒泡（约一半概率失败）。
/// </summary>
[StronglyTypedId(Validator = nameof(IsPositive), ExceptionType = typeof(DomainRuleException))]
public partial record struct CustomExceptionPositiveIntId(int Value)
{
    private static bool IsPositive(int value) => value > 0;
}

/// <summary>
/// 配了 <c>ExceptionType</c> 且验证器恒为 false，用来验证重试预算耗尽后仍给出可操作的失败提示。
/// </summary>
[StronglyTypedId(Validator = nameof(AlwaysRejects), ExceptionType = typeof(DomainRuleException))]
public partial record struct CustomExceptionImpossibleId(int Value)
{
    private static bool AlwaysRejects(int _) => false;
}

/// <summary>
/// 验证器自身抛异常，这不是「值被拒」，重试循环必须让它冒泡，而不是当成拒绝吞掉。
/// </summary>
[StronglyTypedId(Validator = nameof(Explodes))]
public partial record struct ExplodingValidatorId(int Value)
{
    private static bool Explodes(int _) => throw new InvalidOperationException("validator exploded");
}

/// <summary>
/// AutoFixture 用例的宿主类型：验证 Id 能作为构造参数被填充。
/// </summary>
public sealed record Order(GuidId Id, StringId Customer, int Quantity);

/// <summary>
/// Bogus 用例的宿主类型。
/// </summary>
/// <remarks>
/// Bogus 的默认实例化器走 <c>Activator.CreateInstance&lt;T&gt;()</c>，要求目标类型有无参构造
/// 所以这里用「类 + 可写属性」而不是 record（record 只有带参构造）。
/// </remarks>
public sealed class BogusOrder
{
    public GuidId Id { get; set; } = default!;

    public StringId Customer { get; set; } = default!;

    public int Quantity { get; set; }
}

/// <summary>
/// Bogus 用例的宿主类型：同时带非可空与可空 Id 属性。
/// </summary>
/// <remarks>
/// 可空属性的声明类型是 <c>Nullable&lt;GuidId&gt;</c>，与 <c>GuidId</c> 是两个不同的运行期类型，
/// 只注册 <c>RuleForType(typeof(GuidId))</c> 匹配不到它。
/// </remarks>
public sealed class BogusNullableOrder
{
    public GuidId Id { get; set; } = default!;

    public GuidId? OptionalId { get; set; }
}

/// <summary>
/// Bogus 用例的宿主类型：Id 带验证器，验证适配走的是生成的 <c>Create</c> 而不是绕开它。
/// </summary>
public sealed class BogusPositiveOrder
{
    public PositiveIntId Id { get; set; } = default!;
}

/// <summary>
/// 泛型容器里的 Id：自动发现会看到开放泛型 <c>Box&lt;T&gt;.BoxedId</c>，它无法被闭合构造。
/// 适配包必须跳过它，而不是让 <c>MakeGenericMethod</c> 抛「not fully instantiated」。
/// 显式 Integrations = None 是刻意的：本夹具只关心自动发现，关掉集成后 STIA0028 不再提示。
/// </summary>
public partial class Box<T>
{
    [StronglyTypedId(Integrations = StronglyTypedIdIntegrations.None)]
    public partial record struct BoxedId(Guid Value);
}
