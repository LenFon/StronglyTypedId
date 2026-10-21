# 示例

七个示例工程，按「想学什么」挑一个看即可。全部随解决方案一起编译，`dotnet build Len.StronglyTypedId.slnx` 之后可直接运行。

| 工程 | 形态 | 演示重点 |
| --- | --- | --- |
| `Len.StronglyTypedId.SampleConsole` | 控制台（net8.0 / net10.0） | 逐节打印全部特性：核心 API、验证器、归一化、文本前缀、命名实例、异常类型、`[Flags]` 枚举、文化不变性、集成过滤、两个 JSON 库、嵌套与泛型容器、反射、测试辅助 |
| `Len.StronglyTypedId.SampleIntegrations` | 控制台 + 内存 SQLite | EF Core `ValueConverter` 与 Dapper `TypeHandler` 的真实读写往返（含「驱动返回类型与基元不一致」的转换路径）；MessagePack 格式化器与 MongoDB BSON 序列化器的二进制往返（载荷是裸基元，不含文本前缀） |
| `Len.StronglyTypedId.Domain` | 类库 | 被 Sample1 / Sample2 / Sample3 / Sample4 共用的领域模型与 Id 声明 |
| `Len.StronglyTypedId.Sample1` | ASP.NET Core MVC | 模型绑定、路由约束、Newtonsoft 格式化器、EF Core + SQL Server、Swagger |
| `Len.StronglyTypedId.Sample2` | Minimal API | 路由 / 查询参数绑定、`[AsParameters]`、内置 OpenAPI |
| `Len.StronglyTypedId.Sample3` | MVC + Swashbuckle 10 | `Microsoft.OpenApi` 2.x 下的 schema 映射 |
| `Len.StronglyTypedId.Sample4` | Minimal API + `Microsoft.AspNetCore.OpenApi` **8.0.0** | 8.0 线下的行为：包可正常引用、应用可正常启动，文档由 Swashbuckle 产出（内置转换器要 9.0+） |

## 运行

```bash
# 控制台：一次性打印全部特性，不需要任何外部依赖
dotnet run --project samples/Len.StronglyTypedId.SampleConsole

# 集成：内存 SQLite，同样零配置
dotnet run --project samples/Len.StronglyTypedId.SampleIntegrations

# Web：Sample1 需要 SQL Server 连接串；Sample2 / Sample3 / Sample4 直接起
dotnet run --project samples/Len.StronglyTypedId.Sample2
dotnet run --project samples/Len.StronglyTypedId.Sample3
dotnet run --project samples/Len.StronglyTypedId.Sample4
```

控制台示例按小节输出，每节开头是 `── 标题 ──`，可以只看关心的一段。

## 逐特性对照

「某个特性去哪个文件看」的完整对照表，以及示例遵循的三条约定，见文档站：
[示例程序 · 特性 → 示例文件](../docs/zh-CN/testing.md#samples)。
