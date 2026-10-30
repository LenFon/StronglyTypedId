---
layout: default
title: 从 1.3.0 升级到 1.4.0
---

<a id="migration-1-4-0"></a>

# 从 1.3.0 升级到 1.4.0

1.4.0 是集成版本。四处变更在升级时需要动手，另有四处会改变行为但不破坏编译。其余都是新增 —— 新基元、
新集成、新选项 —— 不主动启用就不影响既有产物。

先走一遍清单，再读[行为变化](#behaviour-changes)：那些改动能编译通过，却仍会改变运行结果。

<a id="checklist"></a>

## 清单

1. [把 `StronglyTypedIds.ApplyTo(x)` 换成 `x.AddStronglyTypedIds()`](#integration-entry-points)
2. [修正所有 `STIAO###` 的抑制与 `.editorconfig` 条目](#diagnostic-ids-use-digit-zero)
3. [捕获 `Parse` 的 `FormatException`，而不是 `ArgumentException`](#parse-throws-format-exception)
4. [去掉对字符串驻留的依赖](#string-interning-removed)
5. [检查字符串基元 Id 的有序集合](#ordinal-string-comparison)
6. [复核文化敏感的格式化与解析](#invariant-culture)
7. [把警告当错误时复核 `STIA0011`](#stia0011-warning)

> 变更日志里还有两条标了 Breaking，但它们不影响从 1.3.0 升级：格式化输出保留前缀，以及种子生成算法。
> 两者都属于**1.4.0 新增**的能力 —— `Prefix` 与测试辅助包在 1.3.0 里都不存在 —— 因此没有东西需要迁移。
> 它们只相对未发布的 1.4.0 开发线算破坏性变更，对任何已发布版本都不成立。

<a id="integration-entry-points"></a>

## 集成入口改为扩展方法

过去每个集成都是生成类 `StronglyTypedIds` 上的静态方法。现在它是被配置对象上的扩展方法，并返回该
对象，因此可以链式调用。

| 1.3.0 | 1.4.0 |
| --- | --- |
| `StronglyTypedIds.ApplyTo(configurationBuilder)` | `configurationBuilder.AddStronglyTypedIds()` |
| `StronglyTypedIds.ApplyTo(modelBuilder)` | `modelBuilder.AddStronglyTypedIds()` |
| `StronglyTypedIds.ApplyTo(swaggerGenOptions)` | `swaggerGenOptions.AddStronglyTypedIds()` |
| `StronglyTypedIds.ApplyTo(openApiOptions)` | `openApiOptions.AddStronglyTypedIds()` |
| `StronglyTypedIds.ApplyTo(mvcOptions)` | `mvcOptions.AddStronglyTypedIds()` |
| `StronglyTypedIds.ApplyTo(routeOptions)` | `routeOptions.AddStronglyTypedIds()` |
| `StronglyTypedIds.ApplyTo(connection)` | `connection.AddStronglyTypedIds()` |

Dapper 还多了一个无参重载 `StronglyTypedIds.AddStronglyTypedIds()`，因为处理器注册表是进程级的，不是
按连接注册的。`IDbConnection` 重载仍会生成，以保持源码兼容，但它现在忽略传入的参数。

方法组写法（如 `services.AddSwaggerGen(StronglyTypedIds.ApplyTo)`）不再编译，改写成 lambda：

```csharp
services.AddSwaggerGen(options => options.AddStronglyTypedIds());
```

<a id="diagnostic-ids-use-digit-zero"></a>

## 诊断 ID 改用数字零

规则 ID 过去用字母 `O` 拼写（`STIAO001`），现在改用数字零（`STIA001`）。凡是 ID 以字符串形式出现、
而非作为编译器符号出现的地方都受影响：

- `#pragma warning disable STIAO011` 与 `[SuppressMessage("…", "STIAO011")]`
- `.editorconfig` 里的 `dotnet_diagnostic.STIAO011.severity = none`
- 项目文件里的 `NoWarn` 条目
- 按 ID 匹配的基线文件与 CI 过滤条件

在仓库里搜索 `STIAO` 并替换为 `STIA0`。这种失效是静默的：过期的抑制不会报错，只是不再抑制，规则会
重新出现。

<a id="parse-throws-format-exception"></a>

## `Parse` 抛 `FormatException`

`Parse` 过去对无法解析的文本抛 `ArgumentException`，现在抛 `FormatException` —— 这正是 BCL 里
`IParsable<TSelf>` 实现（`int.Parse`、`Guid.Parse`）的做法。`Create` 不变：仍抛 `ArgumentException`，
或 `ExceptionType` 配置的类型。

```csharp
// 1.3.0
catch (ArgumentException) { /* 文本非法 */ }

// 1.4.0
catch (FormatException) { /* 文本非法 */ }
```

`FormatException` 没有带参数名的构造函数，因此异常不再携带 `ParamName`。若你按它匹配，请改按消息匹配，
或改用 `TryParse`。

<a id="string-interning-removed"></a>

## 移除字符串驻留

相等的 `string` 基元 Id 过去通过按 Id 类型建立的缓存共享同一个字符串实例。该缓存已移除，因此两个值
相等的 Id 现在各自持有不同（但相等）的字符串。只有按引用比较、或指望那点分配节省时才会受影响；需要
去重请用 `string.Intern` 或你自己层的缓存。

<a id="behaviour-changes"></a>

## 行为变化

以下改动能正常编译，但可能改变运行结果。

<a id="ordinal-string-comparison"></a>

### 字符串基元 Id 按序数比较

`string` 基元 Id 的 `CompareTo` 过去用 `string.CompareTo`，那是文化敏感的。现在改用
`String.CompareOrdinal`。可见后果在有序集合里：`SortedSet<T>` 与 `SortedDictionary<TKey, TValue>` 用
`CompareTo` 判重，于是两个仅在大小写上不同、或仅在该文化视为等价的字符上不同的 Id，过去可能塌成一条
记录。现在不会了。

这类 Id 的排序本身也变了，所以按旧顺序生成的持久化有序序列或黄金文件需要重新生成。非字符串基元不受
影响。

<a id="null-ordering"></a>

### 比较运算符对 null 安全

对引用类型 Id（`record`，而非 `record struct`），比较运算符（`<`、`>`、`<=`、`>=`）现在能处理
`null` 操作数，而不是抛异常。1.3.0 里它们直接读 `left.Value.CompareTo(right.Value)`，因此比较一个
`null` Id 会抛 `NullReferenceException`。

排序本身没变：`null` 仍按最小值比较，这与 1.3.0 的 `CompareTo` 一致。现在运算符也与之一致了。若你
靠那个异常来发现 null Id，请改为显式判空。

<a id="invariant-culture"></a>

### 文本形使用不变文化

三条过去跟随当前文化的路径现在改用不变文化：

- 可格式化基元的 `ToString()`。`decimal` 基元 Id 在任何区域下都打印 `1234.5`，而 1.3.0 在 de-DE 下
  打印 `1234,5`。
- `IFormatProvider` 为 `null` 时的 `Parse` / `TryParse`。这让 `Parse(id.ToString())` 在任何区域设置
  下都能往返。
- 生成的 `TypeConverter`：`ConvertFrom` 按不变文化解析，并忽略传给它的 culture，与 `ToString()` 保持
  一致。

若你依赖过隐式取当前文化，请显式传入。注意字符串插值不受影响 —— `$"{id}"` 仍走 `IFormattable` 并
跟随当前文化。

<a id="stia0011-warning"></a>

### `STIA0011` 升级为警告，且覆盖面更广

`STIA0011`（「直接构造强类型 Id」）过去是提示级，且只报 `new Xxx(...)`。现在它是**警告**，还会报
目标类型推断的 `new(...)` 以及 `default` / `default(T)`。把警告当错误的项目会开始因绕过 `Create` 的
Id 而构建失败。要么把构造改走 `Create`，要么在 `.editorconfig` 里把级别调回去。

<a id="analyzer-muting"></a>

## 静音分析器

现在可以在不影响代码生成的前提下关掉分析器：

```xml
<PropertyGroup>
  <StronglyTypedIdEnableAnalyzers>false</StronglyTypedIdEnableAnalyzers>
</PropertyGroup>
```

这是迁移期的粗粒度手段：它静音全部 `STIA` 规则，而生成器照常产出同样的代码。若只是被一两条规则卡住，
优先用 `.editorconfig` 按规则调级别。

<a id="new-capabilities"></a>

## 升级之后可以按需采用

以下内容升级时都不必动；每一项都是可选的，不用时既有产物不变。

- **自定义基元** —— 实现 `IParsable<TSelf>`、`IComparable<TSelf>` 与非泛型 `IComparable`，并暴露公开
  静态 `Parse` / `TryParse` 的类型即可作为被包装的基元。Cysharp 的 `Ulid` 已满足契约，因此 ULID 无需
  额外代码。见[已知限制](limitations.md#primitive-types)。
- **新增九种基元** —— `char`、`Int128`、`UInt128`、`Half`、`float`、`double`、`DateOnly`、`TimeOnly`
  与 `BigInteger`，连同 `enum` 共 24 种。
- **更多集成** —— protobuf-net、HotChocolate（GraphQL）、MessagePack 与 MongoDB BSON。见
  [集成](integrations.md#protobuf-net)。
- **新的 Id 选项** —— `Normalizer`、`ExceptionType`、`RedactValidationMessage`、`Prefix` /
  `PrefixReadMode`、`JsonShape`、`Integrations` 与 `[StronglyTypedIdInstance]`，均可在程序集级设默认值。
  `Prefix` 是本次新增的，没有迁移负担：不设置时生成的代码逐字节不变。见
  [声明一个 Id](declaration.md#the-declaration)。
- **显式 JSON 注册** —— System.Text.Json 与 Newtonsoft.Json 的 `options.AddStronglyTypedIds()`，以及
  面向源生成 `JsonSerializerContext` 的 `WithStronglyTypedIds()`。见[序列化](serialization.md#serialization)。
- **测试辅助包** —— `Len.StronglyTypedId.Testing` 以及 AutoFixture 与 Bogus 适配包。这些是新包，没有
  1.3.0 的行为需要迁移；按种子生成的序列使用 SplitMix64，跨运行时复现。见[测试](testing.md#testing)。

<a id="not-changed"></a>

## 没有变化的部分

- 声明语法；无任何选项时 Id 的生成成员集合。
- 各集成的数据库存储：EF Core、Dapper、MessagePack 与 MongoDB 仍存裸基元值。
- 包的分工，以及 `Len.StronglyTypedId` 与其生成器包之间的版本钉定。

[<- 返回目录](index.md) · [English](../migration.md)
