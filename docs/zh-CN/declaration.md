---
layout: default
title: 声明一个 Id
---

# 声明一个 Id

<a id="the-declaration"></a>

## 声明形状

一个 Id 就是一个 `partial` record —— 或 `partial record struct` —— 且只有一个名为 `Value` 的位置参数：

```csharp
[StronglyTypedId]
public partial record struct OrderId(Guid Value);
```

`record struct` 生成值类型：不可为 `null`，接口调用不装箱。`record` 生成引用类型，可以为 `null` ——
`default` 得到的是空引用，而不是一个全零的值。两者都具备结构化相等性、都能直接当字典键，暴露的成员
也完全一致。请按可空性与分配预期来选，而不是按功能选。

参数类型决定被包装的基元，清单是封闭的，见
[支持的基元类型](https://github.com/LenFon/StronglyTypedId/blob/main/README.zh-CN.md#supported-primitive-types)。
参数的可空性标注可以跟随项目的 `#nullable` 上下文，但类型本身不能是可空类型 —— `OrderId(Guid? Value)`
会被分析器拒绝（`STIA0006`）。

你的声明永远不会被改写：生成器在同名命名空间里产出一个兄弟 `partial`（嵌套 Id 则产回包含类型内部，
见[嵌套类型](#nested-types)）。完整的形状规则与对应诊断，见[故障排查](troubleshooting.md#nothing-was-generated)。

<a id="value-validation"></a>

## 值校验

生成器约束的是 Id 的**形状**，而不是它允许取的**值**。把 `Validator` 指向 Id 自身的静态方法，生成的
入口就会拒绝非法取值：

```csharp
[StronglyTypedId(Validator = nameof(Validate))]
public partial record struct OrderId(Guid Value)
{
    private static bool Validate(Guid value) => value != Guid.Empty;
}

OrderId.Create(Guid.Empty);                            // 抛 ArgumentException
OrderId.TryParse(Guid.Empty.ToString(), null, out _);  // 返回 false，不抛异常
```

方法必须是该 Id 的静态成员，签名形如 `static bool Validate(TPrimitiveId value)`，取值合法时返回 `true`。
生成代码会在 `Create`、`TryCreate` 与全部 `TryParse` 重载（string、span、UTF-8）里调用它，因此 `TryParse`
依旧遵守约定——失败返回 `false` 而不抛异常。JSON 与 Entity Framework Core 集成都经由 `Create` 构造，
所以反序列化同样会拒绝非法值。

源码生成器唯一做不到的是拦截你声明的主构造函数：`new OrderId(...)` 会绕过校验。当取值来自你的代码
之外（用户输入、数据库、报文）时，请让它经过 `Create` 或解析成员。

不设置 `Validator` 时完全不生成校验代码——生成成员的行为与没有该属性时逐字节一致。

### 指定校验失败的异常类型

校验拒绝取值时 `Create` 默认抛 `ArgumentException`。领域库若想抛自己的异常，用 `ExceptionType` 指过去：

```csharp
public sealed class DomainRuleViolationException(string message) : Exception(message);

[StronglyTypedId(Validator = nameof(Validate), ExceptionType = typeof(DomainRuleViolationException))]
public partial record struct OrderId(Guid Value)
{
    private static bool Validate(Guid value) => value != Guid.Empty;
}

OrderId.Create(Guid.Empty);   // 抛 DomainRuleViolationException
```

该类型必须派生自 `System.Exception`、不能是抽象类，并且要有可访问的、接收单个 `string` 消息的构造函数；
否则在声明处报 [STIA0018](diagnostics.md#diagnostics)。只有 `Create` 会用它——`Parse` 抛的是 `FormatException`，
遵循 `IParsable<TSelf>` 的惯例（与 `int.Parse` / `Guid.Parse` 一致），因为它的失败还包含「文本根本不是 Id」。
与其他选项一样，它也可以在程序集级设置：`[assembly: StronglyTypedIdDefaults(ExceptionType = typeof(...))]`。

<a id="value-normalization"></a>

## 值归一化

`Validator` 只能**拒绝**取值。把 `Normalizer` 指向 Id 自身的静态方法，生成的入口会先**修正**它——
去首尾空白、统一大小写、去掉分隔符——再校验，最后构造：

```csharp
[StronglyTypedId(Normalizer = nameof(Normalize), Validator = nameof(Validate))]
public partial record struct EmailAddress(string Value)
{
    private static string Normalize(string value) => value.Trim().ToLowerInvariant();

    private static bool Validate(string value) => value.Contains('@');
}

EmailAddress.Create("  Alice@Example.COM  ").Value;  // "alice@example.com"
```

方法必须是该 Id 的静态成员，签名形如 `static TPrimitiveId Normalize(TPrimitiveId value)`。顺序固定且不可颠倒：
**归一化 → 校验 → 构造**。这样校验看到的就是真正落库的那个值，`"  a@b.com  "` 会被接受而不是被判非法。
`Create`、`TryCreate`、`Parse` 以及全部 `TryParse` 重载（string、span、UTF-8）都走这条路径。

每个入口只调用一次归一化方法——结果存进局部变量而不是重复调用，所以「追加前缀」这类不幂等的归一化
不会被执行两次。

不设置 `Normalizer` 时完全不生成归一化代码。与 `Validator` 一样，它也可以用
`[assembly: StronglyTypedIdDefaults(Normalizer = "MyNormalizers.Canonical")]` 在程序集级统一设置。

<a id="named-instances"></a>

## 命名实例

有些 Id 的取值在领域里是「有名字」的——未指定的租户、系统账号、匿名用户。用 `[StronglyTypedIdInstance]`
（可重复标注）声明它们，生成器会为每个实例产出一个 `public static readonly` 字段：

```csharp
[StronglyTypedId]
[StronglyTypedIdInstance("Unspecified", "-1")]
[StronglyTypedIdInstance("Anonymous", "0")]
public partial record struct TenantId(int Value);

TenantId.Unspecified.Value;                     // -1
TenantId.Unspecified == TenantId.Create(-1);    // true
```

取值是**基元值**的不变文化文本形——也就是 `Parse` 接受的那种文本，但**不带** `Prefix`：前缀装饰的是
规范文本形，而实例承载的是基元值。它在编译期就被校验并翻译成字面量（或构造函数调用），运行期不再解析。

每个实例都经 `Create` 构造，这正是保证成立的原因：实例不是特权值，它与任何其它取值一样经过
`Normalizer` 与 `Validator`，因此 `Parse(instance.ToString())` 必定读得回来。被验证器拒绝的取值不会在
编译期被发现——而是在首次使用时由类型初始化抛 `TypeInitializationException`，这比悄悄交出一个
「不可能存在」的 Id 更响亮。

名字必须是合法标识符、在同一个 Id 内唯一，且不能与 Id 类型本身、既有成员或生成成员撞名（`STIA0020`）；
取值必须是该基元类型的合法值（`STIA0021`）；名字不是 PascalCase 时报 `STIA0022`（`Info`）。

<a id="trycreate-and-utf8"></a>

## TryCreate 与 UTF-8 接口

`Create` 抛异常，`TryCreate` 不抛——后者在解析失败时返回 `false`，并把 `result` 置为 `default`：

```csharp
OrderId.TryCreate(Guid.NewGuid(), out var id);          // true
OrderId.TryCreate(badValue, out var invalid);           // false，result 为 default(OrderId)
```

`Guid` 自 .NET 10 起实现 `IUtf8SpanParsable<Guid>`，生成器据此为 `GuidId` 一并实现 `IUtf8SpanFormattable`
（net8.0+ 即可用）与 `IUtf8SpanParsable<GuidId>`，并提供 `ReadOnlySpan<byte>` 的 `Parse` / `TryParse`。
以 `string` 为基元的 Id 两者都不生成（`string` 未实现）：

```csharp
var utf8 = Encoding.UTF8.GetBytes(orderId.Value.ToString());
var same = OrderId.Parse(utf8, CultureInfo.InvariantCulture); // IUtf8SpanParsable<TSelf>
```

<a id="type-converter"></a>

## 类型转换器（TypeConverter）

默认只生成 JSON 转换器。需要经 `TypeDescriptor` 在字符串与 Id 间往返时（配置绑定、`XmlSerializer`、
Newtonsoft 字典键读路径等），用 `TypeConverter = true` 逐类型开启：

```csharp
[StronglyTypedId(TypeConverter = true)]
public partial record struct ConvertibleGuidId(Guid Value);
```

生成的嵌套 `XxxTypeConverter` 会带上 `[TypeConverter(typeof(XxxTypeConverter))]` 特性，因此
`TypeDescriptor.GetConverter(typeof(ConvertibleGuidId))` 即可在字符串与 Id 间转换。把 `null` 的 Id 转成
`string` 得到 `null`；传入的不是字符串（含 `null`）时抛 `NotSupportedException`。两个方向都按**不变文化**
读写，与生成的 `ToString()` 一致，因此在某个区域设置下写出的值，换个区域也能读回。

> 该转换器仅覆盖「字符串 ↔ Id」这一路径；它不替代 JSON 转换器，二者各自独立生效。

<a id="assembly-defaults"></a>

## 程序集级默认设置（StronglyTypedIdDefaults）

同一程序集里很多 Id 想共用同一个验证器时，可用程序集特性统一设置默认 `Validator`，免去逐个标注：

```csharp
[assembly: StronglyTypedIdDefaults(Validator = "MyValidators.NonEmpty")]
```

`StronglyTypedIdDefaults.Validator` 仅作为**默认**：单个 Id 上写了 `[StronglyTypedId(Validator = ...)]`
时，以该 Id 自己的为准；都没写时才回落到程序集默认值。

`Normalizer` 遵循同样的优先级规则：

```csharp
[assembly: StronglyTypedIdDefaults(Normalizer = "MyNormalizers.Canonical")]
```

`ExceptionType` 同样遵循这一规则：`[assembly: StronglyTypedIdDefaults(ExceptionType = typeof(DomainException))]`
对所有未自行指定异常类型的 Id 生效。

> 验证器方法仍需满足 [值校验](#value-validation) 的签名约定（`static bool Validate(TPrimitiveId value)`），
> 否则由 [STIA0010](diagnostics.md#diagnostics) 报错。归一化方法同理，必须是
> `static TPrimitiveId Normalize(TPrimitiveId value)`，否则由 [STIA0017](diagnostics.md#diagnostics) 报错。

<a id="integrations-filter"></a>

## 关闭指定集成

各类集成默认「只要引用了对应包就生成」。但有时这还不够：宿主应用为了自己的用途引用了 EF Core，
却希望某个 Id 永远不要拿到值转换器。`Integrations` 可以**逐个 Id** 收窄范围：

```csharp
[StronglyTypedId(Integrations = StronglyTypedIdIntegrations.None)]
public partial record struct TenantId(Guid Value);          // 只要核心实现：无 EF、Dapper、JSON、Swagger……

[StronglyTypedId(Integrations = StronglyTypedIdIntegrations.Dapper | StronglyTypedIdIntegrations.SystemTextJson)]
public partial record struct OrderId(Guid Value);           // 恰好这两类
```

`Integrations` 是一道**过滤器**，不是开关：它只能在「已引用对应包」的基础上做减法，绝不会凭空造出
依赖。请求了某个集成却没引用它的程序集时会报 [STIA0019](diagnostics.md#diagnostics)，不会静默失效。

| 取值 | 效果 |
| --- | --- |
| *（不写）* | 引用了什么就生成什么 —— 与引入本选项之前完全一致 |
| `None` | 不生成任何集成代码；核心实现不受影响 |
| `Dapper \| SystemTextJson` | 只生成列出的这几类（仍需已引用对应包） |
| `All` | 显式全开；用于在程序集默认 `None` 时把单个 Id 捞回来 |

`StronglyTypedIdDefaults` 上也有同名选项，优先级照旧：逐类型 > 程序集默认 > `All`。

```csharp
[assembly: StronglyTypedIdDefaults(Integrations = StronglyTypedIdIntegrations.None)]
```

> `TypeConverter` **不属于** `Integrations`。它是显式 opt-in（`TypeConverter = true`），不是「引用了包
> 就有」的东西，所以 `Integrations = None` 不会关掉它。

> 引用程序集里的 Id 会尊重**它自己所在程序集**的默认值：领域库若写了
> `[assembly: StronglyTypedIdDefaults(Integrations = None)]`，消费者就不会再为它的 Id 生成集成代码。

<a id="nested-types"></a>

## 嵌套类型

强类型 Id 可以声明在类、结构、记录或接口内部。`partial` 的各段必须处于同一容器内，因此生成代码会把
声明**逐层嵌回原本的包含类型**，而不是落在命名空间层级：

```csharp
public partial class OrderAggregate          // 容器必须 partial
{
    [StronglyTypedId]
    public partial record struct OrderId(Guid Value);
}

var id = OrderAggregate.OrderId.Create(value);   // 按嵌套名使用，与普通类型无异
```

前置条件只有一条：**包含类型必须都能被生成代码原样重开**——链上每一层都必须是 `partial`、
也不是 `file` 本地类型。不满足时由 [STIA0009](diagnostics.md#diagnostics)（缺 `partial`，或 `file` 本地类型）
报错，并附带补 `partial` 的代码修复。

**泛型容器。** 包含类型可以是泛型（例如 `Outer<T>`）：生成器会带着它的类型参数表把声明重开，
因此声明在泛型容器里的 Id 能正常编译，并保留核心生成器产出的能力——比较与排序、格式化与 span
解析、System.Text.Json 字典键。**JSON 是唯一的例外**：转换器类照常生成，但挂不上
`[JsonConverter]` 特性（特性实参不能引用携带容器类型参数的转换器，会 CS0416），
`AddStronglyTypedId` / `ApplyTo` 入口也会跳过它（开放泛型不能作类型实参）。请按实际使用的闭合类型
手动注册：

```csharp
options.Converters.Add(new Outer<int>.InnerId.SystemTextJsonConverter());
```

需要从外部按名字引用 Id 类型的集成（EF Core、Swagger、Dapper、MessagePack、MongoDB、MVC、OpenAPI）
出于同样的原因会跳过声明在泛型容器里的 Id；对于这些集成，请把 Id 声明在命名空间层级或非泛型容器里。

非泛型的普通嵌套不受影响：比较与排序、格式化与 span 解析、两种 JSON 序列化、System.Text.Json 字典键
都照常工作。

[<- 返回目录](index.md) · [English](../declaration.md)
