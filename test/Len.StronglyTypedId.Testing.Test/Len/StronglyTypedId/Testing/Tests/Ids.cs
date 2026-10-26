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
/// Bogus 用例的宿主类型：Id 带验证器，验证适配走的是生成的 <c>Create</c> 而不是绕开它。
/// </summary>
public sealed class BogusPositiveOrder
{
    public PositiveIntId Id { get; set; } = default!;
}
