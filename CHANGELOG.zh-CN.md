# 变更日志

本项目的重要变更都记录在此文件中。

格式遵循 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，版本号遵循
[语义化版本](https://semver.org/lang/zh-CN/)。

`1.3.0` 及更早的版本依据仓库 tag 重建：每条记录概述该版本交付的变更集合，而不是逐一复述每个提交。

> 📖 英文版：[CHANGELOG.md](CHANGELOG.md)

## [未发布]

下一个版本：自定义基元（含 ULID）、protobuf-net 与 GraphQL 集成、每个 Id 的调试器代理，以及序列化与
分析器修复。其中数处为破坏性变更，「变更」一节已逐条标注。

### 新增

- **自定义基元（含 ULID）。** 实现 `IParsable<TSelf>`、`IComparable<TSelf>` 与非泛型 `IComparable`，
  并暴露公开静态 `Parse` / `TryParse` 的类型即可作为被包装的基元（否则报 `STIA0008`）。所有序列化路径
  存的都是它的规范文本；`System.Uri` 被刻意排除。Cysharp 的 `Ulid` 已满足契约，因此 ULID 无需额外代码。
- **protobuf-net、GraphQL（HotChocolate ≥ 13）、MessagePack 与 MongoDB BSON 集成。** protobuf-net 无需
  注册（surrogate 由特性发现）；GraphQL 经 `AddStronglyTypedIds()` 注册 scalar；MessagePack 与 BSON
  写入基元值本身，不含文本前缀。
- **每个 Id 都带调试器代理** —— 暴露 `Value` 与 `Text`（规范文本形，含前缀）；私有，运行期无开销。
- **测试辅助包** —— `Len.StronglyTypedId.Testing`（`Any.Id<T>()`、可设定种子的序列、
  `Any.Register<T>(factory)`、`Any.Reset()`），以及同样遵守验证器的 AutoFixture 与 Bogus 适配包。
- **新增九种基元类型** —— `char`、`Int128`、`UInt128`、`Half`、`float`、`double`、`DateOnly`、`TimeOnly`
  与 `BigInteger` —— 连同 `enum` 共 24 种。
- **Id 选项** —— `Normalizer`、`ExceptionType`、`RedactValidationMessage`、`Prefix` / `PrefixReadMode`、
  `JsonShape`、`Integrations`（过滤器而非开关）、`[StronglyTypedIdInstance]` 与 `IStronglyTypedId<TSelf>`，
  均可在 `StronglyTypedIdDefaults` 设程序集级默认值。
- **注册入口** —— System.Text.Json 与 Newtonsoft.Json 的 `options.AddStronglyTypedIds()`，以及面向源生成
  `JsonSerializerContext` 的 `WithStronglyTypedIds()`（Native AOT 下可用）。
- **诊断 `STIA0012`–`STIA0025`**，26 条规则中 21 条附带代码修复；
  `<StronglyTypedIdEnableAnalyzers>false</StronglyTypedIdEnableAnalyzers>` 可静音分析器而不影响代码生成。
- `record` 转强类型 Id 的重构、可运行示例（`SampleConsole`、`SampleIntegrations`、`Sample1`–`Sample4`、
  `SampleGraphQL`）、路线图页、构建时校验的功能索引，以及「配置绑定」文档 —— Id 必须开
  `TypeConverter = true` 才能被绑定，不开时 binder 静默失败。

### 变更

- **破坏性变更：** 集成入口改为在被配置对象上调用 `AddStronglyTypedIds(...)`，不再使用
  `StronglyTypedIds.ApplyTo(...)`。调用点把 `StronglyTypedIds.ApplyTo(x)` 换成 `x.AddStronglyTypedIds()`。
- **破坏性变更：** 文本不是合法 Id 时，`Parse` 抛 `FormatException` 而不是 `ArgumentException`；
  `Create` 仍抛 `ArgumentException`（或配置的 `ExceptionType`）。
- **破坏性变更：** 诊断 ID 从 `STIAO###`（字母 O）改名为 `STIA0###`（数字零）。
- **对带前缀的 Id 属破坏性变更：** `ToString(format, provider)` 与 `TryFormat` 在 `format` 非空时也保留
  文本前缀。
- 指定种子的生成改用 SplitMix64，序列跨运行时、跨平台可复现 —— **既有种子产出的值会变**。
- Id 发现改读程序集元数据并按模块 MVID 缓存（分配量 −72%、耗时 −54%）；未引用 `Len.StronglyTypedId`
  的程序集直接跳过。
- 格式提供程序为 `null` 时 `Parse` / `TryParse` 回退到不变文化；`STIA0011` 现在也报告 `new(...)` 与
  `default` 构造，默认级别提升为 `Warning`。
- 生成代码去掉多余的 `/// <summary>`；构建与测试新增零警告基线、包校验、Native AOT 冒烟测试与更快的
  测试基建（`FluentAssertions` 钉在 7.2.0，即最后一个 MIT 许可版本）。

### 移除

- 按 Id 类型分别驻留字符串：相等的字符串型 Id 不再共享同一实例。需要去重请自行使用 `string.Intern`
  或在自有层做缓存。

### 修复

- **序列化** —— 转换器不再把基元外包给 `JsonSerializer`；`BigInteger`、`Int128`、`UInt128`、`Half`、
  `DateOnly` 与 `TimeOnly` 在 System.Text.Json 与 Newtonsoft.Json 下都能正确往返；失败抛序列化库自己的
  异常；未定义的枚举取值在值路径上同样被拒；枚举字典键遵守 `DictionaryKeyPolicy`；声明程序集未引用
  Newtonsoft.Json 的 Id 会在消费方程序集得到转换器。
- **OpenAPI** —— schema 与 JSON 的真实形态一致（数值、枚举与日期时间基元、`JsonShape.Object`），
  Minimal API 参数重新带上 `format`，路由约束不再阻断链接生成。
- **EF Core 与 Dapper** —— 自定义基元映射为 `string`；Dapper 处理器改用非可空签名、为引用类型 Id 写入
  基元、转换驱动返回的值，并按不区分大小写解析枚举文本。
- **比较与解析** —— 字符串型 Id 按序数比较，`SortedSet` / `SortedDictionary` 不再把不同 Id 塌成一条；
  `null` 当作最小值；枚举解析拒绝未定义取值；`Half` 接受 `NaN`、`±Infinity` 与会舍入到最大有限值的文本。
- **生成器** —— 关键字命名、泛型容器、嵌套 `TypeConverter` 路径、缩进、空注册类，以及未引用 JSON 库时
  的 JSON 入口均已修正。
- **分析器与代码修复** —— `STIA0016` 不再退化为 O(N²)；分析器不再假定 `Constructors[0].Parameters[0]`；
  `STIA0019` 覆盖 MessagePack 与 MongoDB；内联重构只删除真正的特性；`STIA0005` 与 `STIA0008` 的修复已
  修正；`file` 本地 Id 报 `STIA0025`。
- **Native AOT** —— MessagePack 与 BSON 对全部基元可用，枚举 formatter 与 BSON 默认序列化器显式注册。
- **测试与运行时辅助** —— 泛型容器里的 Id 不再崩溃；不再阻止可回收 `AssemblyLoadContext` 卸载；
  `Any.Id<T>()` 覆盖完整数值范围；`AddStronglyTypedIds(MvcOptions)` 幂等。

## [1.3.0] - 2026-10-07

现代化版本：生成器迁移到增量流水线，能力面从「一个能解析的 Id」扩展为完整的 DDD 基元。

### 新增

- 14 种基元类型外加任意 `enum`：`Guid`、`string`、`byte`、`sbyte`、`short`、`ushort`、`int`、
  `uint`、`long`、`ulong`、`decimal`、`DateTime`、`DateTimeOffset` 与 `TimeSpan`。
- 工厂与解析：`Create` / `TryCreate`、`Parse` / `TryParse`（字符串与 `ReadOnlySpan<char>`）、
  `IParsable<TSelf>`，以及基元支持时的 `IFormattable`、`ISpanFormattable` 与 UTF-8 span 接口。
- 比较运算符、`Deconstruct`，以及可选的 `TypeConverter`（`TypeConverter = true`）。
- `[StronglyTypedId]` 上的 `Validator`：`Create`、`TryCreate` 与 `TryParse` 共用同一条规则；
  程序集级的 `StronglyTypedIdDefaults` 为程序集内每个 Id 提供该规则。
- Id 可声明在嵌套类型与泛型容器中。
- 集成：EF Core（`ApplyTo(ModelConfigurationBuilder)` 与 `ApplyTo(ModelBuilder)`）、Dapper
  （`ApplyTo(IDbConnection)`）、Swashbuckle 与 ASP.NET Core OpenAPI 的 schema 映射、MVC 模型绑定
  与 `{id:OrderId}` 路由约束、System.Text.Json 与 Newtonsoft.Json 转换器。
- 按 Id 类型分别驻留字符串：相等的字符串型 Id 共享同一个底层实例，且不会泄漏到全局驻留池。
- `STIA0000`–`STIA0011` 诊断及代码修复，已本地化（en / zh-CN）。

### 变更

- 增量生成器流水线；确定性构建、公共 API 基线、包校验与符号包进入 CI。
- `Len.StronglyTypedId` 以单个包发布，并自动引入生成器包。

### 修复

- Swashbuckle 7/8/9 不再被误判为 Microsoft.OpenApi 2.x，`Guid` 支撑的 Id 重新获得 `format: uuid`。
- 不同命名空间中的同名 Id 不再在 EF Core 转换器里冲突。
- 多段 `partial` 声明不再触发 `STIA0005`。
- 代码修复不再残留多余的 `where` 约束，也不再丢失特性与注释。
- `GetModules` 对模块级（`netmodule`）引用安全返回，而不是抛出异常。

## [1.3.0-preview.11] - 2026-10-02

### 修复

- 重复发布符号包不再让发布失败：`dotnet nuget push` 现在传入 `--skip-duplicate`，把 409 降级为警告。

## [1.3.0-preview.10] - 2026-10-02

### 变更

- 补充 ASP.NET Core OpenAPI 生成器的测试覆盖，包括其可访问性分支。

## [1.3.0-preview.9] - 2026-10-02

_对 `1.3.0-preview.8` 的重新打标；内容无变化。_

## [1.3.0-preview.8] - 2026-10-02

### 新增

- `STIA0011` 代码修复、`Deconstruct` 支持与控制台示例。
- 从 README 中拆出 `PACKAGE.md`，作为 NuGet 包的自述文件。

## [1.3.0-preview.7] - 2026-09-20

### 新增

- ASP.NET Core MVC 模型绑定与 `{id:OrderId}` 路由约束。

## [1.3.0-preview.6] - 2026-09-20

### 新增

- `TryCreate`、UTF-8 span 格式化与解析、`TypeConverter`、Dapper 与 OpenAPI 集成、
  `STIA0010` / `STIA0011`、程序集级默认设置、嵌套类型中的 Id、比较 / 格式化 / 字典键支持，
  以及验证器钩子。

### 变更

- 公共 API 基线、包校验、符号包与确定性构建。

### 修复

- 约二十处生成器、分析器与代码修复缺陷，包括符号级基元检测、跨命名空间的 EF Core 转换器冲突、
  Swagger 中缺失的 `uuid` 格式、第三方接口抑制生成，以及多段 `partial` 的误报。

## [1.3.0-preview.5] - 2026-09-13

### 修复

- `Len.StronglyTypedId` 现在声明它对 `Len.StronglyTypedId.Generators` 的依赖。

## [1.3.0-preview.4] - 2026-09-13

_对 `1.3.0-preview.2` 的重新打标；内容无变化。_

## [1.3.0-preview.3] - 2026-09-13

_对 `1.3.0-preview.2` 的重新打标；内容无变化。_

## [1.3.0-preview.2] - 2026-09-13

### 修复

- 发布工作流同时安装 .NET 8 与 .NET 10 SDK，并修复了 `OutputPath` 尾部斜杠导致的 NU5019。

## [1.3.0-priview.1] - 2026-09-13

_以此拼写发布；后续版本改用 `preview`。_

### 新增

- 仓库现代化：新增 .NET 10 目标与扩展后的测试矩阵。

### 变更

- 发布改用 NuGet 可信发布（OIDC），不再使用长期有效的 API 密钥。

## [1.2.1] - 2024-02-26

### 新增

- `net7.0` 目标。

### 修复

- `ModuleInfo.GetTypes` 不再缓存，修复了结果过期的问题。

## [1.2.0] - 2023-11-27

### 变更

- 目标框架改为 `net8.0`。

## [1.1.3] - 2023-10-08

### 变更

- 仅文档变更。

## [1.1.2] - 2023-10-08

### 变更

- `GetTypes` 的结果会被缓存。

### 修复

- 生成的 `InvalidOperationException` 消息中的命名空间改为完全限定名。

## [1.1.1] - 2023-05-20

### 修复

- 分析器消息恢复可本地化。

## [1.1.0] - 2023-05-11

### 新增

- 增量生成器流水线与语法分析器。
- Swagger 与 EF Core 的 `ValueConverter` 生成器。

### 修复

- 基于 `record` 的 Id 所产生的警告。

## [1.0.7] - 2023-04-26

### 新增

- 本地化的生成器资源（zh-CN）。

## [1.0.6] - 2023-04-25

### 新增

- 针对非法 Id 声明的生成器诊断。
- 本地化的生成器错误消息。

## [1.0.5] - 2023-04-23

### 新增

- 生成 Id 上的 `IParsable<TSelf>` 与 System.Text.Json 转换器。
- `byte`、`sbyte`、`short` 与 `ushort` 基元。

### 移除

- `Len.StronglyTypedId.AspNetCore`，由生成的集成取代。

### 修复

- 两次调用 `AddStronglyTypedId` 不再注册重复的解析器。

[未发布]: https://github.com/lenfon/StronglyTypedId/compare/1.3.0...HEAD
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
