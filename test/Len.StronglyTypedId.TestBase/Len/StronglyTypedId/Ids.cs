namespace Len.StronglyTypedId;

[StronglyTypedId]
public partial record struct GuidId(Guid Value);

[StronglyTypedId]
public partial record struct Int32Id(int Value);

[StronglyTypedId]
public partial record struct UInt32Id(uint Value);

[StronglyTypedId]
public partial record struct Int64Id(long Value);

[StronglyTypedId]
public partial record struct UInt64Id(ulong Value);

[StronglyTypedId]
public partial record struct StringId(string Value);

[StronglyTypedId]
public partial record struct ByteId(byte Value);

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

public partial record NotStronglyTypedId();

// readonly record struct 形态：生成器必须把 readonly 修饰符透出到生成的 partial 段，否则与使用者
// 写下的 readonly 段对「是否 readonly struct」不一致而编译失败。
[StronglyTypedId]
public readonly partial record struct ReadonlyGuidId(Guid Value);

// 逐类型开启 TypeConverter：生成的嵌套转换器经 TypeDescriptor 在字符串与 Id 间往返，
// 覆盖 XmlSerializer / IConfiguration 绑定 / Newtonsoft 字典键读路径等场景。
[StronglyTypedId(TypeConverter = true)]
public partial record struct ConvertibleGuidId(Guid Value);

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

