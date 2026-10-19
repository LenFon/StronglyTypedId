# 变更日志

本项目的重要变更都记录在此文件中。

格式遵循 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，版本号遵循
[语义化版本](https://semver.org/lang/zh-CN/)。

`1.3.0` 及更早的版本依据仓库 tag 重建：每条记录概述该版本交付的变更集合，而不是逐一复述每个提交。

> 📖 英文版：[CHANGELOG.md](CHANGELOG.md)

## [未发布]

### 新增

- `Len.StronglyTypedId.Testing`：测试用的确定性 Id 生成 —— `Any.Id<T>()`、`Any.Ids<T>(count)`、
  可设定种子的序列、`Any.Register<T>(factory)` 覆盖与 `Any.Reset()`。被生成的验证器拒绝的值会自动
  重试，而不是表现为测试失败。
- `Len.StronglyTypedId.Testing.AutoFixture`：`fixture.WithStronglyTypedIds()` 通过同一套生成器填充
  强类型 Id，因此 AutoFixture 创建的对象图同样遵守验证器。
- `IStronglyTypedId<TSelf>` —— Id 契约的单参数视图，泛型代码可以用「某个强类型 Id」作约束，而不必
  指明基元类型。当基元实现 `IFormattable` 时会生成 `IFormattableStronglyTypedId<TSelf>`。
- 新增八种基元类型：`Int128`、`UInt128`、`Half`、`float`、`double`、`DateOnly`、`TimeOnly` 与
  `BigInteger`（连同 `enum` 共 23 种受支持类型）。
- 新增 `char` 基元，连同 `enum` 共 24 种受支持类型。它的解析与格式化成员都是显式接口实现，
  故生成器改为发射长度判定与接口调用，而不是 `char.TryParse` / `char.ToString(format, provider)`。
- `Normalizer` —— 指向 `static TPrimitiveId Normalize(TPrimitiveId value)` 成员，即可在校验与落库
  之前修正传入值（去空白、统一大小写、去分隔符）。每个入口都按「归一化 → 校验 → 构造」执行，
  并可用 `[assembly: StronglyTypedIdDefaults(Normalizer = ...)]` 设置程序集级默认。引用不合法时
  报告 `STIA0017`，代码修复可生成直通桩方法或删掉该实参。
- `ExceptionType` —— 指向自定义异常，即可改变校验拒绝取值时 `Create` 抛出的异常。该类型必须派生自
  `System.Exception`、不能是抽象类，并具有可访问的、接收单个 `string` 消息的构造函数；否则报告
  `STIA0018`，代码修复可删掉该实参。`Parse` 仍抛 `ArgumentException`，因为它的失败还包含「文本根本
  不是 Id」。可用 `[assembly: StronglyTypedIdDefaults(ExceptionType = ...)]` 设置程序集级默认。
- 文本前缀：`Prefix` 在 `ToString` 中写入固定前缀、在 `Parse` 中将其剥离；`PrefixReadMode` 选择
  接受裸值（`Lenient`）还是拒绝裸值（`Strict`）。
- `JsonShape` 选择序列化形态：裸基元（`Primitive`）或 `{ "value": ... }`（`Object`）。
- System.Text.Json（`AddStronglyTypedId`）与 Newtonsoft.Json（`AddStronglyTypedId`）的注册入口，
  供显式配置转换器、不依赖生成的 `[JsonConverter]` 特性的调用方使用。
- 把现有 `record` 转成强类型 Id 的重构（refactoring）提供程序。
- `STIA0012`–`STIA0018` 诊断，其中包括 `STIA0013`（`Prefix` 为空串或纯空白）、`STIA0014`（带验证器
  或归一化器的 Id 被声明为 `public`，主构造函数仍可直达——`Info` 级别，默认关闭）、`STIA0015`
  （Id 名称应遵循 `...Id` 约定）、`STIA0016`（同一程序集内有两个 Id 使用同一基元）、`STIA0017`
  （`Normalizer` 引用不合法）与 `STIA0018`（`ExceptionType` 不可用）。
- 可运行的示例：`SampleConsole`（全功能导览）、`SampleIntegrations`（EF Core + Dapper 跑内存
  SQLite）、`Sample1`（MVC + Swashbuckle 6 + Newtonsoft.Json）、`Sample2`（Minimal API + 内置
  OpenAPI）、`Sample3`（MVC + Swashbuckle 10 / Microsoft.OpenApi 2.x）。

### 变更

- **破坏性变更：** 诊断 ID 从 `STIAO###`（字母 O）改名为 `STIA0###`（数字零）。`.editorconfig` 里
  已有的 `dotnet_diagnostic.STIAO011.severity` 条目不再匹配；改名发生在首个稳定版发布之前。
- 当格式提供程序为 `null` 时，`Parse` / `TryParse` 现在回退到不变文化，与 `ToString()` 写出的内容
  一致。此前 `null` 表示当前文化，于是 `Parse(id.ToString())` 在 `de-DE` 等区域下会静默产生错误值
  （`"1.5"` → `15`）。显式传入的 provider 仍按其文化处理。
- README 的参考表格（基元类型、集成、诊断）由 `tools/Len.StronglyTypedId.DocGen` 从生成器源码
  生成；一旦发生漂移，CI 即失败。
- 性能：生成器的发现结果在整条流水线内复用，`TryFormat` 不再装箱，`CompilationProvider` 依赖下沉。
- 构建：`.editorconfig` + `Directory.Build.props` 固定零警告基线（IDE/CA/xUnit 规则提升为警告），
  `dotnet pack` 时执行包校验，CI 新增 Native AOT 冒烟测试、包内容断言与生成器分配门槛。
- `STIA0011` 现在除 `new Xxx(...)` 外，还报告目标类型化的 `new(...)` 与 `default` / `default(T)`
  构造，默认级别由 `Info` 提升为 `Warning`，使「绕过验证器」能在 build 输出与 CI 中显现。只声明了
  `Normalizer` 的 Id 同样会触发该规则——直接构造出来的 Id 不是规范形。仅用于比较的 `default`
  （`id == default`）不报告。开启 `TreatWarningsAsErrors` 的项目将因绕过验证器而构建失败；如需恢复
  原有行为，设置 `dotnet_diagnostic.STIA0011.severity = suggestion`。

### 修复

- OpenAPI 参数 schema 现在带上基元的 `format`（`/orders/{id}` → `type: string, format: uuid`）。
  路由与查询参数在 `JsonTypeInfo` 中描述的是基元，此前的 schema 转换器会漏掉它们；请求体与响应体
  本来就是正确的。
- 声明在泛型容器内的 Id 不再发出 `[JsonConverter(typeof(...))]` —— 特性实参中出现类型参数是非法的
  （CS0416）。转换器类型仍会生成，可为封闭构造手动注册。
- 对于声明程序集未引用 JSON 库的 Id，不再生成 JSON 注册入口，消除了消费者项目中的 CS0426。
- Dapper 类型处理器改为重写 `SqlMapper.TypeHandler<TId>` 的非可空签名（`SetValue(IDbDataParameter, TId)`
  与 `Parse(object?)`）。此前的 `TId?` 签名对 struct Id 会解析成 `Nullable<TId>`，编译时报
  CS0115 / CS0508。

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
