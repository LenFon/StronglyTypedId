---
layout: default
title: 生成产物
---

# 生成产物

生成器在 Id 所在的命名空间里补出一份 `partial` 声明（嵌套的 Id 会声明回其包含类型内部，见
[嵌套类型](declaration.md#nested-types)），你自己的声明永远不会被改写。**产出哪些文件**取决于编译期引用了
什么：

<!-- BEGIN GENERATED:generated-files -->
| 编译中的条件                                                                                             | 生成的文件                                                               |
| -------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------ |
| 始终                                                                                                     | `<命名空间>.<类型名>.g.cs`                                               |
| 引用了 `System.Text.Json`                                                                                | `…SystemTextJson.g.cs`, `StronglyTypedIds.SystemTextJson.g.cs`           |
| 引用了 `Newtonsoft.Json` ≥ 13.0.0                                                                        | `…NewtonsoftJson.g.cs`, `StronglyTypedIds.NewtonsoftJson.g.cs`           |
| 引用了 `Microsoft.EntityFrameworkCore` ≥ 7.0.0                                                           | `…EntityFrameworkCore.g.cs`, `StronglyTypedIds.EntityFrameworkCore.g.cs` |
| 引用了 `Swashbuckle.AspNetCore.SwaggerGen` ≥ 6.0.0                                                       | `StronglyTypedIds.Swagger.g.cs`                                          |
| 引用了 `Dapper` ≥ 2.0.0                                                                                  | `…Dapper.g.cs`, `StronglyTypedIds.Dapper.g.cs`                           |
| 引用了 `MessagePack` ≥ 2.0.0                                                                             | `…MessagePack.g.cs`, `StronglyTypedIds.MessagePack.g.cs`                 |
| 引用了 `MongoDB.Bson` ≥ 3.0.0                                                                            | `…MongoDb.g.cs`, `StronglyTypedIds.MongoDb.g.cs`                         |
| 引用了 `HotChocolate` ≥ 13.0.0                                                                           | `…HotChocolate.g.cs`, `StronglyTypedIds.HotChocolate.g.cs`               |
| 引用了 `protobuf-net` ≥ 3.0.0                                                                            | `…Protobuf.g.cs`                                                         |
| 引用了 `Microsoft.AspNetCore.OpenApi` ≥ 9.0.0                                                            | `StronglyTypedIds.AspNetCoreOpenApi.g.cs`                                |
| 引用了 `Microsoft.AspNetCore.Mvc` ≥ 2.0.0（或其 `Microsoft.AspNetCore.Mvc.Core` / `.Abstractions` 门面） | `StronglyTypedIds.AspNetCoreMvc.g.cs`                                    |
<!-- END GENERATED:generated-files -->

本页余下部分讲每个生成成员的具体语义。[README](https://github.com/LenFon/StronglyTypedId/blob/main/README.zh-CN.md#what-gets-generated)
里是同一份清单，但不含文件名，用作一眼扫完的导览。

## 核心实现

- `IStronglyTypedId<TSelf, TPrimitiveId>`、`IParsable<TSelf>`、`ISpanParsable<TSelf>`、
  `IComparable<TSelf>`、`IEqualityOperators<TSelf, TSelf, bool>` 与
  `IComparisonOperators<TSelf, TSelf, bool>` 的实现。
- `IFormattableStronglyTypedId<TSelf>`（继承 `IFormattable`）与 `ISpanFormattable`，按基元类型条件生成：
  `string` 两者都没实现，枚举基元则两者都跳过，因为 `System.Enum` 继承来的这两个接口成员带 `[Obsolete]`。
  该标记接口让泛型代码在不知道基元类型的前提下统一格式化：
  `static string Render<TId>(TId id) where TId : IFormattableStronglyTypedId<TId>`。
  `IStronglyTypedId<TSelf>` 刻意不约束 `IFormattable`，因此仍能接受所有 Id。
- `IUtf8SpanFormattable`（net8.0+ 即可用）与 `IUtf8SpanParsable<TSelf>`，同样按基元类型条件生成：
  `Guid` 自 .NET 10 起才实现后者；`BigInteger` 虽然实现了它却被排除，因为它的 `TryParse` 是显式接口
  实现，按类型名调用会 `CS1501`。两者共同提供 `ReadOnlySpan<byte>` 的 `Parse` / `TryParse` 与 `TryFormat`。
- `[DebuggerDisplay("{Value}")]` 特性，调试器里直接显示被包装的值，而不是 record 展开后的形态。
- `[DebuggerTypeProxy]` 特性，指向一个私有嵌套的 `{TypeName}DebugView`，改变调试器**展开**该 Id 时显示的
  内容：`Value`（裸基元值）与 `Text`（规范文本形，含 `Prefix`）。`DebuggerDisplay` 只覆盖折叠时那一行摘要，
  因此带前缀的 Id 在调试器里原本只看得到裸基元值。代理类是私有的，不构成 API 面；声明在泛型容器里的 Id
  两者都不生成，因为特性实参不能引用携带容器类型参数的类型（CS0416）。
- 静态的 `Create(TPrimitiveId value)` 工厂。配置了 `Validator` 时，它拒绝不合法的值：抛
  `ArgumentException`（`The value '…' is not a valid OrderId.`）或你指定的 `ExceptionType`，
  因此经它无法构造出非法 Id。
- 静态的 `TryCreate(TPrimitiveId value, out TSelf result)` 工厂，即 `Create` 的不抛异常版本：
  失败时返回 `false` 并把 `result` 置为 `default`。
- 每个 `[StronglyTypedIdInstance]` 对应一个 `public static readonly TSelf` 字段，经 `Create` 初始化。
- `Deconstruct(out TPrimitiveId value)`，因此 `id.Deconstruct(out var value);` 可解构出基元值。
  （单元素的 `var (value) = id;` 不是合法 C#，解构声明至少要两个元素。）
- `string` 与 `ReadOnlySpan<char>` 两套 `Parse` / `TryParse`，委托给基元类型。`provider` 传 `null` 时按
  **不变文化**解析（而非当前文化），因此 `Parse(id.ToString())` 在任何区域设置下都能往返；需要按特定文化
  解析时显式传 provider。对于以 `string` 为基元的 Id，`TryParse` 不接受 `null` 与空字符串。`Parse` 对
  格式错误的文本抛 `FormatException`（`Could not parse supplied value.`），遵循 `IParsable<TSelf>`
  的惯例而非 `ArgumentException`。
- `CompareTo`，以及基于被包装值的 `<`、`>`、`<=`、`>=` 运算符。`record` 引用类型 Id 的形参与两个操作数
  都可为 `null`，且 `null` 最小：`CompareTo(null)` 返回 `1`，`null < id` 为 `true`。运算符委托给私有的
  `CompareValues` 辅助方法，因此 `null` 操作数不会去解引用 `Value`。
  以 `string` 为基元的 Id 按**序数**比较（`string.CompareOrdinal`），而不是 `string.CompareTo(string)`：
  后者区域性敏感，与 record 合成的 `Equals` 不一致（`"a\u00ADb"` 与 `"ab"` 比较结果为相等却并不相等），
  `"I"` 与 `"i"` 的顺序还会随区域翻转。`SortedSet<T>` 与 `SortedDictionary<TKey, TValue>` 正是靠
  `CompareTo` 判重的，区域性比较会在那里静默丢掉元素。序数比较让排序与相等性、以及与序列化写出的
  不变文化文本保持一致。
- 返回被包装值**不变文化**文本的 `ToString()`（不再是 record 默认的 `OrderId { Value = … }` 形式），
  与序列化写出的文本一致，`Parse(id.ToString())` 可往返；需要按特定文化
  格式化时，用基元类型支持时的 `ToString(string?, IFormatProvider?)` 与 `TryFormat(...)`。
  非空 `format` 只交给基元处理，文本形态不变：`Guid` 基元的 `ToString("N")` 得到去掉短横线的 GUID；
  带前缀的 Id 仍带前缀（`ord_d8ac85d4…`），格式化路径不会退化成裸基元。
  注意字符串插值**不等同于** `ToString()`：`$"{id}"` 走 `IFormattable.ToString(null, CultureInfo.CurrentCulture)`，
  会跟随当前区域（de-DE 下 `decimal` 基元写成 `1234,5`），而 `id.ToString()` 不会。
- 逐类型开启 `TypeConverter` 时，嵌套的 `XxxTypeConverter`（`[TypeConverter]` 特性）经 `TypeDescriptor`
  在字符串与 Id 间往返：`ConvertFrom` 按不变文化走 `Parse`，`ConvertTo` 走 `ToString()`，把 `null` 的 Id
  转成 `string` 得到 `null`。传入的不是字符串（含 `null`）时抛 `NotSupportedException`。

<a id="enum-backed-ids"></a>

## 枚举型 Id

枚举型 Id 保留 `IComparable<TSelf>`、`IParsable<TSelf>` 与 `ISpanParsable<TSelf>`（解析走
`Enum.TryParse<TEnum>`），但刻意省略 `IFormattable`/`ISpanFormattable`，因为 `Enum` 的
`ToString(string, IFormatProvider)` 已被标记过时，其 `ToString()` 直接返回底层值的文本。

`Enum.TryParse` 会接受任意数字文本，因此生成的 `TryParse` 把它收紧了：普通枚举必须命中已定义的成员
（`Enum.IsDefined`），`[Flags]` 枚举允许成员组合，但不许出现没有任何成员占用的位，所以 `Read | Write` 能解析，
`999` 不能。掩码在编译期由各成员常量算出并按枚举底层类型宽度裁剪，运行期只剩一次按位与；若某个枚举
定义了全部位（典型是 `All = -1`），则完全不生成校验。

除这道校验外，语义就是 `Enum.TryParse` 本身的：前后空白与数字文本只要结果能通过上面的校验就接受，
`[Flags]` 枚举还接受逗号分隔的组合，成员名**区分大小写**。因此 `1` 是已定义成员时 `" 1 "` 与 `"01"`
都能解析，flags 枚举下 `"Read, Write"` 也能解析；规范文本请用 `ToString()`（它写成员名）。

<a id="the-integration-entry-point"></a>

## 集成入口

各集成入口统一生成到同一个类里，即 `Len.StronglyTypedId` 命名空间下的
`internal static partial class StronglyTypedIds`，因此无论项目声明了多少个 Id，各个 `AddStronglyTypedIds`
重载都并存于该类中。调用它们需要 `using Len.StronglyTypedId;`（声明特性时通常已经有了）。

[<- 返回目录](index.md) · [English](../generated-code.md)
