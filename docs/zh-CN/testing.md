---
layout: default
title: 测试
---

# 测试

<a id="testing"></a>

## 测试辅助

`Len.StronglyTypedId.Testing` 用来在 arrange 阶段造 Id，不必到处写 `OrderId.Create(Guid.NewGuid())`：

```csharp
using Len.StronglyTypedId.Testing;

OrderId orderId = Any.Id<OrderId>();
IReadOnlyList<OrderId> batch = Any.Ids<OrderId>(10);
```

取值来自 `IStronglyTypedId<TSelf, TPrimitiveId>` 暴露的基元类型，因此对任意程序集声明的 Id 都有效
（包括在本测试包编译之后才生成的 Id）。覆盖生成器支持的全部基元类型，外加 `enum`。取值刻意保持
「普通」：有限浮点数（正负都有）、非空字符串、1970–2100 之间的日期 —— 免得测试因为 `double.NaN`
之类的值报出与被测代码无关的失败。

- **可复现** —— `Any.Id<OrderId>(seed: 42)` 在同一运行时版本下返回相同的值；要让一整串值重复，
  请复用同一个 `AnyIdGenerator` 实例，或用 `Any.Ids<T>(count, seed)`。
- **尊重验证器** —— 生成最多重试 `AnyIdOptions.MaxAttempts` 次（默认 100），仍不满足则抛出异常，
  并在消息中指向 `Any.Register<T>(factory)`。拒绝只按异常类型识别，因此验证器因其他原因抛
  `ArgumentException` 时同样会被当作拒绝而重试。
- **可覆盖** —— `AnyIdOptions.PrimitiveValueFactory` 可按基元类型替换取值来源；返回 `null`
  表示回落到内置生成逻辑。
- **可重置** —— `Any.Reset()` 丢弃共享生成器，连同在其上做过的全部 `Any.Register<T>(factory)`。
  注册了互相冲突的工厂的测试之间请调用它。
- **可配置文本** —— `AnyIdOptions.StringLength`（默认 8）设定生成 `string` 基元的长度；验证器要求更长
  输入时调大它。
- **按需覆盖边界** —— `AnyIdOptions.IncludeBoundaryValues` 会掺入基元的 `MinValue` / `MaxValue`
  （约每 16 个值一个）。默认关闭：极端值会让「断言唯一性」的测试偶发失败，而大多数测试要的就是普通值。
  仅对整数与 `decimal` 生效 —— 浮点数的边界是 `NaN` / `±Infinity`，它们永远不会被生成，否则 JSON 往返
  会因为与被测代码无关的原因失败。

另有两类取值值得说明：`enum` 从已声明的成员里抽，`char` 从 71 个字符的字母表（字母、数字与
`@ . - _ / :`）里抽 —— 因此「必须含分隔符」这类验证器也能在重试预算内通过。

配合 AutoFixture 使用时，加上 `Len.StronglyTypedId.Testing.AutoFixture` 并启用定制：

```csharp
var fixture = new Fixture().WithStronglyTypedIds();

OrderId orderId = fixture.Create<OrderId>();
```

完整说明见 [TESTING.md](https://github.com/LenFon/StronglyTypedId/blob/main/TESTING.md)。

<a id="samples"></a>

## 示例程序

上文每一项能力在 [`samples/`](https://github.com/LenFon/StronglyTypedId/tree/main/samples) 下都有可直接运行的示例：

| 工程 | 目标框架 | 演示内容 |
|---|---|---|
| `Len.StronglyTypedId.SampleConsole` | `net8.0;net10.0` | 核心能力巡览：`Create` / `TryCreate` / `Parse`、格式化、相等性与排序、解构、验证器、命名实例、文本前缀、`TypeConverter`、System.Text.Json 与 Newtonsoft.Json、嵌套类型、泛型容器、反射辅助、测试辅助包。 |
| `Len.StronglyTypedId.SampleIntegrations` | `net8.0` | EF Core（`ApplyTo(ModelConfigurationBuilder)`）与 Dapper（`ApplyTo()`）跑内存 SQLite，并演示程序集级 `StronglyTypedIdDefaults`。 |
| `Len.StronglyTypedId.Sample1` | `net8.0` | MVC 模型绑定与 `{id:OrderId}` 路由约束、把 Newtonsoft.Json 用作 MVC 格式化器、EF Core（SQL Server）与 Swashbuckle 6。启动需要可连接的 SQL Server。 |
| `Len.StronglyTypedId.Sample2` | `net10.0` | Minimal API：`IParsable` 绑定、`[AsParameters]`、路由约束、JSON 请求体，以及内置 OpenAPI 转换器（`AddOpenApi(ApplyTo)`）。 |
| `Len.StronglyTypedId.Sample3` | `net8.0` | MVC + Swashbuckle 10（Microsoft.OpenApi 2.x）的 schema 映射。 |
| `Len.StronglyTypedId.Sample4` | `net8.0` | `Microsoft.AspNetCore.OpenApi` 钉在 **8.0.0**：包能正常还原、应用能正常启动，文档由 Swashbuckle 产出——内置 schema 转换器要 9.0+。 |

### 特性 → 示例文件

| 特性 | 看哪里 |
| --- | --- |
| `Create` / `TryCreate` / `Parse` / `TryParse`、解构、相等性 | `SampleConsole/Demos/CoreDemo.cs` |
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
| `Any.Id` / `Any.Ids` / `Any.Register` / AutoFixture | `SampleConsole/Demos/TestingDemo.cs` |
| EF Core 值转换器 | `SampleIntegrations/EfCoreDemo.cs` |
| Dapper 类型处理器 | `SampleIntegrations/DapperDemo.cs` |
| MVC 模型绑定 + 路由约束 | `Sample1/Controllers/OrderController.cs` |
| Minimal API 绑定 + `[AsParameters]` | `Sample2/Program.cs` |
| OpenAPI / Swagger schema 映射 | `Sample2/Program.cs`、`Sample3/Program.cs` |
| OpenAPI 8.0 替代路径（文档由 Swashbuckle 产出） | `Sample4/Program.cs` |

```shell
dotnet run --project samples/Len.StronglyTypedId.SampleConsole
dotnet run --project samples/Len.StronglyTypedId.SampleIntegrations
```

控制台示例逐步打印后退出；Web 示例的文档地址分别是 `/openapi/v1.json`（Sample2）与
`/swagger/v1/swagger.json`（Sample1、Sample3、Sample4）。

### 约定

- 示例里的 Id 都用 `record struct`；`SampleConsole/Ids.cs` 另有 `record` 引用类型与嵌套 / 泛型容器的例子。
- 示例代码刻意不写 `new OrderId(...)`：直接构造会绕过验证器与归一化，分析器 `STIA0011` 会对配了验证器的 Id 报警。
- `SampleConsole` 同时面向 `net8.0` 与 `net10.0`：UTF-8 span 那一节只在 `net10.0` 下编译，用来演示 `IUtf8SpanParsable` 的可用性差异。

[<- 返回目录](index.md) · [English](../testing.md)
