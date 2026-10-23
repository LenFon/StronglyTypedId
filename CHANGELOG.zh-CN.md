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
  重试，而不是表现为测试失败。重试同样认得自定义 `ExceptionType`；验证器抛出的其它异常则原样冒泡。
- `Len.StronglyTypedId.Testing.AutoFixture`：`fixture.WithStronglyTypedIds()` 通过同一套生成器填充
  强类型 Id，因此 AutoFixture 创建的对象图同样遵守验证器。
- `IStronglyTypedId<TSelf>` —— Id 契约的单参数视图，泛型代码可以用「某个强类型 Id」作约束，而不必
  指明基元类型。当基元实现 `IFormattable` 时会生成 `IFormattableStronglyTypedId<TSelf>`。
- 新增九种基元类型——`char`、`Int128`、`UInt128`、`Half`、`float`、`double`、`DateOnly`、`TimeOnly`
  与 `BigInteger`——连同 `enum` 共 24 种受支持类型。`char` 的解析与格式化成员都是显式接口实现，
  故生成器改为发射长度判定与接口调用，而不是 `char.TryParse` / `char.ToString(format, provider)`。
- `Normalizer` —— 指向 `static TPrimitiveId Normalize(TPrimitiveId value)` 成员，即可在校验与落库
  之前修正传入值（去空白、统一大小写、去分隔符）。每个入口都按「归一化 → 校验 → 构造」执行，
  并可用 `[assembly: StronglyTypedIdDefaults(Normalizer = ...)]` 设置程序集级默认。引用不合法时
  报告 `STIA0017`，代码修复可生成直通桩方法或删掉该实参。
- `ExceptionType` —— 指向自定义异常，即可改变校验拒绝取值时 `Create` 抛出的异常。该类型必须派生自
  `System.Exception`、不能是抽象类，并具有可访问的、接收单个 `string` 消息的构造函数；否则报告
  `STIA0018`，代码修复可删掉该实参。`Parse` 抛的是 `FormatException`，因为它的失败还包含「文本根本
  不是 Id」。可用 `[assembly: StronglyTypedIdDefaults(ExceptionType = ...)]` 设置程序集级默认。
- `RedactValidationMessage` —— 在校验拒绝消息中去掉被拒绝的原始值，消息变为
  `The value is not a valid OrderId.` 而不是 `The value '...' is not a valid OrderId.`。那段原文会随
  异常进入日志、APM 与错误上报，而对 `string` 基元的 Id 来说它就是用户输入。可用
  `[assembly: StronglyTypedIdDefaults(RedactValidationMessage = true)]` 整库打开；逐类型写 `false`
  无法撤销，因为脱敏是安全策略而非逐类型偏好。
- `STIA0023` —— `Prefix` 的首字符也能开启基元自身的文本，导致 `Parse` 分不清前缀与取值。
  `STIA0024` —— `Integrations` 里设置了不匹配任何已知标志的位；此前这些位被静默忽略。
- 文本前缀：`Prefix` 在 `ToString` 中写入固定前缀、在 `Parse` 中将其剥离；`PrefixReadMode` 选择
  接受裸值（`Lenient`）还是拒绝裸值（`Strict`）。
- `JsonShape` 选择序列化形态：裸基元（`Primitive`）或 `{ "value": ... }`（`Object`）。
- System.Text.Json（`StronglyTypedIds.ApplyTo(JsonSerializerOptions)`）与 Newtonsoft.Json
  （`settings.AddStronglyTypedId()`）的注册入口，供显式配置转换器、不依赖生成的 `[JsonConverter]`
  特性的调用方使用。
- 把现有 `record` 转成强类型 Id 的重构（refactoring）提供程序。
- `Integrations` —— 收窄单个 Id 生成哪些集成代码。它是**过滤器**而非开关：只能在「已引用对应包」的
  基础上做减法。`None` 关掉全部集成但保留核心实现，`Dapper | SystemTextJson` 只保留这两类，`All`
  可在程序集默认 `None` 时把单个 Id 捞回来。可用
  `[assembly: StronglyTypedIdDefaults(Integrations = ...)]` 设置默认值，且引用程序集里的 Id 会尊重
  **它自己所在程序集**的默认值。`TypeConverter` 有意不在掩码内。请求了未引用程序集的集成时报告
  `STIA0019`。
- MessagePack 与 MongoDB BSON 集成 —— 生成 `IMessagePackFormatter<TId>` / `IBsonSerializer<TId>`，分别由
  `StronglyTypedIds.ApplyTo(MessagePackSerializerOptions)` 与 `StronglyTypedIds.RegisterBsonSerializers()`
  接上。两者写入的都是基元值本身：文本前缀属于文本形（`ToString()` / JSON），不会进入载荷。
- `[StronglyTypedIdInstance]` 声明命名静态实例（`Unknown` / `Unspecified` / `System` 之类），生成为
  `public static readonly` 字段。值以基元的不变文化文本形书写，在编译期校验并翻译成字面量，再经
  `Create` 构造——因此实例必然经过归一化与验证器，且 `Parse(instance.ToString())` 一定读得回来。
  被验证器拒绝的值会在首次使用时抛 `TypeInitializationException`，而不是悄悄产出一个不可能存在的 Id。
- `STIA0012`–`STIA0019` 诊断，其中包括 `STIA0013`（`Prefix` 为空串或纯空白）、`STIA0014`（带验证器
  或归一化器的 Id 被声明为 `public`，主构造函数仍可直达——`Info` 级别，默认关闭）、`STIA0015`
  （Id 名称应遵循 `...Id` 约定）、`STIA0016`（同一程序集内有两个 Id 使用同一基元）、`STIA0017`
  （`Normalizer` 引用不合法）、`STIA0018`（`ExceptionType` 不可用）与 `STIA0019`（请求的集成未被
  引用）。
- 命名实例的三条诊断：`STIA0020`（名字不是合法标识符、与其他实例重名，或与既有/生成的成员撞名）、
  `STIA0021`（值不是该基元的合法取值）与 `STIA0022`（名字不是 PascalCase，`Info` 级别）。
- 可运行的示例：`SampleConsole`（全功能导览）、`SampleIntegrations`（EF Core + Dapper 跑内存
  SQLite）、`Sample1`（MVC + Swashbuckle 6 + Newtonsoft.Json）、`Sample2`（Minimal API + 内置
  OpenAPI）、`Sample3`（MVC + Swashbuckle 10 / Microsoft.OpenApi 2.x）。

### 变更

- 生成的类型声明不再带 `/// <summary>` 注释。那句话只是把声明本身又说了一遍，而且是整个文件里
  唯一不来自 `/// <inheritdoc/>` 的文档注释，在每个生成文件里都像噪音。
- 其余生成的文档注释改为 C# 惯例写法：`<summary>` 独占一行，正文另起一行，`</summary>` 再占一行。
  此前 `TryCreate`、`Deconstruct`、null 安全比较辅助方法、`TypeConverter` 以及各集成注册方法都是
  单行 `<summary>…</summary>`。纯排版变更，编译产物不变。
- **破坏性变更：** 文本不是合法 Id 时，`Parse` 现在抛 `FormatException` 而不是 `ArgumentException`，
  与 `IParsable<TSelf>` 的惯例（`int.Parse` / `Guid.Parse`）一致。原先在 `Parse` 外层
  `catch (ArgumentException)` 的代码必须改为 `catch (FormatException)`，否则抓不到。`Create` 在校验器
  拒绝取值时仍抛 `ArgumentException`（或配置的 `ExceptionType`），`TryParse` 行为不变。
- **破坏性变更：** 诊断 ID 从 `STIAO###`（字母 O）改名为 `STIA0###`（数字零）。`.editorconfig` 里
  已有的 `dotnet_diagnostic.STIAO011.severity` 条目不再匹配；改名发生在首个稳定版发布之前。
- 当格式提供程序为 `null` 时，`Parse` / `TryParse` 现在回退到不变文化，与 `ToString()` 写出的内容
  一致。此前 `null` 表示当前文化，于是 `Parse(id.ToString())` 在 `de-DE` 等区域下会静默产生错误值
  （`"1.5"` → `15`）。显式传入的 provider 仍按其文化处理。
- Dapper 注册新增无参重载 `StronglyTypedIds.ApplyTo()`。Dapper 的类型处理器存在进程级注册表里，
  `IDbConnection` 实参本来就没有可作用的对象；原有的 `ApplyTo(IDbConnection)` 保留以维持源码兼容，
  内部转发到新重载。两者重复调用都无害 —— Dapper 对同一类型是替换而非追加，注册表不会膨胀。
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
- **对带前缀的 Id 属破坏性变更：** `ToString(string?, IFormatProvider?)` 与 `TryFormat(...)` 在
  `format` 非空时也保留文本前缀。此前会退回裸基元，于是 `$"{id:N}"` 打出 `d8ac85d4…`，而
  `id.ToString()` 打出 `ord_d8ac85d4…` —— 同一个 Id 两种形态，凡涉及格式串的地方前缀就丢了。
  前缀是规范文本形的一部分，现在无条件加上，`format` 只作用于基元。无前缀的 Id 产物逐字节不变。
- 生成器的发现流程会跳过「没有引用 `Len.StronglyTypedId`」的被引用程序集。这类程序集不可能声明 Id
  （那需要写 `IStronglyTypedId<,>`），但此前每个模块都要走一遍完整类型树 —— 光
  `System.Private.CoreLib` 就贡献上万个符号。该判定读 `ReferencedAssemblyNames`，是纯元数据。

### 移除

- 按 Id 类型分别驻留字符串。`Create` / `TryCreate` / `Parse` / `TryParse` 不再把字符串取值送进每类型的
  `ConcurrentDictionary`，因此字符串型 Id 不再承诺「相等取值共享同一实例」。该缓存只在「少量取值被高频
  重复创建」时才划算，代价却是每次工厂调用都多一次字典探测、每 Id 类型在进程生命周期内钉住最多 1024 个
  字符串，且在加上限之前会对无界取值集合无限增长。需要去重请自行使用 `string.Intern` 或在自有层做缓存。

### 修复

- `IsStronglyTypedId` / `GetPrimitiveIdType` / `TryGetPrimitiveIdType` 不再阻止可回收
  `AssemblyLoadContext` 卸载。它们的结果缓存原先是以 `Type` 为键的静态字典，而 `Type` 持有
  `Assembly`、`Assembly` 持有加载上下文 —— 于是每次插件加载、热重载或重复加载程序集的测试宿主，
  都会把该上下文及其全部类型钉在进程生命周期内。缓存现在改用 `ConditionalWeakTable`，记录随键
  一起消失。普通应用代码无感知，只有会卸载程序集的宿主才看得出差别。
- 当一个类型实现多份 `IStronglyTypedId<TSelf, TPrimitiveId>` 时，现在取 `TSelf` 等于该类型自身的
  那份接口来确定基元类型。此前取第一个匹配项，报告的基元类型因此取决于 `Type.GetInterfaces()` 的
  枚举顺序 —— 同一类型在不同运行时可能给出不同基元，而模型绑定器与 EF 转换器正是据此选择转换器。
- OpenAPI 文档现在把声明为 `JsonShape = JsonShape.Object` 的 Id 描述成它实际序列化成的对象 ——
  `{ "value": <基元> }`，`value` 必填 —— 而不是裸基元。两个集成都受影响：Swashbuckle 的 `MapType`
  为这类 Id 发出的是 `type: string, format: uuid`，ASP.NET Core OpenAPI 的转换器同样把框架的 schema
  改写成裸基元。按这两种文档生成的客户端会按纯字符串收发，而服务端读写的是对象，凡涉及该 Id 的调用
  都会失败。内层 `value` 属性沿用常规的基元映射，包括 `Prefix` 规则。
- 以 `string` 为基元的 Id 现在按序数比较，排序因此与相等性一致。`CompareTo` 与 `<`、`>`、`<=`、`>=`
  原先走 `string.CompareTo(string)`，那是区域性敏感的：它把 `"a\u00ADb"` 与 `"ab"` 判为相等，尽管
  `Equals` 认为两者不同；`"I"` 与 `"i"` 的顺序在 `en-US` 与 `tr-TR` 下还会反过来。`SortedSet<T>` 与
  `SortedDictionary<TKey, TValue>` 正是靠 `CompareTo` 判重的，于是两个不同的 Id 会塌成一个条目 ——
  静默的数据丢失，不抛异常也不打日志。生成的比较现在改用 `string.CompareOrdinal`，与 record 合成的
  `Equals` 以及各序列化器写出的不变文化文本保持一致。其它基元的 Id 不受影响：它们的 `CompareTo`
  本就是强类型的，在需要序数语义的地方已经是序数比较。
- 被 `Validator` 拒绝的值在反序列化时现在抛序列化库自己的异常，而不是让 `ArgumentException` 穿透。
  生成的转换器原先调用 `Id.Create(...)`，其 `ArgumentException`（或配置的 `ExceptionType`）会直接穿过
  `JsonSerializer.Deserialize` / `JsonConvert.DeserializeObject` / `MessagePackSerializer` /
  `BsonSerializer` —— 这四个库都不会包装转换器抛出的异常。按文档契约 `catch (JsonException)` 的调用方
  因此完全漏掉这条拒绝路径。转换器现在改走 `TryCreate` 并自行抛出 `JsonException` /
  `JsonSerializationException` / `MessagePackSerializationException` / `BsonSerializationException`，
  与字典键那条路径此前的做法一致。EF Core 的 `ValueConverter` 仍调用 `Create`：从数据库行物化实体属于
  数据完整性边界，那里 `ArgumentException` 是更准确的报告。以 `BigInteger` 为基元的 Id 原先经
  `BigInteger.Parse` 读入非数字载荷，`FormatException` 同样会泄漏；现在改用 `TryParse` 并报告
  `JsonException`。
- JSON 令牌类型不符时现在抛序列化库自己的异常，而不是 `InvalidOperationException`。生成的转换器在处理
  意外令牌的 `switch` 分支里抛 `InvalidOperationException` —— 例如从数字读 `string` 基元的 Id，或
  （Newtonsoft.Json）把 `null` 交给值类型 Id —— 而同文件里所有结构性错误抛的都是 `JsonException` /
  `JsonSerializationException`。按文档契约 `catch (JsonException)` 的调用方因此漏掉这条路径，且异常不带
  序列化库给自己异常类型补的 JSON 路径。两处现在都抛 `JsonException` / `JsonSerializationException`。
- `BigInteger` 基元的 Id 在 System.Text.Json 下会丢值。`BigInteger` 没有内置转换器，生成的转换器落到
  `JsonSerializer.Serialize(writer, primitiveId, options)`，把结构体的公开属性序列化成
  `{"IsPowerOfTwo":false,…,"Sign":1}`，读回来则是 `default` —— 每次往返都静默丢数据。现在读写都走
  不变文化文本，即 `ToString()` 产出的那段文本。
- `Int128`、`UInt128`、`Half`、`DateOnly`、`TimeOnly` 基元的 Id 在 Newtonsoft.Json 下抛
  `JsonWriterException: Unsupported type`。`JsonWriter.WriteValue` 对这些类型没有对应的
  `PrimitiveTypeCode`，按值写直接失败。它们的转换器现在写不变文化文本、经 `Id.TryParse` 读回，
  与带前缀的 Id 一致。
- `Create` 现在与其他成员一样带缩进。模板里的占位符落在原始字符串的去缩进基准列上，首行拿不到缩进，
  于是表达式体重载顶格输出，而块体重载（配置 `Normalizer` 时）是正常的。纯排版问题 —— 两种形态都能
  编译 —— 但每个生成文件在编辑器里看着都是坏的。
- 又有三处生成块的缩进统一了。引用类型 Id 的 `CompareValues` 辅助方法缩进 20 空格，而同级成员是 4 空格；
  EF Core 与 Swagger 的 `ApplyTo` 方法体用制表符缩进（Swagger 的左大括号后还多一个尾随空格），而其余
  生成行一律 4 空格。纯排版问题 —— 两种形态都能编译。
- 枚举 Id 作 `Dictionary` 键时忽略 `JsonSerializerOptions.DictionaryKeyPolicy`：`CamelCase` 策略下写出
  `{"Active":1}`，而原生 `Dictionary<Status, int>` 写出的是 `{"active":1}`。生成的 `WriteAsPropertyName`
  现在应用该策略，`ReadAsPropertyName` 相应改为忽略大小写解析，被改写的键仍能读回。非枚举基元刻意不动：
  它们的键文本**就是**值本身，改写等于让键读不回来。
- Swagger 与内置 OpenAPI 转换器现在按 JSON 的真实形态描述 `decimal`、`double`、`float`、`Half`、
  `Int128`、`UInt128`、`BigInteger`、枚举与日期时间基元。此前 `decimal` 被声明成 `string`（Swagger）
  或 `integer`（OpenAPI），枚举被声明成 `string`，浮点落到字符串默认分支，导致生成的客户端按错误类型
  反序列化。两个生成器现在共用同一张映射表。
- `{id:OrderId}` 路由约束不再阻断链接生成。`RouteDirection.UrlGeneration` 阶段路由值是 Id 实例而非
  字符串，约束里的 `is not string` 判断把它当成永久不匹配，这类路由因此永远生成不出 URL。现在先用
  `Convert.ToString(..., InvariantCulture)` 归一，与框架内置约束一致。
- 枚举基元的 `Parse` / `TryParse` 不再接受未定义数值（`"999"`）、逗号组合（`"1,2"`）等
  `Enum.TryParse` 恰好放行的文本：解析成功后追加 `Enum.IsDefined` 校验。`[Flags]` 枚举用不了这项校验
  （位组合本身就是合法取值），改为按「没有任何成员占用的位」掩码校验：`Read | Write` 通过，`999` 被拒。
  前后空白照旧被裁剪，与其它 BCL 解析器一致。
- 嵌套在其他类型内部的 Id 开启 `[StronglyTypedId(TypeConverter = true)]` 后，不再生成指向不存在类型的
  `typeof(...)`。此前特性实参由「命名空间 + Id 名」拼成，丢掉了包含类型链，`Aggregate.NestedId` 会生成
  `global::Ns.NestedId.NestedIdTypeConverter` 而非 `global::Ns.Aggregate.NestedId.NestedIdTypeConverter`，
  消费者项目直接 CS0246。
- OpenAPI 参数 schema 现在带上基元的 `format`（`/orders/{id}` → `type: string, format: uuid`）。
  路由与查询参数在 `JsonTypeInfo` 中描述的是基元，此前的 schema 转换器会漏掉它们；请求体与响应体
  本来就是正确的。
- 声明在泛型容器内的 Id 不再发出 `[JsonConverter(typeof(...))]` —— 特性实参中出现类型参数是非法的
  （CS0416）。转换器类型仍会生成，可为封闭构造手动注册；`AddStronglyTypedId` / `ApplyTo` 出于同样的
  原因（开放泛型不能作类型实参）会跳过它。README 此前声称两种序列化开箱即用，现已改为说明按闭合类型
  手动注册（`new Outer<int>.InnerId.SystemTextJsonConverter()`）。
- 对于声明程序集未引用 JSON 库的 Id，不再生成 JSON 注册入口，消除了消费者项目中的 CS0426。
- Dapper 类型处理器改为重写 `SqlMapper.TypeHandler<TId>` 的非可空签名（`SetValue(IDbDataParameter, TId)`
  与 `Parse(object?)`）。此前的 `TId?` 签名对 struct Id 会解析成 `Nullable<TId>`，编译时报
  CS0115 / CS0508。
- EF Core、Dapper、Swagger、MVC 与 OpenAPI 生成器在「没有可注册的 Id」时不再产出空的
  `StronglyTypedIds` 注册类。此前没有任何合格 Id 的程序集仍会拿到一个 `ApplyTo` 什么都不做的
  `StronglyTypedIds.*.g.cs`；现在直接跳过，与 JSON 注册入口的既有行为一致。
- 引用类型 Id（`partial record`）的 Dapper 类型处理器改为把被包裹的基元写入 `DbParameter.Value`，
  不再写入 Id 包装对象。SqlClient / SQLite 等驱动会以「No mapping exists from object type ...」
  拒绝该包装对象，导致引用类型 Id 的所有插入与更新在参数绑定阶段失败。值类型 Id 不受影响。
- Dapper 类型处理器读取时改为「转换驱动返回的值」而非直接拆箱。`Parse` 原先把 `object` 强转成基元，
  只有驱动恰好返回该 CLR 类型时才成立：SQLite 的枚举列返回 `long`、`char` 与 `Guid` 列返回 `string`、
  `decimal` 列返回 `double`，这四种基元的读取全部抛 `InvalidCastException`（写入正常）。现在驱动已返回
  基元本身时直取，否则按基元种类转换。
- 文件里没有 `using System;` 时，`STIA0008` 的代码修复不再留下裸 `Guid`。改为写入
  `global::System.Guid` 再由简化器缩短，修复不会再把一条诊断换成一条 `CS0246`。
- 分析器不再从 `Constructors[0].Parameters[0]` 取基元类型 —— 那假定主构造函数排在最前且有参数。
  若类型的首个构造函数是无参的，这里会抛 `IndexOutOfRangeException`，被 Roslyn 包成 `AD0001`，
  连带吞掉该类型的全部 `STIA` 诊断。现在改为遍历实例构造函数，取「单参数」的那一个，并跳过编译器
  合成的拷贝构造函数。
- 内嵌本地化资源缺失时，不再从诊断消息的渲染路径上抛异常。加载器返回空表、文案回退成资源键，
  打包失误只会让措辞退化，而不会让分析器崩溃。
- 引用类型 Id（`partial record`）的比较运算符在左操作数为 `null` 时不再抛 `NullReferenceException`。
  此前无条件解引用 `left.Value`，而同类型的 `CompareTo` 早已把 `null` 当作最小值；现在运算符共用这一
  约定，`null < id` 为 `true`、`null >= id` 为 `false`。值类型 Id 不变。
- 声明在泛型容器内的 Id 不再有 hint 名撞车的风险。净化全名时会剔除 `<`、`>`、`,` 与空格，于是
  `Ns.Outer<T>.Inner` 会塌陷到真实存在的 `Ns.OuterT.Inner` 上；`AddSource` 因重复 hint 名抛异常，
  整个 Id 降级成 `STIA0012`。现在只要确实剔掉了字符，就在末尾追加由原始全名算出的稳定 FNV-1a 后缀，
  两者在不同构建、不同机器上都保持区分。
- string 基元的 Id 作字典键、且键值被验证器拒绝时，抛的是 `ArgumentException` 而不是 `JsonException`。
  `ReadAsPropertyName` 走的是 `Create`，于是被拒的值以原始 `ArgumentException`（或配置的 `ExceptionType`）
  直接从 `JsonSerializer.Deserialize` 里逃出来，违反「转换器失败一律抛 `JsonException`」的契约。
  现在改走 `TryCreate` 并抛 `JsonException`，与带前缀、非 string 两条分支一致。
- `Half` 基元的 Id 会拒绝 `Half.Parse` 明明接受的命名实例取值。编译期校验借 `float` 代判，
  把 `NaN`、`±Infinity` 以及一切大于 `65504` 的文本都判成非法，而 `(65504, 65520)` 只是舍入回
  最大有限值（`65519.9` → `65504`）。现在 `NaN` 与 `±Infinity` 发射对应的 `Half` 常量，
  范围判定改用真正的溢出中点 `65520` —— 只有会舍入成无穷的值才被拒。
- `Len.StronglyTypedId.Testing` 从不生成负数 `int` / `long`，也不会生成大于 `long.MaxValue` 的
  `ulong`：内置工厂用的是非负的 `Random.Next()` / `NextInt64()` 重载。现在采样覆盖完整取值范围，
  `Any.Id<T>()` 才真正走到负数相关的分支（格式化、排序、数据库往返）。
- `STIA0005` 的代码修复会静默丢掉参数。标题只说「需要一个单参数主构造函数」，修复却把整张参数表
  换成 `(Guid Value)`，于是 `record OrderId(Guid Value, string Name)` 里的 `Name` 无声消失。
  参数确实必须删（诊断要求恰好一个参数），但现在标题把这件事说清楚并写明替换结果：
  「改为单参数主构造函数 (Guid Value)，其余参数将被移除」。
- `MessagePack` 与 `MongoDb` 两个集成从不报 `STIA0019`。分析器的「标志 → 程序集」对照表只登记了
  九个标志中的七个，于是在未引用 `MessagePack` 的项目里写
  `[StronglyTypedId(Integrations = StronglyTypedIdIntegrations.MessagePack)]` 既不生成代码也不报警 ——
  尽管生成器对这两个集成都做了程序集门控，文档也承诺会报诊断而不是静默失效。两行现已补齐，
  并新增一条结构性测试断言对照表覆盖 `StronglyTypedIdIntegrations.All` 的每一位，
  将来新增集成不会再重演同一处遗漏。
- 「内联强类型 Id」重构不再删除恰好同名的无关特性。它此前只按名字判断，于是 `[Acme.StronglyTypedId]`
  ——或从别的命名空间引入的短名 `[StronglyTypedId]`——会和真正的那个一起被剥掉，静默抹掉另一个库
  用它表达的信息。现在特性经语义解析，只有绑定到 `Len.StronglyTypedId.StronglyTypedIdAttribute` 时
  才删除；名字根本没解析到任何特性（被同名命名空间遮蔽）时仍按强类型 Id 处理，那种场景下重构照常可用。
- `StronglyTypedIds.ApplyTo(MvcOptions)` 现在是幂等的。它此前每次调用都插入一个新的
  `StronglyTypedIdModelBinderProvider`，于是启动代码从多个模块各注册一次、或配置回调被执行两次，
  都会在 `ModelBinderProviders` 里留下重复的提供者，每个都在每次请求上重建绑定器。现在检测到已存在
  就直接返回。
- Dapper 类型处理器现在按不区分大小写解析枚举文本，并拒绝不对应任何成员的取值。`Enum.Parse` 区分
  大小写，驱动对存为 `Active` 的列返回 `ACTIVE` 时即便取值合法也会抛异常；换成 `Enum.TryParse` 又会
  接受 `"999"` 与 `"1,2"`，得到不匹配任何成员的 Id。非 `[Flags]` 枚举现用 `Enum.IsDefined` 校验，
  `[Flags]` 枚举用未定义位掩码校验，与 `Id.TryParse` 及 JSON 字典键路径一致。被拒的文本抛
  `ArgumentException`，与 `Create` 抛出的异常相同。
- `RegisterBsonSerializers` 现在写明「同一 Id 类型的序列化器已被注册」时的行为：驱动的
  `TryRegisterSerializer` 返回 `false` 并保留既有的那个，冲突的注册会静默胜出。行为本身未变——
  强行覆盖会破坏文档承诺的幂等性——但生成的 XML 注释与集成文档现在把它讲明，而不是留给使用者自己撞见。

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
