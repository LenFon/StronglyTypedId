namespace Len.StronglyTypedId;

[StronglyTypedId]
public partial record struct GuidId(Guid Value);

// C1 新增基元：扩展整型、浮点、日期/时间、大整数。各自代表一条生成器形态分支 ——
// Int128/UInt128/DateOnly/TimeOnly/Single/Double/Half 与整型同构（全套含 UTF-16 / UTF-8 span）；
// BigInteger 虽有 UTF-8 语义，但其 TryParse(ReadOnlySpan<byte>,…) 是显式接口实现，故不生成 UTF-8 解析。
[StronglyTypedId]
public partial record struct Int128Id(Int128 Value);

[StronglyTypedId]
public partial record struct UInt128Id(UInt128 Value);

[StronglyTypedId]
public partial record struct DateOnlyId(DateOnly Value);

[StronglyTypedId]
public partial record struct TimeOnlyId(TimeOnly Value);

[StronglyTypedId]
public partial record struct DoubleId(double Value);

// H2：decimal 是「静默错值」最危险的场景 —— de-DE 下小数分隔符是逗号、千位分隔符是句点，
// 规范形的 "1.5" 会被读成 15。用它验证 Parse / TryParse 在 provider 为 null 时按不变文化解析。
[StronglyTypedId]
public partial record struct DecimalId(decimal Value);

[StronglyTypedId]
public partial record struct SingleId(float Value);

[StronglyTypedId]
public partial record struct HalfId(Half Value);

[StronglyTypedId]
public partial record struct BigIntegerId(System.Numerics.BigInteger Value);

[StronglyTypedId]
public partial record struct Int32Id(int Value);

[StronglyTypedId]
public partial record struct UInt32Id(uint Value);

[StronglyTypedId]
public partial record struct Int64Id(long Value);

[StronglyTypedId]
public partial record struct UInt64Id(ulong Value);

[StronglyTypedId]
public partial record struct ByteId(byte Value);

[StronglyTypedId]
public partial record struct StringId(string Value);

// C1：char 是唯一「接口面同构、但成员全是显式接口实现」的基元 —— IParsable / IFormattable 的
// Parse / TryParse / ToString(format, provider) 都不能按类型名直呼（CS1501），故生成器为它单列分支：
// 解析按「长度恰为 1」判定，格式化走无参 ToString() 或 IFormattable 接口调用。
[StronglyTypedId]
public partial record struct CharId(char Value);

[StronglyTypedId]
public partial record GuidIdV2(Guid Value);

[StronglyTypedId]
public partial record Int32IdV2(int Value);

[StronglyTypedId]
public partial record UInt32IdV2(uint Value);

[StronglyTypedId]
public partial record Int64IdV2(long Value);

[StronglyTypedId]
public partial record UInt64IdV2(ulong Value);

[StronglyTypedId]
public partial record StringIdV2(string Value);

[StronglyTypedId]
public partial record ByteIdV2(byte Value);

[StronglyTypedId]
public partial record struct SByteId(sbyte Value);

[StronglyTypedId]
public partial record struct Int16Id(short Value);

[StronglyTypedId]
public partial record struct UInt16Id(ushort Value);

[StronglyTypedId]
public partial record SByteIdV2(sbyte Value);

[StronglyTypedId]
public partial record Int16IdV2(short Value);

[StronglyTypedId]
public partial record UInt16IdV2(ushort Value);

[StronglyTypedId]
public partial record struct StringIdV3(System.String Value);

// 以下三个 Id 用 Validator 钩子覆盖「取值校验」：验证器是 Id 类型自身的私有静态方法，
// 生成代码在 Create 与两个 TryParse 重载里调用它。三种基元各自代表一条分支 ——
// Guid/int 的解析结果落在 out 参数上，string 则直接以参数本身参与校验。
[StronglyTypedId(Validator = nameof(Validate))]
public partial record struct NonEmptyGuidId(Guid Value)
{
    private static bool Validate(Guid value) => value != Guid.Empty;
}

[StronglyTypedId(Validator = nameof(Validate))]
public partial record NonEmptyInt32Id(int Value)
{
    private static bool Validate(int value) => value > 0;
}

[StronglyTypedId(Validator = nameof(Validate))]
public partial record struct ShortStringId(string Value)
{
    private static bool Validate(string value) => value.Length <= 8;
}

// N3：验证失败异常类型可配。领域库希望 Create 抛自己的异常而不是 ArgumentException，
// 故把异常类型写成 attribute 命名实参；Parse 抛的是 FormatException —— 它的失败还包含
// 「文本根本不是 Id」，与「值被领域规则拒绝」不是同一类错误。
public sealed class DomainRuleViolationException(string message) : Exception(message);

[StronglyTypedId(Validator = nameof(Validate), ExceptionType = typeof(DomainRuleViolationException))]
public partial record struct DomainValidatedGuidId(Guid Value)
{
    private static bool Validate(Guid value) => value != Guid.Empty;
}

public partial record NotStronglyTypedId();

// readonly record struct 形态：生成器必须把 readonly 修饰符透出到生成的 partial 段，否则与使用者
// 写下的 readonly 段对「是否 readonly struct」不一致而编译失败。
[StronglyTypedId]
public readonly partial record struct ReadonlyGuidId(Guid Value);

// 逐类型开启 TypeConverter：生成的嵌套转换器经 TypeDescriptor 在字符串与 Id 间往返，
// 覆盖 XmlSerializer / IConfiguration 绑定 / Newtonsoft 字典键读路径等场景。
[StronglyTypedId(TypeConverter = true)]
public partial record struct ConvertibleGuidId(Guid Value);

// H1：文化敏感的基元 + TypeConverter，用来验证生成的转换器与 ToString() 一样按不变文化往返，
// 不受当前区域（如 de-DE 的小数逗号）影响。
[StronglyTypedId(TypeConverter = true)]
public partial record struct ConvertibleDoubleId(double Value);

// C4：JSON 形态可配置。默认（Primitive）下 Id 与基元值在 JSON 里同形；Object 形态读写 { "value": … }，
// 供「已对外承诺对象形态」的既有契约在不改动客户端的前提下迁移。取 Guid 与 string 两种基元：
// 值位置的写入路径两者相同，但字典键的写入路径不同（string 直取取值，其余经不变文化 ToString）。
[StronglyTypedId(JsonShape = JsonShape.Object)]
public partial record struct ObjectShapeGuidId(Guid Value);

[StronglyTypedId(JsonShape = JsonShape.Object)]
public partial record struct ObjectShapeStringId(string Value);

// 嵌套类型的 Id：包含类型必须声明为 partial —— partial 的各段必须处于同一容器内，生成代码要补的
// 成员只能逐层嵌回容器里，故容器得能被生成代码原样重开一次。
// 三种容器种类各取一个，覆盖「种类关键字按符号判定」这条路径：record 与 class 的 TypeKind 同为 Class，
// 只能靠 IsRecord 区分；struct 容器则用来验证不会给它加上非法的 sealed 修饰符。
public partial class OrderAggregate
{
    [StronglyTypedId]
    public partial record struct OrderId(Guid Value);
}

public partial struct ShipmentBatch
{
    [StronglyTypedId]
    public partial record BatchId(Guid Value);
}

public partial record Inventory(Guid WarehouseId)
{
    [StronglyTypedId]
    public partial record struct SkuId(string Value);
}

// C10：文本前缀。前缀进入规范文本形（ToString 前置、Parse/TryParse 剥离），从而自动贯通所有文本路径
// （STJ / Newtonsoft / MVC / 路由约束 / TypeConverter），而以基元值为媒介的 EF Core / Dapper 不受影响。
// 取 Guid（值类型）与 string（引用基元）两种基元，覆盖「数字基元 JSON 变 string」与「字符串基元 JSON 内容变化」。
[StronglyTypedId(Prefix = "ord_")]
public partial record struct PrefixedGuidId(Guid Value);

[StronglyTypedId(Prefix = "ord_")]
public partial record struct PrefixedStringId(string Value);

// 前缀 + 对象形态：{ "value": "ord_x" }。
[StronglyTypedId(Prefix = "ord_", JsonShape = JsonShape.Object)]
public partial record struct PrefixedObjectGuidId(Guid Value);

// 前缀 + 验证器：验证器收到的是剥离后的基元值。
[StronglyTypedId(Prefix = "ord_", Validator = nameof(Validate))]
public partial record struct PrefixedValidatedGuidId(Guid Value)
{
    private static bool Validate(Guid value) => value != Guid.Empty;
}

// 严格读入：缺前缀即解析失败（PrefixReadMode 默认宽松，此处显式改为严格）。
[StronglyTypedId(Prefix = "ord_", PrefixReadMode = PrefixReadMode.Strict)]
public partial record struct StrictPrefixedGuidId(Guid Value);

// 前缀 + 枚举：键文本含前缀，DictionaryKeyPolicy 会把前缀一起改写导致读不回，故生成器对它不应用 policy。
[StronglyTypedId(Prefix = "pri_")]
public partial record struct PrefixedPriorityId(SamplePriority Value);

// K1：属性测试要覆盖「规范文本形有损」的时间基元。无参 ToString() 对 IFormattable 基元走
// Value.ToString(null, InvariantCulture)，即默认格式串 —— DateTime / DateTimeOffset 的 "G" 丢亚秒、
// TimeOnly 的 "t" 丢秒。这三个夹具把这条边界显式测出来：它们只能断言文本幂等，不能断言值往返。
[StronglyTypedId]
public partial record struct DateTimeId(DateTime Value);

[StronglyTypedId]
public partial record struct DateTimeOffsetId(DateTimeOffset Value);

[StronglyTypedId]
public partial record struct TimeSpanId(TimeSpan Value);

/// <summary>
/// 非 <c>[Flags]</c> 枚举基元。<c>Enum.TryParse</c> 对未定义数值（<c>"999"</c>）、逗号组合与前后空白
/// 都返回 true，生成器必须再查 <c>Enum.IsDefined</c> 才能挡住这些野值。
/// </summary>
public enum SampleStatus
{
    Active,
    Suspended,
}

[StronglyTypedId]
public partial record struct SampleStatusId(SampleStatus Value);

/// <summary>
/// <c>[Flags]</c> 枚举基元：位组合本身就是合法取值，<c>IsDefined</c> 会误杀，故生成器改用「未定义位掩码」校验 ——
/// <c>Read | Write</c> 通过，<c>999</c>（含未定义位）被拒。
/// </summary>
[Flags]
public enum SampleAccess
{
    None = 0,
    Read = 1,
    Write = 2,
}

[StronglyTypedId]
public partial record struct SampleAccessId(SampleAccess Value);

/// <summary>
/// 成员名含多个单词的枚举基元：只有这种形状才能看出 <c>DictionaryKeyPolicy</c> 是否真的生效
/// （单单词成员在 camelCase 下只差首字母大小写，容易被「恰好相等」蒙混过去）。
/// </summary>
public enum SamplePriority
{
    VeryHigh,
    LowPriority,
}

[StronglyTypedId]
public partial record struct SamplePriorityId(SamplePriority Value);

// N2：命名实例。值以「基元的不变文化文本形」声明，编译期校验并翻译成字面量，运行期经 Create 构造 ——
// 因此实例必然满足验证器，Parse(id.ToString()) 一定读得回来。
// int 覆盖负值与零：零值实例与 default 相等是值语义的必然结果，不是缺陷。
[StronglyTypedId]
[StronglyTypedIdInstance("Unspecified", "-1")]
[StronglyTypedIdInstance("Anonymous", "0")]
public partial record struct InstanceInt32Id(int Value);

// long.MinValue：字面量发射要依赖 C# 对最小值的一元负号特例，是最容易写错的一个边界。
[StronglyTypedId]
[StronglyTypedIdInstance("Min", "-9223372036854775808")]
public partial record struct InstanceInt64Id(long Value);

// 最常见的哨兵值：Guid.Empty。
[StronglyTypedId]
[StronglyTypedIdInstance("Empty", "00000000-0000-0000-0000-000000000000")]
public partial record struct InstanceGuidId(Guid Value);

// 引用类型 record：static readonly 字段保证 ReferenceEquals，且实例与 default(null) 不相等。
[StronglyTypedId]
[StronglyTypedIdInstance("Empty", "00000000-0000-0000-0000-000000000000")]
public partial record InstanceGuidRefId(Guid Value);

// string 基元：文本即值，空串是合法取值（Parse 的「非空」条件只约束读入，不约束实例声明）。
[StronglyTypedId]
[StronglyTypedIdInstance("Unknown", "unknown")]
[StronglyTypedIdInstance("Blank", "")]
public partial record StringInstanceId(string Value);

// 枚举：非 flags 走 Enum.IsDefined 同源的成员名解析。
[StronglyTypedId]
[StronglyTypedIdInstance("Active", "Active")]
public partial record struct InstanceStatusId(SampleStatus Value);

// 归一化 + 实例：声明的文本先归一化再校验，故 Value 是 "ABC" 而不是 "abc"。
[StronglyTypedId(Normalizer = nameof(Normalize))]
[StronglyTypedIdInstance("Upper", "abc")]
public partial record struct NormalizedInstanceStringId(string Value)
{
    private static string Normalize(string value) => value.ToUpperInvariant();
}

// 前缀 + 实例：实例声明的是基元值，不带前缀；ToString() 照常带前缀。
[StronglyTypedId(Prefix = "ord_")]
[StronglyTypedIdInstance("Empty", "00000000-0000-0000-0000-000000000000")]
public partial record struct PrefixedInstanceGuidId(Guid Value);

// 时间基元：DateTime 必须透出 Kind，DateTimeOffset 必须带偏移，否则往返后不再相等。
[StronglyTypedId]
[StronglyTypedIdInstance("Epoch", "1970-01-01T00:00:00Z")]
public partial record struct InstanceDateTimeId(DateTime Value);

[StronglyTypedId]
[StronglyTypedIdInstance("PlusEight", "2020-01-01T00:00:00+08:00")]
public partial record struct InstanceDateTimeOffsetId(DateTimeOffset Value);

// N2：实例必须通过验证器。哨兵值被业务规则拒绝时，类型初始化器会抛 TypeInitializationException ——
// 这是刻意的取舍：宁可第一次触碰就失败，也不要让一个「不可能存在」的 Id 悄悄流进领域模型。
[StronglyTypedId(Validator = nameof(Validate))]
[StronglyTypedIdInstance("Zero", "0")]
public partial record struct RejectedInstanceInt32Id(int Value)
{
    private static bool Validate(int value) => value > 0;
}
