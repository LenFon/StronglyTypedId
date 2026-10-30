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
[JsonSerializable(typeof(OrderId))]
internal sealed partial class AppJsonContext : JsonSerializerContext;

var options = new JsonSerializerOptions { TypeInfoResolver = AppJsonContext.Default };
```

要登记每一个被你**当作根类型**序列化的类型 —— 上下文只服务从 `[JsonSerializable]` 根可达的类型，
因此 `JsonSerializer.Serialize(orderId, options)` 与其它根类型一样需要
`[JsonSerializable(typeof(OrderId))]`。

**Id 所包裹的基元类型不必登记。** 生成的转换器直接用 `Utf8JsonWriter` / `Utf8JsonReader` 读写基元
（`Guid` 走 `WriteStringValue(Guid)`，数值走 `WriteNumberValue`），不经过 `JsonSerializer`，
因此不需要基元类型的 `JsonTypeInfo`。这一点很重要：源生成的上下文只服务登记过的类型，
而漏登记时报的是框架内部的 `NotSupportedException: JsonTypeInfo metadata for type 'System.Guid'
was not provided`——从这条报错很难反推「少登记了一个基元类型」。

少数基元没有对应的直写形式（`Int128`、`UInt128`、`Half`、`TimeSpan`、`DateOnly`、`TimeOnly`，
以及读单字符的 `char`），它们仍由 `JsonSerializer` 处理，此时上下文需要为基元补一条
`[JsonSerializable]`。若漏了，本库会把框架的 `NotSupportedException` 转成一条说得清楚的
`JsonException`，直接告诉你该补哪一条。

### 两种 `GenerationMode` 的差别

`JsonSourceGenerationMode.Metadata` 只产出元数据，`Default` 还会产出序列化逻辑。两者的差别在于
**类型上的 `[JsonConverter]` 特性是否被取到**：

- `Default` 模式下，源生成器会把类型上的 `[JsonConverter]` 展开成转换器；
- `Metadata` 模式下走的是「按属性反射」那条路，特性不被取到——Id 会被当成普通对象按属性写出
  （`{"Value":"…"}`），而不是字符串。

`Metadata` 是 AOT 场景的推荐配置（产物体积更小），因此在那种配置下要靠
`options.AddStronglyTypedIds()` 把转换器注册进 `options.Converters`——
那条路与源生成器的决策无关，是确定生效的。默认模式下类型上的特性已经够用，`AddStronglyTypedIds` 可有可无。

### 让 context 写出同一种形态

`AddStronglyTypedIds` 修的是「序列化器从 `options.Converters` 里找转换器」那条路，而源生成的 context 对被要求
产出元数据的类型并不查这个列表：它自己发一份 `JsonTypeInfo`，按属性写出。结果是 context 写
`{"Value":"…"}`、反射式序列化写 `"…"`——同一个 Id 两种形态，且不报任何错。

`WithStronglyTypedIds()` 补上这个缺口。它把你的 resolver 包一层，让 Id 的 `JsonTypeInfo`（由生成的
转换器构造，零反射）先被问到：

```csharp
var options = new JsonSerializerOptions
{
    TypeInfoResolver = AppJsonContext.Default.WithStronglyTypedIds(),
};
```

顺序很关键，方法内部已经处理好：`JsonTypeInfoResolver.Combine` 取第一个返回非 null `JsonTypeInfo`
的 resolver，而 context 对 Id 也会返回一份。把 Id 的 resolver 放前面，转换器才会赢；其余类型照旧
落到你的 context。

`AddStronglyTypedIds` 与 `WithStronglyTypedIds()` 互不依赖，按你配置序列化的方式选一个即可：把
`TypeInfoResolver` 设成 context 就用 `WithStronglyTypedIds()`，靠 `options.Converters` 就用
`AddStronglyTypedIds`。两个都设也没有副作用。

两个目标框架都适用：`System.Text.Json` 的源生成器在 net8.0 与 net10.0 的引用包里都有，因此这个缺口
与修复在两个 TFM 上同样成立。


## MessagePack 与 MongoDB.Bson 在 AOT 下

生成的集成产物本身零反射，注册入口还会把 Id 包裹的**基元**一并覆盖：

- **MessagePack** —— `MessagePackSerializerOptions.AddStronglyTypedIds()` 会为每个去重后的枚举
  基元追加 `GenericEnumFormatter<TEnum>`。MessagePack 在 AOT（禁用动态代码）下的 resolver 链里没有
  任何枚举 resolver —— 走 IL Emit 的 `DynamicEnumResolver` 被排除在外 —— 不补这一项，枚举基元的 Id
  连序列化都过不去。非枚举基元（`int`、`Guid` 等）由内置 resolver 提供，本身零反射。该格式化器与
  动态 resolver 写同样的底层数值载荷，线格式不变。
- **MongoDB.Bson** —— `StronglyTypedIds.RegisterBsonSerializers()` 会为全部二进制路径基元（`int`、
  `char`、`decimal`、`string`、枚举等）预注册默认配置的序列化器。否则 BSON 的
  `PrimitiveSerializationProvider` 会经 `Activator` 反射创建 `Int32Serializer`、`EnumSerializer<T>`
  等实例，构造器被裁剪后在 AOT 下直接抛 `MissingMethodException`。注册的实例与 provider 创建的默认
  实例配置一致，行为不变。它还会注册 BSON 的动态类型——`object`、`List<object>`、`ExpandoObject`——
  因为任何根序列化上下文都会取 `BsonDefaults.DynamicArraySerializer` 与
  `BsonDefaults.DynamicDocumentSerializer`，而它们是去注册表里查的。**`Guid` 是例外**：driver 3.0 起
  没有全局默认表示，应用必须按既有数据自行注册相应表示的 `GuidSerializer`。

## 覆盖了哪些路径

本库自己的 AOT 冒烟测试会发布一个真实的 `PublishAot` 可执行程序并在 CI 里运行，所以「AOT 可用」是被测出来的，
不是写在文档里就算数。它在发布后的 AOT 二进制上覆盖：

| 范围 | 覆盖内容 |
| --- | --- |
| 工厂 | `Create`、`TryCreate`，以及验证器拒绝非法值 |
| 文本 | `ToString`、`Parse`、`TryParse`，分别走 `string`、`ReadOnlySpan<char>`、`ReadOnlySpan<byte>` |
| 格式化 | `TryFormat` 写入 `Span<char>` 与 `Span<byte>`，以及插值形式的 `ToString()` |
| 前缀 | 带前缀的 `ToString`、往返解析，以及宽松的裸值读取 |
| JSON | 转换器的 `Write` / `Read` 往返，以及 `WriteAsPropertyName` / `ReadAsPropertyName` |
| 经 context 的 JSON | `JsonSerializer` 走源生成的 `JsonSerializerContext` + `WithStronglyTypedIds()`，含带前缀与验证器拒绝两条路径 |
| MessagePack | `int` 与 `enum` 基元的值往返，以及拒绝未定义枚举值 |
| MongoDB.Bson | `int` 与 `enum` 基元的值往返，以及拒绝未定义枚举值 |

你不需要在自己的应用里复现这些内容；列出来只是让你知道哪些路径已在 AOT 下验证过。冒烟工程属于 CI 的事，
如果你要改这个库本身，见 [CONTRIBUTING](https://github.com/LenFon/StronglyTypedId/blob/main/.github/CONTRIBUTING.md)。

## AOT 应用的检查清单

1. 用 `<PublishAot>true</PublishAot>`（或 `<PublishTrimmed>true</PublishTrimmed>`）发布。
2. 如果要序列化 Id，提供一个源生成的 `JsonSerializerContext` 并设为 `TypeInfoResolver`——它本来也是你为
   其他类型准备的那一个。用 `.WithStronglyTypedIds()` 包一层，Id 才会保持字符串形态而不是被写成对象。
3. 保留裁剪分析器。若它在你把 `Type` 传进 `GetPrimitiveIdType` / `IsStronglyTypedId` 的调用点报警，请给
   那个参数补上 `[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)]`，而不是压制警告
   ——保住元数据的正是这个标注。
4. 没有第 4 步。生成器里不存在 AOT 专用开关。

[<- 返回目录](index.md) · [English](../aot.md)
