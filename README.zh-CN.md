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
- [TryCreate 与 UTF-8 接口](#trycreate-and-utf8)
- [类型转换器（TypeConverter）](#type-converter)
- [程序集级默认设置](#assembly-defaults)
- [嵌套类型](#nested-types)
- [序列化](#serialization)
- [Entity Framework Core](#entity-framework-core)
- [Swashbuckle](#swashbuckle)
- [Dapper](#dapper)
- [ASP.NET Core OpenAPI](#aspnetcore-openapi)
- [用接口约束泛型](#using-the-interface)
- [运行时反射](#reflection-helpers)
- [诊断与代码修复](#diagnostics)
- [常见问题](#faq)
- [许可证](#license)

<a id="features"></a>

## 功能特性 <a href="#top" style="float:right">↑ 返回目录</a>

- **声明极简** —— 用 `[StronglyTypedId]` 标注一个 `partial record`（或 `partial record struct`）即可。
- **编译期安全** —— 内建分析器会把不合法的声明作为错误报出，其中 12 条规则里有 6 条附带代码修复，
  可一键修好声明（见[诊断与代码修复](#diagnostics)）。
- **解析与格式化** —— 自动生成 `IParsable<TSelf>` 与 `ISpanParsable<TSelf>` 支持（`Parse` / `TryParse`
  的 `string` 与 `ReadOnlySpan<char>` 两套重载），并为具备相应接口的基元类型（除 `string` 外的全部）
  一并生成 `IFormattable` 与 `ISpanFormattable`。
- **比较与排序** —— 每个 Id 都实现了 `IComparable<TSelf>` 与四个比较运算符，因此 `OrderBy`、
  `SortedSet<T>` 与区间判断都无需自定义比较器。
- **值校验** —— 把 `Validator` 指向 Id 自身的静态方法，即可让 `Create` 与 `TryParse` 拒绝非法取值
  （见[值校验](#value-validation)）。
- **`TryCreate` 工厂** —— `Create` 的非抛异常版本：解析失败返回 `false`，`result` 为 `default`，而不是抛异常。
- **UTF-8 接口** —— 对支持相应接口的基元类型（如 .NET 10 的 `Guid`）自动生成 `IUtf8SpanFormattable` 与
  `IUtf8SpanParsable<TSelf>`，可直接以 `ReadOnlySpan<byte>` 解析 / 格式化。
- **逐类型类型转换器** —— 用 `[StronglyTypedId(TypeConverter = true)]` 开启后，生成的嵌套 `TypeConverter`
  经 `TypeDescriptor` 在字符串与强类型 Id 间往返，覆盖配置绑定 / `XmlSerializer` 等场景。
- **程序集级默认** —— 用 `[assembly: StronglyTypedIdDefaults(Validator = ...)]` 为整个程序集统一设置默认验证器。
- **Dapper 集成** —— 引用 `Dapper` ≥ 2.0.0 时生成 `TypeHandler` 与 `ApplyTo(IDbConnection)`。
- **ASP.NET Core OpenAPI 集成** —— 引用 `Microsoft.AspNetCore.OpenApi` ≥ 9.0.0 时生成 `ApplyTo(OpenApiOptions)` 架构映射。
- **序列化** —— 自动生成 `System.Text.Json` 与 `Newtonsoft.Json`（≥ 13.0.0）转换器，
  其中 `System.Text.Json` 还支持字典键。
- **Entity Framework Core** —— 在需要时生成 EF Core（≥ 7.0.0）值转换器。
- **Swagger / OpenAPI** —— 在引用 Swashbuckle.AspNetCore 时生成架构映射，同时兼容 Microsoft.OpenApi 1.x 与 2.x。
- **运行时反射助手** —— 可在运行时判断任意类型是否为强类型 Id，并取回其底层基元类型。
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
   （[STIAO006](#diagnostics)）。

<a id="supported-primitive-types"></a>

## 支持的基元类型 <a href="#top" style="float:right">↑ 返回目录</a>

被包装的 `Value` 参数可以是以下类型之一：

| C# 类型   | 声明示例                                   |
| --------- | ------------------------------------------ |
| `Guid`    | `record struct OrderId(Guid Value)`         |
| `string`  | `record UserId(string Value)`               |
| `byte`    | `record struct ByteId(byte Value)`          |
| `sbyte`   | `record struct SByteId(sbyte Value)`        |
| `short`   | `record struct ShortId(short Value)`        |
| `ushort`  | `record struct UShortId(ushort Value)`       |
| `int`     | `record struct ProductId(int Value)`        |
| `uint`    | `record struct UIntId(uint Value)`           |
| `long`    | `record struct LongId(long Value)`          |
| `ulong`   | `record struct ULongId(ulong Value)`        |

类型按名称匹配，因此写成 `System.Guid`（或使用别名/using）效果完全相同。仅识别上表中的类型——
你自己定义的、恰好也叫 `Guid` 的类型并不受支持，会被 [STIAO008](#diagnostics) 拒绝。

<a id="what-gets-generated"></a>

## 生成器会生成什么 <a href="#top" style="float:right">↑ 返回目录</a>

对于每一个被标注的类型，生成器会在**同一命名空间**下产出一份 `partial` 声明（嵌套类型则嵌回它的包含
类型里，见[嵌套类型](#nested-types)），你手写的声明不会被改动。
具体产出取决于当前编译引用了什么：

| 编译中的条件                                                                          | 生成的代码                                                                          |
| ------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------- |
| 始终                                                                                  | 核心实现 → `<命名空间>.<类型名>.g.cs`                                                |
| 引用了 `System.Text.Json`                                                             | 嵌套 `SystemTextJsonConverter` 与 `[JsonConverter]` 特性 → `…SystemTextJson.g.cs`    |
| 引用了 `Newtonsoft.Json` ≥ 13.0.0                                                     | 嵌套 `NewtonsoftJsonConverter` 与 `[JsonConverter]` 特性 → `…NewtonsoftJson.g.cs`    |
| 引用了 `Microsoft.EntityFrameworkCore` ≥ 7.0.0 **且**存在重写了 `ConfigureConventions` 的 `DbContext` | 嵌套 `{类型名}Converter` → `…EntityFrameworkCore.g.cs`，以及 `StronglyTypedIds.ApplyTo(ModelConfigurationBuilder)` → `StronglyTypedIds.EntityFrameworkCore.g.cs` |
| 引用了 `Swashbuckle.AspNetCore.SwaggerGen` ≥ 6.0.0                                     | `StronglyTypedIds.ApplyTo(SwaggerGenOptions)` → `StronglyTypedIds.Swagger.g.cs`      |
| 引用了 `Dapper` ≥ 2.0.0                                                                | 嵌套 `{类型名}TypeHandler` → `…Dapper.g.cs`，以及 `StronglyTypedIds.ApplyTo(IDbConnection)` → `StronglyTypedIds.Dapper.g.cs` |
| 引用了 `Microsoft.AspNetCore.OpenApi` ≥ 9.0.0                                          | `StronglyTypedIds.ApplyTo(OpenApiOptions)` → `StronglyTypedIds.AspNetCoreOpenApi.g.cs` |

核心实现会补上：

- `IStronglyTypedId<TSelf, TPrimitiveId>`、`IParsable<TSelf>`、`ISpanParsable<TSelf>`、
  `IComparable<TSelf>`、`IEqualityOperators<TSelf, TSelf, bool>` 与
  `IComparisonOperators<TSelf, TSelf, bool>` 的实现。
- 基元类型具备时的 `IFormattable` 与 `ISpanFormattable` —— 以 `string` 为基元的 Id 例外，
  因为 `string` 并未实现这两者。
- 基元类型具备时的 `IUtf8SpanFormattable`（net8.0+ 即可用）与 `IUtf8SpanParsable<TSelf>`（`Guid` 自 .NET 10 起才实现，故按基元类型条件生成）—— 提供 `ReadOnlySpan<byte>` 的 `Parse` / `TryParse` 与 `TryFormat`。
- 静态的 `Create(TPrimitiveId value)` 工厂。
- 静态的 `TryCreate(TPrimitiveId value, out TSelf result)` 工厂——解析失败返回 `false`，`result` 为 `default`。
- `string` 与 `ReadOnlySpan<char>` 两套 `Parse` / `TryParse`，委托给基元类型。对于以 `string` 为基元的
  Id，`TryParse` 不接受 `null` 与空字符串。
- `CompareTo`，以及基于被包装值的 `<`、`>`、`<=`、`>=` 运算符。
- 返回被包装值文本的 `ToString()`（不再是 record 默认的 `OrderId { Value = … }` 形式），
  以及基元类型支持时的 `ToString(string?, IFormatProvider?)` 与 `TryFormat(...)`。
- 逐类型开启 `TypeConverter` 时，嵌套的 `XxxTypeConverter`（`[TypeConverter]` 特性）经 `TypeDescriptor` 在字符串与 Id 间往返；引用类型 Id 的 `null` 会解析回 `default`。

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
`null` 会解析回 `default`。

> 该转换器仅覆盖「字符串 ↔ Id」这一路径；它不替代 JSON 转换器，二者各自独立生效。

<a id="assembly-defaults"></a>

## 程序集级默认设置（StronglyTypedIdDefaults） <a href="#top" style="float:right">↑ 返回目录</a>

同一程序集里很多 Id 想共用同一个验证器时，可用程序集特性统一设置默认 `Validator`，免去逐个标注：

```csharp
[assembly: StronglyTypedIdDefaults(Validator = "MyValidators.NonEmpty")]
```

`StronglyTypedIdDefaults.Validator` 仅作为**默认**：单个 Id 上写了 `[StronglyTypedId(Validator = ...)]`
时，以该 Id 自己的为准；都没写时才回落到程序集默认值。

> 验证器方法仍需满足 [值校验](#value-validation) 的签名约定（`static bool Validate(TPrimitiveId value)`），
> 否则由 [STIAO010](#diagnostics) 报错。

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

前置条件只有一条：**包含类型必须都能被生成代码原样重开**——链上每一层都必须是 `partial`、不是泛型、
也不是 `file` 本地类型。不满足时由 [STIAO009](#diagnostics)（缺 `partial`，或 `file` 本地类型）
或 [STIAO003](#diagnostics)（泛型容器）报错，前者附带补 `partial` 的代码修复。

限制：容器不能带类型参数。重开 `Outer<T>` 需要复现它的类型参数表与约束，而且 `Outer<T>.OrderId`
这样的全名带 `<>`，无法作为生成文件的名称。

嵌套不影响其它任何能力：比较与排序、格式化与 span 解析、两种 JSON 序列化、
System.Text.Json 字典键、EF Core 转换器与 Swagger `MapType` 都照常工作。

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

Newtonsoft.Json 的字典键走的是 `TypeConverter` / `ToString()`，并不经过 `JsonConverter`，因此生成的
转换器接管不到它。键仍可往返——生成的 `ToString()` 返回的正是基元值文本——但该文本跟随当前区域性。
若需要控制键的格式，请自行给 Id 标注 `[TypeConverter]`。

两种转换器都把 Id 序列化为其底层基元值而不是对象，因此 JSON 结构与引入强类型 Id 之前保持一致。
引用类型 Id 为 `null` 时写出的就是 JSON `null`。

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
```

> EF Core 转换器只在 `DbContext` 重写了 `ConfigureConventions` 时才会生成，因此
> `StronglyTypedIds.ApplyTo(...)` 恰好在你能够调用它时才可用。仅仅引用
> `Microsoft.EntityFrameworkCore` 是不够的。

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

| 基元类型   | OpenAPI `type` | `format` |
| ---------- | -------------- | -------- |
| `Guid`     | `string`       | `uuid`   |
| `string`   | `string`       | —        |
| `byte`     | `integer`      | `byte`   |
| `sbyte`    | `integer`      | `sbyte`  |
| `short`    | `integer`      | `int16`  |
| `ushort`   | `integer`      | `uint16` |
| `int`      | `integer`      | `int32`  |
| `uint`     | `integer`      | `uint32` |
| `long`     | `integer`      | `int64`  |
| `ulong`    | `integer`      | `uint64` |

<a id="dapper"></a>

## Dapper <a href="#top" style="float:right">↑ 返回目录</a>

引用 `Dapper` ≥ 2.0.0 时，为每个强类型 Id 生成 `TypeHandler`，并提供一个统一入口：

```csharp
using Dapper;

// 为连接注册所有强类型 Id 的 TypeHandler。
StronglyTypedIds.ApplyTo(connection);

connection.Execute("INSERT INTO Orders (Id) VALUES (@Id)", new { Id = OrderId.Create(Guid.NewGuid()) });
```

每个 Id 的 `TypeHandler` 会把数据库值（值类型取 `Value`、引用类型取自身）写入参数，并把读取到的值
经 `Create` 还原为 Id。不同命名空间下同名 Id 的 `TypeHandler` 会以「命名空间下划线连接」前缀避免冲突
（与 EF Core 转换器同名处理一致）。

> 引用 `Dapper` 但主版本低于 2.0.0 时不生成，避免与旧版 `SqlMapper.TypeHandler` API 不兼容。

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

<a id="diagnostics"></a>

## 诊断与代码修复 <a href="#top" style="float:right">↑ 返回目录</a>

分析器会强制以下规则。每一条都会作为**编译错误**报告，因此不合法的声明无法通过编译。诊断文案
会按编译器区域设置输出英文或中文。

| 规则 ID  | 说明                                                       | 代码修复             |
| -------- | ---------------------------------------------------------- | -------------------- |
| STIAO000 | 类型必须是 `record`（或 `record struct`）。                | —                    |
| STIAO001 | 类型必须是 `partial`。                                     | 补上 `partial`       |
| STIAO002 | 类型不能是 `abstract`。                                    | 去掉 `abstract`      |
| STIAO003 | 类型不能是 `generic`（泛型）。                             | 移除类型参数表       |
| STIAO004 | 类型必须声明在命名空间内。                                 | —                    |
| STIAO005 | 类型必须拥有单参数的主构造函数。                           | —                    |
| STIAO006 | 主构造函数参数不能为可空类型。                             | 去掉 `?` 后缀        |
| STIAO007 | 主构造函数参数必须命名为 `Value`。                         | 重命名参数           |
| STIAO008 | 主构造函数参数类型必须是受支持的基元类型。                 | —                    |
| STIAO009 | 包含类型必须能被生成代码重开：是 `partial`，且不是 `file` 本地类型。 | 给包含类型补上 `partial` |
| STIAO010 | `Validator` 指向的方法必须存在、为静态、返回 `bool`、形参类型等于基元 Id 类型；否则不生成校验。 | — |
| STIAO011 | 不要用 `new XxxId(...)` 绕过 `Create` / `TryParse`（验证器不会被调用）。仅当该 Id 设了 `Validator` 时才提示，默认级别为 Info，可在 `.editorconfig` 调高。 | — |

代码修复通过 IDE 的常规灯泡菜单提供，并支持**在文档 / 项目 / 解决方案中修复全部出现处**。类型可以
拆成多个 `partial` 声明段：类型级规则按整个类型判定一次，参数级规则只在声明主构造函数的那一段上判定，
因此在另一个声明段里追加成员完全合法。

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
（3）使用 Swagger 时项目引用了 `Swashbuckle.AspNetCore.SwaggerGen` ≥ 6.0.0。

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

答：输出被包装基元值的文本（`"42"`、`"3f2c…"`），而不是 record 默认的 `OrderId { Value = 42 }`
形式，因此在日志与字符串内插里更易读。

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
