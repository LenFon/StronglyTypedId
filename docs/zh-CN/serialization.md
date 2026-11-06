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

### 像日期的值会在转换器运行前被解析

<a id="date-like-values-are-parsed-before-the-converter-runs"></a>

Newtonsoft 在调用转换器 *之前* 就把「像日期」的字符串解析成了 `DateTime`，因此生成的转换器拿到的是
本地时区的 `DateTime`，而不是原始文本。两类 Id 会受影响。

`DateTimeOffset` 基元的 Id 读回来时带的是机器本地 offset，不是写出去的那个：

```csharp
var id = TimestampId.Create(new DateTimeOffset(2020, 1, 2, 3, 4, 5, TimeSpan.FromHours(-5)));

var json = JsonConvert.SerializeObject(id);          // "2020-01-02T03:04:05-05:00"
var back = JsonConvert.DeserializeObject<TimestampId>(json);

back.Value.Offset;                                   // UTC+8 的机器上是 +08:00，不是 -05:00
```

时刻本身没丢 —— `back.Value.UtcTicks` 与原值相同 —— 但 offset 变了，且变成什么取决于机器。

`string` 基元的 Id 更糟：若它的值恰好长得像 ISO 日期时间，值本身会被替换成该时刻的本地渲染：

```csharp
var id = TokenId.Create("2020-01-02T03:04:05Z");

var json = JsonConvert.SerializeObject(id);          // "2020-01-02T03:04:05Z"
var back = JsonConvert.DeserializeObject<TokenId>(json);

back.Value;                                          // "01/02/2020 03:04:05" —— 原字符串没了
```

两者都是「经过转换器」才有的问题：普通的 `DateTimeOffset` 或 `string` 属性是能正确往返的。转换器内部
无解 —— 它运行时原文已经没了，改写成规范文本也没用，token 照样会被当成日期解析。手写的
`JsonConverter<string>` 也会遇到同样的事，所以这是 Newtonsoft 的限制，不是本库引入的。

把序列化器设成 `DateParseHandling = DateParseHandling.None` 可保留原始字符串，两种情况都能往返：

```csharp
var settings = new JsonSerializerSettings { DateParseHandling = DateParseHandling.None };

var json = JsonConvert.SerializeObject(id, settings);
var back = JsonConvert.DeserializeObject<TimestampId>(json, settings);

back.Value.Offset;                                   // -05:00
```

该设置对整个序列化器生效，但对其他基元安全：`DateTime` 的 `Kind` 保持不变，`DateOnly` / `TimeOnly` /
`TimeSpan` 写出的字符串本来就不会被 Newtonsoft 当成日期。System.Text.Json 不受影响，始终保留原值。

## 序列化器不认识的基元

<a id="primitives-the-serializers-do-not-know"></a>

有七个基元在其中一方库里没有原生 JSON 表示，它们的转换器改为写出不变文化文本，也就是 `ToString()`
产出的那段文本：

| 基元                                          | 库               | 按值写会怎样                                                   |
| --------------------------------------------- | ---------------- | -------------------------------------------------------------- |
| `BigInteger`                                  | System.Text.Json | 退化成 `{"IsPowerOfTwo":false,…,"Sign":1}`，读回来是 `default`  |
| `Int128`、`UInt128`、`Half`、`DateOnly`、`TimeOnly` | Newtonsoft.Json | 抛 `JsonWriterException: Unsupported type`                     |
| `decimal`                                     | Newtonsoft.Json | 超过约 15 位有效数字时静默舍入，`decimal.MaxValue` / `MinValue` 直接抛异常 |

```csharp
JsonSerializer.Serialize(BigIntegerId.Create(42));       // "42"
JsonConvert.SerializeObject(HalfId.Create((Half)1.5));   // "1.5"
JsonConvert.SerializeObject(MoneyId.Create(1.5m));       // "1.5"
```

读写两侧走同一段规范文本，往返无损；唯一的差别是这些 Id 在 JSON 里呈现为字符串而不是数字。其余基元
保持自然形态（数字仍是数字、`Guid` 仍是字符串、枚举仍是数字）。

`decimal` 上榜的原因与其余几个不同：Newtonsoft 能写它，但在调用转换器之前就把数字 token 解析成了
`double`，转换器拿不到原始文本。`123456789012345.67` 读回来是 `123456789012346`，`decimal.MaxValue`
直接抛 `JsonSerializationException`。读侧任何写法都救不回已丢掉的位数，故转换器改写成规范文本。代价与
`Int128` 已经承担的一样：Newtonsoft 写出 JSON 字符串，而 System.Text.Json 写数字，一方写出的载荷另一方
读不了。同一个契约只用一种库，或把 Id 的基元声明成 `string`，才能共享线上格式。

读侧刻意放宽：JSON number 仍然接受，因此旧版本（写数字）产出的载荷不会失效。这些数字仍走原来的
`double` 解析，精度上限与修复前一致 —— 只有文本形是无损的。

`DateTime`、`DateTimeOffset`、`TimeOnly` 与 `DateOnly` 的规范文本用 `"O"` 格式：前三个的默认格式有损
（丢掉亚秒，`TimeOnly` 连秒都丢），拿它做字典键会把两个不同的 Id 塌缩成同一个条目；`DateOnly` 本身
无损，但默认短日期（`01/02/2020`）与 System.Text.Json 写出的 ISO 形不一致，两个库会对同一个 Id
给出不同文本。

## 字典键

默认情况下 `JsonConverter<T>` 看不到字典键（`System.Text.Json` 只认得内建的键类型，其余一律抛
`NotSupportedException`），因此生成的转换器另外重写了 `ReadAsPropertyName` / `WriteAsPropertyName`：

```csharp
var cart = new Dictionary<OrderId, int> { [id] = 3 };

var json = JsonSerializer.Serialize(cart);                        // {"3f2c…":3}
var back = JsonSerializer.Deserialize<Dictionary<OrderId, int>>(json);
```

键以基元值的**不变文化**文本写出，因此同一份数据在任何区域设置下都会得到相同的键。以 `string` 为
基元的 Id 直接使用该字符串，故空串也能往返；无法解析的键会抛 `JsonException`。

以 `enum` 为基元的 Id 沿用 `System.Text.Json` 对枚举键的既定约定：键是成员名（`"Active"`），值是底层
数字（`1`），除非你的 `JsonSerializerOptions` 另有配置。这种不对称是 JSON 本身的限制（对象的键只能是
字符串），也与原生 `Dictionary<MyEnum, MyEnum>` 的输出完全一致，因此 Id 与裸枚举可以在同一份文档里互换。
枚举键也是 `options.DictionaryKeyPolicy` 唯一生效的情形：键是「名字」而非数据，按 camelCase 改写它与
内建枚举键的行为一致，读路径则忽略大小写解析以接受被改写过的键。其余基元的键文本原样保留，因为那段文本
**就是**值本身，改写它会让文档读不回来；带前缀的 Id 同样排除在外，因为策略会把前缀一起改写。把 `null`
的 Id 当属性名写出会抛 `NotSupportedException`；被包装的基元为 `null`（`string` 基元或自定义 `class`
基元）时同样如此，因为 JSON 属性名本身不允许 `null`。带前缀的 Id 也不例外：它的键文本会是裸前缀，
读回时剥掉前缀只剩空文本，任何读路径都不接受。

Newtonsoft.Json 的字典键走的是 `TypeConverter` / `ToString()`，并不经过 `JsonConverter`，因此生成的
转换器接管不到它。请在 Id 上开启 `TypeConverter = true`，Newtonsoft 才能在读路径还原键。键文本与转换器
写出的一致，都是**不变文化**文本：生成的 `ToString()` 与生成的 `TypeConverter` 两个方向都按不变文化读写，
因此在任何区域设置下键都保持稳定。

两种转换器都把 Id 序列化为其底层基元值而不是对象，因此 JSON 结构与引入强类型 Id 之前保持一致。
引用类型 Id 为 `null` 时写出的就是 JSON `null`，读回也得到 `null`，但这只对 `record` 成立。`record struct`
Id 无法表示 `null`，因此两种转换器都会拒绝该 token，而不是悄悄产出 `default`（`JsonException` /
`JsonSerializationException`）。

以 `enum` 为基元的 Id 在读路径上与它的文本形执行同一道校验：不对应任何成员的数字会被同样的框架异常
拒绝，因此 `{"status": 999}` 不可能造出一个值为 `(Status)999` 的 Id。这正是 `Id.TryParse("999")` 早已
执行的校验，Dapper 的三条路径也一样；补上它之后，唯一还放行野值的入口也关上了。`[Flags]` 枚举则改按
未定义位掩码校验：已定义成员的任意位组合照常通过，而 `999` 不通过。

## 验证失败的异常契约

`Validator` 拒绝的是「能解析但违反领域规则」的值。两种转换器在读入时都会执行它，并用各自的异常类型
报告拒绝，而不是让 `Create` 的 `ArgumentException` 穿透出来：

| 序列化器 | 值被拒绝时抛出的异常 |
| --- | --- |
| `System.Text.Json` | `JsonException` |
| `Newtonsoft.Json` | `JsonSerializationException` |

这正是两个库为「转换失败」约定的异常：用 `catch (JsonException)` 包住反序列化的调用方，会把「载荷格式
不对」和「领域规则不满足」当作同一类失败处理，也不会收到一个它无从预期的 `ArgumentException`。

以 `BigInteger` 为基元的 Id 按文本读入载荷，因此「载荷不是数字」也按同一方式报告：抛 `JsonException`，
而不是 `BigInteger.Parse` 原本会抛的 `FormatException`。

EF Core 是另一种意义上的例外：它的 `ValueConverter` 同样经 `TryCreate` 构造，但把被拒绝的值报成
`InvalidOperationException`，而不是某个序列化库专属的异常类型。从数据库行物化实体属于数据完整性边界，
那里用这个异常比套一层 ORM 专属包装更能说明问题。

## JSON 形态

默认情况下，Id 与其基元**形态兼容**：以 `Guid` 为基元的 `OrderId` 序列化结果与裸 `Guid` 完全相同，
因此引入强类型 Id 不会改变线上格式。当既有契约已经对外承诺了对象形态（典型情况是客户端发送
`{ "value": "3f2c…" }`）时，可以按 Id 选择启用：

```csharp
[StronglyTypedId(JsonShape = JsonShape.Object)]
public partial record struct OrderId(Guid Value);

// JsonShape.Primitive（默认）："3f2c…"
// JsonShape.Object：           {"value":"3f2c…"}
```

该设置对 `System.Text.Json` 与 `Newtonsoft.Json` 同时生效，且只作用于 Id 自身的值：字典键永远是基元
文本，因为 JSON 的对象键不可能是对象。`JsonShape.Object` 读写单个 `value` 属性；载荷不是对象、或对象
缺少 `value` 时会抛异常（`JsonException` / `JsonSerializationException`），其余属性一律跳过。
被包装基元为 `null` 的 Id（`string` 基元或自定义 `class` 基元）写出 `{"value":null}`，读回得到同一个
default Id，因此对象形态同样可往返。若整个值位置是裸 JSON `null` 而不是对象，`record struct` 的 Id 会像
基元形态那样抛框架自己的序列化异常，而不是把 `null` 交回去（拆箱时会失败）。

`System.Text.Json` 下属性名跟随 `options.PropertyNameCaseInsensitive`，它默认是 `false`：
只接受精确的小写 `value`，除非你打开它 —— 打开后 `{"value":…}`、`{"Value":…}`、`{"VALUE":…}`
读到的是同一个值。`Newtonsoft.Json` 下则是无条件的，因为 Newtonsoft 自己的 POCO 绑定本就大小写
不敏感，转换器若更严就会拒绝文档其余部分都能接受的载荷。由于基元形态是默认值，除非你主动选择
对象形态，既有载荷不受影响。

## 文本前缀

可以按 Id 选择启用文本前缀。前缀进入 Id 的**规范文本形**（`ToString()` 写出、`Parse` / `TryParse`
剥离），因此所有基于文本的路径都会自动带上它：

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
都自动接受并输出带前缀的形态。而走基元**值**的集成（EF Core、Dapper、MessagePack 与 MongoDB）不受影响，
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

空前缀按「未设置」处理，且不报任何诊断，生成代码与不写 `Prefix` 逐字符相同。纯空白前缀同样按「未设置」
处理，但会报 `STIA0013`，因为它几乎必然是笔误。

## 显式注册

`[JsonConverter]` 特性覆盖了常见情形，但当你传入自己的 `JsonSerializerOptions` / `JsonSerializerSettings`
并希望把本库的转换器一并挂上时，该特性不会被采纳。为此，两个序列化器都提供了注册入口，与其他集成生成的
`AddStronglyTypedIds` 家族平行。那些集成（含 MessagePack、MongoDB BSON 这两种
二进制格式，注册方式相同）见[集成](integrations.md)：

```csharp
// System.Text.Json
var options = new JsonSerializerOptions();
options.AddStronglyTypedIds();               // 把每个 Id 的转换器加入 options.Converters

// Newtonsoft.Json
var settings = new JsonSerializerSettings();
settings.AddStronglyTypedIds();              // 把每个 Id 的转换器加入 settings.Converters
```

每个入口只在引用了对应序列化器、且编译中至少声明了一个 Id 时才生成。

两者都是幂等的：已经在列表里的转换器会被跳过，因此从多个模块各注册一次、或同一处调用两次，都不会让列表
增长；你自己添加的转换器保持原位。在 `System.Text.Json` 下这也意味着 options 被使用过之后再调一次是空操作，
而不是抛 `InvalidOperationException`：`JsonSerializerOptions` 一经使用即冻结，而入口在全部已注册时根本不触碰
集合。

### 源生成的 `JsonSerializerContext`

`AddStronglyTypedIds` 把转换器注册进 `options.Converters`，但源生成的 context 对被要求产出元数据的类型并不查这个
列表。在 `JsonSourceGenerationMode.Metadata`（AOT 的推荐模式）下，context 按属性写出 Id，于是同一个 Id
经 context 是 `{"Value":"…"}`、经反射式序列化是 `"…"`。

用 `WithStronglyTypedIds()` 包一层，两者就一致了：

```csharp
var options = new JsonSerializerOptions
{
    TypeInfoResolver = AppJsonContext.Default.WithStronglyTypedIds(),
};
```

该方法把一个只认 Id 的 `IJsonTypeInfoResolver` 放到你的 context 之前。顺序是关键：
`JsonTypeInfoResolver.Combine` 取第一个返回非 null `JsonTypeInfo` 的 resolver，而 context 对 Id 也会返回
一份。Id 以外的类型原样落到 context。每个 `JsonTypeInfo` 都由生成的转换器构造，零反射，在 Native AOT
下同样可用。

`AddStronglyTypedIds` 与 `WithStronglyTypedIds()` 互不依赖：配置 `options.Converters` 时用 `AddStronglyTypedIds`，把
`TypeInfoResolver` 设成 context 时用 `WithStronglyTypedIds()`。两个都设没有副作用。

### Id 声明在别的程序集时

两个序列化器对「转换器放在哪儿」的取舍不同：

- `System.Text.Json` 的转换器嵌在 Id 内部，但 `System.Text.Json.dll` 在 `Microsoft.NETCore.App` 共享框架里，
  任何 net8.0+ 项目都引用得到，因此**声明 Id 的程序集一定带得上转换器**，消费方无需操心。
- `Newtonsoft.Json` 是独立的 NuGet 包。若声明 Id 的程序集（典型如领域层）没有引用它，那个 Id 就没有任何
  Newtonsoft 转换器：内嵌转换器只能生成在声明 Id 的那个程序集里，消费方补一份会撞 `CS0426`（类型已存在）。

  消费方（引用了 Newtonsoft 的宿主应用）会为这类 Id 自动生成一份**顶层**转换器并注册进
  `AddStronglyTypedIds`。但 Newtonsoft 的 `[JsonConverter]` 特性优先级高于
  `JsonSerializerSettings.Converters`，而特性只能加在类型上，所以**跨程序集时必须调用
  `AddStronglyTypedIds`**，否则那个 Id 会被写成对象形态（`{"Value":"…"}`），而 `System.Text.Json` 写的是
  字符串：同一份数据两种表示，编译无警告、运行不抛异常。

  领域层与宿主应用都引用 Newtonsoft 时，声明程序集里的内嵌转换器会随类型一起生效（特性优先），
  `AddStronglyTypedIds` 可有可无。

[<- 返回目录](index.md) · [English](../serialization.md)
