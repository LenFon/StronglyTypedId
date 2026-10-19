using System.Numerics;
using Len.StronglyTypedId;

namespace Len.StronglyTypedId.Testing.Tests;

// 覆盖 SupportedPrimitiveTypes.DocumentedTypes 的全部 24 种基元（含 enum）。
// 每个 Id 都只声明一行，生成器负责其余部分 —— 这正是本包要造值的对象。
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
/// 验证器只接受正数——随机值约有一半会被拒，用来验证重试路径。
/// </summary>
[StronglyTypedId(Validator = nameof(IsPositive))]
public partial record struct PositiveIntId(int Value)
{
    private static bool IsPositive(int value) => value > 0;
}

/// <summary>
/// 验证器恒为 false ——用来验证重试预算耗尽后的失败提示。
/// </summary>
[StronglyTypedId(Validator = nameof(AlwaysRejects))]
public partial record struct ImpossibleId(int Value)
{
    private static bool AlwaysRejects(int _) => false;
}

/// <summary>
/// AutoFixture 用例的宿主类型：验证 Id 能作为构造参数被填充。
/// </summary>
public sealed record Order(GuidId Id, StringId Customer, int Quantity);
