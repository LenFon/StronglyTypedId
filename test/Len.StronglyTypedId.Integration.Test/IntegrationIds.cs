// G2 集成运行时测试专用的本地 Id 声明：集成生成器按「本编译声明的 [StronglyTypedId] + 本编译的包引用」门控，TestBase 没引这些包，故新工程必须自己声明至少一个 Id。
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
