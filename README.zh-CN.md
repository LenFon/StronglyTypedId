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
- [序列化](#serialization)
- [Entity Framework Core](#entity-framework-core)
- [Swashbuckle](#swashbuckle)
- [用接口约束泛型](#using-the-interface)
- [运行时反射](#reflection-helpers)
- [诊断与代码修复](#diagnostics)
- [常见问题](#faq)
- [许可证](#license)

<a id="features"></a>

## 功能特性 <a href="#top" style="float:right">↑ 返回目录</a>

- **声明极简** —— 用 `[StronglyTypedId]` 标注一个 `partial record`（或 `partial record struct`）即可。
- **编译期安全** —— 内建分析器会把不合法的声明作为错误报出，其中 9 条规则里有 5 条附带代码修复，
  可一键修好声明（见[诊断与代码修复](#diagnostics)）。
- **解析与格式化** —— 自动生成 `IParsable<TSelf>` 支持（`Parse` / `TryParse`）。
- **序列化** —— 自动生成 `System.Text.Json` 与 `Newtonsoft.Json`（≥ 13.0.0）转换器。
- **Entity Framework Core** —— 在需要时生成 EF Core（≥ 7.0.0）值转换器。
- **Swagger / OpenAPI** —— 在引用 Swashbuckle.AspNetCore 时生成架构映射，同时兼容 Microsoft.OpenApi 1.x 与 2.x。
- **运行时反射助手** —— 可在运行时判断任意类型是否为强类型 Id，并取回其底层基元类型。
- **零第三方依赖** —— 包内仅包含生成器/分析器程序集，以及一个很小的运行时库，不会往你的依赖图里塞任何东西。

<a id="requirements"></a>

## 环境要求 <a href="#top" style="float:right">↑ 返回目录</a>

| 组件                              | 要求                                                                                     |
| --------------------------------- | ---------------------------------------------------------------------------------------- |
| 项目的目标框架                    | **`net8.0`、`net10.0`** 或更高兼容框架（运行时库的目标框架为 `net8.0` 与 `net10.0`）。    |
| .NET SDK                          | **8.0 及以上**（生成器目标框架为 `netstandard2.0`，需要 **Roslyn 4.4+**，即 6.0.3xx 之后的任意 SDK）。 |
| Newtonsoft.Json                   | ≥ **13.0.0**（仅在使用 Newtonsoft.Json 转换器时需要）。                                   |
| EntityFrameworkCore               | ≥ **7.0.0**（仅在使用 EF Core 转换器时需要）。                                            |
| Swashbuckle.AspNetCore.SwaggerGen | ≥ **6.0.0**（仅在使用 Swagger 架构映射时需要）。                                          |

生成代码依赖 `IParsable<T>` 与 `IEqualityOperators<,,>`，因此运行时库从 `net8.0` 起步，并不跟随生成器
低得多的 `netstandard2.0` 基线。

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
你自己定义的、恰好也叫 `Guid` 的类型并不受支持，会在生成代码处报错。

<a id="what-gets-generated"></a>

## 生成器会生成什么 <a href="#top" style="float:right">↑ 返回目录</a>

对于每一个被标注的类型，生成器会在**同一命名空间**下产出一份 `partial` 声明，你手写的声明不会被改动。
具体产出取决于当前编译引用了什么：

| 编译中的条件                                                                          | 生成的代码                                                                          |
| ------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------- |
| 始终                                                                                  | 核心实现 → `<命名空间>.<类型名>.g.cs`                                                |
| 引用了 `System.Text.Json`                                                             | 嵌套 `SystemTextJsonConverter` 与 `[JsonConverter]` 特性                             |
| 引用了 `Newtonsoft.Json` ≥ 13.0.0                                                     | 嵌套 `NewtonsoftJsonConverter` 与 `[JsonConverter]` 特性                             |
| 引用了 `Microsoft.EntityFrameworkCore` ≥ 7.0.0 **且**存在重写了 `ConfigureConventions` 的 `DbContext` | 嵌套 `{类型名}Converter` 与 `StronglyTypedIds.ApplyTo(ModelConfigurationBuilder)` |
| 引用了 `Swashbuckle.AspNetCore.SwaggerGen` ≥ 6.0.0                                     | `StronglyTypedIds.ApplyTo(SwaggerGenOptions)`                                        |

核心实现会补上：

- `IStronglyTypedId<TSelf, TPrimitiveId>`、`IParsable<TSelf>`、
  `IEqualityOperators<TSelf, TSelf, bool>` 的实现。
- 静态的 `Create(TPrimitiveId value)` 工厂。
- 委托给基元类型的 `Parse` / `TryParse`。对于以 `string` 为基元的 Id，`TryParse` 不接受 `null`
  与空字符串。

各集成入口统一生成到同一个类里——`Len.StronglyTypedId` 命名空间下的
`internal static partial class StronglyTypedIds`——因此无论项目声明了多少个 Id，两个 `ApplyTo`
重载都并存于该类中。调用它们需要 `using Len.StronglyTypedId;`（声明特性时通常已经有了）。

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
| STIAO004 | 类型不能嵌套，且必须声明在命名空间内。                     | —                    |
| STIAO005 | 类型必须拥有单参数的主构造函数。                           | —                    |
| STIAO006 | 主构造函数参数不能为可空类型。                             | 去掉 `?` 后缀        |
| STIAO007 | 主构造函数参数必须命名为 `Value`。                         | 重命名参数           |
| STIAO008 | 主构造函数参数类型必须是受支持的基元类型。                 | —                    |

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
`==` 与 `!=`。

<a id="license"></a>

## 许可证 <a href="#top" style="float:right">↑ 返回目录</a>

本项目采用 [MIT 许可证](LICENSE.txt)。

[nuget-package]: https://www.nuget.org/packages/Len.StronglyTypedId/
[license-file]: LICENSE.txt
[badge-nuget]: https://img.shields.io/nuget/v/Len.StronglyTypedId.svg
[badge-codecov]: https://codecov.io/github/LenFon/StronglyTypedId/branch/main/graph/badge.svg?token=S3PBV7W190
[badge-license]: https://img.shields.io/badge/License-MIT-blue.svg
