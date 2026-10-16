// G2 集成运行时测试专用的本地 Id 声明。
// 集成生成器（Dapper / EF Core / Swagger / 内置 OpenApi / MVC）按「本编译里声明的 [StronglyTypedId]
// + 本编译的包引用」门控触发，产物落在本编译。TestBase 的 Id 没引这些包，集成产物不在 TestBase 里生成，
// 故新工程自身必须声明至少一个 Id，才能让生成器在本编译里产出集成 ApplyTo。
namespace Len.StronglyTypedId;

[StronglyTypedId]
public partial record struct IntegrationGuidId(Guid Value);
