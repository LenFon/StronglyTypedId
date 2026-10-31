# StronglyTypedId

> 英文文档：[README.md](README.md) · 文档站：[lenfon.github.io/StronglyTypedId](https://lenfon.github.io/StronglyTypedId/)

[![codecov][badge-codecov]](https://app.codecov.io/gh/LenFon/StronglyTypedId/tree/main)
[![NuGet][badge-nuget]][nuget-package]
[![License: MIT][badge-license]][license-file]

一个源码生成器、分析器与代码修复（Code Fix）工具。给一行 `record` 声明加上特性，就得到一个功能完备的
强类型 Id：`IStronglyTypedId<TSelf, TPrimitiveId>` 实现、`IParsable<TSelf>` 支持，以及 JSON /
Entity Framework Core / Swagger 集成。`Guid` 与 `OrderId` 从此不会互相误传。

<a id="top"></a>

## 目录

- [功能特性](#features)
- [快速开始](#getting-started)
- [支持的基元类型](#supported-primitive-types)
- [生成器会生成什么](#what-gets-generated)
- [文档站](#documentation)
- [已知限制](#limitations)
- [许可证](#license)

<a id="features"></a>

## 功能特性 <a href="#top" style="float:right">↑ 返回目录</a>

- **一行声明。** 用 `[StronglyTypedId]` 标注一个 `partial record`（或 `partial record struct`），
  类型就具备解析、格式化、比较、`Create` / `TryCreate`、解构、JSON 转换器与调试器视图。
- **`Guid` 冒充不了 `OrderId`。** 编译器会拒绝这种替换；内建分析器把不合法的声明作为错误报出，
  26 条规则里有 21 条附带代码修复。
- **23 种基元，或你自己的类型。** 全部数值类型、`Guid`、`string`、日期与 `BigInteger` 都是内置的，
  另外还支持任意 `enum`，以及任何实现了 `IParsable<TSelf>` 与 `IComparable` 的类型。ULID 零改动即可用。
- **在取值进入的地方校验。** 把 `Validator` 与 `Normalizer` 指向静态方法，所有生成的入口都会执行它们：
  `Create`、`Parse`、JSON、EF Core、Dapper 与模型绑定。
- **11 个集成，只在引用对应包时才生成。** System.Text.Json、Newtonsoft.Json、EF Core、Dapper、
  Swashbuckle、ASP.NET Core MVC 与 OpenAPI、MessagePack、MongoDB、HotChocolate 与 protobuf-net。
- **原生 AOT、裁剪，且零第三方依赖。** 运行时库不按名字加载任何类型，包也不会往你的依赖图里塞东西。

完整清单（每个生成成员、每个配置项、每个集成）见[功能特性](docs/zh-CN/features.md)。

<a id="getting-started"></a>

## 快速开始 <a href="#top" style="float:right">↑ 返回目录</a>

1. 将包安装到你的应用程序或类库中：

   ```text
   Package Manager : Install-Package Len.StronglyTypedId
   CLI             : dotnet add package Len.StronglyTypedId
   ```

   只需安装这一个包：`Len.StronglyTypedId` 已经依赖 `Len.StronglyTypedId.Generators`，生成器、分析器与
   代码修复都在后者之中。该依赖是传递的，**每个引用主包的项目都会同时装上分析器**。为什么这么设计、
   以及不想要诊断时怎么关，见 [NuGet 包](docs/zh-CN/packages.md)。

2. 用单一主构造函数参数（参数名必须为 `Value`）声明一个强类型 Id：

   ```csharp
   [StronglyTypedId]
   public partial record struct OrderId(Guid Value);

   // 或

   [StronglyTypedId]
   public partial record OrderId(Guid Value);
   ```

   > **`record` 与 `record struct` 的取舍。** 一个是值类型、一个是引用类型，生成的成员完全一致。
   > 取舍依据见[声明形状](docs/zh-CN/declaration.md#the-declaration)。

   生成的类型会暴露 `Value`、静态的 `Create(...)` 工厂，以及 `Parse` / `TryParse`。
   凡是原本传递裸基元类型的地方，都可以换成它：

   ```csharp
   public class Order
   {
       public OrderId Id { get; set; }
       public UserId Buyer { get; set; }
   }

   var id = OrderId.Create(Guid.NewGuid());           // 静态工厂
   var parsed = OrderId.Parse("...", null);           // IParsable<TSelf>
   OrderId.TryParse("...", null, out var alsoParsed); // 失败时返回 false，而不是抛异常
   ```

   `Value` 参数可以带可空上下文，但类型本身不能可空：`OrderId(Guid? Value)` 会被分析器拒绝
   （[STIA0006](docs/zh-CN/diagnostics.md#diagnostics)）。

<a id="supported-primitive-types"></a>

## 支持的基元类型 <a href="#top" style="float:right">↑ 返回目录</a>

被包装的 `Value` 参数可以是以下类型之一：

<!-- BEGIN GENERATED:primitive-types -->
| C# 类型          | 声明示例                                               |
| ---------------- | ------------------------------------------------------ |
| `Guid`           | `record struct GuidId(Guid Value)`                     |
| `string`         | `record StringId(string Value)`                        |
| `char`           | `record struct CharId(char Value)`                     |
| `byte`           | `record struct ByteId(byte Value)`                     |
| `sbyte`          | `record struct SByteId(sbyte Value)`                   |
| `short`          | `record struct Int16Id(short Value)`                   |
| `ushort`         | `record struct UInt16Id(ushort Value)`                 |
| `int`            | `record struct Int32Id(int Value)`                     |
| `uint`           | `record struct UInt32Id(uint Value)`                   |
| `long`           | `record struct Int64Id(long Value)`                    |
| `ulong`          | `record struct UInt64Id(ulong Value)`                  |
| `Int128`         | `record struct Int128Id(Int128 Value)`                 |
| `UInt128`        | `record struct UInt128Id(UInt128 Value)`               |
| `decimal`        | `record struct DecimalId(decimal Value)`               |
| `Half`           | `record struct HalfId(Half Value)`                     |
| `float`          | `record struct SingleId(float Value)`                  |
| `double`         | `record struct DoubleId(double Value)`                 |
| `DateTime`       | `record struct DateTimeId(DateTime Value)`             |
| `DateTimeOffset` | `record struct DateTimeOffsetId(DateTimeOffset Value)` |
| `TimeSpan`       | `record struct TimeSpanId(TimeSpan Value)`             |
| `DateOnly`       | `record struct DateOnlyId(DateOnly Value)`             |
| `TimeOnly`       | `record struct TimeOnlyId(TimeOnly Value)`             |
| `BigInteger`     | `record struct BigIntegerId(BigInteger Value)`         |
| 任意 `enum`      | `record StatusId(Status Value)`                        |
<!-- END GENERATED:primitive-types -->

类型按名称匹配，因此写成 `System.Guid`（或使用别名/using）效果完全相同。仅识别上表中的类型。
你自己定义的、恰好也叫 `Guid` 的类型并不受支持，会被 [STIA0008](docs/zh-CN/diagnostics.md#diagnostics) 拒绝。
任意 `enum` 同样受支持；枚举 Id 如何解析、省略了哪些接口，见
[枚举 Id](docs/zh-CN/generated-code.md#enum-backed-ids)。

<a id="custom-primitives"></a>

### 自定义基元

你自己写的类型只要满足以下契约，就可以作为基元：

- 实现 `IParsable<TSelf>` 与 `IComparable<TSelf>`，且类型实参是自身；
- 实现非泛型 `System.IComparable`。这才是 `IStronglyTypedId<TSelf, TPrimitiveId>` 对 `TPrimitiveId`
  的实际约束，`IComparable<T>` 并不蕴含它；
- 暴露**公开静态**的 `Parse(string, IFormatProvider)` 与 `TryParse(string, IFormatProvider, out TSelf)`。
  显式接口实现不算，因为生成代码按类型名直呼它们；
- 是非抽象、非静态、非泛型的 struct 或 class，且不是 `System.Uri`。

```csharp
public readonly record struct Email(string Value)
    : IParsable<Email>, IComparable<Email>, IComparable
{
    public static Email Parse(string s, IFormatProvider? provider)
        => TryParse(s, provider, out var result) ? result : throw new FormatException(s);

    public static bool TryParse(string? s, IFormatProvider? provider, out Email result)
    {
        result = new Email(s ?? string.Empty);
        return s is not null;
    }

    public int CompareTo(Email other) => string.CompareOrdinal(Value, other.Value);

    public int CompareTo(object? obj) => obj is Email other ? CompareTo(other) : throw new ArgumentException();

    public override string ToString() => Value;
}

[StronglyTypedId]
public partial record struct EmailId(Email Value);
```

- **`ToString()` 必须产出你自己的 `Parse` 能读回的文本。** 所有序列化路径写的都是基元的规范文本：
  System.Text.Json、Newtonsoft.Json、MessagePack、BSON 与 Dapper 全都如此，因此同一个 Id 不会在 REST
  是一种形态、在数据库里是另一种。把自定义类型交给序列化器，它会按公开属性写出、读回 `default`。
- **命名实例的值在运行期校验，不在编译期。** 生成器跑在 `netstandard2.0` 上，无法执行你的 `Parse`，
  所以 `[StronglyTypedIdInstance("Unknown", "…")]` 只在首次触碰该类型时才失败。失败是一个
  `InvalidOperationException`，消息里带实例名与 Id 类型名；`Len.StronglyTypedId.Testing` 的
  `StronglyTypedIdConformance.AssertAllInstancesValid(assembly)` 能一次报出全部坏实例。

**ULID 不需要本库写任何代码。** Cysharp 的 [`Ulid`](https://www.nuget.org/packages/Ulid) 是第三方包，
需要你自己引入；本库不依赖它，也不会为它生成任何专属代码。它实现了 `IParsable<Ulid>`、
`IComparable<Ulid>` 与非泛型 `IComparable`，因此已经满足上面的契约：

```shell
dotnet add package Ulid
```

```csharp
[StronglyTypedId]
public partial record struct OrderId(Ulid Value);
```

它的规范文本是 26 字符的 Crockford Base32，因此 JSON、MessagePack、BSON、EF Core 与 Dapper 存的都是
这个字符串。同一毫秒内生成的 ULID 只按随机部分排序，而随机部分不保证单调：「ULID 可排序」只在
跨毫秒成立，同毫秒内不成立。

<a id="what-gets-generated"></a>

## 生成器会生成什么 <a href="#top" style="float:right">↑ 返回目录</a>

对于每一个被标注的类型，生成器会在**同一命名空间**下产出一份 `partial` 声明（嵌套类型则嵌回它的包含
类型里，见[嵌套类型](docs/zh-CN/declaration.md#nested-types)），你手写的声明不会被改动。
具体产出取决于当前编译引用了什么：

<!-- BEGIN GENERATED:generated-code -->
| 编译中的条件                                                                                             | 生成的代码                                                                                                                                                             |
| -------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 始终                                                                                                     | 核心实现                                                                                                                                                               |
| 引用了 `System.Text.Json`                                                                                | 嵌套 `SystemTextJsonConverter` 与 `[JsonConverter]` 特性，以及 `JsonSerializerOptions` 上的 `AddStronglyTypedIds()` 与供源生成 context 使用的 `WithStronglyTypedIds()` |
| 引用了 `Newtonsoft.Json` ≥ 13.0.0                                                                        | 嵌套 `NewtonsoftJsonConverter` 与 `[JsonConverter]` 特性，以及 `JsonSerializerSettings` 上的 `AddStronglyTypedIds()`                                                   |
| 引用了 `Microsoft.EntityFrameworkCore` ≥ 7.0.0                                                           | 嵌套 `{TypeName}Converter`，以及 `ModelConfigurationBuilder` 与 `ModelBuilder` 上的 `AddStronglyTypedIds()`                                                            |
| 引用了 `Swashbuckle.AspNetCore.SwaggerGen` ≥ 6.0.0                                                       | `SwaggerGenOptions` 上的 `AddStronglyTypedIds()`                                                                                                                       |
| 引用了 `Dapper` ≥ 2.0.0                                                                                  | 嵌套 `{TypeName}TypeHandler`，以及 `AddStronglyTypedIds()`（`IDbConnection` 上也有一个）                                                                               |
| 引用了 `MessagePack` ≥ 2.0.0                                                                             | `{TypeName}MessagePackFormatter`，以及 `MessagePackSerializerOptions` 上的 `AddStronglyTypedIds()`                                                                     |
| 引用了 `MongoDB.Bson` ≥ 3.0.0                                                                            | `{TypeName}BsonSerializer`，以及 `StronglyTypedIds.RegisterBsonSerializers()`                                                                                          |
| 引用了 `HotChocolate` ≥ 13.0.0                                                                           | `{TypeName}HotChocolateType` scalar，以及 `IRequestExecutorBuilder` 上的 `AddStronglyTypedIds()`                                                                       |
| 引用了 `protobuf-net` ≥ 3.0.0                                                                            | Id 上的 `[ProtoContract(Surrogate = …)]`，以及带转换运算符的嵌套 `{TypeName}Surrogate`                                                                                 |
| 引用了 `Microsoft.AspNetCore.OpenApi` ≥ 9.0.0                                                            | `OpenApiOptions` 上的 `AddStronglyTypedIds()`                                                                                                                          |
| 引用了 `Microsoft.AspNetCore.Mvc` ≥ 2.0.0（或其 `Microsoft.AspNetCore.Mvc.Core` / `.Abstractions` 门面） | `MvcOptions` 与 `RouteOptions` 上的 `AddStronglyTypedIds()`（同时支撑 Minimal API 的路由约束）                                                                         |
<!-- END GENERATED:generated-code -->

上表是产物清单。生成的文件名，以及每个生成成员的具体语义（实现了哪些接口、`Parse` 如何处理
`null` provider、`[Flags]` 枚举被如何收紧、`ToString()` 打印什么），见[生成产物](docs/zh-CN/generated-code.md)。

<a id="documentation"></a>

## 文档站 <a href="#top" style="float:right">↑ 返回目录</a>

快速开始之后的内容都在 `docs/` 里，并发布在
[lenfon.github.io/StronglyTypedId](https://lenfon.github.io/StronglyTypedId/)：

| 页面 | 内容 |
| --- | --- |
| [功能特性](docs/zh-CN/features.md) | 完整清单：每个生成成员、每个配置项、每个集成。 |
| [功能索引](docs/zh-CN/features-index.md) | 每个功能点对应的可运行示例与讲解页面；表格由源码生成并在构建时校验。 |
| [NuGet 包](docs/zh-CN/packages.md) | 五个包的分工、生成器为何单独成包，以及分析器如何抵达每个项目。 |
| [声明一个 Id](docs/zh-CN/declaration.md) | 校验、归一化、命名实例、异常类型、程序集级默认、关闭集成、嵌套类型、`TryCreate` 与 UTF-8 接口、`TypeConverter`。 |
| [生成产物](docs/zh-CN/generated-code.md) | 每个生成成员做什么：实现了哪些接口、`Parse` 与文化、`[Flags]` 枚举的收紧、`ToString()` 与集成入口。 |
| [序列化](docs/zh-CN/serialization.md) | System.Text.Json、Newtonsoft.Json、字典键、JSON 形态、文本前缀、显式注册，以及序列化器不认识的基元。 |
| [集成](docs/zh-CN/integrations.md) | Entity Framework Core、Swashbuckle、Dapper、MessagePack、MongoDB、HotChocolate（GraphQL）、protobuf-net、ASP.NET Core MVC、Minimal API、ASP.NET Core OpenAPI。 |
| [运行时](docs/zh-CN/runtime.md) | 用 `IStronglyTypedId<,>` 约束泛型，以及运行时反射辅助。 |
| [原生 AOT 与裁剪](docs/zh-CN/aot.md) | 裁剪后什么还能用、反射辅助方法，以及 AOT 应用必须提供的 `JsonSerializerContext`。 |
| [测试](docs/zh-CN/testing.md) | `Any.Id<T>()`、AutoFixture 与 Bogus 集成，以及示例程序。 |
| [诊断](docs/zh-CN/diagnostics.md) | 每一条规则、级别，以及是否带代码修复。 |
| [性能](docs/zh-CN/performance.md) | 生成器在构建期的开销，以及生成代码在运行期的开销。 |
| [故障排查](docs/zh-CN/troubleshooting.md) | 按症状索引：什么都没生成、集成缺失、分析器不刷新、解析出乎意料。 |
| [已知限制](docs/zh-CN/limitations.md) | 源码生成器做不到的事（`Value` 的可见性、封闭的基元清单、不受保护的主构造函数）以及各自的绕法。 |
| [常见问题](docs/zh-CN/faq.md) | 最常被问到的问题。 |
| [从 1.3.0 升级](docs/zh-CN/migration.md) | 1.4.0 中四处需要动手的变更、四处能编译通过但会改变行为的变化，以及升级后可按需采用的能力。 |
| [未来规划](docs/zh-CN/roadmap.md) | 暂无；计划中的方向已全部交付。 |

<a id="limitations"></a>

## 已知限制 <a href="#top" style="float:right">↑ 返回目录</a>

**`Value` 恒为 public。** 该属性来自位置记录的主构造函数，源码生成器无法改变你亲手声明的成员的可见性。
确实需要硬边界时，把 Id 声明为 `internal`；`STIA0014` 会在 Id 对外可见、其验证器可被 `new` 绕过时给出提示。

**开箱只支持清单内的基元类型。** 你自己写的类型只要满足[自定义基元契约](#custom-primitives)就会被接受，
其余一律拒绝。`Uri` 即使满足契约也不支持：`Uri.Equals` 会忽略 fragment，
并对大小写与默认端口做归一化，两个不同的 URI 会被判为相等、静默塌缩成同一个 Id。

**主构造函数不受约束。** `new OrderId(...)` 会同时绕过 `Normalizer` 与 `Validator`，只有 `Create`、
`Parse` 与 `TryParse` 会执行它们。

三条的绕法，以及泛型容器那条附加限制，见[已知限制](docs/zh-CN/limitations.md)。

<a id="license"></a>

## 许可证 <a href="#top" style="float:right">↑ 返回目录</a>

本项目采用 [MIT 许可证](LICENSE.txt)。

[nuget-package]: https://www.nuget.org/packages/Len.StronglyTypedId/
[license-file]: LICENSE.txt
[badge-nuget]: https://img.shields.io/nuget/v/Len.StronglyTypedId.svg
[badge-codecov]: https://codecov.io/github/LenFon/StronglyTypedId/branch/main/graph/badge.svg?token=S3PBV7W190
[badge-license]: https://img.shields.io/badge/License-MIT-blue.svg
