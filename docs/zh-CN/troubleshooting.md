---
layout: default
title: 故障排查
---

<a id="troubleshooting"></a>

# 故障排查

先看症状。某条诊断具体是什么意思，见[诊断](diagnostics.md#diagnostics)；概念性问题见[常见问题](faq.md#faq)。

<a id="nothing-was-generated"></a>

## 什么都没生成

类型能编译，但没有 `Create`、没有 `Parse`、没有转换器。

1. 声明是 `partial` 吗？生成器只能往 partial 类型里加东西——`STIA0001` 会报。
2. 它是非抽象、非泛型的 `record` 或 `record struct`，位于命名空间内，且只有一个名为 `Value` 的位置参数、
   类型属于受支持的基元吗？每一种违反都有自己的规则——`STIA0000` 与 `STIA0002` – `STIA0008`。
3. 项目引用了 `Len.StronglyTypedId` 吗？分析器随包传递，所以只消费 `IStronglyTypedId<,>` 的项目照样有
   诊断；但**声明** Id 的项目需要这个包。

## 生成代码全没了，只剩一条 CS8785

`CS8785` 表示生成器抛了异常，它为那次编译产出的所有文件都被丢弃。生成器能捕获时会把同一失败报成
`STIA0012`。这是生成器的 bug，不是你的声明有问题：请带上编译输出与最小复现开 issue。

## 某个集成没生成

每个集成只在触发条件满足时才生成。`Integrations` 或 `[assembly: StronglyTypedIdDefaults(...)]` 也可能
把它关掉了——如果包明明引用了却没有产物，先查这两处。

| 集成 | 生成条件 |
| --- | --- |
| System.Text.Json | 引用了 `System.Text.Json` |
| Newtonsoft.Json | 引用了 `Newtonsoft.Json` ≥ 13.0.0 |
| EF Core | 引用了 `Microsoft.EntityFrameworkCore` ≥ 7.0.0 |
| Swashbuckle | 引用了 `Swashbuckle.AspNetCore.SwaggerGen` ≥ 6.0.0 |
| Dapper | 引用了 `Dapper` ≥ 2.0.0 |
| MessagePack | 引用了 `MessagePack` ≥ 2.0.0 |
| MongoDB | 引用了 `MongoDB.Bson` ≥ 3.0.0 |
| ASP.NET Core OpenAPI | 引用了 `Microsoft.AspNetCore.OpenApi` ≥ 9.0.0——8.0.x 没有 schema 转换器 API，`net8.0` 上请改用 Swashbuckle |
| ASP.NET Core MVC | 引用了 `Microsoft.AspNetCore.Mvc` ≥ 2.0.0（或其 `.Core` / `.Abstractions` 门面） |

还有两种会让某个 Id 被跳过：Id 声明在**泛型**容器里（外部引用类集成无法重开它——见[嵌套类型](declaration.md#nested-types)），
或者程序集级默认把它关了。

## 找不到 `StronglyTypedIds.ApplyTo`

入口方法位于 `Len.StronglyTypedId` 命名空间的 `internal static partial class StronglyTypedIds`，与拥有它的
集成一起生成。因此：

1. `using Len.StronglyTypedId;` 必须在作用域内。
2. 那个集成必须真的生成了——见上表。`ApplyTo(JsonSerializerOptions)` 需要 System.Text.Json；
   `AddStronglyTypedId()` 需要 Newtonsoft.Json ≥ 13.0.0；`ApplyTo(ModelConfigurationBuilder)` /
   `ApplyTo(ModelBuilder)` 需要 EF Core ≥ 7.0.0；`ApplyTo(MessagePackSerializerOptions)` 需要 MessagePack；
   `RegisterBsonSerializers()` 需要 MongoDB.Bson。

## 分析器不报错，或报的还是旧结果

通常是 Visual Studio 缓存。**清理解决方案并重新生成**——编译产物里始终是最新的生成代码，期间可以先忽略。
若仍不恢复，检查 `.editorconfig` 里是否把相关诊断关掉了。

## 看着没问题的文本解析失败

| 原因 | 检查点 |
| --- | --- |
| 文化 | `provider` 传 `null` 表示**不变文化**，不是当前文化。de-DE 下 `"1,5"` 不是 `1.5`；确实要按当前文化解析就显式传 `CultureInfo.CurrentCulture` |
| 前缀 | 读取默认是宽松的——裸值也能解析。**严格**读取会拒绝不带前缀的文本；先确认该 Id 声明的是哪种模式 |
| 空字符串 | 以 `string` 为基元的 Id，`TryParse` 不接受 `null` 与 `""` |
| 枚举 | 普通枚举必须命中已定义成员；`[Flags]` 枚举不许出现没有成员占用的位。`"999"` 会失败，尽管 `Enum.TryParse` 本身会接受 |

## 从存储里读回来的值对不上了

改动 `Prefix`、`JsonShape`、基元类型或 `Normalizer` 都会改变**存储形态**——`Prefix` / `Normalizer` 改的是
文本形，`JsonShape` 改的是 JSON 结构——而这是数据契约，改动之前写入的行将读不回来。请把它们当作破坏性
变更并迁移存量数据；见[序列化](serialization.md#serialization)。

## Native AOT 下 JSON 抛异常

`System.Text.Json` 在 AOT 下会禁用反射式序列化，应用必须提供源生成的 `JsonSerializerContext`。这是
`System.Text.Json` 的要求，不是本库的特例——见[原生 AOT 与裁剪](aot.md)。

## MongoDB 序列化 `Guid` 基元的 Id 时抛异常

驱动 3.0 起取消了全局默认的 `Guid` 表示形式，没注册就序列化 `Guid` 会抛——而 `Guid` 基元的 Id 恰好走这条路，
因为它的序列化器把活交给了 `Guid` 序列化器。请在写入第一个文档之前注册：

```csharp
BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
```

请按既有文档实际使用的表示形式来选：事后更改会让它们读不回来。见 [MongoDB](integrations.md#mongodb)。

## Swagger 文档里 Id 变成了纯 string

如果这个 Id 出现在 **Minimal API** 的路由参数或查询参数上，而文档里仍是裸的 `type: string`，说明操作过滤器
没有生效 —— 它由 `StronglyTypedIds.ApplyTo(options)` 注册，这一调用必须在：

```csharp
services.AddSwaggerGen(options =>
{
    StronglyTypedIds.ApplyTo(options);
});
```

单靠 `MapType` 覆盖不到这类参数：它按 CLR 类型登记，而 `ApiExplorer` 把 `IParsable` 参数报成 `string`。
生成的操作过滤器改按参数的**声明类型**查表。细节见[集成](integrations.md#swashbuckle)。

## 泛型容器里的 Id 没有 JSON 转换器

这是预期行为：特性实参不能引用携带容器类型参数的转换器（CS0416），所以 `[JsonConverter]` 无法生成。
请为闭合类型自行注册：

```csharp
options.Converters.Add(new Outer<int>.InnerId.SystemTextJsonConverter());
```

[<- 返回目录](index.md) · [English](../troubleshooting.md)
