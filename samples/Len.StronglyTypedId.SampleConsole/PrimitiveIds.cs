using System.Numerics;

namespace Len.StronglyTypedId.SampleConsole;

// 23 种内置基元各声明一个 Id，用来把「支持哪些基元」变成可运行的一行行输出。
// 枚举基元（RegionId）在 Ids.cs 里，这里不重复声明，避免同一命名空间下出现两个同基元类型的 Id。
[StronglyTypedId]
public partial record struct GuidPrimitiveId(Guid Value);

[StronglyTypedId]
public partial record struct StringPrimitiveId(string Value);

[StronglyTypedId]
public partial record struct CharPrimitiveId(char Value);

[StronglyTypedId]
public partial record struct BytePrimitiveId(byte Value);

[StronglyTypedId]
public partial record struct SBytePrimitiveId(sbyte Value);

[StronglyTypedId]
public partial record struct Int16PrimitiveId(short Value);

[StronglyTypedId]
public partial record struct UInt16PrimitiveId(ushort Value);

[StronglyTypedId]
public partial record struct Int32PrimitiveId(int Value);

[StronglyTypedId]
public partial record struct UInt32PrimitiveId(uint Value);

[StronglyTypedId]
public partial record struct Int64PrimitiveId(long Value);

[StronglyTypedId]
public partial record struct UInt64PrimitiveId(ulong Value);

[StronglyTypedId]
public partial record struct Int128PrimitiveId(Int128 Value);

[StronglyTypedId]
public partial record struct UInt128PrimitiveId(UInt128 Value);

[StronglyTypedId]
public partial record struct DecimalPrimitiveId(decimal Value);

[StronglyTypedId]
public partial record struct HalfPrimitiveId(Half Value);

[StronglyTypedId]
public partial record struct SinglePrimitiveId(float Value);

[StronglyTypedId]
public partial record struct DoublePrimitiveId(double Value);

[StronglyTypedId]
public partial record struct DateTimePrimitiveId(DateTime Value);

[StronglyTypedId]
public partial record struct DateTimeOffsetPrimitiveId(DateTimeOffset Value);

[StronglyTypedId]
public partial record struct TimeSpanPrimitiveId(TimeSpan Value);

[StronglyTypedId]
public partial record struct DateOnlyPrimitiveId(DateOnly Value);

[StronglyTypedId]
public partial record struct TimeOnlyPrimitiveId(TimeOnly Value);

[StronglyTypedId]
public partial record struct BigIntegerPrimitiveId(BigInteger Value);

/// <summary>
/// 基元矩阵演示用的 Id 清单。
/// </summary>
/// <remarks>
/// 用 <see cref="Type"/> 而不是泛型列表：演示要按运行期类型逐个序列化，泛型入口在这里无从闭合。
/// 清单本身是 <c>static readonly</c>，避免每次调用都重新分配数组（CA1861）。
/// </remarks>
internal static class PrimitiveIds
{
    /// <summary>
    /// 全部 23 种内置基元的 Id 类型，外加一个枚举基元。
    /// </summary>
    public static readonly Type[] All =
    [
        typeof(GuidPrimitiveId),
        typeof(StringPrimitiveId),
        typeof(CharPrimitiveId),
        typeof(BytePrimitiveId),
        typeof(SBytePrimitiveId),
        typeof(Int16PrimitiveId),
        typeof(UInt16PrimitiveId),
        typeof(Int32PrimitiveId),
        typeof(UInt32PrimitiveId),
        typeof(Int64PrimitiveId),
        typeof(UInt64PrimitiveId),
        typeof(Int128PrimitiveId),
        typeof(UInt128PrimitiveId),
        typeof(DecimalPrimitiveId),
        typeof(HalfPrimitiveId),
        typeof(SinglePrimitiveId),
        typeof(DoublePrimitiveId),
        typeof(DateTimePrimitiveId),
        typeof(DateTimeOffsetPrimitiveId),
        typeof(TimeSpanPrimitiveId),
        typeof(DateOnlyPrimitiveId),
        typeof(TimeOnlyPrimitiveId),
        typeof(BigIntegerPrimitiveId),
        typeof(RegionId),
    ];
}
