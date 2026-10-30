// G2 集成运行时测试专用的本地 Id 声明：集成生成器按「本编译声明的 [StronglyTypedId] + 本编译的包引用」门控，TestBase 没引这些包，故新工程必须自己声明至少一个 Id。
using System.Globalization;

namespace Len.StronglyTypedId;

[StronglyTypedId]
public partial record struct IntegrationGuidId(Guid Value);

// C10：文本前缀。用来验证「前缀经规范文本形自动贯通 MVC 路由约束 / Minimal API 绑定」，
// 这两处生成器零改动，全靠 Id 的 TryParse / ToString 走文本路径。
[StronglyTypedId(Prefix = "ord_")]
public partial record struct PrefixedIntegrationGuidId(Guid Value);

// 引用类型 Id（record class）：专门覆盖 Dapper 处理器的引用类型分支。该分支曾把整个 Id 包装对象
// 赋给 DbParameter.Value，驱动绑定时会抛「No mapping exists from object type ...」；
// 此前测试矩阵里全是 record struct，这条路径零覆盖。
[StronglyTypedId]
public partial record IntegrationReferenceId(Guid Value);

// 配置绑定（Microsoft.Extensions.Configuration）：binder 的字符串转换走 TypeDescriptor，
// 因此只有开了 TypeConverter 的 Id 才能被绑定。不开的那个用来钉住「静默得到 default 而不是报错」——
// 这是最容易踩的坑：配置里明明写了值，绑出来却是全零 Guid，编译与运行都不报错。
[StronglyTypedId(TypeConverter = true)]
public partial record struct ConvertibleIntegrationGuidId(Guid Value);

[StronglyTypedId]
public partial record struct PlainIntegrationGuidId(Guid Value);

// 引用类型 + TypeConverter：值类型与引用类型在 binder 里的失败表现不同（前者留 default、后者留 null），
// 故两种都要有可用路径的证据。
[StronglyTypedId(TypeConverter = true)]
public partial record IntegrationConvertibleReferenceId(Guid Value);

// 前缀 + TypeConverter：确认前缀在 binder 这条路径上同样生效——读入带前缀的文本、写回也是同一形态。
[StronglyTypedId(Prefix = "ord_", TypeConverter = true)]
public partial record struct PrefixedConvertibleIntegrationGuidId(Guid Value);

// 验证器拒绝时，二进制反序列化必须抛框架自己的序列化异常，而不是让 Create 的 ArgumentException 穿透。
[StronglyTypedId(Validator = nameof(Validate))]
public partial record struct ValidatedIntegrationGuidId(Guid Value)
{
    private static bool Validate(Guid value) => value != Guid.Empty;
}

// 泛型容器里的 Id：转换器类照常生成，但 [JsonConverter] 特性无法引用含类型参数的转换器（CS0416），
// 且集成注册入口会跳过它（开放泛型不能作类型实参）。JSON 支持因此只能按闭合类型手动注册。
public partial class IntegrationBox<T>
{
    [StronglyTypedId]
    public partial record struct BoxedGuidId(Guid Value);
}

// 对象 JSON 形：线上报文是 {"value":"<guid>"}。OpenAPI 若仍按裸基元发 schema，
// 生成的客户端会按字符串收发，与服务端实际报文不符。
[StronglyTypedId(JsonShape = JsonShape.Object)]
public partial record struct ObjectShapeIntegrationGuidId(Guid Value);

// N12：自定义基元。集成产物（EF Core 转换器 / Dapper 处理器 / OpenAPI schema）对它是通用的，
// 但「通用」需要端到端证据：EF 的 ValueConverter 与 Dapper 的 Parse 都按基元类型生成代码，
// 自定义类型若落到按短名分派的分支就会生成不存在的 Convert.ToXxx。
public readonly record struct IntegrationMoney(decimal Amount)
    : IParsable<IntegrationMoney>, IComparable<IntegrationMoney>, IComparable
{
    public static IntegrationMoney Parse(string s, IFormatProvider? provider)
        => TryParse(s, provider, out var result) ? result : throw new FormatException($"'{s}' is not a valid IntegrationMoney.");

    public static bool TryParse(string? s, IFormatProvider? provider, out IntegrationMoney result)
    {
        if (decimal.TryParse(s, NumberStyles.Number, provider ?? CultureInfo.InvariantCulture, out var amount))
        {
            result = new IntegrationMoney(amount);
            return true;
        }

        result = default;
        return false;
    }

    public int CompareTo(IntegrationMoney other) => Amount.CompareTo(other.Amount);

    public int CompareTo(object? obj) => obj is IntegrationMoney other ? CompareTo(other) : throw new ArgumentException("not an IntegrationMoney", nameof(obj));

    public override string ToString() => Amount.ToString(CultureInfo.InvariantCulture);
}

[StronglyTypedId]
public partial record struct IntegrationMoneyId(IntegrationMoney Value);

// ULID：第三方库提供的自定义基元，本库对它零改动。Cysharp 的 Ulid 同时实现 IParsable<Ulid>、
// IComparable<Ulid> 与非泛型 IComparable，正好满足契约 —— 这里放一个真实 Id 是为了让
// 「不需要为 ULID 加任何代码」这句话有可执行的证据。
[StronglyTypedId]
public partial record struct IntegrationUlidId(Ulid Value);

// 带前缀的 ULID：文本形是 "ord_" + 26 字符 Crockford Base32。
[StronglyTypedId(Prefix = "ord_")]
public partial record struct PrefixedIntegrationUlidId(Ulid Value);

// ULID 的命名实例：值在运行期经 Ulid.Parse 还原。
[StronglyTypedId]
[StronglyTypedIdInstance("Nil", "00000000000000000000000000")]
public partial record struct InstanceIntegrationUlidId(Ulid Value);

// protobuf-net：覆盖「有原生表示」与「只能走文本」两类基元。
// 引用程序集里的 Id 拿不到 surrogate（特性只能加在本次编译声明的类型上），故这些必须声明在本工程。
[StronglyTypedId]
public partial record IntegrationProtobufReferenceId(string Value);

[StronglyTypedId(Validator = nameof(Validate))]
public partial record struct IntegrationValidatedGuidId(Guid Value)
{
    private static bool Validate(Guid value) => value != Guid.Empty;
}

[StronglyTypedId(Prefix = "ord_")]
public partial record struct PrefixedProtobufGuidId(Guid Value);

// protobuf-net 3.2.30 实测没有原生表示的五种基元：载荷必须是规范文本。
[StronglyTypedId]
public partial record struct IntegrationDateTimeOffsetId(DateTimeOffset Value);

[StronglyTypedId]
public partial record struct IntegrationInt128Id(Int128 Value);

[StronglyTypedId]
public partial record struct IntegrationUInt128Id(UInt128 Value);

[StronglyTypedId]
public partial record struct IntegrationHalfId(Half Value);

[StronglyTypedId]
public partial record struct IntegrationBigIntegerId(System.Numerics.BigInteger Value);

// 有原生表示、但容易误判的三种：protobuf-net 能直接编码它们，不该退回文本。
[StronglyTypedId]
public partial record struct IntegrationProtobufEnumId(SampleStatus Value);

[StronglyTypedId]
public partial record struct IntegrationProtobufDateOnlyId(DateOnly Value);

[StronglyTypedId]
public partial record struct IntegrationProtobufDecimalId(decimal Value);

[StronglyTypedId]
public partial record struct IntegrationProtobufCharId(char Value);
