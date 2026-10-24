# StronglyTypedId

> 📖 英文文档：[README.md](README.md) · 🌐 文档站：[lenfon.github.io/StronglyTypedId](https://lenfon.github.io/StronglyTypedId/)

[![codecov][badge-codecov]](https://app.codecov.io/gh/LenFon/StronglyTypedId/tree/main)
[![NuGet][badge-nuget]][nuget-package]
[![License: MIT][badge-license]][license-file]

一个源码生成器、分析器与代码修复（Code Fix）工具，能用一行 `record` 声明就生成一个功能完备的强类型 Id。
它会自动为你生成 `IStronglyTypedId<TSelf, TPrimitiveId>` 实现、`IParsable<TSelf>` 支持，以及对 JSON /
Entity Framework Core / Swagger 的集成代码——从此 `Guid` 与 `OrderId` 之间不会再互相误传。

<a id="top"></a>

## 目录

- [功能特性](#features)
- [环境要求](#requirements)
- [快速开始](#getting-started)
- [支持的基元类型](#supported-primitive-types)
- [生成器会生成什么](#what-gets-generated)
- [文档站](#documentation)
- [已知限制](#limitations)
- [许可证](#license)

<a id="features"></a>

## 功能特性 <a href="#top" style="float:right">↑ 返回目录</a>

- **声明极简** —— 用 `[StronglyTypedId]` 标注一个 `partial record`（或 `partial record struct`）即可。
- **编译期安全** —— 内建分析器会把不合法的声明作为错误报出，其中 26 条规则里有 17 条附带代码修复，
  可一键修好声明（见[诊断与代码修复](docs/zh-CN/diagnostics.md#diagnostics)）。
- **解析与格式化** —— 自动生成 `IParsable<TSelf>` 与 `ISpanParsable<TSelf>` 支持（`Parse` / `TryParse`
  的 `string` 与 `ReadOnlySpan<char>` 两套重载），并在基元类型自身实现 `IFormattable` / `ISpanFormattable`
  时一并生成 —— `string` 没有实现它们，枚举基元则两者都跳过，因为 `System.Enum` 继承来的成员已标记
  `[Obsolete]`。
- **比较与排序** —— 每个 Id 都实现了 `IComparable<TSelf>` 与四个比较运算符，因此 `OrderBy`、
  `SortedSet<T>` 与区间判断都无需自定义比较器。
- **值校验** —— 把 `Validator` 指向 Id 自身的静态方法，即可让 `Create` 与 `TryParse` 拒绝非法取值
  （见[值校验](docs/zh-CN/declaration.md#value-validation)）。
- **自定义失败异常** —— `ExceptionType` 可以把验证器拒绝取值时 `Create` 抛出的 `ArgumentException`
  换成你自己的异常类型。
- **值归一化** —— 把 `Normalizer` 指向 Id 自身的静态方法，即可在校验与构造之前修正取值（去空白、
  统一大小写、去分隔符），见[值归一化](docs/zh-CN/declaration.md#value-normalization)。
- **命名实例** —— `[StronglyTypedIdInstance("Unspecified", "-1")]` 生成 `public static readonly` 字段，
  且一律经 `Create` 构造，因此同样经过归一化与验证器，`Parse` 一定读得回来（见[命名实例](docs/zh-CN/declaration.md#named-instances)）。
- **`TryCreate` 工厂** —— `Create` 的非抛异常版本：解析失败返回 `false`，`result` 为 `default`，而不是抛异常。
- **UTF-8 接口** —— 对支持相应接口的基元类型（如 .NET 10 的 `Guid`）自动生成 `IUtf8SpanFormattable` 与
  `IUtf8SpanParsable<TSelf>`，可直接以 `ReadOnlySpan<byte>` 解析 / 格式化。
- **逐类型类型转换器** —— 用 `[StronglyTypedId(TypeConverter = true)]` 开启后，生成的嵌套 `TypeConverter`
  经 `TypeDescriptor` 在字符串与强类型 Id 间往返，覆盖配置绑定 / `XmlSerializer` 等场景。
- **程序集级默认** —— 用 `[assembly: StronglyTypedIdDefaults(Validator = ...)]` 为整个程序集统一设置默认验证器。
- **另外七个集成** —— Entity Framework Core、Dapper、Swashbuckle、ASP.NET Core MVC（其注册同时支撑
  Minimal API 的路由约束）、ASP.NET Core OpenAPI、MessagePack、MongoDB。每个集成只在对应包被引用时才生成，
  且各自提供一个 `StronglyTypedIds.ApplyTo(...)` 入口（见[集成](docs/zh-CN/integrations.md)）。
- **序列化** —— 自动生成 `System.Text.Json` 与 `Newtonsoft.Json`（≥ 13.0.0）转换器，其中
  `System.Text.Json` 还支持字典键；两者另提供显式注册入口，并可按 Id 选择基元形态或对象形态
  （见[序列化](docs/zh-CN/serialization.md#serialization)）。
- **文本前缀** —— 用 `[StronglyTypedId(Prefix = "ord_")]` 为 Id 指定规范文本形（`"ord_3f2c…"`），
  该前缀会贯穿 `ToString`、`Parse`、JSON 及其余全部文本路径（见[文本前缀](docs/zh-CN/serialization.md#serialization)）。
- **运行时反射助手** —— 可在运行时判断任意类型是否为强类型 Id，并取回其底层基元类型。
- **原生 AOT 与裁剪** —— 运行时库标记为 AOT 兼容，且不按名字加载任何类型，因此强类型 Id 在 Native AOT
  与经过裁剪的应用中同样可用。AOT 下 `System.Text.Json` 仍需源生成的 `JsonSerializerContext`，那是它自己的
  要求，不是本库的特例（见[原生 AOT 与裁剪](docs/zh-CN/aot.md)）。
- **零第三方依赖** —— 包内仅包含生成器/分析器程序集，以及一个很小的运行时库，不会往你的依赖图里塞任何东西。

<a id="requirements"></a>

## 环境要求 <a href="#top" style="float:right">↑ 返回目录</a>

| 组件                              | 要求                                                                                     |
| --------------------------------- | ---------------------------------------------------------------------------------------- |
| 项目的目标框架                    | **`net8.0`、`net10.0`** 或更高兼容框架（运行时库的目标框架为 `net8.0` 与 `net10.0`）。    |
| .NET SDK                          | **提供 Roslyn 5.9 或更新版本的编译器**。生成器是针对 `Microsoft.CodeAnalysis` 5.9 编译的，更旧的编译器无法加载它：构建会先报 `CS9057`，随后因缺少 `Create` 方法而失败。已实测 .NET SDK **10.0.401** 可用；SDK 10.0.201（Roslyn 5.3）与 9.0.308（Roslyn 4.14）均失败。 |
| Newtonsoft.Json                   | ≥ **13.0.0**（仅在使用 Newtonsoft.Json 转换器时需要）。                                   |
| EntityFrameworkCore               | ≥ **7.0.0**（仅在使用 EF Core 转换器时需要）。                                            |
| Swashbuckle.AspNetCore.SwaggerGen | ≥ **6.0.0**（仅在使用 Swagger 架构映射时需要）。                                          |

生成代码依赖 `IParsable<T>`、`ISpanParsable<T>` 以及 `System.Numerics` 下的比较/相等运算符接口，因此
运行时库从 `net8.0` 起步，并不跟随生成器低得多的 `netstandard2.0` 基线。

<a id="getting-started"></a>

## 快速开始 <a href="#top" style="float:right">↑ 返回目录</a>

1. 将包安装到你的应用程序或类库中：

   ```text
   Package Manager : Install-Package Len.StronglyTypedId
   CLI             : dotnet add package Len.StronglyTypedId
   ```

   只需安装这一个包：`Len.StronglyTypedId` 已经依赖 `Len.StronglyTypedId.Generators`，生成器、分析器与
   代码修复都在后者之中。该依赖是传递的，**每个引用主包的项目都会同时装上分析器** —— 为什么这么设计、
   以及不想要诊断时怎么关，见 [NuGet 包](docs/zh-CN/packages.md)。

2. 用单一主构造函数参数（参数名必须为 `Value`）声明一个强类型 Id：

   ```csharp
   [StronglyTypedId]
   public partial record struct OrderId(Guid Value);

   // 或

   [StronglyTypedId]
   public partial record OrderId(Guid Value);
   ```

   > **`record` 与 `record struct` 的取舍** —— 一个是值类型、一个是引用类型，生成的成员完全一致。
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

类型按名称匹配，因此写成 `System.Guid`（或使用别名/using）效果完全相同。仅识别上表中的类型——
你自己定义的、恰好也叫 `Guid` 的类型并不受支持，会被 [STIA0008](docs/zh-CN/diagnostics.md#diagnostics) 拒绝。
任意 `enum` 同样受支持；枚举 Id 如何解析、省略了哪些接口，见
[枚举 Id](docs/zh-CN/generated-code.md#enum-backed-ids)。

<a id="what-gets-generated"></a>

## 生成器会生成什么 <a href="#top" style="float:right">↑ 返回目录</a>

对于每一个被标注的类型，生成器会在**同一命名空间**下产出一份 `partial` 声明（嵌套类型则嵌回它的包含
类型里，见[嵌套类型](docs/zh-CN/declaration.md#nested-types)），你手写的声明不会被改动。
具体产出取决于当前编译引用了什么：

<!-- BEGIN GENERATED:generated-code -->
| 编译中的条件                                                                                             | 生成的代码                                                                                                                         |
| -------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------- |
| 始终                                                                                                     | 核心实现                                                                                                                           |
| 引用了 `System.Text.Json`                                                                                | 嵌套 `SystemTextJsonConverter` 与 `[JsonConverter]` 特性，以及 `StronglyTypedIds.ApplyTo(JsonSerializerOptions)`                   |
| 引用了 `Newtonsoft.Json` ≥ 13.0.0                                                                        | 嵌套 `NewtonsoftJsonConverter` 与 `[JsonConverter]` 特性，以及 `StronglyTypedIds.AddStronglyTypedId(this JsonSerializerSettings)`  |
| 引用了 `Microsoft.EntityFrameworkCore` ≥ 7.0.0                                                           | 嵌套 `{TypeName}Converter`，以及 `StronglyTypedIds.ApplyTo(ModelConfigurationBuilder)` 与 `StronglyTypedIds.ApplyTo(ModelBuilder)` |
| 引用了 `Swashbuckle.AspNetCore.SwaggerGen` ≥ 6.0.0                                                       | `StronglyTypedIds.ApplyTo(SwaggerGenOptions)`                                                                                      |
| 引用了 `Dapper` ≥ 2.0.0                                                                                  | 嵌套 `{TypeName}TypeHandler`，以及 `StronglyTypedIds.ApplyTo()`                                                                    |
| 引用了 `MessagePack` ≥ 2.0.0                                                                             | `{TypeName}MessagePackFormatter`，以及 `StronglyTypedIds.ApplyTo(MessagePackSerializerOptions)`                                    |
| 引用了 `MongoDB.Bson` ≥ 3.0.0                                                                            | `{TypeName}BsonSerializer`，以及 `StronglyTypedIds.RegisterBsonSerializers()`                                                      |
| 引用了 `Microsoft.AspNetCore.OpenApi` ≥ 9.0.0                                                            | `StronglyTypedIds.ApplyTo(OpenApiOptions)`                                                                                         |
| 引用了 `Microsoft.AspNetCore.Mvc` ≥ 2.0.0（或其 `Microsoft.AspNetCore.Mvc.Core` / `.Abstractions` 门面） | `StronglyTypedIds.ApplyTo(MvcOptions)` + `ApplyTo(RouteOptions)`（同时支撑 Minimal API 的路由约束）                                |
<!-- END GENERATED:generated-code -->

上表是产物清单。生成的文件名，以及每个生成成员的具体语义——实现了哪些接口、`Parse` 如何处理
`null` provider、`[Flags]` 枚举被如何收紧、`ToString()` 打印什么——见[生成产物](docs/zh-CN/generated-code.md)。

<a id="documentation"></a>

## 文档站 <a href="#top" style="float:right">↑ 返回目录</a>

快速开始之后的内容都在 `docs/` 里，并发布在
[lenfon.github.io/StronglyTypedId](https://lenfon.github.io/StronglyTypedId/)：

| 页面 | 内容 |
| --- | --- |
| [NuGet 包](docs/zh-CN/packages.md) | 四个包的分工、生成器为何单独成包，以及分析器如何抵达每个项目。 |
| [声明一个 Id](docs/zh-CN/declaration.md) | 校验、归一化、命名实例、异常类型、程序集级默认、关闭集成、嵌套类型、`TryCreate` 与 UTF-8 接口、`TypeConverter`。 |
| [生成产物](docs/zh-CN/generated-code.md) | 每个生成成员做什么：实现了哪些接口、`Parse` 与文化、`[Flags]` 枚举的收紧、`ToString()` 与集成入口。 |
| [序列化](docs/zh-CN/serialization.md) | System.Text.Json、Newtonsoft.Json、字典键、JSON 形态、文本前缀、显式注册，以及序列化器不认识的基元。 |
| [集成](docs/zh-CN/integrations.md) | Entity Framework Core、Swashbuckle、Dapper、MessagePack、MongoDB、ASP.NET Core MVC、Minimal API、ASP.NET Core OpenAPI。 |
| [运行时](docs/zh-CN/runtime.md) | 用 `IStronglyTypedId<,>` 约束泛型，以及运行时反射辅助。 |
| [原生 AOT 与裁剪](docs/zh-CN/aot.md) | 裁剪后什么还能用、反射辅助方法，以及 AOT 应用必须提供的 `JsonSerializerContext`。 |
| [测试](docs/zh-CN/testing.md) | `Any.Id<T>()`、AutoFixture 集成，以及示例程序。 |
| [诊断](docs/zh-CN/diagnostics.md) | 每一条规则、级别，以及是否带代码修复。 |
| [性能](docs/zh-CN/performance.md) | 生成器在构建期的开销、生成代码在运行期的开销，以及按分配量判定的回归门禁。 |
| [故障排查](docs/zh-CN/troubleshooting.md) | 按症状索引：什么都没生成、集成缺失、分析器不刷新、解析出乎意料。 |
| [已知限制](docs/zh-CN/limitations.md) | 源码生成器做不到的事 —— `Value` 的可见性、封闭的基元清单、不受保护的主构造函数 —— 以及各自的绕法。 |
| [常见问题](docs/zh-CN/faq.md) | 最常被问到的问题。 |

<a id="limitations"></a>

## 已知限制 <a href="#top" style="float:right">↑ 返回目录</a>

**`Value` 恒为 public。** 该属性来自位置记录的主构造函数，源码生成器无法改变你亲手声明的成员的可见性。
确实需要硬边界时，把 Id 声明为 `internal`；`STIA0014` 会在 Id 对外可见、其验证器可被 `new` 绕过时给出提示。

**只支持清单内的基元类型。** 任意自定义类型不被接受，`Uri` 则是刻意缺席：`Uri.Equals` 会忽略 fragment，
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
