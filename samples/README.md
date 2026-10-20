# 示例

六个示例工程，按「想学什么」挑一个看即可。全部随解决方案一起编译，`dotnet build Len.StronglyTypedId.slnx` 之后可直接运行。

| 工程 | 形态 | 演示重点 |
| --- | --- | --- |
| `Len.StronglyTypedId.SampleConsole` | 控制台（net8.0 / net10.0） | 逐节打印全部特性：核心 API、验证器、归一化、文本前缀、命名实例、异常类型、`[Flags]` 枚举、文化不变性、集成过滤、两个 JSON 库、嵌套与泛型容器、反射、测试辅助 |
| `Len.StronglyTypedId.SampleIntegrations` | 控制台 + 内存 SQLite | EF Core `ValueConverter` 与 Dapper `TypeHandler` 的真实读写往返（含「驱动返回类型与基元不一致」的转换路径） |
| `Len.StronglyTypedId.Domain` | 类库 | 被 Sample1 / Sample2 / Sample3 共用的领域模型与 Id 声明 |
| `Len.StronglyTypedId.Sample1` | ASP.NET Core MVC | 模型绑定、路由约束、Newtonsoft 格式化器、EF Core + SQL Server、Swagger |
| `Len.StronglyTypedId.Sample2` | Minimal API | 路由 / 查询参数绑定、`[AsParameters]`、内置 OpenAPI |
| `Len.StronglyTypedId.Sample3` | MVC + Swashbuckle 10 | `Microsoft.OpenApi` 2.x 下的 schema 映射 |

## 运行

```bash
# 控制台：一次性打印全部特性，不需要任何外部依赖
dotnet run --project samples/Len.StronglyTypedId.SampleConsole

# 集成：内存 SQLite，同样零配置
dotnet run --project samples/Len.StronglyTypedId.SampleIntegrations

# Web：Sample1 需要 SQL Server 连接串；Sample2 / Sample3 直接起
dotnet run --project samples/Len.StronglyTypedId.Sample2
dotnet run --project samples/Len.StronglyTypedId.Sample3
```

控制台示例按小节输出，每节开头是 `── 标题 ──`，可以只看关心的一段。

## 特性 → 示例对照

| 特性 | 看哪里 |
| --- | --- |
| `Create` / `TryCreate` / `Parse` / `TryParse` / 解构 / 相等性 | `SampleConsole/Demos/CoreDemo.cs` |
| 验证器、归一化（先修正再校验） | `SampleConsole/Demos/ValidationDemo.cs` |
| 文本前缀（宽松 / 严格读取）、`JsonShape.Object`、`TypeConverter` | `SampleConsole/Demos/TextDemo.cs` |
| 命名实例 `[StronglyTypedIdInstance]` | `SampleConsole/Demos/InstanceDemo.cs` |
| 验证失败异常类型 `ExceptionType` | `SampleConsole/Demos/ExceptionDemo.cs` |
| `[Flags]` 枚举的位校验 | `SampleConsole/Demos/FlagsDemo.cs` |
| 文化不变性（de-DE 小数逗号） | `SampleConsole/Demos/CultureDemo.cs` |
| 集成过滤 `Integrations = None` | `SampleConsole/Demos/IntegrationsDemo.cs` |
| System.Text.Json（含字典键、显式注册） | `SampleConsole/Demos/SystemTextJsonDemo.cs` |
| Newtonsoft.Json（含字典键、显式注册） | `SampleConsole/Demos/NewtonsoftJsonDemo.cs` |
| 嵌套类型、泛型容器、泛型工厂 | `SampleConsole/Demos/StructureDemo.cs` |
| 运行时反射识别 Id 与基元 | `SampleConsole/Demos/ReflectionDemo.cs` |
| `Any.Id` / `Any.Ids` / `Register` / AutoFixture | `SampleConsole/Demos/TestingDemo.cs` |
| EF Core 值转换器 | `SampleIntegrations/EfCoreDemo.cs` |
| Dapper 类型处理器 | `SampleIntegrations/DapperDemo.cs` |
| MVC 模型绑定 + 路由约束 | `Sample1/Controllers/OrderController.cs` |
| Minimal API 绑定 + `[AsParameters]` | `Sample2/Program.cs` |
| OpenAPI / Swagger schema 映射 | `Sample2/Program.cs`、`Sample3/Program.cs` |

## 约定

- 示例里的 Id 都用 `record struct`（`SampleConsole/Ids.cs` 另有 `record` 引用类型与嵌套/泛型容器的例子）。
- 示例代码刻意不写 `new OrderId(...)`：直接构造会绕过验证器与归一化，分析器 `STIA0011` 会对配了验证器的 Id 报警。
- `SampleConsole` 同时面向 `net8.0` 与 `net10.0`：UTF-8 span 那一节只在 `net10.0` 下编译，用来演示 `IUtf8SpanParsable` 的可用性差异。
