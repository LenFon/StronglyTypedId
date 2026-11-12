using System.Globalization;

namespace Len.StronglyTypedId;

[StronglyTypedId]
public partial record struct GuidId(Guid Value);

// C1 新增基元：扩展整型、浮点、日期/时间、大整数。各自代表一条生成器形态分支，
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

// H2：decimal 是「静默错值」最危险的场景，de-DE 下小数分隔符是逗号、千位分隔符是句点，
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

// C1：char 是唯一「接口面同构、但成员全是显式接口实现」的基元，IParsable / IFormattable 的
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
// 生成代码在 Create 与两个 TryParse 重载里调用它。三种基元各自代表一条分支，
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
// 故把异常类型写成 attribute 命名实参；Parse 抛的是 FormatException，它的失败还包含
// 「文本根本不是 Id」，与「值被领域规则拒绝」不是同一类错误。
public sealed class DomainRuleViolationException(string message) : Exception(message);

[StronglyTypedId(Validator = nameof(Validate), ExceptionType = typeof(DomainRuleViolationException))]
public partial record struct DomainValidatedGuidId(Guid Value)
{
    private static bool Validate(Guid value) => value != Guid.Empty;
}

// 验证失败消息是否内插被拒原文。默认内插（便于定位问题），开启 RedactValidationMessage 后只报
// 「不是合法的 X」，对 string 基元的 Id 来说，被拒原文就是用户输入，会顺着异常进日志与 APM。
// 两个夹具除该开关外完全同构，差异只来自这一个布尔值。
[StronglyTypedId(Validator = nameof(Validate))]
public partial record struct VerboseValidatedStringId(string Value)
{
    private static bool Validate(string value) => value.Length <= 8;
}

[StronglyTypedId(Validator = nameof(Validate), RedactValidationMessage = true)]
public partial record struct RedactedValidatedStringId(string Value)
{
    private static bool Validate(string value) => value.Length <= 8;
}

// 脱敏与自定义异常类型正交：换成领域异常后同样不内插原文，且不带 paramName
// （自定义异常未必有 (string, string) 双参构造，故生成器只传消息）。
public sealed class RedactedDomainException(string message) : Exception(message);

[StronglyTypedId(
    Validator = nameof(Validate),
    ExceptionType = typeof(RedactedDomainException),
    RedactValidationMessage = true)]
public partial record struct RedactedCustomExceptionId(string Value)
{
    private static bool Validate(string value) => value.Length <= 8;
}

public partial record NotStronglyTypedId();

// R3：验证器可以返回 StronglyTypedIdValidationResult，把拒绝原因与错误码带回调用方。
// 与上面的 bool 验证器夹具同构，差异只在返回类型 —— 用来锁「两种签名走不同模板」。
[StronglyTypedId(Validator = nameof(Validate))]
public partial record struct ResultValidatedGuidId(Guid Value)
{
    private static StronglyTypedIdValidationResult Validate(Guid value)
        => value == Guid.Empty
            ? StronglyTypedIdValidationResult.Failure("An id cannot be empty.", "E_EMPTY")
            : StronglyTypedIdValidationResult.Success;
}

// result 验证器 + 自定义异常：拒绝原因同样进入该异常的消息。
[StronglyTypedId(Validator = nameof(Validate), ExceptionType = typeof(DomainRuleViolationException))]
public partial record struct ResultValidatedDomainExceptionId(Guid Value)
{
    private static StronglyTypedIdValidationResult Validate(Guid value)
        => value == Guid.Empty
            ? StronglyTypedIdValidationResult.Failure("An id cannot be empty.", "E_EMPTY")
            : StronglyTypedIdValidationResult.Success;
}

// result 验证器 + 脱敏：验证器文案可能含用户输入，打开脱敏后一律忽略它。
[StronglyTypedId(Validator = nameof(Validate), RedactValidationMessage = true)]
public partial record struct ResultValidatedRedactedId(string Value)
{
    private static StronglyTypedIdValidationResult Validate(string value)
        => value.Length <= 8
            ? StronglyTypedIdValidationResult.Success
            : StronglyTypedIdValidationResult.Failure($"'{value}' is too long.", "E_TOO_LONG");
}

// result 验证器 + 归一化：四个入口都按「归一化 → 校验 → 构造」执行。
[StronglyTypedId(Validator = nameof(Validate), Normalizer = nameof(Normalize))]
public partial record struct ResultValidatedNormalizedId(string Value)
{
    private static string Normalize(string value) => value.Trim();

    private static StronglyTypedIdValidationResult Validate(string value)
        => value.Length > 0
            ? StronglyTypedIdValidationResult.Success
            : StronglyTypedIdValidationResult.Failure("The value cannot be blank.", "E_BLANK");
}

// result 验证器 + 前缀：带前缀的读入路径（JSON / Newtonsoft / MVC 绑定）只能经 TryParse，
// 用来锁「前缀路径同样执行验证器」。
[StronglyTypedId(Validator = nameof(Validate), Prefix = "ord_")]
public partial record struct ResultValidatedPrefixedId(Guid Value)
{
    private static StronglyTypedIdValidationResult Validate(Guid value)
        => value == Guid.Empty
            ? StronglyTypedIdValidationResult.Failure("An id cannot be empty.", "E_EMPTY")
            : StronglyTypedIdValidationResult.Success;
}

// 验证器重载：生成代码按实参类型绑定，只有 Guid 那个重载会被调用。
// 用来锁「按实参类型挑重载」——取第一个静态同名方法会在返回类型不同时产出不可编译的代码。
[StronglyTypedId(Validator = nameof(Validate))]
public partial record struct OverloadedValidatorId(Guid Value)
{
    private static bool Validate(string value) => value.Length > 0;

    private static StronglyTypedIdValidationResult Validate(Guid value)
        => value == Guid.Empty
            ? StronglyTypedIdValidationResult.Failure("An id cannot be empty.", "E_EMPTY")
            : StronglyTypedIdValidationResult.Success;
}

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

// 对象形态 + 验证器：对象形态的读入分支与基元形态是两段生成代码，验证失败的异常契约必须两边一致。
[StronglyTypedId(JsonShape = JsonShape.Object, Validator = nameof(Validate))]
public partial record struct ObjectShapeValidatedGuidId(Guid Value)
{
    private static bool Validate(Guid value) => value != Guid.Empty;
}

// 嵌套类型的 Id：包含类型必须声明为 partial，生成代码要补的成员只能逐层嵌回容器里。
// 三种容器各取一个：record 与 class 的 TypeKind 同为 Class，只能靠 IsRecord 区分；struct 容器用来验证不会加上非法的 sealed。
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

// 严格前缀 + string 基元：空串的规范文本形是裸前缀，严格读也必须能读回。
[StronglyTypedId(Prefix = "ord_", PrefixReadMode = PrefixReadMode.Strict)]
public partial record struct StrictPrefixedStringId(string Value);

// 严格前缀 + TypeConverter：TypeDescriptor 是与 JSON 并列的另一条文本读入路径。
// 生成的转换器内部调 Parse，故严格模式下的拒绝表现为 FormatException，而不是 NotSupportedException。
[StronglyTypedId(Prefix = "ord_", PrefixReadMode = PrefixReadMode.Strict, TypeConverter = true)]
public partial record struct StrictConvertiblePrefixedGuidId(Guid Value);

// 严格前缀 + 对象形态：{ "value": "ord_x" } 的对象读入分支与基元形态是两段生成代码，
// 前缀策略必须两边都生效，否则严格模式在对象形态上形同虚设。
[StronglyTypedId(Prefix = "ord_", PrefixReadMode = PrefixReadMode.Strict, JsonShape = JsonShape.Object)]
public partial record struct StrictPrefixedObjectGuidId(Guid Value);

// 前缀 + 枚举：键文本含前缀，DictionaryKeyPolicy 会把前缀一起改写导致读不回，故生成器对它不应用 policy。
[StronglyTypedId(Prefix = "pri_")]
public partial record struct PrefixedPriorityId(SamplePriority Value);

// K1：属性测试覆盖时间基元的规范文本形。这三个基元的默认格式串有损（DateTime / DateTimeOffset 的 "G"
// 丢亚秒与 Kind，TimeOnly 的 "t" 连秒都丢），故生成器固定用 "O"：值往返与文本幂等都必须成立。
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
/// <c>[Flags]</c> 枚举基元：位组合本身就是合法取值，<c>IsDefined</c> 会误杀，故生成器改用「未定义位掩码」校验
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

// N2：命名实例。值以「基元的不变文化文本形」声明，编译期校验并翻译成字面量，运行期经 Create 构造，
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

// 归一化 + 验证：Create 与两条读入路径（UTF-16 / UTF-8）都必须「先归一化、再校验」。
// 负数取绝对值，原文 -5 若先校验（要求 > 0）会被拒，先归一化则是 5。
[StronglyTypedId(Normalizer = nameof(Normalize), Validator = nameof(Validate))]
public partial record struct NormalizedValidatedInt32Id(int Value)
{
    // int.MinValue 取反会溢出，原样返回交给验证器拒绝。
    private static int Normalize(int value) => value is < 0 and not int.MinValue ? -value : value;

    private static bool Validate(int value) => value > 0;
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

// Half 的特殊值与舍入边界：NaN / ±Infinity 是合法取值（Half.Parse 接受），
// 而 (65504, 65520) 会舍入回最大有限值，都不是溢出。
[StronglyTypedId]
[StronglyTypedIdInstance("NotANumber", "NaN")]
[StronglyTypedIdInstance("PositiveInfinity", "Infinity")]
[StronglyTypedIdInstance("NegativeInfinity", "-Infinity")]
[StronglyTypedIdInstance("RoundedToMax", "65510")]
public partial record struct InstanceHalfId(Half Value);

// N2：实例必须通过验证器。哨兵值被业务规则拒绝时，类型初始化器会抛 TypeInitializationException，
// 这是刻意的取舍：宁可第一次触碰就失败，也不要让一个「不可能存在」的 Id 悄悄流进领域模型。
[StronglyTypedId(Validator = nameof(Validate))]
[StronglyTypedIdInstance("Zero", "0")]
public partial record struct RejectedInstanceInt32Id(int Value)
{
    private static bool Validate(int value) => value > 0;
}

// N12：自定义基元。Money 满足全部可选能力（IParsable + IComparable + IFormattable + span 接口），
// 用于验证「能力探测对自定义类型同样生效」—— 它会拿到 ToString(format, provider)、TryFormat 与 UTF-8 重载。
// 非泛型 IComparable 是 IStronglyTypedId<,> 对 TPrimitiveId 的实际约束，必须显式实现。
public readonly record struct Money(decimal Amount)
    : IParsable<Money>,
      IComparable<Money>,
      IComparable,
      IFormattable,
      ISpanFormattable,
      IUtf8SpanFormattable
{
    public static Money Parse(string s, IFormatProvider? provider)
        => TryParse(s, provider, out var result) ? result : throw new FormatException($"'{s}' is not a valid Money.");

    public static bool TryParse(string? s, IFormatProvider? provider, out Money result)
    {
        if (decimal.TryParse(s, NumberStyles.Number, provider ?? CultureInfo.InvariantCulture, out var amount))
        {
            result = new Money(amount);
            return true;
        }

        result = default;
        return false;
    }

    public int CompareTo(Money other) => Amount.CompareTo(other.Amount);

    public int CompareTo(object? obj) => obj is Money other ? CompareTo(other) : throw new ArgumentException("Object is not a Money.", nameof(obj));

    public override string ToString() => Amount.ToString(CultureInfo.InvariantCulture);

    public string ToString(string? format, IFormatProvider? formatProvider) => Amount.ToString(format, formatProvider);

    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
        => Amount.TryFormat(destination, out charsWritten, format, provider);

    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
        => Amount.TryFormat(utf8Destination, out bytesWritten, format, provider);
}

// N12：只满足最低契约（IParsable + IComparable，无 IFormattable / 无 span 接口）。
// 它验证「能力探测」不会因为自定义基元就一律透出格式化成员 —— 否则产物会 CS0535。
public readonly record struct Slug(string Value) : IParsable<Slug>, IComparable<Slug>, IComparable
{
    public static Slug Parse(string s, IFormatProvider? provider)
        => TryParse(s, provider, out var result) ? result : throw new FormatException($"'{s}' is not a valid Slug.");

    public static bool TryParse(string? s, IFormatProvider? provider, out Slug result)
    {
        if (!string.IsNullOrEmpty(s))
        {
            result = new Slug(s);
            return true;
        }

        result = default;
        return false;
    }

    public int CompareTo(Slug other) => string.CompareOrdinal(Value, other.Value);

    public int CompareTo(object? obj) => obj is Slug other ? CompareTo(other) : throw new ArgumentException("Object is not a Slug.", nameof(obj));

    public override string ToString() => Value;
}

[StronglyTypedId]
public partial record struct MoneyId(Money Value);

// 对象形态：值同样是基元的规范文本，不是它的公开属性。
[StronglyTypedId(JsonShape = JsonShape.Object)]
public partial record struct ObjectShapeMoneyId(Money Value);

[StronglyTypedId]
public partial record struct SlugId(Slug Value);

// 显式实现 IFormattable 的自定义基元：封装类型的常见写法，公开面只留无参 ToString()。
// 生成代码若按类型名直呼两参重载会 CS1501；若写成 ((IFormattable)value) 又会为值类型装箱。
// 用 24 字节的基元（三个引用字段）测装箱：小结构体可能被 JIT 的逃逸分析消除，那样测不出问题。
public readonly record struct ExplicitFormattableMoney(string Amount, string Currency, string Note)
    : IParsable<ExplicitFormattableMoney>, IComparable<ExplicitFormattableMoney>, IComparable, IFormattable
{
    public static ExplicitFormattableMoney Parse(string s, IFormatProvider? provider)
        => TryParse(s, provider, out var result) ? result : throw new FormatException($"'{s}' is not a valid ExplicitFormattableMoney.");

    public static bool TryParse(string? s, IFormatProvider? provider, out ExplicitFormattableMoney result)
    {
        if (decimal.TryParse(s, NumberStyles.Number, provider ?? CultureInfo.InvariantCulture, out var amount))
        {
            result = new ExplicitFormattableMoney(amount.ToString(CultureInfo.InvariantCulture), "CNY", string.Empty);
            return true;
        }

        result = default;
        return false;
    }

    public int CompareTo(ExplicitFormattableMoney other) => string.CompareOrdinal(Amount, other.Amount);

    public int CompareTo(object? obj) => obj is ExplicitFormattableMoney other ? CompareTo(other) : throw new ArgumentException("Object is not an ExplicitFormattableMoney.", nameof(obj));

    public override string ToString() => Amount;

    string IFormattable.ToString(string? format, IFormatProvider? formatProvider) => Amount;
}

[StronglyTypedId]
public partial record struct ExplicitFormattableMoneyId(ExplicitFormattableMoney Value);

// 自定义基元的命名实例：值只能在运行期校验，故生成 CreateInstance 包装。
[StronglyTypedId]
[StronglyTypedIdInstance("Zero", "0")]
[StronglyTypedIdInstance("Ten", "10.5")]
public partial record struct MoneyInstanceId(Money Value);

// 值写错的自定义基元实例：静态构造抛 TypeInitializationException，内层是带实例名的 InvalidOperationException。
// 单独成类型，避免污染上面那个正常类型。
[StronglyTypedId]
[StronglyTypedIdInstance("Broken", "not-a-money")]
public partial record struct BrokenMoneyInstanceId(Money Value);

// 引用类型基元（class）：实例可以是 null —— default(TId) 与 TryCreate / TryParse 失败的结果都是它。
// 规范文本形与各序列化写出路径都必须容忍这一点，否则会在 ToString() 上抛 NullReferenceException。
public sealed class ReferenceCode : IParsable<ReferenceCode>, IComparable<ReferenceCode>, IComparable
{
    private ReferenceCode(string value) => Value = value;

    public string Value { get; }

    public static ReferenceCode Parse(string s, IFormatProvider? provider)
        => TryParse(s, provider, out var result) ? result : throw new FormatException($"'{s}' is not a valid ReferenceCode.");

    public static bool TryParse(string? s, IFormatProvider? provider, [System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out ReferenceCode result)
    {
        if (string.IsNullOrEmpty(s))
        {
            result = null;
            return false;
        }

        result = new ReferenceCode(s);
        return true;
    }

    public int CompareTo(ReferenceCode? other) => string.CompareOrdinal(Value, other?.Value);

    public int CompareTo(object? obj) => CompareTo(obj as ReferenceCode);

    public override string ToString() => Value;
}

[StronglyTypedId]
public partial record struct ReferenceCodeId(ReferenceCode Value);

[StronglyTypedId(Prefix = "rc_")]
public partial record struct PrefixedReferenceCodeId(ReferenceCode Value);

// 引用类型基元 + IFormattable / ISpanFormattable：带格式的 ToString 与 TryFormat 同样要容忍 null 基元，
// 否则 default(TId).ToString("F", provider) 会在解引用时抛 NullReferenceException。
public sealed class FormattableReferenceCode : IParsable<FormattableReferenceCode>, IComparable<FormattableReferenceCode>, IComparable, ISpanFormattable
{
    private FormattableReferenceCode(string value) => Value = value;

    public string Value { get; }

    public static FormattableReferenceCode Parse(string s, IFormatProvider? provider)
        => TryParse(s, provider, out var result) ? result : throw new FormatException($"'{s}' is not a valid FormattableReferenceCode.");

    public static bool TryParse(string? s, IFormatProvider? provider, [System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out FormattableReferenceCode result)
    {
        if (string.IsNullOrEmpty(s))
        {
            result = null;
            return false;
        }

        result = new FormattableReferenceCode(s);
        return true;
    }

    public int CompareTo(FormattableReferenceCode? other) => string.CompareOrdinal(Value, other?.Value);

    public int CompareTo(object? obj) => CompareTo(obj as FormattableReferenceCode);

    public override string ToString() => Value;

    public string ToString(string? format, IFormatProvider? formatProvider) => Value;

    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        if (!Value.AsSpan().TryCopyTo(destination))
        {
            charsWritten = 0;
            return false;
        }

        charsWritten = Value.Length;
        return true;
    }
}

[StronglyTypedId]
public partial record struct FormattableReferenceCodeId(FormattableReferenceCode Value);

[StronglyTypedId(Prefix = "fc_")]
public partial record struct PrefixedFormattableReferenceCodeId(FormattableReferenceCode Value);

// 显式实现 IFormattable 且两参 ToString 返回 null 的自定义基元。IFormattable 的返回类型标注为非可空，
// 但运行时不强制；生成代码若直接转发就会把 null 泄漏给调用方，让 id.ToString(format, provider).Length 抛 NRE。
public readonly record struct NullReturningFormat(string Value)
    : IParsable<NullReturningFormat>, IComparable<NullReturningFormat>, IComparable, IFormattable
{
    public static NullReturningFormat Parse(string s, IFormatProvider? provider) => new(s);

    public static bool TryParse(string? s, IFormatProvider? provider, out NullReturningFormat result)
    {
        result = new NullReturningFormat(s ?? string.Empty);
        return s is not null;
    }

    public int CompareTo(NullReturningFormat other) => string.CompareOrdinal(Value, other.Value);

    public int CompareTo(object? obj) => obj is NullReturningFormat other ? CompareTo(other) : throw new ArgumentException("Object is not a NullReturningFormat.", nameof(obj));

    public override string ToString() => Value;

    public string ToString(string? format, IFormatProvider? formatProvider) => null!;
}

[StronglyTypedId]
public partial record struct NullReturningFormatId(NullReturningFormat Value);
