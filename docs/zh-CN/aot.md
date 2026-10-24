---
layout: default
title: 原生 AOT 与裁剪
---

# 原生 AOT 与裁剪

强类型 Id 就是普通的生成类型，因此发布 Native AOT 或裁剪时不需要再跑一次源生成器。运行时库标记为
`<IsAotCompatible>true</IsAotCompatible>`，无论是库本身还是生成器产出的代码，都不依赖动态代码生成，
也不按名字加载类型。

## 库提供了什么保证

- **不用 `Reflection.Emit`、不按名字 `Activator.CreateInstance`、不 `Type.GetType(string)`。**
  每个生成的成员都是包裹基元之上的普通静态或实例方法。
- **验证器与归一化器是直调的。** 它们是你 Id 上的静态方法，编译期就已确定，AOT 下的行为与 JIT 完全一致。
- **序列化器是生成出来的，不是反射出来的。** System.Text.Json、Newtonsoft.Json、MessagePack、MongoDB
  的转换器都是嵌套类型；库不会为了造一个转换器去反射你的 Id。
- **反射辅助是裁剪安全的。** `Type.GetPrimitiveIdType()`、`Type.TryGetPrimitiveIdType(out Type?)`、
  `Type.IsStronglyTypedId()` 是仅有的三个反射入口，每个参数都带
  `[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)]`——正是这个标注让「识别强类型
  Id」所需的接口元数据在裁剪后依然保留。

## AOT 下的 System.Text.Json

`System.Text.Json` 在 Native AOT 下会禁用反射式序列化，因此 AOT 应用必须提供源生成的
`JsonSerializerContext`。**这是 `System.Text.Json` 的要求，不是本库的特例**——生成的转换器和其他转换器
一样挂进那个上下文即可：

```csharp
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(Guid))]
[JsonSerializable(typeof(string))]
internal sealed partial class AppJsonContext : JsonSerializerContext;

var options = new JsonSerializerOptions { TypeInfoResolver = AppJsonContext.Default };
```

要登记 Id 所包裹的基元类型，以及每一个被你**当作根类型**序列化的类型 —— 上下文只服务从
`[JsonSerializable]` 根可达的类型，因此 `JsonSerializer.Serialize(orderId, options)` 与其它根类型一样
需要 `[JsonSerializable(typeof(OrderId))]`。当 Id 作为某个已登记类型的成员出现时，它不必单独列出：
生成的 `[JsonConverter]` 会从这个特性上被取到。

## MessagePack 与 MongoDB.Bson 在 AOT 下

生成的集成产物本身零反射，注册入口还会把 Id 包裹的**基元**一并覆盖：

- **MessagePack** —— `StronglyTypedIds.ApplyTo(MessagePackSerializerOptions)` 会为每个去重后的枚举
  基元追加 `GenericEnumFormatter<TEnum>`。MessagePack 在 AOT（禁用动态代码）下的 resolver 链里没有
  任何枚举 resolver —— 走 IL Emit 的 `DynamicEnumResolver` 被排除在外 —— 不补这一项，枚举基元的 Id
  连序列化都过不去。非枚举基元（`int`、`Guid` 等）由内置 resolver 提供，本身零反射。该格式化器与
  动态 resolver 写同样的底层数值载荷，线格式不变。
- **MongoDB.Bson** —— `StronglyTypedIds.RegisterBsonSerializers()` 会为全部二进制路径基元（`int`、
  `char`、`decimal`、`string`、枚举等）预注册默认配置的序列化器。否则 BSON 的
  `PrimitiveSerializationProvider` 会经 `Activator` 反射创建 `Int32Serializer`、`EnumSerializer<T>`
  等实例，构造器被裁剪后在 AOT 下直接抛 `MissingMethodException`。注册的实例与 provider 创建的默认
  实例配置一致，行为不变。**`Guid` 是例外**：driver 3.0 起没有全局默认表示，应用必须按既有数据
  自行注册相应表示的 `GuidSerializer`。

## 这件事是怎么验证的

`test/Len.StronglyTypedId.Aot.SmokeTest` 是一个真实的 `PublishAot` 可执行工程，由 CI 的 `aot` job 发布并
运行——「AOT 可用」是被测出来的，不是写在文档里就算数的。它在发布后的 AOT 二进制上覆盖：

| 范围 | 覆盖内容 |
| --- | --- |
| 工厂 | `Create`、`TryCreate`，以及验证器拒绝非法值 |
| 文本 | `ToString`、`Parse`、`TryParse`，分别走 `string`、`ReadOnlySpan<char>`、`ReadOnlySpan<byte>` |
| 格式化 | `TryFormat` 写入 `Span<char>` 与 `Span<byte>`，以及插值形式的 `ToString()` |
| 前缀 | 带前缀的 `ToString`、往返解析，以及宽松的裸值读取 |
| JSON | 转换器的 `Write` / `Read` 往返，以及 `WriteAsPropertyName` / `ReadAsPropertyName` |
| MessagePack | `int` 与 `enum` 基元的值往返，以及拒绝未定义枚举值 |
| MongoDB.Bson | `int` 与 `enum` 基元的值往返，以及拒绝未定义枚举值 |

该工程把 `TrimmerSingleWarn` 设为 `false`，让裁剪与 AOT 警告逐条列出而不是折叠成一条 `IL2104`，并且要求
**零警告**编译：分析器干净 + 运行时干净，才算「AOT 可用」。

## AOT 应用的检查清单

1. 用 `<PublishAot>true</PublishAot>`（或 `<PublishTrimmed>true</PublishTrimmed>`）发布。
2. 如果要序列化 Id，提供一个源生成的 `JsonSerializerContext` 并设为 `TypeInfoResolver`——它本来也是你为
   其他类型准备的那一个。
3. 保留裁剪分析器。若它在你把 `Type` 传进 `GetPrimitiveIdType` / `IsStronglyTypedId` 的调用点报警，请给
   那个参数补上 `[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)]`，而不是压制警告
   ——保住元数据的正是这个标注。
4. 没有第 4 步。生成器里不存在 AOT 专用开关。

[<- 返回目录](index.md) · [English](../aot.md)
