# StronglyTypedId

> 📖 英文文档：[README.md](README.md)

[![codecov][badge-codecov]](https://app.codecov.io/gh/LenFon/StronglyTypedId/tree/main)
[![NuGet][badge-nuget]][nuget-package]
[![License: MIT][badge-license]][license-file]

一个源码生成器与分析器，能用一行 `record` 声明就生成一个功能完备的强类型 Id。它会自动为你生成
`IStronglyTypedId<TSelf, TPrimitiveId>` 实现、`IParsable<TSelf>` 支持，以及对 JSON / Entity Framework Core /
Swagger 的集成代码。

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
- [约束条件](#constraints)
- [常见问题](#faq)
- [许可证](#license)

<a id="features"></a>

## 功能特性 <a href="#top" style="float:right">↑ 返回目录</a>

- **声明极简** —— 用 `[StronglyTypedId]` 标注一个 `partial record`（或 `partial record struct`）即可。
- **编译期安全** —— 内建分析器会把不合法的声明作为错误报出（见[约束条件](#constraints)）。
- **解析与格式化** —— 自动生成 `IParsable<TSelf>` 支持（`Parse` / `TryParse`）。
- **序列化** —— 自动生成 `System.Text.Json` 与 `Newtonsoft.Json`（≥ 13.0.0）转换器。
- **Entity Framework Core** —— 在需要时生成 EF Core（≥ 7.0.0）值转换器。
- **Swagger / OpenAPI** —— 在引用 Swashbuckle.AspNetCore 时生成架构映射。
- **零运行时依赖** —— 包内仅包含生成器/分析器程序集，以及一个很小的运行时库。

<a id="requirements"></a>

## 环境要求 <a href="#top" style="float:right">↑ 返回目录</a>

| 组件                 | 要求                                                                 |
| -------------------- | -------------------------------------------------------------------- |
| .NET SDK             | 任意现代 .NET SDK（生成器目标框架为 `netstandard2.0`）。             |
| C# 源生成器          | 需搭载 **Roslyn 4.4+** 的 .NET SDK（例如 .NET 6.0.3xx 及以上）。    |
| 运行时库             | 目标框架为 **.NET 8.0** 与 **.NET 10.0**。                          |
| Newtonsoft.Json      | ≥ **13.0.0**（仅在使用 Newtonsoft.Json 转换器时需要）。              |
| EntityFrameworkCore  | ≥ **7.0.0**（仅在使用 EF Core 转换器时需要）。                      |
| Swashbuckle.AspNetCore | ≥ **6.0.0**（仅在使用 Swagger 架构映射时需要）。                  |

<a id="getting-started"></a>

## 快速开始 <a href="#top" style="float:right">↑ 返回目录</a>

1. 将包安装到你的应用程序或类库中：

   ```text
   Package Manager : Install-Package Len.StronglyTypedId
   CLI             : dotnet add package Len.StronglyTypedId
   ```

2. 用单一主构造函数参数（参数名必须为 `Value`）声明一个强类型 Id：

   ```csharp
   [StronglyTypedId]
   public partial record struct OrderId(Guid Value);

   // 或

   [StronglyTypedId]
   public partial record OrderId(Guid Value);
   ```

   > **`record` 与 `record struct` 的取舍** —— `record struct` 生成值类型（不可为 `null`、
   > 接口调用不会装箱），`record` 生成引用类型（可为 `null`）。按你的可空性与性能预期选择其一，
   > 两者暴露的成员完全一致。

   生成的类型会暴露 `Value`、静态的 `Create(...)` 工厂，以及 `Parse` / `TryParse`。
   凡是原本传递裸基元类型的地方，都可以换成它：

   ```csharp
   public class Order
   {
       public OrderId Id { get; set; }
       public UserId Buyer { get; set; }
   }
   ```

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

<a id="what-gets-generated"></a>

## 生成器会生成什么 <a href="#top" style="float:right">↑ 返回目录</a>

对于每一个被标注的类型，生成器会产出：

- `IStronglyTypedId<TSelf, TPrimitiveId>`、`IParsable<TSelf>`、
  `IEqualityOperators<TSelf, TSelf, bool>` 的实现。
- 静态的 `Create(TPrimitiveId value)` 工厂，以及 `Parse` / `TryParse` 方法。
- 当引用了 `System.Text.Json` 时，生成嵌套的 `SystemTextJsonConverter`。
- 当引用了 `Newtonsoft.Json` ≥ 13.0.0 时，生成嵌套的 `NewtonsoftJsonConverter`。
- 只有当编译中还存在重写了 `ConfigureConventions` 的 `DbContext` 时，才会生成嵌套的
  `{类型名}Converter`（EF Core `ValueConverter`）。
- 只有当引用了 `Swashbuckle.AspNetCore.SwaggerGen` 时，才会生成 Swagger 的 `MapType` 注册。

<a id="serialization"></a>

## 序列化 <a href="#top" style="float:right">↑ 返回目录</a>

### System.Text.Json

转换器通过生成的 `[JsonConverter]` 特性自动应用，无需额外配置。

```csharp
var json = JsonSerializer.Serialize(new OrderId(Guid.NewGuid()));
var id   = JsonSerializer.Deserialize<OrderId>(json);
```

### Newtonsoft.Json（≥ 13.0.0）

同样通过生成的 `[JsonConverter]` 特性实现零配置。

```csharp
var json = JsonConvert.SerializeObject(new OrderId(Guid.NewGuid()));
var id   = JsonConvert.DeserializeObject<OrderId>(json);
```

<a id="entity-framework-core"></a>

## Entity Framework Core <a href="#top" style="float:right">↑ 返回目录</a>

在 `DbContext` 中为所有强类型 Id 注册生成的值转换器（≥ 7.0.0）：

```csharp
protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
{
    base.ConfigureConventions(configurationBuilder);

    // 为项目中每一个强类型 Id 注册 ValueConverter。
    StronglyTypedIds.ApplyTo(configurationBuilder);
}
```

> EF Core 转换器只在 `DbContext` 重写了 `ConfigureConventions` 时才会生成，因此
> `StronglyTypedIds.ApplyTo(...)` 恰好在你能够调用它时才可用。

<a id="swashbuckle"></a>

## Swashbuckle <a href="#top" style="float:right">↑ 返回目录</a>

将每一个强类型 Id 映射到其底层基元类型的 Swagger 架构（Swashbuckle.AspNetCore）：

```csharp
services.AddSwaggerGen(options =>
{
    StronglyTypedIds.ApplyTo(options);
});
```

该实现同时兼容 Microsoft.OpenApi 1.x（Swashbuckle 6.x）与 2.x（Swashbuckle 7.x）。

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

<a id="constraints"></a>

## 约束条件 <a href="#top" style="float:right">↑ 返回目录</a>

分析器会强制以下规则。每一条都会作为**编译错误**报告，因此不合法的声明无法通过编译：

| 规则 ID  | 说明                                                       |
| -------- | ---------------------------------------------------------- |
| STIAO000 | 类型必须是 `record`（或 `record struct`）。                |
| STIAO001 | 类型必须是 `partial`。                                     |
| STIAO002 | 类型不能是 `abstract`。                                    |
| STIAO003 | 类型不能是 `generic`（泛型）。                             |
| STIAO004 | 类型不能嵌套，且必须声明在命名空间内。                     |
| STIAO005 | 类型必须拥有单参数的主构造函数。                           |
| STIAO006 | 主构造函数参数不能为可空类型。                             |
| STIAO007 | 主构造函数参数必须命名为 `Value`。                         |
| STIAO008 | 主构造函数参数类型必须是受支持的基元类型。                 |

<a id="faq"></a>

## 常见问题 <a href="#top" style="float:right">↑ 返回目录</a>

**问：分析器没有识别出我最新的改动，为什么？**

答：通常是 Visual Studio 缓存导致。**清理解决方案**后重新生成即可。编译产物中始终包含最新的生成代码，
因此即便此刻忽略该提示也无妨。

<a id="license"></a>

## 许可证 <a href="#top" style="float:right">↑ 返回目录</a>

本项目采用 [MIT 许可证](LICENSE.txt)。

[nuget-package]: https://www.nuget.org/packages/Len.StronglyTypedId/
[license-file]: LICENSE.txt
[badge-nuget]: https://img.shields.io/nuget/v/Len.StronglyTypedId.svg
[badge-codecov]: https://codecov.io/github/LenFon/StronglyTypedId/branch/main/graph/badge.svg?token=S3PBV7W190
[badge-license]: https://img.shields.io/badge/License-MIT-blue.svg
