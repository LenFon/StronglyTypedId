---
layout: default
title: 集成
---

# 集成

每个集成只在其对应的包被引用时才生成，并且都把入口加到同一个生成的 `StronglyTypedIds` 类上——见
[集成入口](generated-code.md#the-integration-entry-point)。被关掉的集成不产出任何东西：无论是按单个 Id
关闭，还是经程序集级默认值关闭（见[关闭集成](declaration.md#integrations-filter)），编译里一个 Id 都没
声明时也一样。

两个 JSON 序列化器同样是集成，但它们在单独的一页上讲——见[序列化](serialization.md)。

<a id="entity-framework-core"></a>

## Entity Framework Core

在 `DbContext` 中为所有强类型 Id 注册生成的值转换器（≥ 7.0.0）：

```csharp
protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
{
    base.ConfigureConventions(configurationBuilder);

    // 为项目中每一个强类型 Id 注册 ValueConverter。
    StronglyTypedIds.ApplyTo(configurationBuilder);

    // 注册之后，常规的约定配置依旧对某个 Id 生效。
    configurationBuilder.Properties<UserId>().HaveMaxLength(100);
}
```

除基于约定的注册外，还一并生成了一个按模型扫描的重载，便于你从 `OnModelCreating` 而非约定注册转换器：

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    // 遍历每个实体的属性，凡是 CLR 类型为强类型 Id 的就挂上生成的转换器。
    StronglyTypedIds.ApplyTo(modelBuilder);
}
```

> 两个重载都在引用了 EF Core ≥ 7.0.0 时生成——与其余集成同一个门控，并不要求某个 `DbContext` 重写
> `ConfigureConventions`。`ModelConfigurationBuilder` 自 EF Core 7 起才存在，这才是此处版本下限更高的原因。
>
> `ApplyTo(ModelBuilder)` 扫描的是**调用那一刻**的模型快照，因此之后再配置的实体类型不会被它看到：
> 请在 `OnModelCreating` 的**末尾**调用。约定重载没有这个顺序约束，是更好的默认选择。

每个 Id 都会得到一个嵌套在生成的 `StronglyTypedIds` 类中的 `ValueConverter<{Id}, {基元类型}>`，类名
默认为 `{类型名}Converter`。若不同命名空间下存在同名 Id，两者在该类中的类名会相互冲突（CS0102），
因此**仅在这种情况下**才会改名为 `{命名空间下划线连接}_{类型名}Converter`（例如 `Domain_OrderIdConverter`）。
这些转换器是 `ApplyTo` 的实现细节，以 private 嵌套类型生成——你不需要按名字引用它们，两种情况都无需你做任何事。

由于转换器的 provider 类型就是基元本身，EF Core 建出的列类型与 schema 与该基元单独存在时完全一致：
引入 Id 不会产生任何迁移，既有列继续可用。读回一行时走 `Create`，因此 Id 的校验器与归一化器在每次物化时
都会执行——库里存着校验器不接受的值会抛异常，而不是造出一个在代码里根本无法构造的 Id。

> `ApplyTo(ModelBuilder)` 按调用那一刻的模型快照扫描，所以要放在实体与 `IEntityTypeConfiguration`
> 都注册完之后调用；之后再添加的实体不会被扫描。

<a id="swashbuckle"></a>

## Swashbuckle

将每一个强类型 Id 映射到其底层基元类型的 Swagger 架构（Swashbuckle.AspNetCore）：

```csharp
services.AddSwaggerGen(options =>
{
    StronglyTypedIds.ApplyTo(options);
});

// 也可以用方法组：
services.AddSwaggerGen(StronglyTypedIds.ApplyTo);
```

只要引用了 `Swashbuckle.AspNetCore.SwaggerGen` ≥ 6.0.0 就会生成该映射，且同时兼容
Microsoft.OpenApi 1.x（Swashbuckle 6.x–9.x）与 2.x（Swashbuckle 10.x 及更高版本）。

| 基元类型          | OpenAPI `type` | `format` |
| ---------------- | -------------- | -------- |
| `Guid`           | `string`       | `uuid`      |
| `string`、`char` | `string`       | —           |
| `byte`           | `integer`      | `byte`      |
| `sbyte`          | `integer`      | `sbyte`     |
| `short`          | `integer`      | `int16`     |
| `ushort`         | `integer`      | `uint16`    |
| `int`            | `integer`      | `int32`     |
| `uint`           | `integer`      | `uint32`    |
| `long`           | `integer`      | `int64`     |
| `ulong`          | `integer`      | `uint64`    |
| `Int128`、`UInt128`、`BigInteger` | `integer` | —     |
| `decimal`        | `number`       | —           |
| `double`         | `number`       | `double`    |
| `float`          | `number`       | `float`     |
| `Half`           | `number`       | —           |
| `DateTime`、`DateTimeOffset` | `string` | `date-time` |
| `DateOnly`       | `string`       | `date`      |
| `TimeOnly`       | `string`       | `time`      |
| `TimeSpan`       | `string`       | —           |
| 任意 `enum`      | `integer`      | —           |

这张表描述的是转换器**实际写出**的形态，而不是 CLR 类型叫什么：`decimal` 与各浮点基元是 JSON number，
枚举是其底层数字，`Int128` / `UInt128` / `BigInteger` 没有 `int64` / `uint64` 那样的定长范围可标注，
因此只给裸 `integer` 而不带 `format`。带 `Prefix` 的 Id 一律是 `string` 且不带 `format`，因为它的规范
文本形就是文本。

任何编译期映射都有一处限制：枚举基元的 Id 记为 `integer`，因为 `System.Text.Json` 默认就写数字。若你
全局注册了 `JsonStringEnumConverter`，生成的转换器会委托 `JsonSerializer`，实际报文变成成员名，而文档
仍写着 `integer`。schema 在编译期发射，看不到运行期的 `JsonSerializerOptions`；请沿用默认的数字形态，
或把该 Id 的基元声明为 `string`。

声明为 `JsonShape = JsonShape.Object` 的 Id 则映射为对象 schema，因为线上报文就是这个形状：
`{ "value": <基元> }`，`value` 标为必填。内层的 `value` 属性沿用上表的基元映射（含 `Prefix` 规则）：

```json
{
  "type": "object",
  "properties": { "value": { "type": "string", "format": "uuid" } },
  "required": ["value"]
}
```

`MapType<T>` 注册的是 Id **类型**的 schema，而不是某个端点的 schema，因此没有逐端点要配的东西。只要框架
把 Id 类型交给 `ApiExplorer`——DTO 上的属性、响应体、嵌套在另一个模型里的字段、MVC 控制器参数——用的都是
同一份映射。

**Minimal API** 的参数另有一处坑：`ApiExplorer` 会把实现了 `IParsable` 的参数描述成 `string`——
框架认得出这是可解析的文本类型——于是按 CLR 类型登记的 `MapType` 没有机会生效。为此本库额外生成一个
操作过滤器：它按参数的**真实类型**（`ApiParameterDescription.Type` 仍是 Id 类型）查同一份 schema 映射，
把参数上那份裸的 `type: string` 换成带 `format` 的形态。请求 / 响应体与 DTO 属性照常由 `MapType` 照顾；
MVC 控制器参数也一样（`samples/Len.StronglyTypedId.Sample3` 里 `/api/products/{id}` 是
`type: integer, format: int32`）。实测见 `samples/Len.StronglyTypedId.Sample4`（`net8.0` +
Swashbuckle 6.9.0）：`/orders/{id}` 现在是 `type: string, format: uuid`。

过滤器随 `ApplyTo` 一起注册，重复调用不会让过滤器列表增长。内置的
[ASP.NET Core OpenAPI](#aspnetcore-openapi) 集成没有这个缺口 —— 它的转换器看的是框架已建好的 schema。

<a id="dapper"></a>

## Dapper

引用 `Dapper` ≥ 2.0.0 时，为每个强类型 Id 生成 `TypeHandler`，并提供一个统一入口：

```csharp
using Dapper;

// 注册所有强类型 Id 的 TypeHandler。Dapper 把它们放在进程级注册表里，只需调用一次。
StronglyTypedIds.ApplyTo();

connection.Execute("INSERT INTO Orders (Id) VALUES (@Id)", new { Id = OrderId.Create(Guid.NewGuid()) });
```

每个 Id 的 `TypeHandler` 会把被包裹的基元写入参数，并把读取到的值经 `Create` 还原为 Id。不同命名空间下
同名 Id 的 `TypeHandler` 会以「命名空间下划线连接」前缀避免冲突（与 EF Core 转换器同名处理一致）。

引用类型 Id 的 `null` 写成 `DBNull.Value` 而不是 null 对象，这样列上看到的是真正的 SQL `NULL`。处理器经
`SqlMapper.AddTypeHandler<T>` 按 Id 类型登记，因此匿名参数对象、`Query<T>` / `QueryAsync<T>` 映射与多映射
都会自动命中，无需逐次调用配置。

驱动很少原样返回基元的 CLR 类型——SQLite 对枚举列返回 `long`、对 `char` / `Guid` 列返回 `string`、对
`decimal` 列返回 `double`。因此生成的 `Parse` 会先把入参转换成基元再调用 `Create`（数值与日期时间基元走
`System.Convert`，枚举走 `Enum.TryParse`，`Guid`、`DateTimeOffset`、`TimeSpan`、`DateOnly`、
`TimeOnly`、`Int128`、`UInt128`、`Half`、`BigInteger` 走各自的 `IParsable` 文本入口）。已经是基元的值走
直通分支；`NULL`——`DBNull` 或裸 `null`——映射为 `default`。枚举列可能以成员名文本返回，也可能以底层数字
返回，两种形态都接受。

枚举文本按不区分大小写匹配，因为各驱动的大小写习惯不一致（Oracle 返回大写，PostgreSQL 返回存储时的拼写），
而 `Enum.Parse` 区分大小写。文本还会对照枚举的已定义成员校验：`Enum.TryParse` 会接受 `"999"` 和 `"1,2"`，
由此得到的 Id 不对应任何成员——对它做 `switch` 会落空，序列化也会写出野值。非 `[Flags]` 枚举必须命中已定义
成员；`[Flags]` 枚举接受任意已定义位的组合，与 `Id.TryParse` 保持一致。两项校验任一失败都会抛出
`ArgumentException`，与 `Create` 拒绝取值时抛出的异常相同。

数值路径同样要校验，而且必须发生在 `Enum.ToObject` 之前：该方法既不检查范围也不拒绝小数。`300L` 交给
`byte` 底层枚举会静默变成 `44`——甚至可能恰好是某个已定义成员——`1.5d` 则会静默截断成 `0`。因此生成的
处理器先把取值转换成枚举的底层类型（超范围会抛 `OverflowException`，再转成 `ArgumentException`），拒绝一切
带小数部分的取值，最后才对照已定义成员校验。于是 `999L`、`300L`、`1.5d`、`uint.MaxValue` 与它们的文本形式
一样被拒绝；已经是枚举类型的取值也要校验，而不是直接放行。

> 注册是全局的，不是按连接的。`ApplyTo(IDbConnection)` 仍会生成以保持源码兼容，但它忽略实参并转发到
> `ApplyTo()`。两者重复调用都无害：Dapper 对同一类型是替换而非追加，注册表不会膨胀。

> 引用 `Dapper` 但主版本低于 2.0.0 时不生成，避免与旧版 `SqlMapper.TypeHandler` API 不兼容。

<a id="messagepack"></a>

## MessagePack

引用 `MessagePack` ≥ 2.0.0 时，为每个强类型 Id 生成 `IMessagePackFormatter<TId>`，并提供一个统一入口把它们
装配成序列化选项：

```csharp
using MessagePack;

// MessagePackSerializerOptions 是不可变的 —— 要用返回的那个实例。
var options = StronglyTypedIds.ApplyTo(MessagePackSerializerOptions.Standard);

var bytes = MessagePackSerializer.Serialize(OrderId.Create(Guid.NewGuid()), options);
var id    = MessagePackSerializer.Deserialize<OrderId>(bytes, options);
```

返回的选项把生成的格式化器放进一个 `CompositeResolver`，排在实参原本携带的解析器之前，因此你自己写的
自定义格式化器照常生效——生成的这些只对 Id 抢先。

每个 Id 的格式化器名为 `{类型名}MessagePackFormatter`，放在生成的 `StronglyTypedIds` 类里，而不是挂在 Id
自己名下。这是有意的：`[MessagePackFormatter]` 只能加在本次编译声明的类型上，于是「库作者声明 Id、应用
引用使用」这条最常见的路径根本拿不到格式化器。不同命名空间下的同名 Id 按与 Dapper 处理器相同的方式消歧。

`ApplyTo` 还会为 Id 包裹的每个**枚举**基元注册格式化器。MessagePack 在 AOT（禁用动态代码）下的解析器链
里没有任何枚举 resolver——走 IL Emit 的 `DynamicEnumResolver` 被排除——不补这一项，枚举基元的 Id 在
Native AOT 下连序列化都过不去。注册的 `GenericEnumFormatter<TEnum>` 与动态 resolver 写同样的底层数值载荷，
线格式不变。非枚举基元由 MessagePack 的内置 resolver 提供，本身就零反射。详见
[原生 AOT 与裁剪](aot.md)。

载荷是包裹的基元值本身，不是规范文本形：文本前缀只是文本形的装饰，二进制格式里没有文本可装饰。有三种基元是
例外——`BigInteger`、`Int128`、`UInt128`——它们的 Id 写成不变文化的文本形（MessagePack string），读回时经
`TryParse`。对它们来说前缀**确实**会进载荷，因为前缀属于文本。

这个例外是刻意的选择，而不是当前 MessagePack 版本的限制：3.x 已能原生编码这三种基元，本库仍使用文本形，是为了
让 2.x 时代写出的载荷保持可读。改成原生编码会静默改变既有数据的线上格式，因此不做。

对其余基元，字节就是该基元自身序列化的结果，因此 Id 字段与裸基元字段在既有载荷里无法区分——把其中一个
换成另一个不需要重新编码。

引用类型 Id 的 `null` 写成 `nil`、读回为 `null`；值类型 Id 收到 `nil` 会抛
`MessagePackSerializationException`，而不是悄悄落成 `default`——这样「字段缺失」就不会被误认成合法的
`Guid.Empty`。被 Id 的 `Validator` 拒绝的值同样如此：格式化器走 `TryCreate` 并自行抛出
`MessagePackSerializationException`，`Create` 的 `ArgumentException` 不会传到调用方。

<a id="mongodb"></a>

## MongoDB

引用 `MongoDB.Bson` ≥ 3.0.0 时，为每个强类型 Id 生成 `IBsonSerializer<TId>`，并提供注册入口：

```csharp
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

// 驱动 3.0 起没有全局默认的 Guid 表示形式。请按既有文档实际使用的那个来注册，
// 否则序列化 Guid 基元的 Id 会抛异常。
BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

// 注册生成的序列化器。进程级、幂等：只需在第一次序列化文档之前调用一次。
StronglyTypedIds.RegisterBsonSerializers();

var collection = database.GetCollection<Order>("orders");
await collection.InsertOneAsync(new Order { Id = OrderId.Create(Guid.NewGuid()) });
```

`BsonSerializer` 把序列化器放在进程级注册表里，所以注册是全局的、不是按集合的；重复调用也无害——
已注册过的序列化器不会被覆盖。

「不被覆盖」也包括别人为同一个 Id 类型注册的序列化器：驱动的 `TryRegisterSerializer` 返回 `false` 并保留
既有的那个，因此冲突的注册会静默压过生成的序列化器。这是有意的——强行覆盖会破坏上面的幂等性——但意味着
序列化器已在别处注册过的 Id 类型将由那一个负责序列化，而不是我们生成的。

每个 Id 的序列化器名为 `{类型名}BsonSerializer`，放在生成的 `StronglyTypedIds` 类里。`[BsonSerializer]`
本来是更短的路子，但它只能加在本次编译声明的类型上，那样引用程序集里的 Id 就没有序列化器了。

`RegisterBsonSerializers` 还会为 Id 包裹的每个基元预注册默认配置的序列化器——`int`、`char`、`decimal`、
`string`、枚举等等。否则 BSON 的 `PrimitiveSerializationProvider` 会经 `Activator` 反射创建它们，构造器被
裁剪后在 Native AOT 下直接抛 `MissingMethodException`。预注册的实例与 provider 创建的默认实例配置一致，
JIT 应用的行为因此不变。BSON 的动态类型（`object`、`List<object>`、`ExpandoObject`）出于同样原因注册：
任何根序列化上下文都会取 `BsonDefaults.DynamicArraySerializer` 与
`BsonDefaults.DynamicDocumentSerializer`，它们是去注册表里查的。`Guid` 仍被排除：driver 3.0 起它没有
全局默认表示，应用必须按既有数据自行注册，如上例所示。详见[原生 AOT 与裁剪](aot.md)。

载荷是包裹的基元值本身。`BigInteger`、`Int128`、`UInt128` 没有 BSON 序列化器，这些 Id 存成承载不变文化
文本形（含前缀）的 BSON string，读回时经 `TryParse`。引用类型 Id 的 `null` 存成 BSON `null`；值类型 Id
遇到 BSON `null` 会抛 `BsonSerializationException`。被 Id 的 `Validator` 拒绝的值同样如此：序列化器走
`TryCreate` 并自行抛出 `BsonSerializationException`，`Create` 的 `ArgumentException` 不会传到调用方。

对其余基元，存下去的 BSON 值就是基元自身的值，因此既有文档继续可读，Id 字段与裸基元字段无法区分。由于
序列化器按类型注册，LINQ 过滤器与投影（`Builders<Order>.Filter.Eq(x => x.Id, id)`）也会经它序列化——
无需按查询配置。

<a id="aspnet-core-mvc"></a>

## ASP.NET Core MVC

引用 `Microsoft.AspNetCore.Mvc` ≥ 2.0.0 时（.NET 8 / .NET 10 上 `Microsoft.AspNetCore.Mvc.Core`
与 `Microsoft.AspNetCore.Mvc.Abstractions` 程序集同样满足门控），生成器会产出一个模型绑定提供者与
逐 Id 的路由约束，使强类型 Id 无需任何逐类型样板即可完成绑定与校验：

```csharp
using Microsoft.Extensions.DependencyInjection;

// 注册模型绑定提供者与逐 Id 路由约束。
builder.Services.AddControllers(options => StronglyTypedIds.ApplyTo(options));
builder.Services.AddRouting(options => StronglyTypedIds.ApplyTo(options));
```

```csharp
[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    // 路由或查询里的 "ABC123" 经 IParsable<OrderId>.TryParse 绑定为 OrderId。
    [HttpGet("{id}")]
    public IActionResult Get([FromRoute] OrderId id) => Ok(id);

    // {id:OrderId} 约束会在进入 Action 前按该 Id 的解析规则校验分段格式。
    [HttpGet("v2/{id:OrderId}")]
    public IActionResult GetV2([FromRoute] OrderId id) => Ok(id);
}
```

> 绑定器只对实现了 `IStronglyTypedId<TSelf, TPrimitiveId>` 的类型接管；其余类型回退到 MVC 默认绑定链路。
> 绑定本身复用每个 Id 已有的 `IParsable<TSelf>` 成员，因此除注册时的一次性类型判定外没有逐请求反射开销。

该提供者被插到 `ModelBinderProviders` 的最前面，因此优先于框架自带的提供者。`ApplyTo(MvcOptions)` 是幂等的：
列表里已有 `StronglyTypedIdModelBinderProvider` 时直接返回，不再插入第二个，因此启动代码从多个模块各注册一次、
或配置回调被执行两次，都不会让提供者列表增长、也不会让绑定器在每个请求上重建。可空 Id 同样覆盖：
`[FromQuery] OrderId?` 的 CLR 类型是 `Nullable<OrderId>`，它不实现任何接口，所以提供者先拆壳再做接口判定。
少了这一步，该参数会落到 MVC 的 `TryParseModelBinder`，而它按 `ValueProviderResult.Culture` 解析
（查询串与路由传不变文化，表单传当前文化）——同一条路由上的可空与非可空参数对同一份表单载荷就会绑出不同的值。

绑定行为，三种要紧的情形：

| 输入 | 结果 |
| --- | --- |
| 合法文本 | 经 `TryParse` 以不变文化绑定；`Prefix` 由 Id 自身的读取模式处理 |
| 键缺失，或值为空串 | 保持未绑定且**不**产生模型错误，因此可空参数仍是 `null`，非可空参数走框架常规的「必填」处理 |
| Id 拒绝的文本 | 产生一条模型错误——`The value '<文本>' is not a valid strongly typed id.`——`[ApiController]` 会把它变成 400 |

路由约束按 Id 的**短类型名**逐条注册，所以 `{id:OrderId}` 能解析到。不同命名空间下的同名 Id 因此会争抢
`ConstraintMap` 里的同一个键：后注册的覆盖先注册的，只有一个能拿到可用的约束。绑定器没有这个限制，因为它
按参数类型索引。

约束在解析前先把路由值转成文本，这才让它双向都成立：入站请求时值是原始分段，生成 URL 时值已经是 Id 实例。
因此 `{id:OrderId}` 仍能经 `LinkGenerator` / `Url.Action` 产出链接。路由值缺失则约束失败。

<a id="minimal-api"></a>

## Minimal API

Minimal API **无需任何额外生成**。它的参数绑定约定接受「具备静态 `TryParse(string?, IFormatProvider?, out TSelf)`
的类型」，而每个 Id 都经 `IParsable<TSelf>` 天然具备该成员，因此路由与查询参数开箱即用：

```csharp
// 路由里的 "ABC123" 经 IParsable<OrderId>.TryParse 绑定为 OrderId；
// 非法文本在进入处理器前即返回 400。
app.MapGet("/orders/{id}", (OrderId id) => Results.Ok(id.Value));

// [AsParameters] 会把每个 Id 属性按同名的路由 / 查询值逐个绑定。
app.MapGet("/orders", ([AsParameters] OrderQuery query) => Results.Ok(query.Id.Value));

public record OrderQuery(OrderId Id);
```

不需要 `BindAsync`、不需要 `TryParse` 垫片、也不需要 DI 注册——框架直接在类型自身上找到那个静态
`TryParse`。由于绑定走的正是这个成员，`Prefix` 与 Id 的读取模式在这里的表现与 MVC 完全一致。

路由约束与 MVC 共用：生成的 `ApplyTo(RouteOptions)` 会为每个 Id 注册一条约束，因此 `{id:OrderId}`
在 Minimal API 模板里同样可用（`Microsoft.AspNetCore.Mvc.*` 程序集随 ASP.NET Core 共享框架分发，
Minimal API 工程也满足门控）：

```csharp
builder.Services.AddRouting(options => StronglyTypedIds.ApplyTo(options));

app.MapGet("/orders/v2/{id:OrderId}", (OrderId id) => Results.Ok(id.Value));
```

<a id="aspnetcore-openapi"></a>

## ASP.NET Core OpenAPI

引用 `Microsoft.AspNetCore.OpenApi` ≥ 9.0.0 时，为每个强类型 Id 生成 OpenAPI 架构映射，与 Swashbuckle
的 `ApplyTo(SwaggerGenOptions)` 平行：

```csharp
builder.Services.AddOpenApi(options =>
{
    StronglyTypedIds.ApplyTo(options);
});
```

映射把每个 Id 解析为其底层基元类型的 schema，`format` 取值与 [Swashbuckle](#swashbuckle) 一节相同。
声明为 `JsonShape = JsonShape.Object` 的 Id 会被改写成带单个必填 `value` 属性的对象 schema，与
`{ "value": ... }` 报文一致——框架看不穿自定义转换器，给它建的是一份空 schema。

机制与 Swashbuckle 不同：这里是一个经 `AddSchemaTransformer` 挂上的 `IOpenApiSchemaTransformer`，只改写
框架已经建好的 schema 上的 `type` 与 `format`，其余内容——描述、`nullable`、`required`、示例——原样保留。
又因为框架把这份 schema 保留为具名组件，请求体里的 Id 呈现为 `$ref: '#/components/schemas/OrderId'`，
而不是内联对象。Swashbuckle 的 `MapType` 则是整份替换该类型的 schema，换上去的是一份只带 `type` 与
`format` 的内联 schema。`JsonShape.Object` 是例外：两个集成都直接写入对象形态，因为框架那里本来就没有
可保留的 schema。

两者的覆盖面也不同。用 `samples/Len.StronglyTypedId.Sample2`（`net10.0` +
`Microsoft.AspNetCore.OpenApi` 10.0.13）实测：路由参数与查询参数都拿到了 `type: string, format: uuid`。
而 `net8.0` 上的 Swashbuckle（`Sample4`）会让 **Minimal API** 的路由参数停在裸的 `type: string`——
`ApiExplorer` 把 `IParsable` 参数报成 `string`，而 `MapType` 是按 CLR 类型登记的，见该节的
[实测例外](#swashbuckle)。两者都不会碰不是 Id 的 schema。

> 该集成与 Swashbuckle 互不依赖：二者可单独或同时存在，各自生成自己的 `ApplyTo` 重载。

### 为什么是 9.0 而不是 8.0

`Microsoft.AspNetCore.OpenApi` 8.0.x 只带 `WithOpenApi()` 注解辅助方法；schema 转换器 API
（`IOpenApiSchemaTransformer`、`OpenApiSchemaTransformerContext` 与 `AddSchemaTransformer`）是 9.0.0 才引入的，
按 8.0.x 生成的转换器会以 `CS0246` 编译失败，所以这道门控降不下去。`net8.0` 上没有内建的文档生成，
请改用 [Swashbuckle](#swashbuckle) 集成——它把同样的 Id 映射成同样的 `type` / `format`。
`samples/Len.StronglyTypedId.Sample4` 把包钉在 8.0.0，就是这条替代路径的可运行证据：包能正常还原、
应用能正常启动，`/swagger/v1/swagger.json` 里带着映射后的 schema。

[<- 返回目录](index.md) · [English](../integrations.md)
