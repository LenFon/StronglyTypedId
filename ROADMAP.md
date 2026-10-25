# StronglyTypedId 发展规划（Roadmap）

> **一句话结论**：本库的核心能力已完备（9 个集成、26 条诊断、AOT 兼容、零第三方依赖）。下一阶段的价值不来自"再加几个功能"，而来自 **修掉两个实测复现的跨程序集缺陷**，以及 **把 DX 底噪压下去**；集成类方向按「参考项目 issue 佐证 + 社区证据」排序。
>
> **怎么读**
> - 决策者：§1 目标与范围 → §3 方向总览 → §9 里程碑 → §10 风险与待定
> - 执行者：§3 方向总览 → §4–§8 分层详述 → §9 验收标准
> - 想知道凭什么这么排：§11 附录（技术预研、实测复现、外部信号）
>
> 适用版本基线：.NET SDK 10.0.401（Roslyn 5.9），运行时 `net8.0` / `net10.0`。
> 代码快照：commit `fd34160`（2026-10-25）。

---

## 1. 目标与范围

### 1.1 这个库要解决什么

让「Id」在类型系统里成为**不可互换**的一等公民：`Guid` 与 `OrderId` 之间不再互相误传，且这个保证是**编译期**的、**零运行时开销**的、**不要求用户手写样板**的。

### 1.2 边界（明确不做什么）

| 不做 | 理由 |
| --- | --- |
| 隐式转换运算符（`implicit operator Guid(OrderId)`） | 直接削弱「类型安全」核心卖点，社区大概率反对 |
| 任意自定义类型作基元 | 与 `Uri` 被刻意排除同理——某些类型的 `Equals` 语义会让不同值塌缩成同一 id。（ULID 作为**白名单内**的新基元是另一回事，见 §5.4） |
| 运行时库降级到 netstandard2.0 | `IParsable<T>` / `IEqualityOperators<,>` / `static abstract` 成员在该基线上全都不存在 → 实质是另一个产品。详见 §7 |
| 泛型容器内 id 的外部集成 | Roslyn / CS0416 硬限制，短期无解（已在 `docs/limitations.md` 写明） |
| 承诺「支持所有 ORM / 序列化库」 | 按需逐个接入，每个都要独立的口径决策（枚举怎么写、前缀算不算、文本还是二进制） |

### 1.3 成功标准

1. **零破坏**：除显式声明的破坏性变更外，现有用户升级后行为不变——包括 JSON 输出的逐字节一致。
2. **每个方向都带回归测试**：缺陷修复必须先有复现用例，集成方向必须有端到端往返用例。
3. **门禁不退让**：公开 API 变更受 `PublicAPI.*.txt` 约束（RS0016/RS0017 已升为错误）；诊断表受 DocGen `--verify` 约束；性能受分配量门禁约束。
4. **口径唯一**：同一个 Id 在 REST / 二进制 / GraphQL / OpenAPI 两侧的表示口径必须一致或有明确文档说明。

---

## 2. 现状与关键架构事实

### 2.1 能力基线

**核心生成**
- 一行声明 `[StronglyTypedId]`（`partial record` / `partial record struct`），生成 `IStronglyTypedId<TSelf, TPrimitiveId>` 实现。
- 解析/格式化：`IParsable<TSelf>`、`ISpanParsable<TSelf>`、`IFormattable`、`ISpanFormattable`（按基元是否实现）。
- 比较与排序：`IComparable<TSelf>` + 四个比较运算符。
- 校验 `Validator`、异常类型 `ExceptionType`、归一化 `Normalizer`、命名实例 `StronglyTypedIdInstance`、`TryCreate`、UTF-8 接口（`IUtf8SpanFormattable` / `IUtf8SpanParsable`）、`TypeConverter`、`Prefix` 文本前缀、程序集级默认值 `StronglyTypedIdDefaults`、逐 id 集成过滤 `Integrations`。
- 运行时反射助手、Native AOT 与裁剪兼容、零第三方依赖。

**序列化**：`System.Text.Json` + `Newtonsoft.Json`（≥13.0.0）：字典键、JSON 形态可配置（`JsonShape`）、显式注册入口、前缀贯穿文本路径。

**集成（9 个，仅在引用对应包时生成）**：Entity Framework Core（≥7.0.0）、Dapper（≥2.0.0）、Swashbuckle（≥6.0.0）、ASP.NET Core MVC（含 Minimal API 绑定 + 路由约束，≥2.0.0）、ASP.NET Core OpenAPI（≥9.0.0）、MessagePack（≥2.0.0）、MongoDB.Bson（≥3.0.0）、`System.Text.Json`、`Newtonsoft.Json`。

**分析器与 DX**：26 条诊断（STIA0000–STIA0025），其中 21 条带 CodeFix；正向重构 `StronglyTypedIdRefactoringProvider`；诊断中英双语。

**测试与生态**：`Any.Id<T>()`（`Len.StronglyTypedId.Testing`）、AutoFixture 集成、DocGen 文档工具、性能门禁与基准工程；4 个 NuGet 包 + 文档站 + 中英双语 README/CHANGELOG。

### 2.2 四条决定成本的事实

**① 加一个集成要碰 7 个位置**（已被 9 个现有集成验证过）

| # | 文件 | 改什么 |
| --- | --- | --- |
| 1 | `src/Len.StronglyTypedId/…/StronglyTypedIdIntegrations.cs` | 加公开枚举位，并把它并进 `All` |
| 2 | `src/Len.StronglyTypedId.Generators/…/IntegrationFlags.cs` | 加**镜像**常量（生成器不引用运行时程序集，只能按整数位匹配，两处必须逐位一致） |
| 3 | `src/Len.StronglyTypedId.Generators/…/Generators/<Xxx>CodeGenerator.cs` | 新建生成器，实现 `IIntegrationCodeGenerator`（`Order` 决定执行顺序） |
| 4 | `…/Generators/StronglyTypedIdGenerator.cs` | `_generatorCatalog` 加一条 `CodeGeneratorRegistration`（含程序集闸门 + 中英 `DocText`）+ 一个 `GenerateXxxCode` 方法 + 一行 `RegisterSourceOutput` |
| 5 | `…/Generators/PrimitiveIdSchemaMap.cs` | 若该集成要暴露 OpenAPI schema，口径写在这里（Swagger 与内置 OpenAPI 共用，避免两处漂移） |
| 6 | `docs/integrations.md` + `docs/zh-CN/integrations.md` | 集成说明；DocGen 的 `--verify` 会校验 README 表格与登记项一致 |
| 7 | `test/…/Generators/` + `test/Len.StronglyTypedId.Integration.Test/` | 生成器单测 + 端到端往返用例 |

> 枚举位余量：现占 bit 0–9（`None` + 9 个集成），下一个可用 `1 << 10`，还能再加 22 个。

**② 门控与幂等是既有约定**
- 门控统一 `modules.HasModule("Xxx.dll", 最低主版本)`，版本比较用 `module.Version.Major`。
- 注册入口**一律幂等**：STJ / Newtonsoft 的 `ApplyTo` 先把已注册转换器类型收进 `HashSet` 再逐个 `Add`，重复调用不增长列表，也不会在 `JsonSerializerOptions` 冻结后抛 `InvalidOperationException`。
- MessagePack 的 `ApplyTo` **返回新实例**（`MessagePackSerializerOptions` 不可变），不改动入参。

**③ 跨程序集规则（决定了 §4 两项 P0 的成因）**
- 集成代码会在**消费方程序集**重新生成一份（`StronglyTypedIdDiscovery.Discover` 合并「引用程序集里已实现 `IStronglyTypedId` 的类型」）。
- **实现放在 `StronglyTypedIds` 内部**的集成（MessagePack / Dapper / MongoDB / EF Core）→ 可跨程序集重新生成 → 行为一致。
- **转换器嵌套在 Id 内部**的集成（STJ / Newtonsoft）→ 只能在声明 Id 的程序集生成（消费方补一份会撞 CS0426）→ 跨程序集行为可能不一致。
- STJ 恰好免疫：`System.Text.Json.dll` 在 `Microsoft.NETCore.App` 共享框架里，任何 net8.0+ 项目都引用得到 → 声明程序集必然生成转换器。**Newtonsoft 没有这个免疫**（独立 NuGet 包）。

**④ 基元能力判据是「封闭表 + 名字特判」**
- `SupportedPrimitiveTypes.DocumentedTypes` 是封闭表，`IsSupported` 按「命名空间 + CLR 名」匹配。
- 能力判据里 `IsFormattable` / `IsSpanFormattable` / `IsUtf8SpanParsable` / `IsUtf8SpanFormattable` 已是**接口探测**（通用）；但 `IsBigIntegerPrimitive` / `IsNewtonsoftTextPrimitive` / `IsBinaryTextPrimitive` 是**按名字特判**。
- `PrimitiveIdSchemaMap.Resolve` 是短名 switch；`TryFormatKind` 对 `char` / `byte` 单列分支。
- → 任何「加一个第三方基元」的方向都要先跨过这道坎（见 §5.4）。

---

## 3. 方向总览

**证据强度**：`实测复现` > `参考项目 issue` > `社区记录` > `推断`。本项目外部用户极少（stars/forks/watchers 各 1），所以**实测复现的结论权重最高**。

| # | 方向 | 层 | 状态 | 证据强度 | 价值 | 可行性 | 破坏性 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| **0a** | **Newtonsoft 跨程序集的静默契约不一致**（缺陷） | **P0** | ✅ 已实现 | 实测复现 | 高 | 中 | 无 |
| **0b** | **STJ 转换器自给自足**（不再把基元外包给 `JsonSerializer`） | **P0** | ✅ 已实现 | 实测复现 | 高 | 中 | 无 |
| 1 | **分析器一键静音**（`build_property` 通道） | T1 | ✅ 已实现 | 技术前提已验证 | 高 | 高 | 无 |
| 2 | **GraphQL 集成（HotChocolate）** | T1 | 待实施 | 社区记录 | 中高 | 中 | 无 |
| 3 | **Protobuf 集成（protobuf-net Surrogate）** | T1 | 待实施 | 社区记录 | 中高 | 中 | 无 |
| 4 | **ULID / 可插拔基元白名单** | T1 | 待实施 | 参考项目 issue | 高 | **低-中** | 中 |
| 5 | **AutoMapper 集成** | T2 | 待实施 | 参考项目 issue | 中 | 中 | 无 |
| 6 | **Minimal API 参数的 OpenAPI schema 补齐** | T2 | ✅ 已实现 | 实测复现 | 中高 | 中 | 无 |
| 7 | **Bogus 集成**（Testing 系列新包） | T2 | ✅ 已实现 | 实测复现 | 中 | 高 | 无 |
| 8 | **程序集级默认值补齐**（JsonShape / TypeConverter / PrefixReadMode） | T2 | 待实施 | 推断 | 中 | 高 | 无 |
| 9 | **确定性 Id 生成**（跨运行时稳定） | T2 | 待实施 | 推断 | 中高 | 高 | 仅 Testing 包 |
| 10 | **源生成场景的 Id 形态一致性** | T2 | 待实施 | 实测复现 | 中 | 低-中 | 无 |
| 11 | **YamlDotNet / CBOR 序列化** | T2 | 待实施 | 推断 | 中 | 中 | 无 |
| 12 | **NHibernate / linq2db 集成** | T2 | 待实施 | 推断 | 中 | 中 | 无 |
| 13 | **反向重构（裸基元 → 强类型 Id）** | T2 | 待实施 | 推断 | 高（DX） | 低 | 无 |
| 14 | **`DebuggerTypeProxy`** | T3 | 待实施 | 推断 | 低-中 | 高 | 无 |
| 15 | **生成器与分析器拆包** | T3 | 待实施 | 推断 | 中 | 中 | **包结构变更** |
| 16 | **运行时库 netstandard2.0 降级** | T3 | ❌ 不做（见 §8） | — | 低 | **低** | 是 |
| 17 | **跨编译的元数据级 Id 发现缓存** | T3 | 待实施 | 推断 | 中（IDE 场景） | 低-中 | 无 |
| 18 | **OpenTelemetry 日志脱敏** | T3 | 待实施 | 推断 | 中低 | 中 | 无 |
| 19 | **更多示例 / 快照测试** | T3 | ◐ 部分实施 | 推断 | 中 | 高 | 无 |
| — | 命名规则可配置（`.editorconfig` 后缀） | — | ✅ 已实现 | — | 中 | — | 无 |
| — | 缺失 CodeFix（STIA0019/0020/0021/0024） | — | ✅ 已实现 | — | 中 | — | 无 |

---

## 4. P0 · 缺陷修复（已实现，2026-10-25）

> 两项均已落地并有回归测试守着。实现过程中又挖出四条与序列化契约有关的既有行为，记在 §11.2 的 E9–E12。

### 4.1 Newtonsoft 跨程序集时 JSON 形态静默不一致 ✅ 已实现

**复现**（双工程，已实测）：

```
Lib：只声明 Id，不引用 Newtonsoft.Json          App：引用 Newtonsoft.Json 13.0.3
  [StronglyTypedId]
  public partial record struct OrderId(Guid Value);
```

| 序列化器 | 输出 |
| --- | --- |
| `System.Text.Json` | `"3fa85f64-5717-4562-b3fc-2c963f66afa6"`（字符串） |
| `Newtonsoft.Json` | `{"Value":"3fa85f64-5717-4562-b3fc-2c963f66afa6"}`（**对象**） |

**根因**：Newtonsoft 的转换器是**嵌套在 Id 内部**的（`OrderId.NewtonsoftJsonConverter`），只能生成在**声明 Id 的那个程序集**里——消费方补一份会撞 CS0426（类型已存在）。STJ 恰好免疫（见 §2.2 ③）。

**为什么是 P0**：同一个 Id 在 REST（STJ）与既有系统（Newtonsoft）两侧表示不同 → **数据契约静默不一致**。编译无警告、运行不抛异常，只在真实往返时暴露；测试若只覆盖一侧就发现不了。

**范围**：只影响 Newtonsoft。EF Core / Dapper / MessagePack / MongoDB 的实现都放在 `StronglyTypedIds` 内部 → 可跨程序集重新生成 → 行为一致。

**修法**：把 Newtonsoft 的转换器也移出 Id——与 MessagePack 同形：在 `StronglyTypedIds` 里生成一个顶层转换器类并在 `ApplyTo(JsonSerializerSettings)` 注册。代价是失去「声明程序集里 `[JsonConverter]` 零配置」这一捷径（对外部 Id 本来就没有）。
**备选**：两处都生成（Id 内嵌的给声明程序集，顶层的给消费方）——需长期维护两者的等价性。

**实现取舍（已定）**：选**两处都生成**。内嵌转换器与 `[JsonConverter]` 特性一律保留（声明程序集零配置不变），消费方为「声明程序集没有 Newtonsoft 转换器」的那些 Id 补一份顶层转换器并注册。理由是 §1.3 成功标准第 1 条要求零破坏——「只生成顶层版」会让现有用户的 Newtonsoft 零配置失效。Newtonsoft 的特性优先级高于 `JsonSerializerSettings.Converters`，所以两边同时存在也不打架。

**实现落点**：`StronglyTypedIdInfo.RequiresExternalNewtonsoftJsonConverter`（新增判据）+ `NewtonsoftJsonRegistrationCodeGenerator`（同时产出顶层转换器与注册入口，形态与 MessagePack 生成器一致）。

**参考项目对应**：#61（领域层绑定基础设施，open / 9 评论）、#151（AutoMapper 与 TypeConverter，open）。
**工作量**：中。**破坏性**：无（默认行为不变；跨程序集场景从「对象形态」变成「字符串形态」——那正是修复）。

### 4.2 STJ 转换器把「基元怎么写」外包给了 `JsonSerializer` ✅ 已实现

**复现**（已实测）：`App` 用源生成的 `JsonSerializerContext`（`[JsonSerializable(typeof(OrderId))]`），但**没登记基元类型**：

```
System.NotSupportedException: JsonTypeInfo metadata for type 'System.Guid' was not provided by
TypeInfoResolver of type 'App.AppJsonContext'. ...
   at Lib.OrderId.SystemTextJsonConverter.Write(...) in .../Lib.OrderId.SystemTextJson.g.cs:line 49
```

**根因**：`SystemTextJsonCodeGenerator` 把基元的读写**全部委托**给 `JsonSerializer`：

- 写：`global::System.Text.Json.JsonSerializer.Serialize(writer, primitiveId, options);`（第 318 行）
- 读：`global::System.Text.Json.JsonSerializer.Deserialize<基元>(ref reader, options)`（第 146 行）

而 `JsonSerializer.Serialize<T>` 需要 `options` 里有 `T` 的 `JsonTypeInfo` → 源生成模式下 context 只服务 `[JsonSerializable]` 登记过的类型 → **基元必须也登记**。

**现状不算 bug，但架构上是个依赖倒置**：写一个 Guid **不需要任何元数据**——`Utf8JsonWriter.WriteStringValue(Guid)` 一行就够，且输出与 STJ 默认**逐字节相同**（"D" 格式、小写带连字符）。转换器却把这件事外包给 `JsonSerializer`，于是被迫要求用户为基元类型登记元数据。

**文档现状**：`docs/zh-CN/aot.md` 确实要求「要登记 Id 所包裹的基元类型」，AOT 冒烟测试也照做了（`SmokeTestJsonContext` 登记了 `int` / `Guid` / `string`）。所以**照文档走是通的**；问题在于这条要求既冗余又易漏，而漏了之后的报错（框架的 `NotSupportedException`）几乎无法让用户自行定位到「少登记了一个基元类型」。

**修法**
1. **按基元类型直写**：`Guid` → `writer.WriteStringValue(primitiveId)`；数值 → `writer.WriteNumberValue(...)`；`bool` → `writer.WriteBooleanValue(...)`；`char` → `writer.WriteStringValue(primitiveId.ToString())`；枚举 → 按底层类型写数字（与 STJ 默认一致）。
2. **日期时间系列**（`DateTime` / `DateTimeOffset` / `TimeSpan` / `DateOnly` / `TimeOnly`）格式敏感：STJ 用的是 trimmed ISO 8601（`JsonWriterHelper` 内部实现），公开 API 无法逐字节复刻 → 保留委托，但捕获 `NotSupportedException` 并转成带指引的 `JsonException`（明确告诉用户加 `[JsonSerializable(typeof(DateTime))]`）。
3. 读侧同理：`reader.GetGuid()` / `reader.GetInt32()` 等原生方法，不依赖元数据。

**附带收益**：① AOT 用户不必再登记基元类型 → `aot.md` 能删掉那句要求；② 生成的代码不再走 `JsonSerializer` 的完整管线 → 更快；③ 错误面收窄。

**实现取舍（已定）**：**能直写的基元一律直写**（`PrimitiveJsonIo` 新增一张「基元 → 直写 / 直读表达式」映射表）；**没有直写形式的少数基元**（`Int128` / `UInt128` / `Half` / `TimeSpan` / `DateOnly` / `TimeOnly` / `char`）保留委托，但把框架的 `NotSupportedException` 转成**带指引的 `JsonException`**（直接写出该补哪一条 `[JsonSerializable]`）。直读期的 `InvalidOperationException` / `FormatException` 同样转成 `JsonException`——STJ 的转换器契约要求失败一律抛 `JsonException`。

**等价性由测试守着**：`PrimitiveContractMatrixTests` 新增一条断言——每种基元都拿「STJ 默认序列化结果」与「生成代码的写出结果」比字节（25 种基元全覆盖，BigInteger 除外：STJ 没有它的内置转换器，本库固定写文本形）。

**工作量**：中（要加一张「基元 → 直写表达式」映射表，与 `PrimitiveIdSchemaMap` 同类）。**破坏性**：无（输出逐字节不变）。

---

## 5. T1 · 短期高价值

### 5.1 分析器一键静音 ✅ 已实现

**现状**：`docs/zh-CN/packages.md` 给的关闭手段只有「逐条 `dotnet_diagnostic.STIAxxxx.severity = none`」或「`NoWarn` 列全 26 个 ID」，没有整包静音。一个只消费 `IStronglyTypedId<,>` 而自己不声明 Id 的项目（领域库、工具项目，很常见）要背 26 条规则的噪音。

**实现路径**（技术前提已用最小复现工程实测闭环，详见 §11.1）：消费方 csproj 的属性值 → `<CompilerVisibleProperty Include="…" />` → SDK 写入 `build_property.…` → 分析器从 `AnalyzerConfigOptionsProvider.GlobalOptions` 读取 → 在 `RegisterCompilationStartAction` 起点判断，为 `false` 时直接 return。

**落点**
- `…/Analyzers/StronglyTypedIdAnalyzer.cs`：读 `context.Options.AnalyzerConfigOptionsProvider.GlobalOptions`，命中 `build_property.StronglyTypedIdEnableAnalyzers` 且值为 `false`（大小写不敏感）时直接 return。
- `src/Len.StronglyTypedId.Generators/`：新增 `buildTransitive/Len.StronglyTypedId.props`（内容是 `<CompilerVisibleProperty Include="StronglyTypedIdEnableAnalyzers" />`）并打进包（`PackagePath="buildTransitive/"`）——**库要替用户做这一步**，否则属性不会被暴露。
- 文档：`docs/packages.md` 与 `docs/zh-CN/packages.md` 各加一节；README「环境要求」表加一行属性说明。
- 测试：`StronglyTypedIdAnalyzerIntegrationTests.cs` 加「属性为 false 时零诊断」用例（单测里手工构造 `AnalyzerConfigOptionsProvider` 即可，不必走真实构建）。

**影响**：默认行为完全不变；诊断 ID 与数量不变（26 条）；CodeFix 仍会被加载，但没有诊断就没有修复项。
**工作量**：低（分析器 1 处 + 打包 1 个 props + 文档 3 处 + 测试 1 处）。**价值**：高。

### 5.2 GraphQL 集成（HotChocolate） ⬜ 待实施

**现状**：集成列表里完全没有 GraphQL，而它是 API 层最常见的框架之一。

**落点**：按 §2.2 ① 的 7 个位置。门控 `modules.HasModule("HotChocolate.dll", 13)`。
**产物**：`StronglyTypedIds.ApplyTo(IRequestExecutorBuilder)` + 每个 Id 一个 `HotChocolateType`。

**需要先定的三件事**
1. **scalar 的运行时绑定**：`ScalarType<T>` 的 `Serialize` / `ParseValue` / `TrySerialize` 委托必须绑到 Id 的 `Create` / `TryParse`。好消息是这些都可以用**静态方法引用 + lambda** 表达，**不需要反射**，AOT 友好。
2. **失败路径的异常类型**：HotChocolate 的 scalar 契约要求解析失败抛 `SerializationException`，而本库的 `Parse` 抛 `FormatException` → 生成器要包一层转换。
3. **枚举基元的口径**：HotChocolate 的枚举 scalar 默认按**成员名**序列化，本库 JSON 口径是**底层整数**。二者必须选一个并写进文档，否则同一个 Id 在 REST 与 GraphQL 两侧表示不同。

**工作量**：中（生成器 ~250 行 + 枚举位 2 处 + 文档 2 页 + 集成测试 + 样例工程）。**风险**：中（枚举口径、scalar 命名、注册入口 API 随 HotChocolate 版本漂移）。
**顺带**：GraphQL 是天然的「字典键 / 输入类型」场景，`Prefix` 与 `JsonShape` 的语义需要在 scalar 文档里一并交代。

### 5.3 Protobuf 集成（protobuf-net）

**落点**：优先 protobuf-net 的 `Surrogate` 模式。
**关键差异**：protobuf-net 的 `[Surrogate(typeof(T), typeof(TSurrogate))]` 是**特性**，只能加在**本次编译声明**的类型上——外部程序集里的 Id 加不了。因此这条要实现为**核心生成器**（往 Id 上加特性），而不是「消费方重新生成一份」的集成生成器；`IntegrationFlags` 与 `StronglyTypedIdIntegrations` 都需要新位。
**产物**：每个 Id 一个 `{TypeName}Surrogate`（单字段，装基元值）+ `[ProtoContract]` / `[ProtoMember]`。
**Google.Protobuf 路线**成本高得多（要生成 `.proto` 或用 `IMessage` 包装），建议只做 protobuf-net。
**工作量**：中。**风险**：中（`Surrogate` 的类型约束、成员编号、与现有 JSON 口径的一致性）。

### 5.4 ULID / 可插拔基元白名单 ⬜ 待实施

第一轮记的是「可行性：中」。**代码核对后要下调为「低-中」**：真正的成本不是「加一个类型名」，而是**把基元能力判据从「封闭表 + 名字特判」改成「能力探测」**（见 §2.2 ④）。

- **好消息**：公开接口 `IStronglyTypedId<TSelf, TPrimitiveId> where TPrimitiveId : notnull, IComparable` **不需要破坏性变更**——主流 ULID 库（Cysharp 的 `Ulid`）同时实现了 `IComparable<Ulid>` 与非泛型 `IComparable`，约束成立。
- **坏消息**：第三方类型的**接口实现组合**与 BCL 基元不同（例如只有 `ISpanParsable<T>` 而没有 `IParsable<T>`，或没有 `IUtf8SpanFormattable`），生成器对「哪些接口要透出」的判定必须逐类型核对。
- **还要定的事**：ULID 走**文本基元**（26 字符 Crockford Base32）还是**二进制基元**（16 字节）？这直接决定 JSON / MessagePack / BSON 三条路径的口径。

**结论**：核心重构级，需先出一份「基元能力模型」设计（把「这个类型支持什么」抽成显式声明），再谈 ULID。

---

## 6. T2 · 中期稳收益

### 6.1 AutoMapper 集成 ⬜ 待实施

参考项目 #151（open）：AutoMapper 11+ 为性能弃用 `TypeConverter` 支持 → 映射 Id 需要专门配置；用户还得让「数据库项目」依赖 AutoMapper。

**现状**：本项目有 EF Core / Dapper / MessagePack / MongoDB / Swashbuckle / OpenAPI / MVC 集成，**没有 AutoMapper**。
**修法**：生成 AutoMapper 的值转换配置（`CreateMap` + `ConvertUsing`），或生成一个 `IValueConverter` 供用户挂到 `ProjectTo` 配置上。
**工作量**：中。**价值**：中（AutoMapper 基数大，但受众是 EF 之外的那部分用户）。

### 6.2 Minimal API 参数的 OpenAPI schema 补齐 ✅ 已实现

**已落地**：`SwaggerCodeGenerator` 现在额外生成一个 `IOperationFilter` 实现，按参数的**真实类型**
（`ApiParameterDescription.Type` 仍是 Id 类型）查同一份 schema 映射，把参数上那份裸的 `type: string`
换成带 `format` 的形态；过滤器随 `ApplyTo(SwaggerGenOptions)` 注册，重复调用不增长。

**根因（实测）**：`ApiExplorer` 给这类参数两副面孔 —— `ApiParameterDescription.Type` 是真实的 Id 类型，
而 `ModelMetadata.ModelType` 已被框架换成 `System.String`；Swashbuckle 据后者生成 schema，
按 CLR 类型登记的 `MapType` 因此没有机会生效。

**为什么优先级要提高**：社区架构决策记录（LearnStack ADR-0023，2026-08）选 Vogen 而非 StronglyTypedId 时，三条理由之一正是 OpenAPI schema 成本——原文说「across 60+ ID types projected for Phase 02a–08, that is a real cost」。**本项目的自动 `ApplyTo(SwaggerGenOptions)` 已解决绝大部分**（参考项目 Andrew Lock 版需逐类型 `MapType`），但 **Minimal API 参数这一块仍是残余缺口**，且恰好是外部评审点名的那类成本。

**落点（已实施）**：`SwaggerCodeGenerator` 与 `AspNetCoreOpenApiCodeGenerator` 的补充，不是新集成。
- Swashbuckle 侧：生成 `StronglyTypedIdOperationFilter`，按 `parameterDescription.Type` 查表并覆盖参数 schema；
  幂等检查放在 `SwaggerGeneratorOptions.OperationFilters` 上（Swashbuckle 6 与 10 都有这个成员）。
- 内置 OpenAPI 侧：`.NET 9+` 的 `OpenApiOptions` 转换器看的是框架建好的 schema，本项目原本就无此缺口。

**顺带修掉的既有缺陷**：`MapType` 的底层语义是 `CustomTypeMappings.Add(...)` —— 重复调用 `ApplyTo(SwaggerGenOptions)`
会抛 `An item with the same key has already been added`。现在注册前先查 `ContainsKey`，`ApplyTo` 恢复幂等。

**验证口径**：`samples/Len.StronglyTypedId.Sample4`（`net8.0` + Swashbuckle 6.9.0）实测 `/orders/{id}`
从 `type: string` 变为 `type: string, format: uuid`；自动化落在新工程 `Len.StronglyTypedId.Swagger.Test`
（`Microsoft.AspNetCore.TestHost` 内存服务器，跑真实的 ASP.NET Core 管道）。

### 6.3 Bogus 集成 ✅ 已实现

**已落地**：新建 `src/Len.StronglyTypedId.Testing.Bogus/`，提供 `FakerExtensions.WithStronglyTypedIds<T>()`，
形状与 AutoFixture 的 `WithStronglyTypedIds()` 对称。

**为什么提出来**：生态对比里，Vogen 已被集成进多个项目的「Bogus customization」——Bogus（bUnit 作者维护的 fake data 库）有 `IRandomizer` 扩展点，是 .NET 测试数据生成的事实标准之一。本库走「自研 + AutoFixture」路线，**Bogus 用户拿不到现成的 Id 定制入口**，只能在 `AnyIdOptions.PrimitiveValueFactory` 里手工接线。

**落点（已实施）**：走 Bogus v35 的 `Faker<T>.RuleForType(Type, Func<Faker, TType>)` —— 它让 Bogus
按**类型**填值，于是适配层只要为每个 Id 类型登记一条规则，`Generate()` 就自动填上 Id 属性。
复用 `AnyIdGenerator`，不重复实现取值逻辑。

**实测修正**：Bogus v35 **没有**老版本的 `IRandomizerExtension` 机制（原设想是「`Randomizer` 扩展」），
真正的入口是 `RuleForType`；而 `Faker<T>` 默认走 `Activator.CreateInstance<T>()`，目标类型必须有无参构造。

**依赖方向**：新包依赖 `Bogus` + `Len.StronglyTypedId.Testing`；核心测试包**不**依赖 Bogus（包级断言守着）。
**破坏性**：无（纯新增包）。**价值**：中。

### 6.4 程序集级默认值补齐 ⬜ 待实施

**现状**：`StronglyTypedIdDefaultsAttribute` 只有 5 项：`Validator` / `Normalizer` / `ExceptionType` / `RedactValidationMessage` / `Integrations`。`JsonShape` / `TypeConverter` / `Prefix` / `PrefixReadMode` 只能逐类型写。

**落点**
- 运行时 `…/StronglyTypedIdDefaultsAttribute.cs`：加属性。
- 生成器 `StronglyTypedIdInfo.cs`：加 `GetAssemblyDefault*` 同族方法，构造参数各 +1（**注意**：这个类型是增量缓存的键，构造签名变化会改缓存键，须一并复核相等性判定）。
- `StronglyTypedIdGenerator.GetStronglyTypedIdInfoOrNull`：读取并传入。
- `AttributeReader.cs`：加具名参数读取。`bool` 型的 `TypeConverter` 需沿用 `RedactValidationMessage` 已有的「只能单向打开」语义——程序集默认打开后，单个 Id 无法再关回去（`AttributeReader.GetBoolean` 分不清「没写」与「写了 false」）。

**为什么 `Prefix` 不进程序集级默认**：`STIA0023`（AmbiguousPrefix）会在两个 Id 共用同一前缀时报错。程序集级前缀默认值几乎必然让同程序集的多个 Id 共用前缀 → 诊断大面积触发 → 反倒制造噪音。
**推荐纳入**：`JsonShape`（整库统一对象形很常见）、`TypeConverter`、`PrefixReadMode`。
**工作量**：低-中。**价值**：中。

### 6.5 确定性 Id 生成 ⬜ 待实施

**现状**：`AnyIdOptions.Seed` 的文档明确写「可复现性只对给定运行时版本成立：`Random` 背后的算法是实现细节，.NET 各版本之间可能变」。快照测试 / 黄金文件场景需要**跨 .NET 版本、跨机器完全一致**的序列。

**落点**：`src/Len.StronglyTypedId.Testing/…/PrimitiveValueFactory.cs`（约 20 处 `random.*` 调用）+ `AnyIdGenerator.cs`。
**做法**：引入一个内部 `IDeterministicRandom` 抽象（`Next(int)` / `Next(min,max)` / `NextInt64()` / `NextDouble()` / `NextBytes()`），默认实现换成自研 PRNG（SplitMix64 / xoshiro256\*\*），种子决定初始状态 → 序列在任何运行时、任何平台一致。`Random` 降级为「不指定种子」的可选路径。
**代价**：现有依赖 `Any.Id<T>()` 具体取值的快照会变 → 只影响 Testing 包，可接受，但要写进 CHANGELOG。
**工作量**：低-中。**价值**：中高（测试 DX）。

### 6.6 其余 T2 方向

| 方向 | 说明 | 工作量 / 风险 |
| --- | --- | --- |
| **源生成场景的 Id 形态一致性** | STJ 源生成器对**当前编译声明**的类型走 fast-path，不取本库生成器补在 partial 声明上的 `[JsonConverter]` → Id 被按属性写成对象形态 `{"Value":"…"}`，而反射式序列化写的是字符串（见 §11.2 的 E9）。目前靠 `ApplyTo`（与源生成器决策无关的那条路）绕开，`docs/aot.md` 已写明两种 `GenerationMode` 的差别。可考虑再生成一个 `IJsonTypeInfoResolver` 实现，让用户一行组合进 `options.TypeInfoResolver` | 低 / 中（受众限于 AOT 用户） |
| **YamlDotNet / CBOR 序列化** | YamlDotNet 走字符串↔id 的文本路径（复用 `Prefix` 与 `TryParse`）；`System.Formats.Cbor` 走二进制（IoT / CoAP，参照 MessagePack 的做法） | 中 / 低 |
| **NHibernate / linq2db 集成** | 扩展 ORM 覆盖面。建议先评估采用率再挑 1–2 个——每个 ORM 的值转换器 API 都不同，收益分散 | 中 / 中 |
| **反向重构（裸基元 → 强类型 Id）** | 与现有正向 `StronglyTypedIdRefactoringProvider` 互补：扫描某 `Guid` / `int` 字段或参数并替换为 id，同时生成类型 | 低（工作量高）/ 跨文件 |
| **缺失的安全可达 CodeFix** | **已实现**：`STIA0019` / `STIA0024`（删 `Integrations` 实参）与 `STIA0020` / `STIA0021`（删 `[StronglyTypedIdInstance]` 特性），附带修复的规则由 17 条增至 21 条。`STIA0022` 刻意不给（重命名牵动所有引用点）；`STIA0015` / `STIA0016` / `STIA0023` 依旧故意不给 | — |
| **命名规则可配置** | **已实现**：`STIA0015` 后缀支持 `.editorconfig`（`dotnet_diagnostic.STIA0015.suffix`，默认 `Id`），诊断消息随之带上实际后缀 | — |

---

## 7. T3 · 战略 / 长尾

| 方向 | 状态 | 价值 | 说明 |
| --- | --- | --- | --- |
| **`DebuggerTypeProxy`** | ⬜ 待实施 | 低-中 | `DebuggerDisplay` 已由生成器无条件附加。**但 `DebuggerTypeProxyAttribute` 只对 sealed 类型生效**——本库支持 `record`（引用类型，默认非 sealed）与 `record struct`，只有后者受益。要么强制 Id 为 sealed（改变现有语义），要么只对 `record struct` 生成（半数场景）。建议与 T2 同批做，或直接放弃。 |
| **生成器与分析器拆包** | ⬜ 待实施 | 中 | 两者同在 `Len.StronglyTypedId.Generators` 程序集，无法只要生成器。**破坏性**：包结构变更，`InjectGeneratorsPackageDependency` 的 nuspec 补丁链要扩成两条，`Packaging.Test` 的断言要改。**但 §5.1 的「分析器一键静音」以 1/10 的成本覆盖了大部分使用场景 → 优先做那个。** |
| **运行时库 netstandard2.0 降级** | ❌ 不做 | 低 | **逐条障碍**：`IParsable<T>` / `ISpanParsable<T>`（.NET 7 引入）、`IEqualityOperators<,>`（.NET 7）、`IUtf8SpanFormattable`（.NET 8）、`static abstract` 成员（需运行时支持）在 netstandard2.0 上全都不存在。唯一可行形态是「不实现这些接口，只生成同名静态方法」→ 依赖 `where TId : IParsable<TId>` 的泛型用户代码全部失效 → **实质是另一个产品**。**建议不做**，改为在 `docs/limitations.md` 写清替代路径（`string` 基元 + 手写包装）。 |
| **跨编译的元数据级 Id 发现缓存** | ⬜ 待实施 | 中（仅 IDE 场景） | **重算放大分析**：改任意一个 Id → `Collect()` 后的数组变化 → `DiscoveredIds.Create` 重跑 → `ModuleInfo.GetTypes()` 重新遍历所有「能声明 Id」的引用程序集（其懒缓存是实例级的，新编译即失效）。技术上可绕开：改用 `MetadataReader` 直接查 `InterfaceImplementations`（不绑定符号、不依赖 `IAssemblySymbol` 身份），缓存键取「程序集名 + 版本」。**但**构建期一次性编译几乎无感，收益集中在 IDE 连续编辑 → T3 性能子类。 |
| **定位澄清：用文档说明本库如何覆盖值对象场景** | ⬜ 待实施 | 中 | 生态对比里最常见的选型困惑是「StronglyTypedId vs Vogen 怎么选」。Vogen 覆盖 Email / Money 等值对象，本库只做 Id——但本库的 `Validator` + `Normalizer` 实际已能承担「带不变量的值对象」职责，只是命名上不叫值对象。**这不是转向，是补一段说清楚的话**：在 `docs/index.md` 与 README 加一节「当你要的不只是 Id」。 |
| **OpenTelemetry 日志脱敏** | ⬜ 待实施 | 中低 | 把 id 作为 span 属性时自动格式化 / 脱敏，呼应已有的 `RedactValidationMessage` 安全取向。 |
| **更多示例 / 快照测试** | ◐ 部分实施 | 中 | 示例已补齐（`SampleConsole` 全功能导览、`SampleIntegrations` 跑 EF Core + Dapper、`Sample1`–`Sample4` 覆盖 MVC / Minimal API 多代 Swashbuckle 与 OpenAPI，外加共享 `Domain`）；快照测试（`.verified.*`）尚未引入。 |

---

## 8. 不建议做的方向

除 §1.2 的边界外，还有四条基于代码事实的否定结论：

- **泛型容器内 id 的外部集成**：Roslyn / CS0416 硬限制（已在 `docs/limitations.md` 写明），短期无解，只能让用户对闭合类型手动注册。
- **程序集级 `Prefix` 默认值**：会与 `STIA0023` 大面积冲突，见 §6.4。
- **UUIDv7 专用支持**：**UUIDv7 就是一个 `Guid`**，而 `Guid` 已是本库的受支持基元（`SupportedPrimitiveTypes` 首项；JSON / MongoDB / MessagePack / Dapper / EF 全部按 `Guid` 统一处理，库内**没有**任何把 `Guid` 当作 v4 的特判）→ **类型层面零成本**，用户写 `OrderId.Create(Guid.CreateVersion7())` 即可（.NET 9+）。唯一缺口是 .NET 8 没有 `Guid.CreateVersion7()`，需自行实现 RFC 9562（约 20 行）——这是**用户侧一行代码**的事，不值得本库为此改生成器或加基元类型。**实测补充**：BCL 的 `Guid.CreateVersion7()` 用的是 RFC 9562 **Method 2（随机 rand_a）**，固定时间戳连发 50 次只有 25/49 递增 → **同毫秒内不保证单调**，「v7 天然有序」只在**跨毫秒**成立。这一点在评估该方向时被误当作「单调性保证」，据此不实施。
- **`string` 基元的字符串操作扩展**：**`Value` 是 `IStronglyTypedId<TSelf, TPrimitiveId>` 的公开成员**，用户写 `id.Value.StartsWith("ORD-")` 即可——解包成本是**一个属性访问**，不构成值得开一个扩展类别的理由。且该方向的**变换类成员大半已被现有能力覆盖**：`Normalizer`（生成时做 trim / 小写化等）、`Prefix`（前缀处理）已经在生成期解决，运行时再提供 `Trim` / `ToLowerInvariant` / `Replace` 属于重复造轮子。查询类成员（`Contains` / `StartsWith`）在 .NET Core 2.1+ 的 `string` 重载**本就是序数比较**，与本库 `string` 基元 Id 的既定序数口径一致，无需本库再包一层。**结论**：不实施；若将来确有高频场景，用户自行写一个扩展方法即可，成本同样很低。

---

## 9. 里程碑与验收标准

> 原 M1（`New()` 工厂）经评估后取消，不再实现。已交付的轮次列在表首，与待实施项同表对照。

| 里程碑 | 状态 | 内容 | 交付物 | 验收标准 |
| --- | --- | --- | --- | --- |
| **上一轮** | ✅ 已交付 | 缺失的安全可达 CodeFix（STIA0019 / STIA0020 / STIA0021 / STIA0024）+ 命名规则可配置（`.editorconfig` 后缀） | 4 条 CodeFix + `dotnet_diagnostic.STIA0015.suffix` | ✅ 附带修复的规则由 17 条增至 21 条；后缀默认 `Id`，诊断消息带实际后缀 |
| **M0** | ✅ 已交付 | 两项 P0 缺陷修复（§4.1 / §4.2） | STJ 生成器按基元直写；Newtonsoft 顶层转换器 | ✅ ① 跨程序集双工程回归：STJ 与 Newtonsoft 两侧输出同为字符串形态<br>② 源生成 `JsonSerializerContext` 场景下，未登记基元类型也能往返（自动化落在**生成器层断言**与**逐基元字节对比**：STJ 源生成器 .NET 9+ 才内置在引用包里，net8.0 没有，双目标工程拿不到它）<br>③ JSON 输出对既有快照**逐字节不变**<br>④ 全部既有测试通过 + 新增回归用例（2519/2519） |
| **M1** | ✅ 已交付 | 分析器一键静音（§5.1） | 分析器静音开关 + `buildTransitive` props | ✅ ① 属性 `false` → 零诊断；`true` / 缺失 → 行为不变<br>② 诊断 ID 与数量不变（26 条）<br>③ **打包级 + 端到端**验证：真实 NuGet 包被消费项目装上后，属性确实进 `build_property` 且诊断归零 |
| **M2** | ⬜ 待实施 | GraphQL 集成（§5.2） | `HotChocolateCodeGenerator` + 枚举位 | ① 集成测试往返通过<br>② 枚举口径写进中英文档<br>③ 门控 `HotChocolate.dll` ≥ 13；未引用时不产出任何代码 |
| **M3** | ⬜ 待实施 | Protobuf 集成（§5.3） | 核心生成器 + surrogate 类型 | ① protobuf-net 往返测试<br>② 外部程序集里的 Id 明确**不**加 surrogate（避免 CS0426）<br>③ 载荷口径与 JSON 口径的差异写进文档 |
| **M4** | ✅ 已交付 | Minimal API schema + Bogus（§6.2 / §6.3） | `IOperationFilter` + `Testing.Bogus` 包 | ✅ ① `Sample4` 的 Swagger 输出从 `type: string` 变为 `type: string, format: uuid`（端到端回归落在新工程 `Len.StronglyTypedId.Swagger.Test`）<br>② Bogus 包独立发布，`Len.StronglyTypedId.Testing` 的依赖里没有 `Bogus`（包级断言）<br>③ 顺带修掉 `MapType` 不幂等（重复调用抛重复键异常） |
| **M5** | ⬜ 待实施 | 程序集默认值 + 确定性生成（§6.4 / §6.5） | 运行时属性 +3；`IDeterministicRandom` | ① 逐类型特性仍能覆盖程序集默认<br>② 同一 seed 在不同运行时产出**相同序列**<br>③ CHANGELOG 写明 `Any.Id<T>()` 取值可能变化 |
| **M6** | ⬜ 待实施 | ULID / 可插拔基元（§5.4） | 「基元能力模型」设计 + 实现 | ① 先出设计文档再动代码<br>② 现有 25 种基元的产出**零变化**（快照对比）<br>③ 公开接口约束不破坏（`IComparable` 保持） |
| **M7** | ⬜ 待实施 | YamlDotNet / CBOR / 更多 ORM（§6.6） | 按需逐个接入 | 每个集成独立走「7 落点 + 端到端往返 + 口径文档」 |

---

## 10. 风险与待定

### 10.1 风险登记

| # | 风险 | 触发条件 | 应对 |
| --- | --- | --- | --- |
| R1 | **分析器静音属性未暴露** → 开关静默失效，用户以为静音了其实没静音 | `buildTransitive` props 没进包，或 `CompilerVisibleProperty` 声明写错 | M1 验收要求**打包级**验证；文档给出「如何自检」的说明 |
| R2 | **STJ 直写与默认输出不一致** → JSON 契约悄悄变化 | 浮点 / `decimal` / 枚举 / `char` 的写出格式与 STJ 默认有细微差别 | M0 验收要求对**每种基元**做「与 STJ 默认输出逐字节对比」的契约测试 |
| R3 | **Newtonsoft 转换器移出 Id 改变了声明程序集的行为** | 现有用户升级后 Newtonsoft 序列化形态变化 | 默认仍保留内嵌版（零配置捷径），只**新增**顶层版供消费方注册；或在 CHANGELOG 明确列为行为变更 |
| R4 | **GraphQL 与 REST 的枚举口径不一致** → 契约分裂 | HotChocolate 默认按成员名，本库按底层整数 | M2 动手**前**先定口径（§10.2），并写进中英文档 |
| R5 | **方向排序与真实需求错配** | 本项目外部用户极少（stars 1 / forks 1 / watchers 1），13 条 issue 多为 dependabot | 优先做**实测复现**的缺陷，其次**参考项目 issue 佐证**的方向；纯推断方向一律排在 T2/T3 |
| R6 | **ULID 触及核心重构** → 影响所有基元路径 | 改动 `SupportedPrimitiveTypes` + 能力判据 | 独立里程碑 M6；先出「基元能力模型」设计；用快照对比守住既有 25 种基元的产出 |
| R7 | **性能门禁误报** | 新代码路径的分配量超出 +10% 容差 | 新代码不进热路径；若确实需要，重写基线并在同一提交里说明原因 |
| R8 | **拆包类变更影响打包流水线** | §7 的「生成器与分析器拆包」改动 nuspec 补丁链 | 该方向已排在 T3，且 M1 的静音开关已覆盖其大部分使用场景 |

### 10.2 待定问题（落地前需拍板）

**M1**
- 静音粒度：整包一个开关就够，还是再给一个「只保留 CodeFix、不要诊断」的更细粒度开关？
- `buildTransitive` props 放主包还是 `Len.StronglyTypedId.Generators` 包？（后者随依赖链自然流到消费者，但语义上「装分析器的包」更贴切）

**M2**
- GraphQL 优先适配 HotChocolate，还是同时考虑 StrawberryShake / 原生 `GraphQL.NET`？
- 枚举 scalar 跟 HotChocolate 默认（成员名）还是跟本库既定口径（底层整数）？

**M3**
- protobuf-net 的 `Surrogate` 载荷用文本还是二进制？与 MessagePack / BSON 的口径要不要统一？

**M4**
- ~~Minimal API 的 schema 补齐走 Swashbuckle 的 `IOperationFilter`，还是改走 `ApiDescription` 的 `ParameterDescriptor.Type`？~~ **已定**：走 `IOperationFilter`（第九轮落地）。

**M5**
- 确定性 Id 生成要不要**替换**现有 `Random` 默认实现，还是作为 `AnyIdOptions` 的新选项（如 `Deterministic = true`）保留两者？

**M6**
- ULID 走文本基元还是二进制基元？

**全局**
- 是否将本路线图同步进 `CHANGELOG` 与文档站的「未来规划」页？

---

## 11. 附录

### 11.1 技术预研：MSBuild → 分析器的属性暴露通道

**结论**：`build_property` 通道成立，且是「分析器一键静音」的技术基础。已用最小复现工程（临时工程 + 30 行探测分析器 + 三态对照，验证后清理）实测闭环。

```
消费方 csproj: <StronglyTypedIdEnableAnalyzers>false</StronglyTypedIdEnableAnalyzers>
     ↓  需由 <CompilerVisibleProperty Include="…" /> 显式放行
SDK 生成的 .GeneratedMSBuildEditorConfig.editorconfig: build_property.StronglyTypedIdEnableAnalyzers = false
     ↓  分析器从 AnalyzerConfigOptionsProvider.GlobalOptions 读取
分析器在 RegisterCompilationStartAction 起点判断，为 false 时直接 return（不注册任何诊断动作）
```

**三态对照实测**

| 属性取值 | 分析器行为 | 向后兼容 |
| --- | --- | --- |
| `false` | 完全静音，不报任何诊断 | — |
| `true` | 正常报诊断 | ✓ |
| 缺失（默认） | 正常报诊断 | ✓ 默认不变 |

**四个必须知道的坑**

1. **SDK 不会注入任意 MSBuild 属性。** 实测：自定义属性 `SomeCustomProbeProp` 不会出现在 `build_property.*` 里——只有 SDK 内建的 23 条（`TargetFramework`、`RootNamespace`、`ProjectDir` 等）。要暴露自定义属性，**必须**显式声明 `<CompilerVisibleProperty Include="X" />`。→ **库要替用户做这一步**：`buildTransitive` 的 props 声明这个 item，用户只设属性值；`buildTransitive` 会沿依赖链继续向下流到最终消费者。
2. **`CompilerVisibleProperty` 是 item，不是分号分隔的属性字符串**——写法是 `<CompilerVisibleProperty Include="X" />`。
3. **API 名是 `AnalyzerConfigOptionsProvider.GlobalOptions`，不是 `GlobalConfig`。** 后者不存在（`Microsoft.CodeAnalysis.Common` 5.9.0 的 XML 文档里只有 `GlobalOptions`）。
4. **`DiagnosticSeverity.Info` 的诊断在 `dotnet build` 输出里默认不显示。** 验证「静音是否生效」必须用 `Warning` 级或 `/warnaserror`，否则会得到「两种情况都没诊断」的假阴性。

### 11.2 实测复现记录

| 编号 | 场景 | 结果 | 结论 |
| --- | --- | --- | --- |
| E1 | 跨程序集：Lib 不引用 Newtonsoft，App 引用 | STJ → `"3fa85f64-…"`；Newtonsoft → `{"Value":"3fa85f64-…"}` | §4.1 的缺陷成立 |
| E2 | 源生成 `JsonSerializerContext`，未登记基元类型 | `NotSupportedException: JsonTypeInfo metadata for type 'System.Guid' was not provided` | §4.2 的成因成立 |
| E3 | 同上，但登记 `[JsonSerializable(typeof(Guid))]` | 正常输出 `"3fa85f64-…"` | 照 `aot.md` 走是通的 → 是易用性/架构问题而非崩溃 |
| E4 | 源生成产物检查 | STJ 源生成器**自动**取到了本库的嵌套转换器（`ExpandConverter(typeof(OrderId), new OrderId.SystemTextJsonConverter(), options)`） | 用户不需要额外接线，转换器自给自足即可 |
| E5 | `Guid.CreateVersion7()`（net10.0） | 可用，输出 `01a1720f-e069-7fca-…`，version nibble 正确 | v7 **无需本库支持**（就是 `Guid`）；net8.0 需自实现，见 §8 |
| E6 | `string` 基元 Id 的 `GetHashCode` / `Equals` / 字典键 | 相等 / 为真 / 可用 | 参考项目 #49 在本项目**不成立**（用 record 合成相等性） |
| E7 | `CompilerVisibleProperty` 与 `build_property` 注入 | 自定义属性需显式声明才被注入 | §11.1 的坑 1 成立 |
| E8 | `AnalyzerConfigOptionsProvider` 的属性名 | 只有 `GlobalOptions`，无 `GlobalConfig` | §11.1 的坑 3 成立 |
| E9 | STJ 源生成器对**当前编译声明**的类型 | 走 fast-path（`JsonObjectInfoValues` + `SerializeHandler`），**不取**本库生成器补在 partial 声明上的 `[JsonConverter]` → Id 被按属性写成对象形态 | **既有行为，与本库无关**；`ApplyTo` 是与源生成器决策无关的那条路 |
| E10 | STJ 源生成器对**引用程序集**里的类型 | 退回「查特性」路径（`ExpandConverter(typeof(OrderId), new OrderId.SystemTextJsonConverter(), options)`）→ 转换器生效 | 跨程序集场景下转换器会被调用 → M0 的修复正是在这条路径上验证的 |
| E11 | `JsonSourceGenerationMode.Metadata` vs 默认 | `Metadata` 同样走按属性反射那条路，特性不被取到；默认模式会取 | 两种模式的差别已写进 `docs/aot.md`（中英） |
| E12 | STJ 源生成器的来源 | .NET 9+ 内置在**引用包**（`Microsoft.NETCore.App.Ref/*/analyzers`）里；net8.0 的引用包没有 | 双目标工程拿不到它 → 源生成 context 的回归测试只能落在 net10.0-only 的工程或生成器层 |
| E13 | NuGet 对 `buildTransitive` 的导入规则 | **只自动导入文件名与包 ID 同名的 `.props` / `.targets`** | 端到端才暴露：props 叫 `Len.StronglyTypedId.props` 而包 ID 是 `Len.StronglyTypedId.Generators` → 包里带着它、NuGet 却不导入 → 属性不进 `build_property` → **开关静默失效**（设了不报错、也不生效）。打包级测试只查「条目在不在」发现不了，必须真实消费一次 |
| E14 | Minimal API 参数在 `ApiExplorer` 里的两副面孔 | `ApiParameterDescription.Type` 是**真实的 Id 类型**，`ModelMetadata.ModelType` 已被框架换成 `System.String` | Swashbuckle 据后者生成 schema → 路径 / 查询参数只剩裸 `type: string`，`MapType` 无从下手。补救办法是按 `Type` 查表（操作过滤器） |
| E15 | `MapType` 的底层语义 | `CustomTypeMappings.Add(type, factory)` —— **Add，不是索引器赋值** | 重复调用 `ApplyTo(SwaggerGenOptions)` 抛 `An item with the same key has already been added`。既有缺陷，由「ApplyTo 必须幂等」的验收标准暴露 |
| E16 | Swashbuckle 6 → 10 的过滤器 API 差异 | 6.x 的 `SwaggerGenOptions.OperationFilters`（`IList<IFilterMetadata>`）在 10.x 变成 `OperationFilterDescriptors`；但 `SwaggerGeneratorOptions.OperationFilters`（`List<IOperationFilter>`）**两版都有** | 幂等检查放在 `SwaggerGeneratorOptions.OperationFilters` 上，两版通用；且 `filter is X` 的写法不必写出接口名（1.x 在 `Microsoft.OpenApi.Models`，2.x 提到了根命名空间） |
| E17 | Bogus v35 的扩展点 | **没有** 老版本的 `IRandomizerExtension` 机制；真正的入口是 `Faker<T>.RuleForType(Type, Func<Faker, TType>)` | 它让 Bogus 按**类型**填值 → 适配层只要为每个 Id 类型登记一条规则，`Generate()` 就自动填上 Id 属性 |
| E18 | `Faker<T>` 的实例化要求 | 默认走 `Activator.CreateInstance<T>()` → 目标类型必须有**无参构造** | 用 record（只有带参构造）当宿主会抛 `MissingMethodException: No parameterless constructor defined`。适配包的测试宿主因此用「类 + 可写属性」 |
| E19 | UUIDv7 的时间序性质（net8.0 + net10.0 双目标实测） | v7 的 48 位时间戳落在 `Guid` 大端字节序的**高位** → 跨毫秒 `CompareTo` 天然有序（两个 TFM 均验证）；但 BCL 的 `Guid.CreateVersion7()` 用 **RFC 9562 Method 2（随机 rand_a）**——固定时间戳连发 50 次仅 25/49 递增 → **同毫秒内不保证单调** | 印证 §8 的结论：v7 就是 `Guid`，**类型层零成本**；且「v7 有单调性保证」这一原有论据**不成立**（Method 2 而非 Method 1）→ UUIDv7 方向整体移除 |

### 11.3 外部信号与参考项目 issue 对照

**同名参考项目 Andrew Lock `StronglyTypedId` 的 100 条 issue**（2026-10-25 拉取，按评论数排序；该项目与本库同名、独立维护，**两者互不隶属**，此处仅作为同类需求信号的参考）：

| 参考项目 issue | 热度 | 本项目对应 |
| --- | --- | --- |
| #61 领域层被基础设施绑死 | 9 评论 / open | §4.1（本项目已解决 EF/Dapper/MessagePack/MongoDB，残留 Newtonsoft） |
| #58 OpenAPI 规范不正确 | 7 评论 / open | §6.2（Minimal API 参数） |
| #141 为 `JsonSerializerContext` 指定转换器 | 5 评论 / open | §4.2（症状不同、成因相同） |
| #128 string 基元用不了字符串函数 | 3 评论 / open | **不需要**（`id.Value.StartsWith(...)` 已够用；见 §8） |
| #142 支持 UUIDv7 | 6 评论 / closed | **不需要**（v7 就是 `Guid`，类型层零成本；见 §8 与实测 E14） |
| #151 AutoMapper 用法 | 1 评论 / open | §6.1 |
| #49 string 基元 `GetHashCode` 失效 | 3 评论 | **不成立**（实测 E6） |
| #29 不能作字典键 | 7 评论 | **不成立**（既有能力） |
| #14 补整数变体 | 4 评论 / open | **已支持** |
| #100 Id 前缀 / 后缀 | 2 评论 | **已支持** |
| #60 MongoDB / MessagePack 集成 | 3 评论 | **已支持** |
| #153 嵌套 StronglyTypedId | 8 评论 | **已支持** |

**本仓库自身的 issue（13 条）**：几乎全是 dependabot 的依赖升级 PR，无功能请求。**本项目目前几乎没有外部用户**（stars = 1、forks = 1、watchers = 1）→「按社区需求排序」在当前阶段**缺乏一手数据**，只能借参考项目与同类的需求信号外推。这也是把重心放在「实测复现」而非「统计票数」的原因（见风险 R5）。

**LearnStack ADR-0023（2026-08-29 修订）**：选 Vogen 而非 StronglyTypedId，三条理由——值对象覆盖、**OpenAPI schema 需逐类型 `MapType`**、维护节奏放缓。① OpenAPI 那条**本项目已解决**（自动 `ApplyTo`），但 **Minimal API 参数的残余缺口**（§6.3）正是同源问题，且被外部点名过 → 优先级提高。② 值对象覆盖 → 本库用 `Validator`/`Normalizer` 已能覆盖，属**文档澄清**机会而非转向（§7）。③ 维护节奏是本项目当下的差异化点。

**生态对比文章（2026）**：「两个库都是 MIT、都是源生成器，StronglyTypedId 更聚焦、门槛更低；Vogen 面向值对象」→ 佐证**定位清晰**本身是卖点，不必向 Vogen 的范围靠。

**Vogen 生态实践**：多个项目已把 Vogen 接进「Bogus customization」→ 佐证 §6.3 的 Bogus 集成方向。

**Cysharp `Ulid` 源码**：同时实现 `IComparable<Ulid>` 与非泛型 `IComparable` → 佐证 §5.4 的结论：加 ULID **不需要**破坏 `IStronglyTypedId<,>` 的公开约束。

> 说明：以上均为第三方来源，采纳前按 §10.2 逐条确认；优先级排序以本项目自身的代码事实与实测结果为准。

### 11.4 变更记录

| 轮次 | 内容 |
| --- | --- |
| 一轮 | 现状基线 + 方向初筛（GraphQL / Protobuf / ULID / YamlDotNet / CBOR / 更多 ORM / 反向重构 / CodeFix / 命名规则 / DebuggerDisplay / 值对象转向 / OpenTelemetry / 快照测试），并列出「不建议做」 |
| 二轮 | 补入代码级落点（集成 7 位置、门控与幂等约定、基元判据分布）、新增「分析器一键静音 / 程序集级默认值补齐 / 确定性 Id 生成」三方向、修正 ULID 可行性 |
| 三轮 | 「分析器一键静音」完成技术预研并附实测结论与四个坑；新增 Minimal API schema / Bogus / 性能子类 / 定位澄清；引入外部信号表 |
| 四轮 | 以「参考项目 100 条 issue + 可复现实验」做需求挖掘：新增 2 条实测复现的 P0 缺陷、3 条 P1 方向、6 条已排除疑点 |
| 五轮（本次） | **重构为规划文档**：新增目标与范围、成功标准、风险登记、验收标准、证据强度分列；把技术预研与实测记录下沉为附录；章节顺序调整为「目标 → 现状 → 总览 → 分层详述 → 里程碑 → 风险 → 附录」 |
| 六轮 | **M0 落地**：两项 P0 缺陷修复完成（§4 标记为已实现），新增「基元 → 直写 / 直读表达式」映射表 `PrimitiveJsonIo`、顶层 Newtonsoft 转换器、逐基元字节对比回归、跨程序集回归工程 `Len.StronglyTypedId.TestBase.Layered`；补 E9–E12 四条实测记录（STJ 源生成器的 fast-path / 特性取舍、`GenerationMode` 差别、源生成器的来源）；`docs/aot.md` 与 `docs/serialization.md`（中英）同步 |
| 七轮 | **M1 落地**：分析器一键静音（§5 标记为已实现）—— 分析器读 `build_property.StronglyTypedIdEnableAnalyzers`，为「否」时一个诊断动作都不注册；随包发布 `buildTransitive/Len.StronglyTypedId.Generators.props` 替使用者放行属性；补 E13（NuGet 只导入与包 ID 同名的 props —— 端到端才暴露的静默失效） |
| 八轮 | **UUIDv7 方向整体移除**：确认 v7 就是 `Guid`、类型层零成本（库内无任何 v4 特判），且 net8.0 缺 `Guid.CreateVersion7()` 属用户侧一行代码的事 → 不实施（§8 增第三条否定结论）；实测发现 BCL 的 `CreateVersion7()` 用 RFC 9562 **Method 2**、**同毫秒内不保证单调**（原「单调性保证」论据不成立），补 E19；总览表收紧为 20 项、§6 小节顺延为 6.1–6.7、M5 里程碑改为只含 string 字符串操作 |
| 九轮 | **M4 落地**：Minimal API 参数 schema + Bogus 集成（§6.2 / §6.3 标记为已实现）—— Swashbuckle 侧生成 `IOperationFilter`，按参数的真实类型把 schema 补回；新建 `Len.StronglyTypedId.Testing.Bogus` 包（`Faker<T>.RuleForType` 路线）；新工程 `Len.StronglyTypedId.Swagger.Test` 跑真实的 ASP.NET Core 管道做端到端回归；顺带修掉 `MapType` 不幂等的既有缺陷；补 E14–E18 五条实测记录 |
| 十轮 | **string 字符串操作扩展方向移除**：`Value` 是 `IStronglyTypedId<,>` 的公开成员，`id.Value.StartsWith(...)` 的解包成本仅是一个属性访问；且变换类成员大半已被 `Normalizer` / `Prefix` 在生成期覆盖，查询类成员在 .NET Core 2.1+ 本就是序数比较（与本库 `string` 基元的序数口径一致）→ 不实施（§8 增第四条否定结论）。总览表收紧为 **19 项**、§6 小节顺延为 **6.1–6.6**、里程碑顺延为 **M5–M7**（原 M6/M7/M8 依次前移）、§11.3 的 #128 行改「不需要」。顺带修掉上一轮遗留的 **E14 编号重复**（UUIDv7 那条改为 E19），并清理 §10.2 中第九轮已定案的待定项 |
