---
layout: default
title: 功能特性
---

# 功能特性

生成器产出的完整清单，按你要解决的问题分组。[README](https://github.com/LenFon/StronglyTypedId/blob/main/README.zh-CN.md#features)
只保留亮点，这一页是完整列表。

想知道「某个功能在哪儿能看到」，看[功能索引](features-index.md)——那里每个功能点都对应一个可运行示例。

<a id="declaring-an-id"></a>

## 声明一个 Id

- **一行声明** —— 用 `[StronglyTypedId]` 标注一个 `partial record`（或 `partial record struct`），
  生成器产出兄弟 `partial`，永不改写你的声明。
- **把已有 record 转成 Id** —— 把光标放在 record 的声明头上，重构菜单会给出
  **Convert to strongly typed id**，替你补上特性、`partial` 与 `using`；反向的
  **Inline strongly typed id** 则撤销这一切。正向动作只在结果确实是一个合法 Id 时出现 —— 见
  [把已有 record 转成 Id](declaration.md#converting-an-existing-record)。
- **编译期安全** —— 内建分析器把不合法的声明作为错误报出，26 条规则里有 21 条附带代码修复 —— 见
  [诊断](diagnostics.md#diagnostics)。
- **23 种内置基元** —— 全部数值类型（含 `Int128`、`UInt128`、`Half`），以及 `Guid`、`string`、`char`、
  `decimal`、`DateTime`、`DateTimeOffset`、`TimeSpan`、`DateOnly`、`TimeOnly` 与 `BigInteger` —— 见
  [支持的基元类型](https://github.com/LenFon/StronglyTypedId/blob/main/README.zh-CN.md#supported-primitive-types)。
- **任意 `enum`** —— 枚举 Id 按成员名解析，并跳过格式化接口，因为 `System.Enum` 继承来的成员已标记
  `[Obsolete]`。
- **自定义基元** —— 你自己的类型只要实现 `IParsable<TSelf>`、`IComparable<TSelf>` 与非泛型
  `IComparable`，并暴露公开静态的 `Parse` / `TryParse` 即可。ULID 是现成例子 —— 见
  [自定义基元](https://github.com/LenFon/StronglyTypedId/blob/main/README.zh-CN.md#custom-primitives)。
- **命名实例** —— `[StronglyTypedIdInstance("Unspecified", "-1")]` 生成 `public static readonly` 字段，
  一律经 `Create` 构造，因此同样经过归一化与验证器，`Parse` 一定读得回来 —— 见
  [命名实例](declaration.md#named-instances)。
- **文本前缀** —— `[StronglyTypedId(Prefix = "ord_")]` 为 Id 指定规范文本形（`"ord_3f2c…"`），
  该前缀贯穿 `ToString`、`Parse`、JSON 及其余全部文本路径 —— 见
  [文本前缀](serialization.md#serialization)。
- **嵌套与泛型容器** —— Id 可以声明在 class、struct、record 或 interface 内部；泛型容器能编译，
  但会失去外部引用类集成 —— 见[嵌套类型](declaration.md#nested-types)。

<a id="what-gets-generated"></a>

## 生成产物

- **接口** —— `IStronglyTypedId<TSelf, TPrimitiveId>`、`IParsable<TSelf>`、`ISpanParsable<TSelf>`、
  `IComparable<TSelf>`、`IEqualityOperators<TSelf, TSelf, bool>` 与
  `IComparisonOperators<TSelf, TSelf, bool>` —— 见[运行时](runtime.md#using-the-interface)。
- **解析** —— `Parse` / `TryParse` 的 `string` 与 `ReadOnlySpan<char>` 两套重载。`provider` 传 `null`
  表示不变文化，因此 `Parse(id.ToString())` 总能往返 —— 见[生成产物](generated-code.md)。
- **格式化** —— 基元类型自身实现 `IFormattable` / `ISpanFormattable` 时一并生成。`string` 两者都没有，
  枚举 Id 则都跳过。
- **UTF-8 接口** —— 对支持相应接口的基元（如 .NET 10 的 `Guid`）生成 `IUtf8SpanFormattable` 与
  `IUtf8SpanParsable<TSelf>`，可直接以 `ReadOnlySpan<byte>` 解析 / 格式化。
- **比较与排序** —— `CompareTo` 与 `<`、`>`、`<=`、`>=`，因此 `OrderBy`、`SortedSet<T>` 与区间判断
  都无需自定义比较器。引用类型 Id 把 `null` 视为最小。
- **`Create` 与 `TryCreate`** —— 抛异常的工厂及其非抛异常版本，后者失败时返回 `false` 并把 `result`
  置为 `default`。
- **解构** —— `id.Deconstruct(out var value)` 取出被包装的基元值。单元素解构声明
  `var (value) = id;` 不是合法 C#。
- **`ToString()`** —— 输出基元的不变文化文本（或带前缀的规范文本），而不是 record 默认的
  `OrderId { Value = … }` 形态。
- **`WithStronglyTypedIds()`** —— 使用源生成的 `JsonSerializerContext` 时，用它包一层 context 的
  resolver，Id 才会保持字符串形态，而不是被写成对象。生成的 resolver 优先查 Id 的转换器，全程零反射
  —— 见[原生 AOT 与裁剪](aot.md)。
- **`TypeConverter`** —— 用 `[StronglyTypedId(TypeConverter = true)]` 开启，经 `TypeDescriptor` 往返，
  覆盖配置绑定与 `XmlSerializer` —— 见[类型转换器](declaration.md#type-converter)。不开时
  `IConfiguration` 绑定会把属性留在 `default`，而不是报错。
- **调试器视图** —— `[DebuggerDisplay]` 让折叠状态直接显示取值；`[DebuggerTypeProxy]` 让展开时同时看到
  原始 `Value` 与规范文本 `Text`（含前缀）。
- **反射助手** —— `IsStronglyTypedId`、`GetPrimitiveIdType` 与 `TryGetPrimitiveIdType` 可在运行时检查
  任意类型，包括其它程序集生成的 Id —— 见[运行时](runtime.md#reflection-helpers)。

<a id="validation-and-normalization"></a>

## 校验与归一化

- **值校验** —— 把 `Validator` 指向 Id 自身的静态方法，即可让 `Create`、`TryCreate`、`Parse` 与
  `TryParse` 拒绝非法取值 —— 见[值校验](declaration.md#value-validation)。
- **值归一化** —— 把 `Normalizer` 指向 Id 自身的静态方法，即可在校验与构造之前修正取值（去空白、
  统一大小写、去分隔符）—— 见[值归一化](declaration.md#value-normalization)。
- **自定义失败异常** —— `ExceptionType` 可以把验证器拒绝取值时 `Create` 抛出的 `ArgumentException`
  换成你自己的异常类型。
- **消息脱敏** —— `RedactValidationMessage` 让异常消息不包含被拒绝的取值，适用于包装敏感数据的 Id。

<a id="configuration"></a>

## 配置

- **程序集级默认** —— `[assembly: StronglyTypedIdDefaults(...)]` 可为整个程序集统一设置 `Validator`、
  `Normalizer`、`ExceptionType`、`RedactValidationMessage`、`Integrations`、`JsonShape`、`TypeConverter`
  与 `PrefixReadMode` —— 见[程序集级默认设置](declaration.md#assembly-defaults)。
- **逐 Id 集成过滤** —— `[StronglyTypedId(Integrations = ...)]` 可收窄某个 Id 生成哪些集成代码
  （`None`、白名单或 `All`），并支持程序集级默认 —— 见[关闭集成](declaration.md#integrations-filter)。
- **一键静音** —— 在工程文件里设
  `<StronglyTypedIdEnableAnalyzers>false</StronglyTypedIdEnableAnalyzers>`，分析器就一个诊断动作都不注册。
  代码生成不受影响 —— 见[NuGet 包](packages.md#transitive-analyzer)。

<a id="integrations"></a>

## 集成

- **序列化** —— 自动生成 `System.Text.Json` 与 `Newtonsoft.Json`（≥ 13.0.0）转换器，含字典键支持；
  两者另提供显式注册入口，并可按 Id 选择 JSON 形态 —— 见[序列化](serialization.md#serialization)。
- **另外九个集成** —— Entity Framework Core、Dapper、Swashbuckle、ASP.NET Core MVC（其注册同时支撑
  Minimal API 的路由约束）、ASP.NET Core OpenAPI、MessagePack、MongoDB、HotChocolate（GraphQL）与
  protobuf-net。每个集成只在对应包被引用时才生成。除 protobuf-net 外各自提供一个注册入口：除 MongoDB
  用 `RegisterBsonSerializers()` 外，其余都是 `AddStronglyTypedIds(...)`（protobuf-net 不需要——surrogate
  由特性发现）—— 见[集成](integrations.md)。
- **二进制载荷只放基元值** —— MessagePack、BSON 与 protobuf-net 写的是基元值本身；文本前缀只属于
  文本形，不会进入载荷。

<a id="testing-and-deployment"></a>

## 测试与部署

- **测试辅助** —— `Any.Id<T>()` 与 `Any.Ids<T>(count)` 为 arrange 阶段造出可信的 Id，支持可复现的种子
  与尊重验证器的重试。`Len.StronglyTypedId.Testing.AutoFixture` 与 `.Bogus` 把同一套生成器接进这两个库
  —— 见[测试](testing.md#testing)。
- **原生 AOT 与裁剪** —— 运行时库标记为 AOT 兼容，且不按名字加载任何类型，因此强类型 Id 在 Native AOT
  与经过裁剪的应用中同样可用。AOT 下 `System.Text.Json` 仍需源生成的 `JsonSerializerContext`，那是它
  自己的要求，不是本库的特例 —— 见[原生 AOT 与裁剪](aot.md)。
- **零第三方依赖** —— 包内仅包含生成器/分析器程序集，以及一个很小的运行时库，不会往你的依赖图里塞
  任何东西。

[<- 返回目录](index.md) · [English](../features.md)
