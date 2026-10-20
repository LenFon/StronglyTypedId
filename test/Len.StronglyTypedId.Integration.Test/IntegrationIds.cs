// G2 集成运行时测试专用的本地 Id 声明。
// 集成生成器（Dapper / EF Core / Swagger / 内置 OpenApi / MVC）按「本编译里声明的 [StronglyTypedId]
// + 本编译的包引用」门控触发，产物落在本编译。TestBase 的 Id 没引这些包，集成产物不在 TestBase 里生成，
// 故新工程自身必须声明至少一个 Id，才能让生成器在本编译里产出集成 ApplyTo。
namespace Len.StronglyTypedId;

[StronglyTypedId]
public partial record struct IntegrationGuidId(Guid Value);

// C10：文本前缀。用来验证「前缀经规范文本形自动贯通 MVC 路由约束 / Minimal API 绑定」——
// 这两处生成器零改动，全靠 Id 的 TryParse / ToString 走文本路径。
[StronglyTypedId(Prefix = "ord_")]
public partial record struct PrefixedIntegrationGuidId(Guid Value);

// 引用类型 Id（record class）：专门覆盖 Dapper 处理器的引用类型分支。该分支曾把整个 Id 包装对象
// 赋给 DbParameter.Value，驱动绑定时会抛「No mapping exists from object type ...」；
// 此前测试矩阵里全是 record struct，这条路径零覆盖。
[StronglyTypedId]
public partial record IntegrationReferenceId(Guid Value);
