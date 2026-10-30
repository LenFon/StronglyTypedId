# 示例

九个示例工程，按「想学什么」挑一个看即可。全部随解决方案一起编译，`dotnet build Len.StronglyTypedId.slnx` 之后可直接运行。

| 工程 | 形态 | 演示重点 |
| --- | --- | --- |
| `Len.StronglyTypedId.SampleConsole` | 控制台（net8.0 / net10.0） | 逐节打印全部特性：核心 API、验证器、归一化、文本前缀（含格式化路径）、命名实例、异常类型、`[Flags]` 枚举、文化不变性、集成过滤与白名单、两个 JSON 库、JSON 边界（字典键 / 枚举野值 / 文本基元 / `JsonShape.Object`）、源生成 `JsonSerializerContext`、23 种基元 + enum 矩阵、string 基元的序数比较与空串语义、引用类型 Id、自定义基元（含 ULID）、配置绑定、嵌套与泛型容器、接口层次、反射（含可空 Id）、调试器视图、测试辅助（含 Bogus） |
| `Len.StronglyTypedId.SampleIntegrations` | 控制台 + 内存 SQLite | EF Core 两种注册入口（`ModelConfigurationBuilder` 约定与 `ModelBuilder` 扫描）与 Dapper `TypeHandler` 的真实读写往返（含「驱动返回类型与基元不一致」的转换路径、非法值拒绝、引用类型 Id 的 NULL 语义）；MessagePack 与 MongoDB BSON 的二进制往返（载荷是裸基元，不含文本前缀；引用类型 Id 的 null 语义）；protobuf-net 的 surrogate 契约（零注册、`DateTimeOffset` 保留 ticks 与 offset、自定义基元文本载荷、验证器拒绝抛 `ProtoException`） |
| `Len.StronglyTypedId.SampleDefaults` | 控制台 | 程序集级 `[assembly: StronglyTypedIdDefaults(...)]` 的全部选项（含 `Integrations`），以及逐类型覆盖的优先级（验证器 / 异常类型 / `JsonShape` / `PrefixReadMode` / `Integrations` 可双向覆盖，`TypeConverter` 与 `RedactValidationMessage` 只能单向打开） |
| `Len.StronglyTypedId.Domain` | 类库 | 被 Sample1 / Sample2 / Sample3 / Sample4 共用的领域模型与 Id 声明 |
| `Len.StronglyTypedId.Sample1` | ASP.NET Core MVC | 模型绑定、路由约束、Newtonsoft 格式化器、EF Core + SQL Server、Swagger |
| `Len.StronglyTypedId.Sample2` | Minimal API | 路由 / 查询参数绑定、`[AsParameters]`、内置 OpenAPI |
| `Len.StronglyTypedId.Sample3` | MVC + Swashbuckle 10 | `Microsoft.OpenApi` 2.x 下的 schema 映射 |
| `Len.StronglyTypedId.Sample4` | Minimal API + `Microsoft.AspNetCore.OpenApi` **8.0.0** | 8.0 线下的行为：包可正常引用、应用可正常启动，文档由 Swashbuckle 产出（内置转换器要 9.0+） |
| `Len.StronglyTypedId.SampleGraphQL` | HotChocolate（GraphQL） | Id 在 schema 里是 `SCALAR`，字面量种类与 JSON 形态一致；同时演示变量路径与两条失败路径 |

## 运行

```bash
# 控制台：一次性打印全部特性，不需要任何外部依赖
dotnet run --project samples/Len.StronglyTypedId.SampleConsole

# 集成：内存 SQLite，同样零配置
dotnet run --project samples/Len.StronglyTypedId.SampleIntegrations

# 程序集级默认值：零配置
dotnet run --project samples/Len.StronglyTypedId.SampleDefaults

# Web：Sample1 需要 SQL Server 连接串；Sample2 / Sample3 / Sample4 / SampleGraphQL 直接起
dotnet run --project samples/Len.StronglyTypedId.Sample2
dotnet run --project samples/Len.StronglyTypedId.Sample3
dotnet run --project samples/Len.StronglyTypedId.Sample4
dotnet run --project samples/Len.StronglyTypedId.SampleGraphQL
```

控制台示例按小节输出，每节开头是 `── 标题 ──`，可以只看关心的一段。

## 逐特性对照

「某个特性去哪个文件看」的完整对照表，以及示例遵循的三条约定，见文档站：
[示例程序 · 特性 → 示例文件](../docs/zh-CN/testing.md#samples)。
