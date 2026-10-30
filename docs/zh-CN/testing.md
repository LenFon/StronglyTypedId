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

- **可复现** —— `Any.Id<OrderId>(seed: 42)` 跨 .NET 版本、跨平台返回相同的值，而不只是在同一运行时
  版本内：取值源是内置的 SplitMix64，不是算法属实现细节的 `Random`。要让一整串值重复，
  请复用同一个 `AnyIdGenerator` 实例，或用 `Any.Ids<T>(count, seed)`。长期复用的生成器用
  `new AnyIdOptions { Seed = 42 }` 传同一个种子；不传则按时钟播种。
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

另有两类取值值得说明：`enum` 从已声明的成员里抽，`char` 从 68 个字符的字母表（字母、数字与
`@ . - _ / :`）里抽 —— 因此「必须含分隔符」这类验证器也能在重试预算内通过。

`AnyIdGenerator.Create(Type)` 是 `Any.Id<T>()` 的非泛型形式，用于 Id 类型只在运行期才知道的场合——
对 `Type[]` 做 theory，或反射驱动的测试。传入不是强类型 Id 的类型时抛 `ArgumentException`。

### 检查命名实例

`[StronglyTypedIdInstance]` 的值若被 Id 的 `Validator` 拒绝，编译期不会发现：类型初始化器会在该类型第一次
被触碰时抛 `TypeInitializationException`，在测试套件里这可能离声明处很远才暴露。`StronglyTypedIdConformance`
一次报出全部坏实例：

```csharp
// 抛出异常并列出全部坏实例；可作为每个程序集一行的测试。
StronglyTypedIdConformance.AssertAllInstancesValid(typeof(OrderId).Assembly);

// 或者自己检查列表。
IReadOnlyList<(Type IdType, string Message)> broken =
    StronglyTypedIdConformance.FindBrokenInstances(typeof(OrderId).Assembly);
```

自定义基元正是它存在的理由：生成器跑在 `netstandard2.0` 上，无法执行你的 `Parse`，所以这类实例值只能在
运行期校验。

配合 AutoFixture 使用时，加上 `Len.StronglyTypedId.Testing.AutoFixture` 并启用定制：

```csharp
var fixture = new Fixture().WithStronglyTypedIds();

OrderId orderId = fixture.Create<OrderId>();
```

配合 Bogus 使用时，加上 `Len.StronglyTypedId.Testing.Bogus` 并启用定制：

```csharp
var faker = new Faker<Order>().WithStronglyTypedIds();

Order order = faker.Generate();   // order.Id 已经是造好的 Id
```

Bogus 按属性的声明类型填值，而强类型 Id 是一个独立的类型 —— 没有这层适配，Bogus 无从下手，属性就停在
默认值上，且**不报任何错**。适配之后，Bogus 会为每个 Id 类型登记自己的取值规则，生成时照常填上。

Id 类型默认从**当前已加载的程序集**里发现；某个 Id 所在的程序集尚未加载时，显式列出即可：

```csharp
var faker = new Faker<Order>()
    .WithStronglyTypedIds(new AnyIdGenerator(), typeof(OrderId), typeof(CustomerId));
```

完整说明见 [TESTING.md](https://github.com/LenFon/StronglyTypedId/blob/main/TESTING.md)。

<a id="samples"></a>

## 示例程序

上文每一项能力在 [`samples/`](https://github.com/LenFon/StronglyTypedId/tree/main/samples) 下都有可直接运行的示例。
挑一个最接近你要做的事的工程看即可：

| 工程 | 目标框架 | 演示内容 |
|---|---|---|
| `Len.StronglyTypedId.SampleConsole` | `net8.0;net10.0` | 核心能力巡览：`Create` / `TryCreate` / `Parse`、格式化、相等性与排序、解构、验证器、归一化、命名实例、文本前缀（含格式化路径）、`TypeConverter`、集成过滤与白名单、System.Text.Json 与 Newtonsoft.Json、JSON 边界（字典键 / 枚举野值 / 文本基元 / `JsonShape.Object`）、源生成 `JsonSerializerContext`、23 种基元 + enum 矩阵、string 基元的序数比较与空串语义、引用类型 Id、自定义基元（含 ULID）、配置绑定、嵌套类型、泛型容器、接口层次、反射辅助（含可空 Id）、调试器视图、测试辅助包（含 Bogus）。**建议从这里开始。** |
| `Len.StronglyTypedId.SampleIntegrations` | `net8.0` | EF Core 两种注册入口（`AddStronglyTypedIds(ModelConfigurationBuilder)` 与 `AddStronglyTypedIds(ModelBuilder)`）与 Dapper（`AddStronglyTypedIds()`）跑内存 SQLite，含「驱动返回类型与基元不一致」的转换路径、非法值拒绝与引用类型 Id 的 NULL 语义；MessagePack、MongoDB BSON 与 protobuf-net 的往返（含引用类型 Id 的 null 语义与验证器拒绝）。 |
| `Len.StronglyTypedId.SampleDefaults` | `net8.0` | 程序集级 `[assembly: StronglyTypedIdDefaults(...)]` 的全部选项（含 `Integrations`），以及逐类型覆盖的优先级（哪些能双向覆盖、哪些只能单向打开）。 |
| `Len.StronglyTypedId.Sample1` | `net8.0` | MVC 模型绑定与 `{id:OrderId}` 路由约束、把 Newtonsoft.Json 用作 MVC 格式化器、EF Core（SQL Server）与 Swashbuckle 6。启动需要可连接的 SQL Server。 |
| `Len.StronglyTypedId.Sample2` | `net10.0` | Minimal API：`IParsable` 绑定、`[AsParameters]`、路由约束、JSON 请求体，以及内置 OpenAPI 转换器（`AddOpenApi(options => options.AddStronglyTypedIds())`）。 |
| `Len.StronglyTypedId.Sample3` | `net8.0` | MVC + Swashbuckle 10（Microsoft.OpenApi 2.x）的 schema 映射。 |
| `Len.StronglyTypedId.Sample4` | `net8.0` | `net8.0` 上的 `Microsoft.AspNetCore.OpenApi` 8.0.0：内置 schema 转换器要 9.0+，因此文档改由 Swashbuckle 产出。 |
| `Len.StronglyTypedId.SampleGraphQL` | `net8.0` | HotChocolate（`IRequestExecutorBuilder` 上的 `AddStronglyTypedIds()`）：Id 在 schema 里是 `SCALAR`，字面量种类与 JSON 形态一致——`Guid` 与带前缀的 Id 是 `String`，整数与枚举（按底层数值）是 `Int`，浮点是 `Float`。同时演示变量路径（`JsonElement`）与两条失败路径。 |

每个示例都很小且自包含；控制台示例按特性分节打印，可以对照代码看输出。运行方式：

```shell
dotnet run --project samples/Len.StronglyTypedId.SampleConsole
dotnet run --project samples/Len.StronglyTypedId.SampleIntegrations
dotnet run --project samples/Len.StronglyTypedId.SampleDefaults
dotnet run --project samples/Len.StronglyTypedId.SampleGraphQL
```

控制台示例逐步打印后退出；Web 示例的文档地址分别是 `/openapi/v1.json`（Sample2）与
`/swagger/v1/swagger.json`（Sample1、Sample3、Sample4）。

### 阅读示例时

- 示例里的 Id 都用 `record struct`，这是推荐的默认选择。`SampleConsole` 还演示了嵌套 Id 与泛型容器里的 Id。
- 示例代码不对配了验证器的 Id 写 `new OrderId(...)`，因为直接构造会绕过验证器与归一化。取值请走 `Create`。
- `SampleConsole` 同时面向 `net8.0` 与 `net10.0`：UTF-8 span 那一节只在 `net10.0` 下编译，
  `IUtf8SpanParsable` 正是从那里开始可用。

[<- 返回目录](index.md) · [English](../testing.md)
