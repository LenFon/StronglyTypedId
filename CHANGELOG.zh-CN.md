# 变更日志

本项目的重要变更都记录在此文件中。

格式遵循 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，版本号遵循
[语义化版本](https://semver.org/lang/zh-CN/)。

`1.3.0` 及更早的版本依据仓库 tag 重建：每条记录概述该版本交付的变更集合，而不是逐一复述每个提交。

> 英文版：[CHANGELOG.md](CHANGELOG.md)

## [未发布]

## [1.4.2] - 2026-11-12

### 新增

- **`STIA0022` 代码修复：** 命名实例不是 PascalCase 时一键改名。只改字符串字面量，引用旧名的代码由编译器
  报 `CS0117`；`const` 引用不提供修复。
- **`Any` 非泛型重载：** `Any.Create(Type)`、`Any.Ids(Type, count)` 与 `Any.Register(Type, factory)`
  对应泛型成员，用于 Id 类型只在运行期才知道的场合。
- **富验证结果：** `Validator` 可返回 `StronglyTypedIdValidationResult`（消息 + 可选错误码）而非
  `bool`；`Create` 用它作异常消息，三参 `TryCreate` 把整个结果交给调用方。
- **测试辅助：** 自定义基元零配置 —— `Any.Id<T>()` 把七种文本形态依次交给 `TryParse`，候选来自带种子
  的随机源，`Seed` 仍能复现序列。
- **集成版本矩阵：** 探针工程按命令行传入的版本编译并往返每个集成，CI 对每个集成的最新大版本各跑一次；
  `docs/integrations.md` 新增「支持的版本」表。
- **`STIA0028`：** 泛型容器内的 Id 报警告 —— 仍能编译，但全部集成被跳过，转换器特性也因 `CS0416`
  无法发射；显式关掉全部集成时不报。

### 修复

- **生成器：** 用户成员与生成成员撞名（`CS0111` / `CS0102`）时不再产出代码，让 `STIA0029` 成为唯一
  错误；缺 `Value` 属性的 Id 同样不产出，不再用 `CS0535` 盖住自己的诊断。
- **生成器：** 跨文件重复标注不再让全部产物丢失（排序键改为声明序号 + 树内偏移）；特性标在后续
  `partial` 段上的 Id 重新能生成。
- **生成器：** 包内提供 `roslyn5.9` 与 `roslyn4.8` 两份构建，SDK 8、9 与 10.0.2xx 上不再报
  `CS9057` / `CS1929`，生成产物不变。
- **生成代码：** 带前缀的 `string` 基元 Id 值为空串时重新能往返 —— 裸前缀（`"ord_"`）可读回，裸空串
  仍被拒绝。
- **生成代码：** 自定义基元的 `ToString(string?, IFormatProvider?)` 不再返回 `null`，JSON 字典键路径
  补 `string.Empty` 兜底，不再留下 `CS8604`。
- **验证：** 富结果验证器现在拦住每条解析路径（`TryParse`、`Parse` 及 JSON、Newtonsoft、MVC 读入）；
  验证器与归一化方法的重载、继承写法按实参类型解析，`STIA0017` 不再误报。
- **分析器：** 新增 `STIA0029`，成员与生成成员撞名时落在用户自己的声明上；`{TypeName}Surrogate`
  只在生成器确实产出时才报。
- **分析器：** 实例名 `_` 被拒绝（`CS0199`），保留实例名改为按条件判定，`STIA0022` 不再建议会被
  `STIA0020` 拒绝的名字。
- **分析器：** 无法用单个实参调用的 `Validator` / `Normalizer` 提前拒绝，不再在生成代码里报 `CS1620`。
- **重构：** 「Convert to `record`」不再产出 `static record`；`file` 本地 record（`STIA0025`）、
  非 `partial` 容器（`STIA0009`）与可空参数（`STIA0006`）上不再提供「Convert to strongly typed id」。
- **打包：** 三参 `TryCreate` 补进 `CompatibilitySuppressions.xml`，重新通过 `1.3.0-preview.5` 基线
  （`CP0006`）。
- **测试辅助：** 静态缓存改为弱引用，不再钉住可回收 `AssemblyLoadContext`；带种子随机源遵守区间。
- **文档：** `TryCreate` 的 remark 与 `bool` 验证器的实际产物一致；英文功能索引页恢复英文并加 CJK
  守卫；「从其他库迁移」的 issue 链接补上。

### 变更

- **破坏性：** `IStronglyTypedId<TSelf, TPrimitiveId>` 新增 `TryCreate` 声明，手写实现需补上它。
- **破坏性：** `IStronglyTypedId<TSelf>` 改为继承 `IComparable<TSelf>` 与
  `IComparisonOperators<TSelf, TSelf, bool>`，手写实现需补五个成员。
- **构建依赖：** 生成器改用 `PolySharp` 取代 `IndexRange`；两者都是纯源码包，发布的程序集与生成产物
  均不变。

## [1.4.1] - 2026-11-06

### 变更

- **GraphQL：** `Int128`、`UInt128`、`BigInteger` 的 Id 在输入输出两侧都是字符串。字面量改为
  `StringValueNode`，与响应一致。
- **规范文本：** `DateTime`、`DateTimeOffset`、`TimeOnly`、`DateOnly` 改用 `"O"` 格式，空 `format`
  也回退到它。前三个的默认格式有损：会丢亚秒，`TimeOnly` 连秒都丢，不同的 Id 可能写出同一段文本，
  作 JSON 字典键时塌缩成一个条目。`DateOnly` 本身无损，改用 `"O"` 是因为它的默认短日期形
  （`01/02/2020`）与 System.Text.Json 写出的 ISO 形不一致，两个库会对同一个 Id 给出不同文本。
- **集成：** `Integrations = Default` 改为跟随程序集默认值，不再归一成 `All` 而覆盖程序集级的
  `Integrations = None`。
- **EF Core 与 Dapper：** `BigInteger`、`Int128`、`UInt128`、`Half` 改存规范文本。这四个基元没有数据库
  provider 类型；其余内置基元保持原生列类型。

### 修复

- **生成器：** `[StronglyTypedId]` 在多个 `partial` 段重复标注时抛 `CS8785`，现在只有第一个声明产出，
  `CS0579` 成为唯一错误。类型名与生成成员同名时编译失败（`CS0542`），现在报 `STIA0027` 并跳过生成。
- **System.Text.Json：** 对象形态的 Id 拒绝 `null` 基元并抛 `JsonException`，于是
  `{"value":null,"extra":1}` 读不回来 —— 尽管引用类型基元的写侧写出的正是 `{"value":null}`。转换器现在
  接受该 `null` 并继续读到 `EndObject`。
- **Newtonsoft.Json：** 对象形态的 Id 漏出 `FormatException` 与 `ArgumentException`，而不是
  `JsonException`。`decimal` 的 Id 丢精度且拒绝 `decimal.MaxValue`，现在写文本，读侧仍接受 JSON number。
- **HotChocolate：** 不同命名空间的同名 Id 导致 schema 构建失败。GraphQL 名现在回退到净化后的全名。
- **生成代码：** 带 `Prefix` 的 Id 与 HotChocolate scalar 依赖消费方的 `using System;`，在
  `<ImplicitUsings>disable` 下报 `CS1061`。受影响的文件现在以 `using global::System;` 开头。
- **OpenAPI 与 Swagger：** `BigInteger` 的 schema 标成 `integer`，而转换器写的是文本。可空 Id 因查表时
  没拆 `Nullable<T>` 而丢掉 schema。
- **可空 Id：** `AnyIdGenerator.Create(typeof(OrderId?))` 抛异常而没有拆壳。Bogus 把 `OrderId?` 属性留在
  `null`，显式传入 `typeof(OrderId?)` 时还会抛异常。
- **EF Core：** 非枚举基元的转换器抛 `ArgumentException`，而不是文档承诺的 `InvalidOperationException`，
  现在走 `TryCreate`。`AddStronglyTypedIds(ModelBuilder)` 是空操作，因为模型永远不会把强类型 Id 当作
  属性发现，现在按 CLR 反射枚举实体属性。
- **Dapper：** SQL Server 以 `DateTime` 与 `TimeSpan` 返回的 `DateOnly` 与 `TimeOnly` 列会被拒绝。
- **分析器：** `STIA0011` 在 `id.CompareTo(default)`、`id is default` 这类只读比较上误报。
- **测试辅助：** `AnyIdGenerator` 遇空枚举抛 `IndexOutOfRangeException`。
- **自定义基元：** 把 `IComparable<TSelf>.CompareTo` 写成显式接口实现时报五处 `CS7036`。
- **`DateTime`：** 文本解析对以 `"Z"` 结尾的文本丢掉 `DateTimeKind`，现在用
  `DateTimeStyles.RoundtripKind`。
- **protobuf-net：** 引用类型 Id 的 `null` 读回成基元默认值。
- **命名实例：** 叫 `op_LessThan` 之类的名字会产生 `CS0102`，现在列为保留名。`DateTime` 与
  `DateTimeOffset` 取值依赖构建机器时区，两种写法现在都被拒绝（`STIA0021`）。
- **集成辅助类：** 命名空间消歧后类名可能撞车，因为 `A.B.OrderId` 与 `A_B.OrderId` 都变成
  `A_B_OrderIdConverter`。最终类名相同时追加短哈希。
- **重构：** 「Inline strongly typed id」误删 `global using`，导致其它文件报 `CS0246`。

### 已知限制

- **Newtonsoft.Json：** 转换器运行前会解析像日期的值。因此 `DateTimeOffset` 的 Id 会丢掉 offset，
  值像 ISO 日期的 `string` 基元 Id 会被替换成别的文本。这是 Newtonsoft 的限制，生成器无法修复：
  转换器运行时原文已经丢了。把序列化器设成 `DateParseHandling = DateParseHandling.None` 可保留原始
  字符串；详见[序列化](docs/zh-CN/serialization.md#date-like-values-are-parsed-before-the-converter-runs)。

## [1.4.0] - 2026-10-30

集成版本：自定义基元（含 ULID）、protobuf-net 与 GraphQL 集成、每个 Id 的调试器代理，以及序列化与
分析器修复。其中数处为破坏性变更，「变更」一节已逐条标注。

### 新增

- **自定义基元：** 实现 `IParsable<TSelf>`、`IComparable<TSelf>` 与非泛型 `IComparable`，并暴露公开
  静态 `Parse` / `TryParse` 的类型即可作为被包装的基元（否则报 `STIA0008`）；`System.Uri` 被刻意排除。
  所有序列化路径存的都是它的规范文本，Cysharp 的 `Ulid` 已满足契约，ULID 无需额外代码。
- **集成：** 新增 protobuf-net、GraphQL（HotChocolate ≥ 13）、MessagePack 与 MongoDB BSON。
  protobuf-net 无需注册（surrogate 由特性发现）；GraphQL 经 `AddStronglyTypedIds()` 注册 scalar；
  MessagePack 与 BSON 写入基元值本身，不含文本前缀。
- **调试器代理：** 每个 Id 暴露 `Value` 与 `Text`（规范文本形，含前缀）；私有，运行期无开销。
- **测试辅助包：** `Len.StronglyTypedId.Testing`（`Any.Id<T>()`、可设定种子的序列、
  `Any.Register<T>(factory)`、`Any.Reset()`），以及同样遵守验证器的 AutoFixture 与 Bogus 适配包。
- **基元类型：** 新增 `char`、`Int128`、`UInt128`、`Half`、`float`、`double`、`DateOnly`、`TimeOnly`
  与 `BigInteger` 九种，连同 `enum` 共 24 种。
- **Id 选项：** `Normalizer`、`ExceptionType`、`RedactValidationMessage`、`Prefix` / `PrefixReadMode`、
  `JsonShape`、`Integrations`（过滤器而非开关）、`[StronglyTypedIdInstance]` 与 `IStronglyTypedId<TSelf>`，
  均可在 `StronglyTypedIdDefaults` 设程序集级默认值。
- **注册入口：** System.Text.Json 与 Newtonsoft.Json 的 `options.AddStronglyTypedIds()`，以及面向源生成
  `JsonSerializerContext` 的 `WithStronglyTypedIds()`（Native AOT 下可用）。
- **诊断：** 新增 `STIA0012`–`STIA0025`，26 条规则中 21 条附带代码修复；
  `<StronglyTypedIdEnableAnalyzers>false</StronglyTypedIdEnableAnalyzers>` 可静音分析器而不影响代码生成。
- **工具与示例：** `record` 转强类型 Id 的重构、可运行示例（`SampleConsole`、`SampleIntegrations`、
  `Sample1`–`Sample4`、`SampleGraphQL`）、路线图页、构建时校验的功能索引，以及「配置绑定」文档 ——
  Id 必须开 `TypeConverter = true` 才能被绑定，不开时 binder 静默失败。

### 变更

- **破坏性：** 集成入口改为在被配置对象上调用 `AddStronglyTypedIds(...)`，不再使用
  `StronglyTypedIds.ApplyTo(...)`；调用点把 `StronglyTypedIds.ApplyTo(x)` 换成 `x.AddStronglyTypedIds()`。
- **破坏性：** 文本不是合法 Id 时，`Parse` 抛 `FormatException` 而不是 `ArgumentException`；
  `Create` 仍抛 `ArgumentException`（或配置的 `ExceptionType`）。
- **破坏性：** 诊断 ID 从 `STIAO###`（字母 O）改名为 `STIA0###`（数字零）。
- **破坏性（带前缀的 Id）：** `ToString(format, provider)` 与 `TryFormat` 在 `format` 非空时也保留
  文本前缀。
- **测试辅助：** 种子生成改用 SplitMix64，序列跨运行时、跨平台可复现，**既有种子产出的值会变**。
- **性能：** Id 发现改读程序集元数据并按模块 MVID 缓存（分配量 −72%、耗时 −54%）；未引用
  `Len.StronglyTypedId` 的程序集直接跳过。
- **解析：** 格式提供程序为 `null` 时 `Parse` / `TryParse` 回退到不变文化；`STIA0011` 现在也报告
  `new(...)` 与 `default` 构造，默认级别提升为 `Warning`。
- **构建：** 生成代码去掉多余的 `/// <summary>`；构建与测试新增零警告基线、包校验、Native AOT 冒烟
  测试与更快的测试基建（`FluentAssertions` 钉在 7.2.0，即最后一个 MIT 许可版本）。

### 移除

- **字符串驻留：** 按 Id 类型分别驻留字符串的做法被移除，相等的字符串型 Id 不再共享同一实例；需要
  去重请自行使用 `string.Intern` 或在自有层做缓存。

### 修复

- **序列化：** 转换器不再把基元外包给 `JsonSerializer`；`BigInteger`、`Int128`、`UInt128`、`Half`、
  `DateOnly` 与 `TimeOnly` 在 System.Text.Json 与 Newtonsoft.Json 下都能正确往返；失败抛序列化库自己的
  异常；未定义的枚举取值在值路径上同样被拒；枚举字典键遵守 `DictionaryKeyPolicy`；声明程序集未引用
  Newtonsoft.Json 的 Id 会在消费方程序集得到转换器。
- **OpenAPI：** schema 与 JSON 的真实形态一致（数值、枚举与日期时间基元、`JsonShape.Object`），
  Minimal API 参数重新带上 `format`，路由约束不再阻断链接生成。
- **EF Core 与 Dapper：** 自定义基元映射为 `string`；Dapper 处理器改用非可空签名、为引用类型 Id 写入
  基元、转换驱动返回的值，并按不区分大小写解析枚举文本。
- **比较与解析：** 字符串型 Id 按序数比较，`SortedSet` / `SortedDictionary` 不再把不同 Id 塌成一条；
  `null` 当作最小值；枚举解析拒绝未定义取值；`Half` 接受 `NaN`、`±Infinity` 与会舍入到最大有限值的文本。
- **生成器：** 关键字命名、泛型容器、嵌套 `TypeConverter` 路径、缩进、空注册类，以及未引用 JSON 库时
  的 JSON 入口均已修正。
- **分析器与代码修复：** `STIA0016` 不再退化为 O(N²)；不再假定 `Constructors[0].Parameters[0]`；
  `STIA0019` 覆盖 MessagePack 与 MongoDB；内联重构只删除真正的特性；`STIA0005` 与 `STIA0008` 的修复已
  修正；`file` 本地 Id 报 `STIA0025`。
- **Native AOT：** MessagePack 与 BSON 对全部基元可用，枚举 formatter 与 BSON 默认序列化器显式注册。
- **测试与运行时辅助：** 泛型容器里的 Id 不再崩溃；不再阻止可回收 `AssemblyLoadContext` 卸载；
  `Any.Id<T>()` 覆盖完整数值范围；`AddStronglyTypedIds(MvcOptions)` 幂等。

## [1.3.0] - 2026-10-07

现代化版本：生成器迁移到增量流水线，能力面从「一个能解析的 Id」扩展为完整的 DDD 基元。

### 新增

- **基元类型：** 14 种外加任意 `enum` —— `Guid`、`string`、`byte`、`sbyte`、`short`、`ushort`、
  `int`、`uint`、`long`、`ulong`、`decimal`、`DateTime`、`DateTimeOffset` 与 `TimeSpan`。
- **工厂与解析：** `Create` / `TryCreate`、`Parse` / `TryParse`（字符串与 `ReadOnlySpan<char>`）、
  `IParsable<TSelf>`，以及基元支持时的 `IFormattable`、`ISpanFormattable` 与 UTF-8 span 接口。
- **比较与格式化：** 比较运算符、`Deconstruct`，以及可选的 `TypeConverter`（`TypeConverter = true`）。
- **验证器：** `[StronglyTypedId]` 上的 `Validator` 让 `Create`、`TryCreate` 与 `TryParse` 共用同一条
  规则；程序集级的 `StronglyTypedIdDefaults` 为程序集内每个 Id 提供该规则。
- **声明位置：** Id 可声明在嵌套类型与泛型容器中。
- **集成：** EF Core（`ApplyTo(ModelConfigurationBuilder)` 与 `ApplyTo(ModelBuilder)`）、Dapper
  （`ApplyTo(IDbConnection)`）、Swashbuckle 与 ASP.NET Core OpenAPI 的 schema 映射、MVC 模型绑定
  与 `{id:OrderId}` 路由约束、System.Text.Json 与 Newtonsoft.Json 转换器。
- **字符串驻留：** 按 Id 类型分别驻留字符串，相等的字符串型 Id 共享同一个底层实例，且不会泄漏到
  全局驻留池。
- **诊断：** `STIA0000`–`STIA0011` 及代码修复，已本地化（en / zh-CN）。

### 变更

- **构建：** 增量生成器流水线；确定性构建、公共 API 基线、包校验与符号包进入 CI。
- **打包：** `Len.StronglyTypedId` 以单个包发布，并自动引入生成器包。

### 修复

- **OpenAPI：** Swashbuckle 7/8/9 不再被误判为 Microsoft.OpenApi 2.x，`Guid` 支撑的 Id 重新获得
  `format: uuid`。
- **EF Core：** 不同命名空间中的同名 Id 不再在转换器里冲突。
- **分析器：** 多段 `partial` 声明不再触发 `STIA0005`。
- **代码修复：** 不再残留多余的 `where` 约束，也不再丢失特性与注释。
- **生成器：** `GetModules` 对模块级（`netmodule`）引用安全返回，而不是抛出异常。

## [1.3.0-preview.11] - 2026-10-02

### 修复

- **发布：** 重复发布符号包不再让发布失败 —— `dotnet nuget push` 现在传入 `--skip-duplicate`，
  把 409 降级为警告。

## [1.3.0-preview.10] - 2026-10-02

### 变更

- **测试：** 补充 ASP.NET Core OpenAPI 生成器的测试覆盖，包括其可访问性分支。

## [1.3.0-preview.9] - 2026-10-02

_对 `1.3.0-preview.8` 的重新打标；内容无变化。_

## [1.3.0-preview.8] - 2026-10-02

### 新增

- **分析器与示例：** `STIA0011` 代码修复、`Deconstruct` 支持与控制台示例。
- **打包：** 从 README 中拆出 `PACKAGE.md`，作为 NuGet 包的自述文件。

## [1.3.0-preview.7] - 2026-09-20

### 新增

- **MVC：** ASP.NET Core 模型绑定与 `{id:OrderId}` 路由约束。

## [1.3.0-preview.6] - 2026-09-20

### 新增

- **功能：** `TryCreate`、UTF-8 span 格式化与解析、`TypeConverter`、Dapper 与 OpenAPI 集成、
  `STIA0010` / `STIA0011`、程序集级默认设置、嵌套类型中的 Id、比较 / 格式化 / 字典键支持，
  以及验证器钩子。

### 变更

- **构建：** 公共 API 基线、包校验、符号包与确定性构建。

### 修复

- **生成器与分析器：** 约二十处生成器、分析器与代码修复缺陷，包括符号级基元检测、跨命名空间的 EF Core
  转换器冲突、Swagger 中缺失的 `uuid` 格式、第三方接口抑制生成，以及多段 `partial` 的误报。

## [1.3.0-preview.5] - 2026-09-13

### 修复

- **打包：** `Len.StronglyTypedId` 现在声明对 `Len.StronglyTypedId.Generators` 的依赖。

## [1.3.0-preview.4] - 2026-09-13

_对 `1.3.0-preview.2` 的重新打标；内容无变化。_

## [1.3.0-preview.3] - 2026-09-13

_对 `1.3.0-preview.2` 的重新打标；内容无变化。_

## [1.3.0-preview.2] - 2026-09-13

### 修复

- **发布：** 工作流同时安装 .NET 8 与 .NET 10 SDK，并修复 `OutputPath` 尾部斜杠导致的 `NU5019`。

## [1.3.0-priview.1] - 2026-09-13

_以此拼写发布；后续版本改用 `preview`。_

### 新增

- **仓库现代化：** 新增 .NET 10 目标与扩展后的测试矩阵。

### 变更

- **发布：** 改用 NuGet 可信发布（OIDC），不再使用长期有效的 API 密钥。

## [1.2.1] - 2024-02-26

### 新增

- **目标框架：** 新增 `net7.0`。

### 修复

- **缓存：** `ModuleInfo.GetTypes` 不再缓存，修复结果过期。

## [1.2.0] - 2023-11-27

### 变更

- **目标框架：** 改为 `net8.0`。

## [1.1.3] - 2023-10-08

### 变更

- **文档：** 仅文档变更。

## [1.1.2] - 2023-10-08

### 变更

- **缓存：** `GetTypes` 的结果会被缓存。

### 修复

- **生成器：** `InvalidOperationException` 消息中的命名空间改为完全限定名。

## [1.1.1] - 2023-05-20

### 修复

- **分析器：** 消息恢复可本地化。

## [1.1.0] - 2023-05-11

### 新增

- **生成器：** 增量流水线与语法分析器。
- **集成：** Swagger 与 EF Core 的 `ValueConverter` 生成器。

### 修复

- **生成器：** 不再为基于 `record` 的 Id 产生警告。

## [1.0.7] - 2023-04-26

### 新增

- **本地化：** 新增生成器资源（zh-CN）。

## [1.0.6] - 2023-04-25

### 新增

- **诊断：** 针对非法 Id 声明。
- **本地化：** 生成器错误消息。

## [1.0.5] - 2023-04-23

### 新增

- **生成代码：** 生成 Id 上的 `IParsable<TSelf>` 与 System.Text.Json 转换器。
- **基元类型：** `byte`、`sbyte`、`short` 与 `ushort`。

### 移除

- **包：** `Len.StronglyTypedId.AspNetCore`，由生成的集成取代。

### 修复

- **集成：** 两次调用 `AddStronglyTypedId` 不再注册重复的解析器。

[未发布]: https://github.com/lenfon/StronglyTypedId/compare/1.4.2...HEAD
[1.4.2]: https://github.com/lenfon/StronglyTypedId/compare/1.4.1...1.4.2
[1.4.1]: https://github.com/lenfon/StronglyTypedId/compare/1.4.0...1.4.1
[1.4.0]: https://github.com/lenfon/StronglyTypedId/compare/1.3.0...1.4.0
[1.3.0]: https://github.com/lenfon/StronglyTypedId/compare/1.3.0-preview.11...1.3.0
[1.3.0-preview.11]: https://github.com/lenfon/StronglyTypedId/compare/1.3.0-preview.10...1.3.0-preview.11
[1.3.0-preview.10]: https://github.com/lenfon/StronglyTypedId/compare/1.3.0-preview.9...1.3.0-preview.10
[1.3.0-preview.9]: https://github.com/lenfon/StronglyTypedId/compare/1.3.0-preview.8...1.3.0-preview.9
[1.3.0-preview.8]: https://github.com/lenfon/StronglyTypedId/compare/1.3.0-preview.7...1.3.0-preview.8
[1.3.0-preview.7]: https://github.com/lenfon/StronglyTypedId/compare/1.3.0-preview.6...1.3.0-preview.7
[1.3.0-preview.6]: https://github.com/lenfon/StronglyTypedId/compare/1.3.0-preview.5...1.3.0-preview.6
[1.3.0-preview.5]: https://github.com/lenfon/StronglyTypedId/compare/1.3.0-preview.2...1.3.0-preview.5
[1.3.0-preview.4]: https://github.com/lenfon/StronglyTypedId/compare/1.3.0-preview.2...1.3.0-preview.4
[1.3.0-preview.3]: https://github.com/lenfon/StronglyTypedId/compare/1.3.0-preview.2...1.3.0-preview.3
[1.3.0-preview.2]: https://github.com/lenfon/StronglyTypedId/compare/1.3.0-priview.1...1.3.0-preview.2
[1.3.0-priview.1]: https://github.com/lenfon/StronglyTypedId/compare/v1.2.1...1.3.0-priview.1
[1.2.1]: https://github.com/lenfon/StronglyTypedId/compare/v1.2.0...v1.2.1
[1.2.0]: https://github.com/lenfon/StronglyTypedId/compare/v1.1.3...v1.2.0
[1.1.3]: https://github.com/lenfon/StronglyTypedId/compare/v1.1.2...v1.1.3
[1.1.2]: https://github.com/lenfon/StronglyTypedId/compare/v1.1.1...v1.1.2
[1.1.1]: https://github.com/lenfon/StronglyTypedId/compare/v1.1.0...v1.1.1
[1.1.0]: https://github.com/lenfon/StronglyTypedId/compare/v1.0.7...v1.1.0
[1.0.7]: https://github.com/lenfon/StronglyTypedId/compare/v1.0.6...v1.0.7
[1.0.6]: https://github.com/lenfon/StronglyTypedId/compare/v1.0.5...v1.0.6
[1.0.5]: https://github.com/lenfon/StronglyTypedId/releases/tag/v1.0.5
