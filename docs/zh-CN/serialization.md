---
layout: default
title: 序列化
---

<a id="serialization"></a>

# 序列化

## System.Text.Json

转换器通过生成的 `[JsonConverter]` 特性自动应用，无需额外配置。

```csharp
var json = JsonSerializer.Serialize(new OrderId(Guid.NewGuid())); // "..."
var id   = JsonSerializer.Deserialize<OrderId>(json);
```

## Newtonsoft.Json（≥ 13.0.0）

同样通过生成的 `[JsonConverter]` 特性实现零配置。

```csharp
var json = JsonConvert.SerializeObject(new OrderId(Guid.NewGuid()));
var id   = JsonConvert.DeserializeObject<OrderId>(json);
```

## 序列化器不认识的基元

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

## 字典键

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
枚举键也是 `options.DictionaryKeyPolicy` 唯一生效的情形：键是「名字」而非数据，按 camelCase 改写它与
内建枚举键的行为一致，读路径则忽略大小写解析以接受被改写过的键。其余基元的键文本原样保留——那段文本
**就是**值本身，改写它会让文档读不回来——带前缀的 Id 同样排除在外，因为策略会把前缀一起改写。把 `null`
的 Id 当属性名写出会抛 `NotSupportedException`。

Newtonsoft.Json 的字典键走的是 `TypeConverter` / `ToString()`，并不经过 `JsonConverter`，因此生成的
转换器接管不到它。请在 Id 上开启 `TypeConverter = true`，Newtonsoft 才能在读路径还原键。键文本与转换器
写出的一致，都是**不变文化**文本——生成的 `ToString()` 与生成的 `TypeConverter` 两个方向都按不变文化读写——
因此在任何区域设置下键都保持稳定。

两种转换器都把 Id 序列化为其底层基元值而不是对象，因此 JSON 结构与引入强类型 Id 之前保持一致。
引用类型 Id 为 `null` 时写出的就是 JSON `null`，读回也得到 `null`——但这只对 `record` 成立。`record struct`
Id 无法表示 `null`，因此两种转换器都会拒绝该 token，而不是悄悄产出 `default`（`JsonException` /
`JsonSerializationException`）。

以 `enum` 为基元的 Id 在读路径上与它的文本形执行同一道校验：不对应任何成员的数字会被同样的框架异常
拒绝，因此 `{"status": 999}` 不可能造出一个值为 `(Status)999` 的 Id。这正是 `Id.TryParse("999")` 早已
执行的校验——Dapper 的三条路径也一样——补上它之后，唯一还放行野值的入口也关上了。`[Flags]` 枚举则改按
未定义位掩码校验：已定义成员的任意位组合照常通过，而 `999` 不通过。

## 验证失败的异常契约

`Validator` 拒绝的是「能解析但违反领域规则」的值。两种转换器在读入时都会执行它，并用各自的异常类型
报告拒绝，而不是让 `Create` 的 `ArgumentException` 穿透出来：

| 序列化器 | 值被拒绝时抛出的异常 |
| --- | --- |
| `System.Text.Json` | `JsonException` |
| `Newtonsoft.Json` | `JsonSerializationException` |

这一点很重要，因为两个库的转换器契约都是以该异常表述的：用 `catch (JsonException)` 包住反序列化的
调用方，会把「载荷格式不对」和「领域规则不满足」当作同一类失败处理；也不会收到一个它无从预期的
`ArgumentException`。生成代码为此改走 `TryCreate` 并自行抛出框架异常——字典键那条路径本来就是这么做的。

以 `BigInteger` 为基元的 Id 按文本读入载荷，因此「载荷不是数字」也按同一方式报告——抛 `JsonException`，
而不是 `BigInteger.Parse` 原本会抛的 `FormatException`。

EF Core 的 `ValueConverter` 有意保留 `Create`：从数据库行物化实体属于数据完整性边界，那里用
`ArgumentException` 比套一层 ORM 专属异常更能说明问题。

## JSON 形态

默认情况下，Id 与其基元**形态兼容**：以 `Guid` 为基元的 `OrderId` 序列化结果与裸 `Guid` 完全相同，
因此引入强类型 Id 不会改变线上格式。当既有契约已经对外承诺了对象形态——典型情况是客户端发送
`{ "value": "3f2c…" }`——可以按 Id 选择启用：

```csharp
[StronglyTypedId(JsonShape = JsonShape.Object)]
public partial record struct OrderId(Guid Value);

// JsonShape.Primitive（默认）："3f2c…"
// JsonShape.Object：           {"value":"3f2c…"}
```

该设置对 `System.Text.Json` 与 `Newtonsoft.Json` 同时生效，且只作用于 Id 自身的值——字典键永远是基元
文本，因为 JSON 的对象键不可能是对象。`JsonShape.Object` 读写单个 `value` 属性；载荷不是对象、或对象
缺少 `value` 时会抛异常（`JsonException` / `JsonSerializationException`），其余属性一律跳过。

两侧的属性名匹配都不区分大小写，这与它们各自处理普通对象时的行为一致：`{"value":…}`、`{"Value":…}`、
`{"VALUE":…}` 读到的是同一个值。`System.Text.Json` 下这跟随 `options.PropertyNameCaseInsensitive`——
关掉它就只接受精确的小写 `value`。`Newtonsoft.Json` 下则是无条件的，因为 Newtonsoft 自己的 POCO 绑定
本就大小写不敏感，转换器若更严就会拒绝文档其余部分都能接受的载荷。由于基元形态是默认值，除非你主动选择
对象形态，既有载荷不受影响。

## 文本前缀

可以按 Id 选择启用文本前缀。前缀进入 Id 的**规范文本形**——`ToString()` 写出、`Parse` / `TryParse`
剥离——因此所有基于文本的路径都会自动带上它：

```csharp
[StronglyTypedId(Prefix = "ord_")]
public partial record struct OrderId(Guid Value);

var id = OrderId.Create(guid);

id.ToString();                              // "ord_3f2c…"
id.ToString("N");                           // "ord_3f2c…"（去短横线的 GUID，前缀保留）
OrderId.Parse("ord_3f2c…");                 // OrderId
OrderId.Parse("3f2c…");                     // OrderId（宽松读入，默认）
JsonSerializer.Serialize(id);                // "ord_3f2c…"
```

前缀不只出现在 `ToString()` 里：`ToString(format, provider)` 与 `TryFormat(...)` 同样会加上，
因此 `$"{id:N}"` 与结构化日志的输出仍然一眼能认出是 Id。

由于前缀落在规范文本形里，走文本的集成无需额外配置：MVC 模型绑定、路由约束与生成的 `TypeConverter`
都自动接受并输出带前缀的形态。而走基元**值**的集成——EF Core、Dapper、MessagePack 与 MongoDB——不受影响，
仍存裸基元，既有列、数据行与文档继续有效。唯一的例外是 MessagePack 与 BSON 都没有原生表示的三种基元
（`BigInteger`、`Int128`、`UInt128`）：它们按文本存，因此载荷里确实带着前缀。

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

## 显式注册

`[JsonConverter]` 特性覆盖了常见情形，但当你传入自己的 `JsonSerializerOptions` / `JsonSerializerSettings`
并希望把本库的转换器一并挂上时，该特性不会被采纳。为此，两个序列化器都在生成的 `StronglyTypedIds` 类上
提供了注册入口，与其他集成生成的 `ApplyTo` 家族平行——那些集成（含 MessagePack、MongoDB BSON 这两种
二进制格式，注册方式相同）见[集成](integrations.md)：

```csharp
// System.Text.Json
var options = new JsonSerializerOptions();
StronglyTypedIds.ApplyTo(options);          // 把每个 Id 的转换器加入 options.Converters

// Newtonsoft.Json
var settings = new JsonSerializerSettings();
settings.AddStronglyTypedId();              // 把每个 Id 的转换器加入 settings.Converters
```

每个入口只在引用了对应序列化器、且编译中至少声明了一个 Id 时才生成。

两者都是幂等的：已经在列表里的转换器会被跳过，因此从多个模块各注册一次、或同一处调用两次，都不会让列表
增长；你自己添加的转换器保持原位。在 `System.Text.Json` 下这也意味着 options 被使用过之后再调一次是空操作，
而不是抛 `InvalidOperationException`——`JsonSerializerOptions` 一经使用即冻结，而入口在全部已注册时根本不触碰
集合。

### Id 声明在别的程序集时

两个序列化器对「转换器放在哪儿」的取舍不同：

- `System.Text.Json` 的转换器嵌在 Id 内部，但 `System.Text.Json.dll` 在 `Microsoft.NETCore.App` 共享框架里，
  任何 net8.0+ 项目都引用得到 → **声明 Id 的程序集一定带得上转换器**，消费方无需操心。
- `Newtonsoft.Json` 是独立的 NuGet 包。若声明 Id 的程序集（典型如领域层）没有引用它，那个 Id 就没有任何
  Newtonsoft 转换器：内嵌转换器只能生成在声明 Id 的那个程序集里，消费方补一份会撞 `CS0426`（类型已存在）。

  消费方（引用了 Newtonsoft 的宿主应用）会为这类 Id 自动生成一份**顶层**转换器并注册进
  `AddStronglyTypedId`。但 Newtonsoft 的 `[JsonConverter]` 特性优先级高于
  `JsonSerializerSettings.Converters`，而特性只能加在类型上——所以**跨程序集时必须调用
  `AddStronglyTypedId`**，否则那个 Id 会被写成对象形态（`{"Value":"…"}`），而 `System.Text.Json` 写的是
  字符串：同一份数据两种表示，编译无警告、运行不抛异常。

  领域层与宿主应用都引用 Newtonsoft 时，声明程序集里的内嵌转换器会随类型一起生效（特性优先），
  `AddStronglyTypedId` 可有可无。

[<- 返回目录](index.md) · [English](../serialization.md)
