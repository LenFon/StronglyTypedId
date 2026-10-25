# StronglyTypedId 发展规划（Roadmap）

> 本文档梳理 StronglyTypedId 的当前能力基线，并给出可作为**新增功能**的发展方向，按优先级分层（T1 短期高价值 / T2 中期稳收益 / T3 战略长尾）。
> 最后单列「不建议做的方向」，避免与库的设计理念冲突。
>
> 适用版本基线：.NET SDK 10.0.401（Roslyn 5.9），运行时 `net8.0` / `net10.0`。

---

## 1. 现状基线（已具备的能力）

**核心生成**
- 一行声明 `[StronglyTypedId]`（`partial record` / `partial record struct`），生成 `IStronglyTypedId<TSelf, TPrimitiveId>` 实现。
- 解析/格式化：`IParsable<TSelf>`、`ISpanParsable<TSelf>`、`IFormattable`、`ISpanFormattable`（按基元是否实现）。
- 比较与排序：`IComparable<TSelf>` + 四个比较运算符。
- 校验 `Validator`、异常类型 `ExceptionType`、归一化 `Normalizer`、命名实例 `StronglyTypedIdInstance`、`TryCreate`、UTF-8 接口（`IUtf8SpanFormattable` / `IUtf8SpanParsable`）、`TypeConverter`、`Prefix` 文本前缀、程序集级默认值 `StronglyTypedIdDefaults`、逐 id 集成过滤 `Integrations`。
- 运行时反射助手、Native AOT 与裁剪兼容、零第三方依赖。

**序列化**
- `System.Text.Json` + `Newtonsoft.Json`（≥13.0.0）：字典键、JSON 形态可配置（`JsonShape`）、显式注册入口、前缀贯穿文本路径。

**集成（7 个，仅在引用对应包时生成）**
- Entity Framework Core（≥7.0.0）、Dapper（≥2.0.0）、Swashbuckle（≥6.0.0）、ASP.NET Core MVC（含 Minimal API 绑定 + 路由约束，≥2.0.0）、ASP.NET Core OpenAPI（≥9.0.0）、MessagePack（≥2.0.0）、MongoDB.Bson（≥3.0.0）。

**分析器与 DX**
- 26 条诊断（STIA0000–STIA0025），其中 21 条带 CodeFix；正向重构 `StronglyTypedIdRefactoringProvider`。
- 诊断本地化为中英双语。

**测试与生态**
- `Any.Id<T>()`（`Len.StronglyTypedId.Testing`）、AutoFixture 集成、DocGen 文档工具、性能门禁与基准工程。
- 4 个 NuGet 包 + 文档站 + 中英双语 README/CHANGELOG。

---

## 2. 评估原则

1. **贴合近期轨迹**：最近的提交集中在「扩基元 + 二进制集成（MessagePack / MongoDB）+ AOT 零反射」，新功能优先复用这条主线。
2. **增量、零破坏**：新增集成/基元默认走「仅当引用对应包/类型时生成」，不影响现有用户。
3. **守住类型安全卖点**：不强加隐式转换、不放开任意自定义基元（与 `Uri` 被刻意排除同理）。
4. **复用现有生成骨架**：每个集成都是 `IIntegrationCodeGenerator` + 程序集闸门 + `StronglyTypedIds.ApplyTo(...)` 入口，`docs/integrations.md` 的「仅当引用时生成」范式可直接套用。

---

## 3. 发展方向

### T1 · 短期高价值（建议优先落地）

| 方向 | 价值 | 可行性 | 落地要点 |
| --- | --- | --- | --- |
| **GraphQL 集成（HotChocolate）** | 中高 | 中 | 生成 scalar 注册入口（如 `ApplyTo(IRequestExecutorBuilder)` 或 `ISchemaTypeOptions`）。当前集成列表完全空缺 GraphQL。 |
| **Protobuf 集成** | 中高 | 中 | 优先 protobuf-net 的 `Surrogate` 模式（最省事），或 Google.Protobuf 的自定义序列化钩子。覆盖 gRPC / 微服务。 |
| **ULID / 新基元（放宽类型白名单）** | 高 | 中 | 把 `SupportedPrimitiveTypes.cs` 的封闭白名单改为「可插拔的类型+格式化器表」，先支持 `Ulid`（第三方包）或 `byte[]`。需同步改每个序列化/集成分支的基元判据。 |

**T1 选择建议**：GraphQL 是高频空缺，与现有「7 集成」范式完全一致，可作为首个落地项；Protobuf / ULID 视社区需求排序。

### T2 · 中期稳收益（增量改动）

| 方向 | 价值 | 可行性 | 落地要点 |
| --- | --- | --- | --- |
| **补齐安全可达的缺失 CodeFix** | 中 | 高 | 现有 26 条中 `STIA0015/0016`（命名/重复）与 `STIA0023`（前缀冲突）是故意不给；但 `STIA0019`（请求了未引用集成）、`STIA0020/0021`（命名实例名/值非法）、`STIA0022`（PascalCase）、`STIA0024`（掩码未定义位）都有安全机械修复，直接补进 `StronglyTypedIdCodeFixProvider`。 |
| **YamlDotNet / CBOR 序列化** | 中 | 中 | YAML 序列化器（字符串↔id）；`System.Formats.Cbor` 二进制（IoT / CoAP 场景）。 |
| **NHibernate / linq2db 集成** | 中 | 中 | 扩展 ORM 覆盖面；建议先评估采用率再挑 1–2 个。 |
| **反向重构（裸基元 → 强类型 Id）** | 高（DX 卖点） | 低（跨文件、工作量大） | 与现有正向 `StronglyTypedIdRefactoringProvider` 互补：扫描某 `Guid`/`int` 字段或参数并替换为 id，同时生成类型。 |
| **命名规则可配置** | 中 | 高（小增强） | `STIA0015`（必须以 `Id` 结尾）目前硬编码，改为允许 `.editorconfig` 配置后缀。 |
| **`DebuggerDisplay` / `[DebuggerTypeProxy]`** | 中（DX） | 高（零风险） | 生成代码加调试可视化。 |

### T3 · 战略 / 长尾（需先评估定位）

| 方向 | 价值 | 说明 |
| --- | --- | --- |
| **广义值对象（Vogen 路线）** | 高但转向大 | 把「强类型 id」扩成「强类型值对象」（Money/Email/…）。属产品定位重大转向，建议单独论证，不混入当前库。 |
| **OpenTelemetry 日志脱敏** | 中低 | 把 id 作为 span 属性时自动格式化/脱敏，呼应已有的 `RedactValidationMessage` 安全取向。 |
| **更多示例 / 快照测试** | 中 | 扩充 `samples/`、`Verify` 覆盖，降低上手成本。 |

---

## 4. 不建议做的方向（与理念冲突）

- **隐式转换运算符**（`implicit operator Guid(OrderId)`）：直接削弱「类型安全」核心卖点，社区大概率反对。
- **任意自定义类型作基元**：与 `Uri` 被刻意排除同理——某些类型 `Equals` 语义会让不同值塌缩成同一 id，违背设计初衷。
- **泛型容器内 id 的外部集成**：Roslyn / CS0416 硬限制（已在 `docs/limitations.md` 写明），短期无解，只能让用户对闭合类型手动注册。

---

## 5. 建议落地顺序（里程碑）

> 原 M1（`New()` 工厂）经评估后取消，不再实现。

1. **M1 — GraphQL 集成**：补齐高频框架空缺，复用现有集成范式。
2. **M2 — 缺失 CodeFix + 命名规则可配置 + DebuggerDisplay**（已实现）：STIA0019/STIA0024/STIA0020/STIA0021 已补齐代码修复，`STIA0015` 后缀支持 `.editorconfig` 配置（`dotnet_diagnostic.STIA0015.suffix`），`DebuggerDisplay` 生成器早已具备；附带 CodeFix 的规则由 17 条增至 21 条。一轮 DX 提质，吃现有分析器架构。
3. **M3 — 基元扩展（ULID/可插拔白名单）**：较大的核心改动，需在 M1–M2 之后单独设计。
4. **M4 — Protobuf / YamlDotNet / CBOR / 更多 ORM**：按社区需求排序，逐个增量接入。

---

## 6. 待定问题（落地前需确认）

- ULID 走第三方 `Ulid` 包（类型名匹配）还是推动可插拔白名单？（后者更通用但改动更大）
- GraphQL 优先适配 HotChocolate 还是同时考虑 StrawberryShake / 原生 `GraphQL.NET`？
- 是否将本路线图同步进 `CHANGELOG` 与文档站的「未来规划」页？

---

_文档生成于 2026-10-25，基于仓库当前代码与文档快照。_
