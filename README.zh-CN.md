# StronglyTypedId

> 📖 英文文档：[README.md](README.md)

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
- [值校验](#value-validation)
- [值归一化](#value-normalization)
- [命名实例](#named-instances)
- [TryCreate 与 UTF-8 接口](#trycreate-and-utf8)
- [类型转换器（TypeConverter）](#type-converter)
- [程序集级默认设置](#assembly-defaults)
- [关闭指定集成](#integrations-filter)
- [嵌套类型](#nested-types)
- [序列化](#serialization)
- [Entity Framework Core](#entity-framework-core)
- [Swashbuckle](#swashbuckle)
- [Dapper](#dapper)
- [ASP.NET Core MVC](#aspnet-core-mvc)
- [Minimal API](#minimal-api)
- [ASP.NET Core OpenAPI](#aspnetcore-openapi)
- [用接口约束泛型](#using-the-interface)
- [运行时反射](#reflection-helpers)
- [测试辅助](#testing)
- [示例程序](#samples)
- [诊断与代码修复](#diagnostics)
- [已知限制](#limitations)
- [常见问题](#faq)
- [许可证](#license)

<a id="features"></a>

## 功能特性 <a href="#top" style="float:right">↑ 返回目录</a>

- **声明极简** —— 用 `[StronglyTypedId]` 标注一个 `partial record`（或 `partial record struct`）即可。
- **编译期安全** —— 内建分析器会把不合法的声明作为错误报出，23 条规则大多附带代码修复，
  可一键修好声明（见[诊断与代码修复](#diagnostics)）。
- **解析与格式化** —— 自动生成 `IParsable<TSelf>` 与 `ISpanParsable<TSelf>` 支持（`Parse` / `TryParse`
  的 `string` 与 `ReadOnlySpan<char>` 两套重载），并为具备相应接口的基元类型（除 `string` 外的全部）
  一并生成 `IFormattable` 与 `ISpanFormattable`。
- **比较与排序** —— 每个 Id 都实现了 `IComparable<TSelf>` 与四个比较运算符，因此 `OrderBy`、
  `SortedSet<T>` 与区间判断都无需自定义比较器。
- **值校验** —— 把 `Validator` 指向 Id 自身的静态方法，即可让 `Create` 与 `TryParse` 拒绝非法取值
  （见[值校验](#value-validation)）。
- **自定义失败异常** —— `ExceptionType` 可以把验证器拒绝取值时 `Create` 抛出的 `ArgumentException`
  换成你自己的异常类型。
- **值归一化** —— 把 `Normalizer` 指向 Id 自身的静态方法，即可在校验与构造之前修正取值（去空白、
  统一大小写、去分隔符），见[值归一化](#value-normalization)。
- **命名实例** —— `[StronglyTypedIdInstance("Unspecified", "-1")]` 生成 `public static readonly` 字段，
  且一律经 `Create` 构造，因此同样经过归一化与验证器，`Parse` 一定读得回来（见[命名实例](#named-instances)）。
- **`TryCreate` 工厂** —— `Create` 的非抛异常版本：解析失败返回 `false`，`result` 为 `default`，而不是抛异常。
- **UTF-8 接口** —— 对支持相应接口的基元类型（如 .NET 10 的 `Guid`）自动生成 `IUtf8SpanFormattable` 与
  `IUtf8SpanParsable<TSelf>`，可直接以 `ReadOnlySpan<byte>` 解析 / 格式化。
- **逐类型类型转换器** —— 用 `[StronglyTypedId(TypeConverter = true)]` 开启后，生成的嵌套 `TypeConverter`
  经 `TypeDescriptor` 在字符串与强类型 Id 间往返，覆盖配置绑定 / `XmlSerializer` 等场景。
- **程序集级默认** —— 用 `[assembly: StronglyTypedIdDefaults(Validator = ...)]` 为整个程序集统一设置默认验证器。
- **Dapper 集成** —— 引用 `Dapper` ≥ 2.0.0 时生成 `TypeHandler` 与进程级 `ApplyTo()`。
- **ASP.NET Core MVC 集成** —— 引用 `Microsoft.AspNetCore.Mvc`（或其 `Microsoft.AspNetCore.Mvc.Core` 门面）≥ 2.0.0 时，生成模型绑定提供者与逐 Id 路由约束，使 `[FromRoute]`/`[FromQuery]` 与 `{id:OrderId}` 开箱即用。
- **Minimal API 支持** —— 无需额外生成：路由 / 查询参数经 `IParsable<T>` 自动绑定，`{id:OrderId}` 路由约束复用 MVC 集成注册的 `RouteOptions`。
- **ASP.NET Core OpenAPI 集成** —— 引用 `Microsoft.AspNetCore.OpenApi` ≥ 9.0.0 时生成 `ApplyTo(OpenApiOptions)` 架构映射。
- **序列化** —— 自动生成 `System.Text.Json` 与 `Newtonsoft.Json`（≥ 13.0.0）转换器，
  其中 `System.Text.Json` 还支持字典键；两者另提供显式注册入口（`StronglyTypedIds.ApplyTo(JsonSerializerOptions)` 与
  `settings.AddStronglyTypedId()`），并可按 Id 选择基元形态或对象形态（见 [JSON 形态](#serialization)）。
- **文本前缀** —— 用 `[StronglyTypedId(Prefix = "ord_")]` 为 Id 指定规范文本形（`"ord_3f2c…"`），
  该前缀会贯穿 `ToString`、`Parse`、JSON 及其余全部文本路径（见 [文本前缀](#serialization)）。
- **Entity Framework Core** —— 在需要时生成 EF Core（≥ 7.0.0）值转换器。
- **Swagger / OpenAPI** —— 在引用 Swashbuckle.AspNetCore 时生成架构映射，同时兼容 Microsoft.OpenApi 1.x 与 2.x。
- **运行时反射助手** —— 可在运行时判断任意类型是否为强类型 Id，并取回其底层基元类型。
- **原生 AOT 与裁剪** —— 运行时库标记为 AOT 兼容，且避免按名字加载类型或生成动态代码，因此强类型 Id
  在 Native AOT 与经过裁剪的应用程序中同样可用。需注意：AOT 下 `System.Text.Json` 会禁用反射式序列化，
  因此 AOT 应用必须提供源生成的 `JsonSerializerContext` —— 这是 `System.Text.Json` 的要求，而非本库的
  特例（最小示例见 `test/Len.StronglyTypedId.Aot.SmokeTest`，已在 CI 中实际发布并运行）。
- **零第三方依赖** —— 包内仅包含生成器/分析器程序集，以及一个很小的运行时库，不会往你的依赖图里塞任何东西。

<a id="requirements"></a>

## 环境要求 <a href="#top" style="float:right">↑ 返回目录</a>

| 组件                              | 要求                                                                                     |
| --------------------------------- | ---------------------------------------------------------------------------------------- |
| 项目的目标框架                    | **`net8.0`、`net10.0`** 或更高兼容框架（运行时库的目标框架为 `net8.0` 与 `net10.0`）。    |
| .NET SDK                          | **8.0 及以上**（生成器目标框架为 `netstandard2.0`，需要 **Roslyn 4.4+**，由 .NET 8 SDK 及更高版本提供）。引用本包的项目须以 `net8.0` / `net10.0` 或更高框架为目标——见上表第一行。 |
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
   代码修复都在后者之中。

2. 用单一主构造函数参数（参数名必须为 `Value`）声明一个强类型 Id：

   ```csharp
   [StronglyTypedId]
   public partial record struct OrderId(Guid Value);

   // 或

   [StronglyTypedId]
   public partial record OrderId(Guid Value);
   ```

   > **`record` 与 `record struct` 的取舍** —— `record struct` 生成值类型（不可为 `null`、
   > 接口调用不会装箱），`record` 生成引用类型（可为 `null`）。两者都具备结构化相等性，都可以直接
   > 用作字典键。按你的可空性与性能预期选择其一，两者暴露的成员完全一致。

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
   （[STIA0006](#diagnostics)）。

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
你自己定义的、恰好也叫 `Guid` 的类型并不受支持，会被 [STIA0008](#diagnostics) 拒绝。任意 `enum` 同样受支持：
枚举 Id 通过 `Enum.TryParse<TEnum>` 解析，并保留 `IComparable<TSelf>`、`IParsable<TSelf>` 与
`ISpanParsable<TSelf>`，但会省略 `IFormattable`/`ISpanFormattable`，因为 `Enum.ToString(string,
IFormatProvider)` 已被标记过时。

<a id="what-gets-generated"></a>

## 生成器会生成什么 <a href="#top" style="float:right">↑ 返回目录</a>

对于每一个被标注的类型，生成器会在**同一命名空间**下产出一份 `partial` 声明（嵌套类型则嵌回它的包含
类型里，见[嵌套类型](#nested-types)），你手写的声明不会被改动。
具体产出取决于当前编译引用了什么：

<!-- BEGIN GENERATED:generated-code -->
| 编译中的条件                                                                                             | 生成的代码                                                                                                                                                                                                     |
| -------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 始终                                                                                                     | 核心实现 → `<命名空间>.<类型名>.g.cs`                                                                                                                                                                          |
| 引用了 `System.Text.Json`                                                                                | 嵌套 `SystemTextJsonConverter` 与 `[JsonConverter]` 特性 → `…SystemTextJson.g.cs`，以及 `StronglyTypedIds.ApplyTo(JsonSerializerOptions)` → `StronglyTypedIds.SystemTextJson.g.cs`                             |
| 引用了 `Newtonsoft.Json` ≥ 13.0.0                                                                        | 嵌套 `NewtonsoftJsonConverter` 与 `[JsonConverter]` 特性 → `…NewtonsoftJson.g.cs`，以及 `StronglyTypedIds.AddStronglyTypedId(this JsonSerializerSettings)` → `StronglyTypedIds.NewtonsoftJson.g.cs`            |
| 引用了 `Microsoft.EntityFrameworkCore` ≥ 7.0.0 **且**存在重写了 `ConfigureConventions` 的 `DbContext`    | 嵌套 `{TypeName}Converter` → `…EntityFrameworkCore.g.cs`，以及 `StronglyTypedIds.ApplyTo(ModelConfigurationBuilder)` 与 `StronglyTypedIds.ApplyTo(ModelBuilder)` → `StronglyTypedIds.EntityFrameworkCore.g.cs` |
| 引用了 `Swashbuckle.AspNetCore.SwaggerGen` ≥ 6.0.0                                                       | `StronglyTypedIds.ApplyTo(SwaggerGenOptions)` → `StronglyTypedIds.Swagger.g.cs`                                                                                                                                |
| 引用了 `Dapper` ≥ 2.0.0                                                                                  | 嵌套 `{TypeName}TypeHandler` → `…Dapper.g.cs`，以及 `StronglyTypedIds.ApplyTo()` → `StronglyTypedIds.Dapper.g.cs`                                                                                              |
| 引用了 `Microsoft.AspNetCore.OpenApi` ≥ 9.0.0                                                            | `StronglyTypedIds.ApplyTo(OpenApiOptions)` → `StronglyTypedIds.AspNetCoreOpenApi.g.cs`                                                                                                                         |
| 引用了 `Microsoft.AspNetCore.Mvc` ≥ 2.0.0（或其 `Microsoft.AspNetCore.Mvc.Core` / `.Abstractions` 门面） | `StronglyTypedIds.ApplyTo(MvcOptions)` + `ApplyTo(RouteOptions)` → `StronglyTypedIds.AspNetCoreMvc.g.cs`（同时支撑 Minimal API 的路由约束）                                                                    |
<!-- END GENERATED:generated-code -->

核心实现会补上：

- `IStronglyTypedId<TSelf, TPrimitiveId>`、`IParsable<TSelf>`、`ISpanParsable<TSelf>`、
  `IComparable<TSelf>`、`IEqualityOperators<TSelf, TSelf, bool>` 与
  `IComparisonOperators<TSelf, TSelf, bool>` 的实现。
- 基元类型具备时的 `IFormattableStronglyTypedId<TSelf>`（继承 `IFormattable`）与 `ISpanFormattable` ——
  以 `string` 为基元的 Id 例外，因为 `string` 并未实现这两者。该标记接口让泛型代码在不知道基元类型的
  前提下统一格式化：`static string Render<TId>(TId id) where TId : IFormattableStronglyTypedId<TId>`。
  `IStronglyTypedId<TSelf>` 刻意不约束 `IFormattable`，因此仍能接受所有 Id。
- 基元类型具备时的 `IUtf8SpanFormattable`（net8.0+ 即可用）与 `IUtf8SpanParsable<TSelf>`（`Guid` 自 .NET 10 起才实现，故按基元类型条件生成）—— 提供 `ReadOnlySpan<byte>` 的 `Parse` / `TryParse` 与 `TryFormat`。
- 静态的 `Create(TPrimitiveId value)` 工厂。
- 静态的 `TryCreate(TPrimitiveId value, out TSelf result)` 工厂——解析失败返回 `false`，`result` 为 `default`。
- 每个 `[StronglyTypedIdInstance]` 对应一个 `public static readonly TSelf` 字段，经 `Create` 初始化。
- `string` 与 `ReadOnlySpan<char>` 两套 `Parse` / `TryParse`，委托给基元类型。`provider` 传 `null` 时按
  **不变文化**解析（而非当前文化），因此 `Parse(id.ToString())` 在任何区域设置下都能往返；需要按特定文化
  解析时显式传 provider。对于以 `string` 为基元的 Id，`TryParse` 不接受 `null` 与空字符串。
- `CompareTo`，以及基于被包装值的 `<`、`>`、`<=`、`>=` 运算符。
- 返回被包装值**不变文化**文本的 `ToString()`（不再是 record 默认的 `OrderId { Value = … }` 形式），
  与序列化写出的文本一致，`Parse(id.ToString())` 可往返；需要按特定文化
  格式化时，用基元类型支持时的 `ToString(string?, IFormatProvider?)` 与 `TryFormat(...)`。
  注意字符串插值**不等同于** `ToString()`：`$"{id}"` 走 `IFormattable.ToString(null, CultureInfo.CurrentCulture)`，
  会跟随当前区域（de-DE 下 `decimal` 基元写成 `1234,5`），而 `id.ToString()` 不会。
- 逐类型开启 `TypeConverter` 时，嵌套的 `XxxTypeConverter`（`[TypeConverter]` 特性）经 `TypeDescriptor` 在字符串与 Id 间往返；引用类型 Id 的 `null` 会解析回 `default`。

枚举型 Id 保留 `IComparable<TSelf>`、`IParsable<TSelf>` 与 `ISpanParsable<TSelf>`（解析走
`Enum.TryParse<TEnum>`），但刻意省略 `IFormattable`/`ISpanFormattable`，因为 `Enum` 的
`ToString(string, IFormatProvider)` 已被标记过时——其 `ToString()` 直接返回底层值的文本。

`Enum.TryParse` 会接受任意数字文本，因此生成的 `TryParse` 把它收紧了：普通枚举必须命中已定义的成员
（`Enum.IsDefined`），`[Flags]` 枚举允许成员组合，但不许出现没有任何成员占用的位——`Read | Write` 能解析，
`999` 不能。掩码在编译期由各成员常量算出并按枚举底层类型宽度裁剪，运行期只剩一次按位与；若某个枚举
定义了全部位（典型是 `All = -1`），则完全不生成校验。

各集成入口统一生成到同一个类里——`Len.StronglyTypedId` 命名空间下的
`internal static partial class StronglyTypedIds`——因此无论项目声明了多少个 Id，两个 `ApplyTo`
重载都并存于该类中。调用它们需要 `using Len.StronglyTypedId;`（声明特性时通常已经有了）。

<a id="value-validation"></a>

## 值校验 <a href="#top" style="float:right">↑ 返回目录</a>

生成器约束的是 Id 的**形状**，而不是它允许取的**值**。把 `Validator` 指向 Id 自身的静态方法，生成的
入口就会拒绝非法取值：

```csharp
[StronglyTypedId(Validator = nameof(Validate))]
public partial record struct OrderId(Guid Value)
{
    private static bool Validate(Guid value) => value != Guid.Empty;
}

OrderId.Create(Guid.Empty);                            // 抛 ArgumentException
OrderId.TryParse(Guid.Empty.ToString(), null, out _);  // 返回 false，不抛异常
```

方法必须是该 Id 的静态成员，签名形如 `static bool Validate(TPrimitiveId value)`，取值合法时返回 `true`。
生成代码会在 `Create` 与两个 `TryParse` 重载里调用它，因此 `TryParse` 依旧遵守约定——失败返回 `false`
而不抛异常。JSON 与 Entity Framework Core 集成都经由 `Create` 构造，所以反序列化同样会拒绝非法值。

源码生成器唯一做不到的是拦截你声明的主构造函数：`new OrderId(...)` 会绕过校验。当取值来自你的代码
之外（用户输入、数据库、报文）时，请让它经过 `Create` 或解析成员。

不设置 `Validator` 时完全不生成校验代码——生成成员的行为与没有该属性时逐字节一致。

### 指定校验失败的异常类型

校验拒绝取值时 `Create` 默认抛 `ArgumentException`。领域库若想抛自己的异常，用 `ExceptionType` 指过去：

```csharp
public sealed class DomainRuleViolationException(string message) : Exception(message);

[StronglyTypedId(Validator = nameof(Validate), ExceptionType = typeof(DomainRuleViolationException))]
public partial record struct OrderId(Guid Value)
{
    private static bool Validate(Guid value) => value != Guid.Empty;
}

OrderId.Create(Guid.Empty);   // 抛 DomainRuleViolationException
```

该类型必须派生自 `System.Exception`、不能是抽象类，并且要有可访问的、接收单个 `string` 消息的构造函数；
否则在声明处报 [STIA0018](#diagnostics)。只有 `Create` 会用它——`Parse` 抛的是 `FormatException`，
遵循 `IParsable<TSelf>` 的惯例（与 `int.Parse` / `Guid.Parse` 一致），因为它的失败还包含「文本根本不是 Id」。
与其他选项一样，它也可以在程序集级设置：`[assembly: StronglyTypedIdDefaults(ExceptionType = typeof(...))]`。

<a id="value-normalization"></a>

## 值归一化 <a href="#top" style="float:right">↑ 返回目录</a>

`Validator` 只能**拒绝**取值。把 `Normalizer` 指向 Id 自身的静态方法，生成的入口会先**修正**它——
去首尾空白、统一大小写、去掉分隔符——再校验，最后构造：

```csharp
[StronglyTypedId(Normalizer = nameof(Normalize), Validator = nameof(Validate))]
public partial record struct EmailAddress(string Value)
{
    private static string Normalize(string value) => value.Trim().ToLowerInvariant();

    private static bool Validate(string value) => value.Contains('@');
}

EmailAddress.Create("  Alice@Example.COM  ").Value;  // "alice@example.com"
```

方法必须是该 Id 的静态成员，签名形如 `static TPrimitiveId Normalize(TPrimitiveId value)`。顺序固定且不可颠倒：
**归一化 → 校验 → 构造**。这样校验看到的就是真正落库的那个值，`"  a@b.com  "` 会被接受而不是被判非法。
`Create`、`TryCreate`、`Parse` 以及全部 `TryParse` 重载（string、span、UTF-8）都走这条路径。

每个入口只调用一次归一化方法——结果存进局部变量而不是重复调用，所以「追加前缀」这类不幂等的归一化
不会被执行两次。

不设置 `Normalizer` 时完全不生成归一化代码。与 `Validator` 一样，它也可以用
`[assembly: StronglyTypedIdDefaults(Normalizer = "MyNormalizers.Canonical")]` 在程序集级统一设置。

<a id="named-instances"></a>

## 命名实例 <a href="#top" style="float:right">↑ 返回目录</a>

有些 Id 的取值在领域里是「有名字」的——未指定的租户、系统账号、匿名用户。用 `[StronglyTypedIdInstance]`
（可重复标注）声明它们，生成器会为每个实例产出一个 `public static readonly` 字段：

```csharp
[StronglyTypedId]
[StronglyTypedIdInstance("Unspecified", "-1")]
[StronglyTypedIdInstance("Anonymous", "0")]
public partial record struct TenantId(int Value);

TenantId.Unspecified.Value;                     // -1
TenantId.Unspecified == TenantId.Create(-1);    // true
```

取值是**基元值**的不变文化文本形——也就是 `Parse` 接受的那种文本，但**不带** `Prefix`：前缀装饰的是
规范文本形，而实例承载的是基元值。它在编译期就被校验并翻译成字面量（或构造函数调用），运行期不再解析。

每个实例都经 `Create` 构造，这正是保证成立的原因：实例不是特权值，它与任何其它取值一样经过
`Normalizer` 与 `Validator`，因此 `Parse(instance.ToString())` 必定读得回来。被验证器拒绝的取值不会在
编译期被发现——而是在首次使用时由类型初始化抛 `TypeInitializationException`，这比悄悄交出一个
「不可能存在」的 Id 更响亮。

名字必须是合法标识符、在同一个 Id 内唯一，且不能与 Id 类型本身、既有成员或生成成员撞名（`STIA0020`）；
取值必须是该基元类型的合法值（`STIA0021`）；名字不是 PascalCase 时报 `STIA0022`（`Info`）。

<a id="trycreate-and-utf8"></a>

## TryCreate 与 UTF-8 接口 <a href="#top" style="float:right">↑ 返回目录</a>

`Create` 抛异常，`TryCreate` 不抛——后者在解析失败时返回 `false`，并把 `result` 置为 `default`：

```csharp
OrderId.TryCreate(Guid.NewGuid(), out var id);          // true
OrderId.TryCreate(badValue, out var invalid);           // false，result 为 default(OrderId)
```

`Guid` 自 .NET 10 起实现 `IUtf8SpanParsable<Guid>`，生成器据此为 `GuidId` 一并实现 `IUtf8SpanFormattable`
（net8.0+ 即可用）与 `IUtf8SpanParsable<GuidId>`，并提供 `ReadOnlySpan<byte>` 的 `Parse` / `TryParse`。
以 `string` 为基元的 Id 两者都不生成（`string` 未实现）：

```csharp
var utf8 = Encoding.UTF8.GetBytes(orderId.Value.ToString());
var same = OrderId.Parse(utf8, CultureInfo.InvariantCulture); // IUtf8SpanParsable<TSelf>
```

<a id="type-converter"></a>

## 类型转换器（TypeConverter） <a href="#top" style="float:right">↑ 返回目录</a>

默认只生成 JSON 转换器。需要经 `TypeDescriptor` 在字符串与 Id 间往返时（配置绑定、`XmlSerializer`、
Newtonsoft 字典键读路径等），用 `TypeConverter = true` 逐类型开启：

```csharp
[StronglyTypedId(TypeConverter = true)]
public partial record struct ConvertibleGuidId(Guid Value);
```

生成的嵌套 `XxxTypeConverter` 会带上 `[TypeConverter(typeof(XxxTypeConverter))]` 特性，因此
`TypeDescriptor.GetConverter(typeof(ConvertibleGuidId))` 即可在字符串与 Id 间转换，引用类型 Id 的
`null` 会解析回 `default`。两个方向都按**不变文化**读写，与生成的 `ToString()` 一致，因此在某个
区域设置下写出的值，换个区域也能读回。

> 该转换器仅覆盖「字符串 ↔ Id」这一路径；它不替代 JSON 转换器，二者各自独立生效。

<a id="assembly-defaults"></a>

## 程序集级默认设置（StronglyTypedIdDefaults） <a href="#top" style="float:right">↑ 返回目录</a>

同一程序集里很多 Id 想共用同一个验证器时，可用程序集特性统一设置默认 `Validator`，免去逐个标注：

```csharp
[assembly: StronglyTypedIdDefaults(Validator = "MyValidators.NonEmpty")]
```

`StronglyTypedIdDefaults.Validator` 仅作为**默认**：单个 Id 上写了 `[StronglyTypedId(Validator = ...)]`
时，以该 Id 自己的为准；都没写时才回落到程序集默认值。

`Normalizer` 遵循同样的优先级规则：

```csharp
[assembly: StronglyTypedIdDefaults(Normalizer = "MyNormalizers.Canonical")]
```

`ExceptionType` 同样遵循这一规则：`[assembly: StronglyTypedIdDefaults(ExceptionType = typeof(DomainException))]`
对所有未自行指定异常类型的 Id 生效。

> 验证器方法仍需满足 [值校验](#value-validation) 的签名约定（`static bool Validate(TPrimitiveId value)`），
> 否则由 [STIA0010](#diagnostics) 报错。归一化方法同理，必须是
> `static TPrimitiveId Normalize(TPrimitiveId value)`，否则由 [STIA0017](#diagnostics) 报错。

<a id="integrations-filter"></a>

## 关闭指定集成 <a href="#top" style="float:right">↑ 返回目录</a>

各类集成默认「只要引用了对应包就生成」。但有时这还不够：宿主应用为了自己的用途引用了 EF Core，
却希望某个 Id 永远不要拿到值转换器。`Integrations` 可以**逐个 Id** 收窄范围：

```csharp
[StronglyTypedId(Integrations = StronglyTypedIdIntegrations.None)]
public partial record struct TenantId(Guid Value);          // 只要核心实现：无 EF、Dapper、JSON、Swagger……

[StronglyTypedId(Integrations = StronglyTypedIdIntegrations.Dapper | StronglyTypedIdIntegrations.SystemTextJson)]
public partial record struct OrderId(Guid Value);           // 恰好这两类
```

`Integrations` 是一道**过滤器**，不是开关：它只能在「已引用对应包」的基础上做减法，绝不会凭空造出
依赖。请求了某个集成却没引用它的程序集时会报 [STIA0019](#diagnostics)，不会静默失效。

| 取值 | 效果 |
| --- | --- |
| *（不写）* | 引用了什么就生成什么 —— 与引入本选项之前完全一致 |
| `None` | 不生成任何集成代码；核心实现不受影响 |
| `Dapper \| SystemTextJson` | 只生成列出的这几类（仍需已引用对应包） |
| `All` | 显式全开；用于在程序集默认 `None` 时把单个 Id 捞回来 |

`StronglyTypedIdDefaults` 上也有同名选项，优先级照旧：逐类型 > 程序集默认 > `All`。

```csharp
[assembly: StronglyTypedIdDefaults(Integrations = StronglyTypedIdIntegrations.None)]
```

> `TypeConverter` **不属于** `Integrations`。它是显式 opt-in（`TypeConverter = true`），不是「引用了包
> 就有」的东西，所以 `Integrations = None` 不会关掉它。

> 引用程序集里的 Id 会尊重**它自己所在程序集**的默认值：领域库若写了
> `[assembly: StronglyTypedIdDefaults(Integrations = None)]`，消费者就不会再为它的 Id 生成集成代码。

<a id="nested-types"></a>

## 嵌套类型 <a href="#top" style="float:right">↑ 返回目录</a>

强类型 Id 可以声明在类、结构、记录或接口内部。`partial` 的各段必须处于同一容器内，因此生成代码会把
声明**逐层嵌回原本的包含类型**，而不是落在命名空间层级：

```csharp
public partial class OrderAggregate          // 容器必须 partial
{
    [StronglyTypedId]
    public partial record struct OrderId(Guid Value);
}

var id = OrderAggregate.OrderId.Create(value);   // 按嵌套名使用，与普通类型无异
```

前置条件只有一条：**包含类型必须都能被生成代码原样重开**——链上每一层都必须是 `partial`、
也不是 `file` 本地类型。不满足时由 [STIA0009](#diagnostics)（缺 `partial`，或 `file` 本地类型）
报错，并附带补 `partial` 的代码修复。

**泛型容器。** 包含类型可以是泛型（例如 `Outer<T>`）：生成器会带着它的类型参数表把声明重开，
因此声明在泛型容器里的 Id 能正常编译，并保留核心生成器产出的能力——比较与排序、格式化与 span
解析、System.Text.Json 字典键。**JSON 是唯一的例外**：转换器类照常生成，但挂不上
`[JsonConverter]` 特性（特性实参不能引用携带容器类型参数的转换器，会 CS0416），
`AddStronglyTypedId` / `ApplyTo` 入口也会跳过它（开放泛型不能作类型实参）。请按实际使用的闭合类型
手动注册：

```csharp
options.Converters.Add(new Outer<int>.InnerId.SystemTextJsonConverter());
```

需要从外部按名字引用 Id 类型的集成（EF Core、Swagger、Dapper、MVC、OpenAPI）出于同样的原因会跳过
声明在泛型容器里的 Id；对于这些集成，请把 Id 声明在命名空间层级或非泛型容器里。

非泛型的普通嵌套不受影响：比较与排序、格式化与 span 解析、两种 JSON 序列化、System.Text.Json 字典键
都照常工作。

<a id="serialization"></a>

## 序列化 <a href="#top" style="float:right">↑ 返回目录</a>

### System.Text.Json

转换器通过生成的 `[JsonConverter]` 特性自动应用，无需额外配置。

```csharp
var json = JsonSerializer.Serialize(new OrderId(Guid.NewGuid())); // "..."
var id   = JsonSerializer.Deserialize<OrderId>(json);
```

### Newtonsoft.Json（≥ 13.0.0）

同样通过生成的 `[JsonConverter]` 特性实现零配置。

```csharp
var json = JsonConvert.SerializeObject(new OrderId(Guid.NewGuid()));
var id   = JsonConvert.DeserializeObject<OrderId>(json);
```

### 序列化器不认识的基元

有六个基元在其中一方库里没有原生 JSON 表示，它们的转换器改为写出不变文化文本——也就是 `ToString()`
产出的那段文本：

| 基元                                          | 库               | 按值写会怎样                                                   |
| --------------------------------------------- | ---------------- | -------------------------------------------------------------- |
| `BigInteger`                                  | System.Text.Json | 退化成 `{"IsPowerOfTwo":false,…,"Sign":1}`，读回来是 `default`  |
| `Int128`、`UInt128`、`Half`、`DateOnly`、`TimeOnly` | Newtonsoft.Json | 抛 `JsonWriterException: Unsupported type`                     |

```csharp
JsonSerializer.Serialize(BigIntegerId.Create(42));       // "42"
JsonConvert.SerializeObject(HalfId.Create((Half)1.5));   // "1.5"
```

读写两侧走同一段规范文本，往返无损；唯一的差别是这些 Id 在 JSON 里呈现为字符串而不是数字。其余基元
保持自然形态（数字仍是数字、`Guid` 仍是字符串、枚举仍是数字）。

### 字典键

默认情况下 `JsonConverter<T>` 看不到字典键——`System.Text.Json` 只认得内建的键类型，其余一律抛
`NotSupportedException`——因此生成的转换器另外重写了 `ReadAsPropertyName` / `WriteAsPropertyName`：

```csharp
var cart = new Dictionary<OrderId, int> { [id] = 3 };

var json = JsonSerializer.Serialize(cart);                        // {"3f2c…":3}
var back = JsonSerializer.Deserialize<Dictionary<OrderId, int>>(json);
```

键以基元值的**不变文化**文本写出，因此同一份数据在任何区域设置下都会得到相同的键。以 `string` 为
基元的 Id 直接使用该字符串，故空串也能往返；无法解析的键会抛 `JsonException`。

以 `enum` 为基元的 Id 沿用 `System.Text.Json` 对枚举键的既定约定：键是成员名（`"Active"`），值是底层
数字（`1`）——除非你的 `JsonSerializerOptions` 另有配置。这种不对称是 JSON 本身的限制（对象的键只能是
字符串），也与原生 `Dictionary<MyEnum, MyEnum>` 的输出完全一致，因此 Id 与裸枚举可以在同一份文档里互换。

Newtonsoft.Json 的字典键走的是 `TypeConverter` / `ToString()`，并不经过 `JsonConverter`，因此生成的
转换器接管不到它。请在 Id 上开启 `TypeConverter = true`，Newtonsoft 才能在读路径还原键。键文本与转换器
写出的一致，都是**不变文化**文本——生成的 `ToString()` 与生成的 `TypeConverter` 两个方向都按不变文化读写——
因此在任何区域设置下键都保持稳定。

两种转换器都把 Id 序列化为其底层基元值而不是对象，因此 JSON 结构与引入强类型 Id 之前保持一致。
引用类型 Id 为 `null` 时写出的就是 JSON `null`。

### JSON 形态

默认情况下，Id 与其基元**形态兼容**：以 `Guid` 为基元的 `OrderId` 序列化结果与裸 `Guid` 完全相同，
因此引入强类型 Id 不会改变线上格式。当既有契约已经对外承诺了对象形态——典型情况是客户端发送
`{ "value": "3f2c…" }`——可以按 Id 选择启用：

```csharp
[StronglyTypedId(JsonShape = JsonShape.Object)]
public partial record struct OrderId(Guid Value);

// JsonShape.Primitive（默认）："3f2c…"
// JsonShape.Object：           {"value":"3f2c…"}
```

该设置对 `System.Text.Json` 与 `Newtonsoft.Json` 同时生效，字典键亦然。`JsonShape.Object` 读写单个
`value` 属性；载荷不是对象、或对象缺少 `value` 时会抛异常（`JsonException` /
`JsonSerializationException`）。由于基元形态是默认值，除非你主动选择对象形态，既有载荷不受影响。

### 文本前缀

可以按 Id 选择启用文本前缀。前缀进入 Id 的**规范文本形**——`ToString()` 写出、`Parse` / `TryParse`
剥离——因此所有基于文本的路径都会自动带上它：

```csharp
[StronglyTypedId(Prefix = "ord_")]
public partial record struct OrderId(Guid Value);

var id = OrderId.Create(guid);

id.ToString();                              // "ord_3f2c…"
OrderId.Parse("ord_3f2c…");                 // OrderId
OrderId.Parse("3f2c…");                     // OrderId（宽松读入，默认）
JsonSerializer.Serialize(id);                // "ord_3f2c…"
```

由于前缀落在规范文本形里，走文本的集成无需额外配置：MVC 模型绑定、路由约束与生成的 `TypeConverter`
都自动接受并输出带前缀的形态。而走基元**值**的集成——EF Core 与 Dapper——不受影响，仍存裸基元，
既有列与数据行继续有效。

读入**默认宽松**：`TryParse` 同时接受带前缀与裸值两种写法，因此既有载荷与已存文本仍可往返，而新写出的
文本恒带前缀。需要前缀强制存在时，可按 Id 改为严格：

```csharp
[StronglyTypedId(Prefix = "ord_", PrefixReadMode = PrefixReadMode.Strict)]
public partial record struct OrderId(Guid Value);

OrderId.TryParse("ord_3f2c…", null, out _);  // true
OrderId.TryParse("3f2c…", null, out _);       // false
```

> **启用后属破坏性变更。** 对数字、枚举与日期基元，JSON 表示会由 JSON number 变为 JSON string，
> 因为带前缀的形态是文本。数据库存储不受影响。不设置 `Prefix` 时，生成代码与之前逐字节一致。

空前缀或纯空白前缀按「未设置」处理，并报 `STIA0013`。

### 显式注册

`[JsonConverter]` 特性覆盖了常见情形，但当你传入自己的 `JsonSerializerOptions` / `JsonSerializerSettings`
并希望把本库的转换器一并挂上时，该特性不会被采纳。为此，两个序列化器都在生成的 `StronglyTypedIds` 类上
提供了注册入口，与 EF Core / Swagger / Dapper / MVC / OpenAPI 的 `ApplyTo` 家族平行：

```csharp
// System.Text.Json
var options = new JsonSerializerOptions();
StronglyTypedIds.ApplyTo(options);          // 把每个 Id 的转换器加入 options.Converters

// Newtonsoft.Json
var settings = new JsonSerializerSettings();
settings.AddStronglyTypedId();              // 把每个 Id 的转换器加入 settings.Converters
```

每个入口只在引用了对应序列化器、且编译中至少声明了一个 Id 时才生成。

<a id="entity-framework-core"></a>

## Entity Framework Core <a href="#top" style="float:right">↑ 返回目录</a>

在 `DbContext` 中为所有强类型 Id 注册生成的值转换器（≥ 7.0.0）：

```csharp
protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
{
    base.ConfigureConventions(configurationBuilder);

    // 为项目中每一个强类型 Id 注册 ValueConverter。
    StronglyTypedIds.ApplyTo(configurationBuilder);

    // 注册之后，常规的约定配置依旧对某个 Id 生效。
    configurationBuilder.Properties<UserId>().HaveMaxLength(100);
}

除基于约定的注册外，还一并生成了一个按模型扫描的重载，便于你从 `OnModelCreating` 而非约定注册转换器：

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    // 遍历每个实体的属性，凡是 CLR 类型为强类型 Id 的就挂上生成的转换器。
    StronglyTypedIds.ApplyTo(modelBuilder);
}
```

> EF Core 集成（上面两个 `ApplyTo` 重载）只在 `DbContext` 重写了 `ConfigureConventions` 且引用了
> EF Core ≥ 7.0.0 时才会生成，因此 `StronglyTypedIds.ApplyTo(...)` 恰好在你能够调用它时才可用。
> 仅仅引用 `Microsoft.EntityFrameworkCore` 是不够的。

每个 Id 都会得到一个嵌套在生成的 `StronglyTypedIds` 类中的 `ValueConverter<{Id}, {基元类型}>`，类名
默认为 `{类型名}Converter`。若不同命名空间下存在同名 Id，两者在该类中的类名会相互冲突（CS0102），
因此**仅在这种情况下**才会改名为 `{命名空间下划线连接}_{类型名}Converter`（例如 `Domain_OrderIdConverter`）。
这些转换器是 `ApplyTo` 的实现细节，以 private 嵌套类型生成——你不需要按名字引用它们，两种情况都无需你做任何事。

<a id="swashbuckle"></a>

## Swashbuckle <a href="#top" style="float:right">↑ 返回目录</a>

将每一个强类型 Id 映射到其底层基元类型的 Swagger 架构（Swashbuckle.AspNetCore）：

```csharp
services.AddSwaggerGen(options =>
{
    StronglyTypedIds.ApplyTo(options);
});

// 也可以用方法组：
services.AddSwaggerGen(StronglyTypedIds.ApplyTo);
```

只要引用了 `Swashbuckle.AspNetCore.SwaggerGen` ≥ 6.0.0 就会生成该映射，且同时兼容
Microsoft.OpenApi 1.x（Swashbuckle 6.x–9.x）与 2.x（Swashbuckle 10.x 及更高版本）。

| 基元类型          | OpenAPI `type` | `format` |
| ---------------- | -------------- | -------- |
| `Guid`           | `string`       | `uuid`      |
| `string`、`char` | `string`       | —           |
| `byte`           | `integer`      | `byte`      |
| `sbyte`          | `integer`      | `sbyte`     |
| `short`          | `integer`      | `int16`     |
| `ushort`         | `integer`      | `uint16`    |
| `int`            | `integer`      | `int32`     |
| `uint`           | `integer`      | `uint32`    |
| `long`           | `integer`      | `int64`     |
| `ulong`          | `integer`      | `uint64`    |
| `Int128`、`UInt128`、`BigInteger` | `integer` | —     |
| `decimal`        | `number`       | —           |
| `double`         | `number`       | `double`    |
| `float`          | `number`       | `float`     |
| `Half`           | `number`       | —           |
| `DateTime`、`DateTimeOffset` | `string` | `date-time` |
| `DateOnly`       | `string`       | `date`      |
| `TimeOnly`       | `string`       | `time`      |
| `TimeSpan`       | `string`       | —           |
| 任意 `enum`      | `integer`      | —           |

这张表描述的是转换器**实际写出**的形态，而不是 CLR 类型叫什么：`decimal` 与各浮点基元是 JSON number，
枚举是其底层数字，`Int128` / `UInt128` / `BigInteger` 没有 `int64` / `uint64` 那样的定长范围可标注，
因此只给裸 `integer` 而不带 `format`。带 `Prefix` 的 Id 一律是 `string` 且不带 `format`，因为它的规范
文本形就是文本。

<a id="dapper"></a>

## Dapper <a href="#top" style="float:right">↑ 返回目录</a>

引用 `Dapper` ≥ 2.0.0 时，为每个强类型 Id 生成 `TypeHandler`，并提供一个统一入口：

```csharp
using Dapper;

// 注册所有强类型 Id 的 TypeHandler。Dapper 把它们放在进程级注册表里，只需调用一次。
StronglyTypedIds.ApplyTo();

connection.Execute("INSERT INTO Orders (Id) VALUES (@Id)", new { Id = OrderId.Create(Guid.NewGuid()) });
```

每个 Id 的 `TypeHandler` 会把被包裹的基元写入参数，并把读取到的值经 `Create` 还原为 Id。不同命名空间下
同名 Id 的 `TypeHandler` 会以「命名空间下划线连接」前缀避免冲突（与 EF Core 转换器同名处理一致）。

> 注册是全局的，不是按连接的。`ApplyTo(IDbConnection)` 仍会生成以保持源码兼容，但它忽略实参并转发到
> `ApplyTo()`。两者重复调用都无害：Dapper 对同一类型是替换而非追加，注册表不会膨胀。

> 引用 `Dapper` 但主版本低于 2.0.0 时不生成，避免与旧版 `SqlMapper.TypeHandler` API 不兼容。

<a id="aspnet-core-mvc"></a>

## ASP.NET Core MVC <a href="#top" style="float:right">↑ 返回目录</a>

引用 `Microsoft.AspNetCore.Mvc` ≥ 2.0.0 时（.NET 8 / .NET 10 上 `Microsoft.AspNetCore.Mvc.Core`
与 `Microsoft.AspNetCore.Mvc.Abstractions` 程序集同样满足门控），生成器会产出一个模型绑定提供者与
逐 Id 的路由约束，使强类型 Id 无需任何逐类型样板即可完成绑定与校验：

```csharp
using Microsoft.Extensions.DependencyInjection;

// 注册模型绑定提供者与逐 Id 路由约束。
builder.Services.AddControllers(options => StronglyTypedIds.ApplyTo(options));
builder.Services.AddRouting(options => StronglyTypedIds.ApplyTo(options));
```

```csharp
[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    // 路由或查询里的 "ABC123" 经 IParsable<OrderId>.TryParse 绑定为 OrderId。
    [HttpGet("{id}")]
    public IActionResult Get([FromRoute] OrderId id) => Ok(id);

    // {id:OrderId} 约束会在进入 Action 前按该 Id 的解析规则校验分段格式。
    [HttpGet("v2/{id:OrderId}")]
    public IActionResult GetV2([FromRoute] OrderId id) => Ok(id);
}
```

> 绑定器只对实现了 `IStronglyTypedId<TSelf, TPrimitiveId>` 的类型接管；其余类型回退到 MVC 默认绑定链路。
> 绑定本身复用每个 Id 已有的 `IParsable<TSelf>` 成员，因此除注册时的一次性类型判定外没有逐请求反射开销。

<a id="minimal-api"></a>

## Minimal API <a href="#top" style="float:right">↑ 返回目录</a>

Minimal API **无需任何额外生成**。它的参数绑定约定接受「具备静态 `TryParse(string?, IFormatProvider?, out TSelf)`
的类型」，而每个 Id 都经 `IParsable<TSelf>` 天然具备该成员，因此路由与查询参数开箱即用：

```csharp
// 路由里的 "ABC123" 经 IParsable<OrderId>.TryParse 绑定为 OrderId；
// 非法文本在进入处理器前即返回 400。
app.MapGet("/orders/{id}", (OrderId id) => Results.Ok(id.Value));

// [AsParameters] 会把每个 Id 属性按同名的路由 / 查询值逐个绑定。
app.MapGet("/orders", ([AsParameters] OrderQuery query) => Results.Ok(query.Id.Value));

public record OrderQuery(OrderId Id);
```

路由约束与 MVC 共用：生成的 `ApplyTo(RouteOptions)` 会为每个 Id 注册一条约束，因此 `{id:OrderId}`
在 Minimal API 模板里同样可用（`Microsoft.AspNetCore.Mvc.*` 程序集随 ASP.NET Core 共享框架分发，
Minimal API 工程也满足门控）：

```csharp
builder.Services.AddRouting(options => StronglyTypedIds.ApplyTo(options));

app.MapGet("/orders/v2/{id:OrderId}", (OrderId id) => Results.Ok(id.Value));
```

<a id="aspnetcore-openapi"></a>

## ASP.NET Core OpenAPI <a href="#top" style="float:right">↑ 返回目录</a>

引用 `Microsoft.AspNetCore.OpenApi` ≥ 9.0.0 时，为每个强类型 Id 生成 OpenAPI 架构映射，与 Swashbuckle
的 `ApplyTo(SwaggerGenOptions)` 平行：

```csharp
builder.Services.AddOpenApi(options =>
{
    StronglyTypedIds.ApplyTo(options);
});
```

映射把每个 Id 解析为其底层基元类型的 schema，`format` 取值与 [Swashbuckle](#swashbuckle) 一节相同。

> 该集成与 Swashbuckle 互不依赖：二者可单独或同时存在，各自生成自己的 `ApplyTo` 重载。

<a id="using-the-interface"></a>

## 用接口约束泛型 <a href="#top" style="float:right">↑ 返回目录</a>

每个生成的 Id 都实现了 `IStronglyTypedId<TSelf, TPrimitiveId>`，因此你可以编写适用于任意强类型 Id 的泛型代码：

```csharp
public class Repository<TId>
    where TId : IStronglyTypedId<TId, Guid>
{
    public TId CreateId(Guid value) => TId.Create(value);
}
```

每个 Id 同时实现了单参数的 `IStronglyTypedId<TSelf>`，因此约束可以只表达「某个强类型 Id」而不必指明
基元类型——日志、缓存、校验辅助方法这类把 Id 当作不透明值处理的场景很合用。
`IFormattableStronglyTypedId<TSelf>` 在此基础上进一步收窄到「基元可格式化」的 Id。

<a id="reflection-helpers"></a>

## 运行时反射 <a href="#top" style="float:right">↑ 返回目录</a>

包还提供 `StronglyTypedIdExtensions`，用于在运行时检查类型：

- `bool IsStronglyTypedId(this Type type)`
- `Type? GetPrimitiveIdType(this Type type)`
- `bool TryGetPrimitiveIdType(this Type type, out Type? primitiveIdType)`

```csharp
if (typeof(OrderId).IsStronglyTypedId())
{
    Type primitive = typeof(OrderId).GetPrimitiveIdType()!; // typeof(Guid)
}
```

该判断基于所实现的接口，因此同样能识别由其它程序集生成的强类型 Id。接口、抽象类型、枚举与数组
永远不会被判定为强类型 Id。

<a id="testing"></a>

## 测试辅助 <a href="#top" style="float:right">↑ 返回目录</a>

`Len.StronglyTypedId.Testing` 用来在 arrange 阶段造 Id，不必到处写 `OrderId.Create(Guid.NewGuid())`：

```csharp
using Len.StronglyTypedId.Testing;

OrderId orderId = Any.Id<OrderId>();
IReadOnlyList<OrderId> batch = Any.Ids<OrderId>(10);
```

取值来自 `IStronglyTypedId<TSelf, TPrimitiveId>` 暴露的基元类型，因此对任意程序集声明的 Id 都有效
（包括在本测试包编译之后才生成的 Id）。覆盖生成器支持的全部基元类型，外加 `enum`。取值刻意保持
「普通」：有限浮点数、非空字符串、1970–2100 之间的日期 —— 免得测试因为 `double.NaN` 之类的值
报出与被测代码无关的失败。

- **可复现** —— `Any.Id<OrderId>(seed: 42)` 在同一运行时版本下返回相同的值；要让一整串值重复，
  请复用同一个 `AnyIdGenerator` 实例，或用 `Any.Ids<T>(count, seed)`。
- **尊重验证器** —— 生成最多重试 `AnyIdOptions.MaxAttempts` 次（默认 100），仍不满足则抛出异常，
  并在消息中指向 `Any.Register<T>(factory)`。
- **可覆盖** —— `AnyIdOptions.PrimitiveValueFactory` 可按基元类型替换取值来源；返回 `null`
  表示回落到内置生成逻辑。

配合 AutoFixture 使用时，加上 `Len.StronglyTypedId.Testing.AutoFixture` 并启用定制：

```csharp
var fixture = new Fixture().WithStronglyTypedIds();

OrderId orderId = fixture.Create<OrderId>();
```

完整说明见 [TESTING.md](TESTING.md)。

<a id="samples"></a>

## 示例程序 <a href="#top" style="float:right">↑ 返回目录</a>

上文每一项能力在 [`samples/`](samples) 下都有可直接运行的示例：

| 工程 | 目标框架 | 演示内容 |
|---|---|---|
| `Len.StronglyTypedId.SampleConsole` | `net8.0;net10.0` | 核心能力巡览：`Create` / `TryCreate` / `Parse`、格式化、相等性与排序、解构、验证器、命名实例、文本前缀、`TypeConverter`、System.Text.Json 与 Newtonsoft.Json、嵌套类型、泛型容器、反射辅助、测试辅助包。 |
| `Len.StronglyTypedId.SampleIntegrations` | `net8.0` | EF Core（`ApplyTo(ModelConfigurationBuilder)`）与 Dapper（`ApplyTo()`）跑内存 SQLite，并演示程序集级 `StronglyTypedIdDefaults`。 |
| `Len.StronglyTypedId.Sample1` | `net8.0` | MVC 模型绑定与 `{id:OrderId}` 路由约束、把 Newtonsoft.Json 用作 MVC 格式化器、EF Core（SQL Server）与 Swashbuckle 6。启动需要可连接的 SQL Server。 |
| `Len.StronglyTypedId.Sample2` | `net10.0` | Minimal API：`IParsable` 绑定、`[AsParameters]`、路由约束、JSON 请求体，以及内置 OpenAPI 转换器（`AddOpenApi(ApplyTo)`）。 |
| `Len.StronglyTypedId.Sample3` | `net8.0` | MVC + Swashbuckle 10（Microsoft.OpenApi 2.x）的 schema 映射。 |

```shell
dotnet run --project samples/Len.StronglyTypedId.SampleConsole
dotnet run --project samples/Len.StronglyTypedId.SampleIntegrations
```

控制台示例逐步打印后退出；Web 示例的文档地址分别是 `/openapi/v1.json`（Sample2）与
`/swagger/v1/swagger.json`（Sample1、Sample3）。

<a id="diagnostics"></a>

## 诊断与代码修复 <a href="#top" style="float:right">↑ 返回目录</a>

分析器会强制以下规则；`STIA0012` 是唯一的例外，由生成器在代码产出本身失败时报告。大多数规则作为
**编译错误**报告，因此不合法的声明无法通过编译；`STIA0011`、`STIA0013` 与 `STIA0019` 是警告，建议性规则
（`STIA0014`、`STIA0015`、`STIA0016`、`STIA0022`）为提示级别。各级别均可按项目在 `.editorconfig` 中调整。
诊断文案会按编译器区域设置输出英文或中文。

有几条规则的触发条件比标题看上去更窄：`STIA0011` 要求该 Id 声明了 `Validator` 或 `Normalizer`
（两者都没有时直接构造并不构成绕过），且同时覆盖 `new Xxx(...)`、目标类型化的 `new(...)` 与
`default` / `default(T)`——仅用于比较（`id == default`）时不报告；`STIA0014` **默认关闭**，需显式设置
`dotnet_diagnostic.STIA0014.severity = suggestion` 才会启用；`STIA0016` 对每组共用同一基元的 Id
只报一次，落在组内名字序最小的成员上。

<!-- BEGIN GENERATED:diagnostics -->
| 规则 ID  | 说明                                       | 代码修复                               |
| -------- | ------------------------------------------ | -------------------------------------- |
| STIA0000 | 类型必须被定义为记录类型                   | 转为 `record`                          |
| STIA0001 | 类型必须被定义为分部类型。                 | 补上 `partial`                         |
| STIA0002 | 类型不能被声明为抽象的                     | 去掉 `abstract`                        |
| STIA0003 | 类型不能被声明为泛型                       | 移除类型参数表                         |
| STIA0004 | 类型必须被声明在命名空间内                 | 包进命名空间                           |
| STIA0005 | 类型必须有一个仅包含一个参数的主构造函数。 | 补主构造函数                           |
| STIA0006 | 参数不能为可空类型                         | 去掉 `?` 后缀                          |
| STIA0007 | 参数名应该是‘Value’                        | 重命名参数                             |
| STIA0008 | 参数类型无效                               | 改为 `Guid`                            |
| STIA0009 | 包含类型必须被声明为分部类型               | 给包含类型补 `partial`                 |
| STIA0010 | 验证器方法必须是合法的静态 bool 方法       | 删掉 `Validator` 实参                  |
| STIA0011 | 强类型 Id 被直接构造                       | 改用 `Create(...)`                     |
| STIA0012 | 强类型 Id 代码生成失败                     | —                                      |
| STIA0013 | 前缀值为空                                 | 删掉 `Prefix` 实参，或给它一个非空白值 |
| STIA0014 | 验证器可被绕过                             | 收窄为 `internal`                      |
| STIA0015 | 类型名应以‘Id’结尾                         | —                                      |
| STIA0016 | 多个强类型 Id 使用相同的基元类型           | —                                      |
| STIA0017 | 归一化方法必须是合法的静态方法             | 删掉 `Normalizer` 实参                 |
| STIA0018 | 异常类型必须是带字符串构造函数的非抽象异常 | 删掉 `ExceptionType` 实参              |
| STIA0019 | 请求的集成不可用                           | —                                      |
| STIA0020 | 命名实例的名字不可用                       | —                                      |
| STIA0021 | 命名实例的值不是该基元类型的合法取值       | —                                      |
| STIA0022 | 命名实例的名字应为 PascalCase              | —                                      |
<!-- END GENERATED:diagnostics -->

报错的规则都提供修复，但有几条只在修复方式唯一时才提供 —— `STIA0003` 仅在 Id 自身是泛型时提供
（删掉*包含类型*的类型参数会改变你的设计），`STIA0005` 仅在类型没有显式构造函数时提供（在已有构造函数旁
再加主构造函数会撞 `CS0111`），`STIA0009` 仅在包含类型确实缺 `partial` 时提供（否则会插进第二个 `partial`）。
两条命名提示（`STIA0015`、`STIA0016`）刻意不提供修复：正确地重命名一个类型意味着同时改掉所有引用，
而「这些 Id 看起来一样」本身没有机械修法。

代码修复通过 IDE 的常规灯泡菜单提供，并支持**在文档 / 项目 / 解决方案中修复全部出现处**。类型可以
拆成多个 `partial` 声明段：类型级规则按整个类型判定一次，参数级规则只在声明主构造函数的那一段上判定，
因此在另一个声明段里追加成员完全合法。

<a id="limitations"></a>

## 已知限制 <a href="#top" style="float:right">↑ 返回目录</a>

**`Value` 恒为 public。** 该属性来自位置记录的主构造函数，源码生成器无法改变你亲手声明的成员的可见性。
若要改成 `internal`，就只能手写整个类型——而那正是本库要消灭的一行声明。请把 `Value` 当作逃生舱口：
持久化映射与互操作时用它，但别让它出现在领域层的签名里。确实需要硬边界时，把 Id 声明为 `internal`，
只对外暴露应用真正需要的操作；`STIA0014` 会在 Id 对外可见、其验证器可被 `new` 绕过时给出提示（默认关闭）。

**只支持清单内的基元类型。** 任意自定义类型不被接受，`Uri` 则是刻意缺席：`Uri.Equals` 会忽略 fragment，
并对大小写与默认端口做归一化，两个不同的 URI 会被判为相等、静默塌缩成同一个 Id。这类值请用受支持的
基元（通常是 `string`）承载，并在验证器里校验格式。

**主构造函数不受约束。** `new OrderId(...)` 会同时绕过 `Normalizer` 与 `Validator`，只有 `Create`、
`Parse` 与 `TryParse` 会执行它们——详见[值归一化](#value-normalization)。

<a id="faq"></a>

## 常见问题 <a href="#top" style="float:right">↑ 返回目录</a>

**问：我应该安装哪个包？**

答：只装 `Len.StronglyTypedId` 即可。它依赖 `Len.StronglyTypedId.Generators`，后者包含生成器、分析器
与代码修复；而代码中引用的是前者这个运行时库。

**问：分析器没有识别出我最新的改动，为什么？**

答：通常是 Visual Studio 缓存导致。**清理解决方案**后重新生成即可。编译产物中始终包含最新的生成代码，
因此即便此刻忽略该提示也无妨。

**问：找不到 `StronglyTypedIds.ApplyTo`。**

答：每个 `ApplyTo` 重载都是随对应集成一起生成的，两者都位于 `Len.StronglyTypedId` 命名空间。请确认：
（1）已 `using Len.StronglyTypedId;`；（2）使用 EF Core 时确实有 `DbContext` 重写了 `ConfigureConventions`；
（3）使用 Swagger 时项目引用了 `Swashbuckle.AspNetCore.SwaggerGen` ≥ 6.0.0。JSON 两个入口遵循同一规则：
`ApplyTo(JsonSerializerOptions)` 需要引用 `System.Text.Json`，`AddStronglyTypedId()` 需要引用
`Newtonsoft.Json` ≥ 13.0.0。

**问：明明引用了 EF Core，为什么没有生成值转换器 / 列映射？**

答：EF Core 转换以「存在重写 `ConfigureConventions` 的 `DbContext`」为前提。补上该重写并调用
`StronglyTypedIds.ApplyTo(configurationBuilder)`，转换器会同时生成。

**问：生成代码全部消失，只看到一条 CS8785 警告。**

答：`CS8785` 表示生成器在运行中抛出了异常，而该次生成产出的所有文件都被丢弃。请附上编译输出与最小
重现工程提交 issue。

**问：强类型 Id 能直接作为 `Dictionary` / `HashSet` 的键吗？**

答：可以。`record` 与 `record struct` 都会基于被包装的 `Value` 生成结构化相等性、`GetHashCode`、
`==` 与 `!=`。以 Id 为键的字典同样支持序列化——见[字典键](#serialization)。

**问：可以对 Id 排序，或放进 `SortedSet<T>` 吗？**

答：可以。每个 Id 都实现了 `IComparable<TSelf>` 与 `<`、`>`、`<=`、`>=`（比较的都是被包装的值），
因此 `OrderBy`、`SortedSet<T>` 与区间判断都无需自定义比较器。引用类型 Id 把 `null` 视为最小、
排在最前，与 `Comparer<T>.Default` 的约定一致。

**问：`ToString()` 输出什么？**

答：输出被包装基元值的**不变文化**文本（`"42"`、`"3f2c…"`、`"1.5"`），而不是 record 默认的
`OrderId { Value = 42 }` 形式，因此在日志里更易读。它刻意与区域设置无关，从而与序列化写出的形态一致，
并且 `Parse(id.ToString())` 必定往返——`Parse` / `TryParse` 把 `null` provider 当作不变文化，正是为了这一点。
需要按特定文化输出时，请调用 `ToString(string?, IFormatProvider?)`。注意字符串插值不走 `ToString()`：
`$"{id}"` 走 `IFormattable.ToString(null, CurrentCulture)`，会跟随当前区域。

**问：设置了 `Validator`，但 `new OrderId(...)` 仍能传入非法值？**

答：主构造函数属于你自己的声明，源码生成器无法往里面注入代码，因此只有生成的入口会校验。JSON、
EF Core 与用户输入都是经由 `Create` / `Parse` / `TryParse` 进入的，校验在实际用到的地方生效——
详见[值校验](#value-validation)。

<a id="license"></a>

## 许可证 <a href="#top" style="float:right">↑ 返回目录</a>

本项目采用 [MIT 许可证](LICENSE.txt)。

[nuget-package]: https://www.nuget.org/packages/Len.StronglyTypedId/
[license-file]: LICENSE.txt
[badge-nuget]: https://img.shields.io/nuget/v/Len.StronglyTypedId.svg
[badge-codecov]: https://codecov.io/github/LenFon/StronglyTypedId/branch/main/graph/badge.svg?token=S3PBV7W190
[badge-license]: https://img.shields.io/badge/License-MIT-blue.svg
