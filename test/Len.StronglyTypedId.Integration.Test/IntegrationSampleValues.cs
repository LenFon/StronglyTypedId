using System.Numerics;

namespace Len.StronglyTypedId;

/// <summary>
/// 集成测试共用的「每种基元取一个普通值」样本表。
/// </summary>
/// <remarks>
/// 两个矩阵测试都要按基元逐个喂值，样本表放一处才不会各自漂移：新增基元时漏改一边，另一边会静默少覆盖一种。
/// </remarks>
internal static class IntegrationSampleValues
{
    /// <summary>
    /// 每个基元取一个「普通」值：非默认、非边界，避免把断言变成边界测试。
    /// </summary>
    public static readonly IReadOnlyDictionary<Type, object> ByPrimitive = new Dictionary<Type, object>
    {
        [typeof(Guid)] = Guid.Parse("3f2c1b4e-0000-4000-8000-000000000001"),
        [typeof(string)] = "abc",
        [typeof(char)] = 'A',
        [typeof(byte)] = (byte)42,
        [typeof(sbyte)] = (sbyte)-42,
        [typeof(short)] = (short)-1234,
        [typeof(ushort)] = (ushort)1234,
        [typeof(int)] = 42,
        [typeof(uint)] = 42u,
        [typeof(long)] = 42L,
        [typeof(ulong)] = 42UL,
        [typeof(Int128)] = (Int128)42,
        [typeof(UInt128)] = (UInt128)42,
        [typeof(decimal)] = 1.5m,
        [typeof(Half)] = (Half)1.5,
        [typeof(float)] = 1.5f,
        [typeof(double)] = 1.5,
        [typeof(DateTime)] = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc),
        [typeof(DateTimeOffset)] = new DateTimeOffset(2020, 1, 2, 3, 4, 5, TimeSpan.FromHours(8)),
        [typeof(TimeSpan)] = TimeSpan.FromMinutes(90),
        [typeof(DateOnly)] = new DateOnly(2020, 1, 2),
        [typeof(TimeOnly)] = new TimeOnly(3, 4, 0),
        [typeof(BigInteger)] = new BigInteger(42),
        [typeof(SampleStatus)] = SampleStatus.Active,
        [typeof(SampleAccess)] = SampleAccess.Read | SampleAccess.Write,
        [typeof(SamplePriority)] = SamplePriority.VeryHigh,
    };

    /// <summary>
    /// 二进制格式没有原生表示、只能退回规范文本的基元（与生成器的
    /// <c>StronglyTypedIdInfo.IsBinaryTextPrimitive</c> 一一对应）。
    /// </summary>
    public static bool IsBinaryTextPrimitive(Type primitiveType)
        => primitiveType == typeof(BigInteger)
            || primitiveType == typeof(Int128)
            || primitiveType == typeof(UInt128);

    /// <summary>
    /// 数据库驱动没有 provider 类型、只能退回规范文本的基元（与生成器的
    /// <c>StronglyTypedIdInfo.IsDatabaseTextPrimitive</c> 一一对应）。
    /// </summary>
    /// <remarks>
    /// 实测 SQLite 8.0：这四个基元直接作为 <c>ValueConverter</c> 的 provider type 会让 EF Core 拒绝映射整个属性，
    /// Dapper 绑定参数时也会报「No mapping exists」。故 EF / Dapper 两侧都改存不变文化文本。
    /// </remarks>
    public static bool IsDatabaseTextPrimitive(Type primitiveType)
        => primitiveType == typeof(BigInteger)
            || primitiveType == typeof(Int128)
            || primitiveType == typeof(UInt128)
            || primitiveType == typeof(Half);

    /// <summary>
    /// 取该基元在数据库集成里实际落库的值：文本基元是不变文化文本，其余是基元本身。
    /// </summary>
    public static object DatabaseProviderValue(Type primitiveType, object value)
        => IsDatabaseTextPrimitive(primitiveType)
            ? Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)!
            : value;
}
