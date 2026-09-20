# StronglyTypedId

> 📖 中文文档：[README.zh-CN.md](README.zh-CN.md)

[![codecov][badge-codecov]](https://app.codecov.io/gh/LenFon/StronglyTypedId/tree/main)
[![NuGet][badge-nuget]][nuget-package]
[![License: MIT][badge-license]][license-file]

A source generator, analyzer and code fix provider that turns a one-line `record` declaration into a
fully featured strongly typed id. It generates the `IStronglyTypedId<TSelf, TPrimitiveId>` implementation,
`IParsable<TSelf>` support, and the JSON / Entity Framework Core / Swagger integrations for you — so a
`Guid` cannot be passed where an `OrderId` is expected, and vice versa.

<a id="table-of-contents"></a>

## Table of contents

- [Features](#features)
- [Requirements](#requirements)
- [Getting started](#getting-started)
- [Supported primitive types](#supported-primitive-types)
- [What gets generated](#what-gets-generated)
- [Value validation](#value-validation)
- [TryCreate & UTF-8 interfaces](#trycreate-and-utf8)
- [TypeConverter](#type-converter)
- [Assembly-level defaults](#assembly-defaults)
- [Nested types](#nested-types)
- [Serialization](#serialization)
- [Entity Framework Core](#entity-framework-core)
- [Swashbuckle](#swashbuckle)
- [Dapper](#dapper)
- [ASP.NET Core OpenAPI](#aspnetcore-openapi)
- [Using the interface](#using-the-interface)
- [Reflection helpers](#reflection-helpers)
- [Diagnostics](#diagnostics)
- [FAQ](#faq)
- [License](#license)

<a id="features"></a>

## Features <a href="#table-of-contents" style="float:right">↑ Back to top</a>

- **Tiny to declare** — annotate a `partial record` (or `partial record struct`) with `[StronglyTypedId]`.
- **Compile-time safety** — a built-in analyzer rejects invalid declarations as errors, and six of the
  twelve rules come with a code fix that repairs the declaration for you (see [Diagnostics](#diagnostics)).
- **Parsing & formatting** — `IParsable<TSelf>` and `ISpanParsable<TSelf>` support (`Parse` / `TryParse`
  for both `string` and `ReadOnlySpan<char>`) is generated automatically, together with `IFormattable`
  and `ISpanFormattable` for every primitive that has them (all but `string`).
- **Comparison & ordering** — each id implements `IComparable<TSelf>` and the four comparison operators,
  so `OrderBy`, `SortedSet<T>` and ranges work without a custom comparer.
- **Value validation** — point `Validator` at a static method of the id to make `Create` and `TryParse`
  reject invalid values (see [Value validation](#value-validation)).
- **`TryCreate` factory** — the non-throwing counterpart of `Create`: on a failed parse it returns `false`
  and sets `result` to `default` instead of throwing.
- **UTF-8 interfaces** — for primitives that expose them (`Guid` from .NET 10, for example),
  `IUtf8SpanFormattable` and `IUtf8SpanParsable<TSelf>` are generated automatically, enabling
  `ReadOnlySpan<byte>` parsing and formatting.
- **Per-type TypeConverter** — set `[StronglyTypedId(TypeConverter = true)]` to emit a nested `TypeConverter`
  that round-trips between string and the id through `TypeDescriptor`, covering config binding /
  `XmlSerializer` scenarios.
- **Assembly-level defaults** — use `[assembly: StronglyTypedIdDefaults(Validator = ...)]` to set a default
  validator for the whole assembly.
- **Dapper integration** — when `Dapper` ≥ 2.0.0 is referenced, `TypeHandler`s and `ApplyTo(IDbConnection)` are generated.
- **ASP.NET Core OpenAPI integration** — when `Microsoft.AspNetCore.OpenApi` ≥ 9.0.0 is referenced,
  `ApplyTo(OpenApiOptions)` schema mappings are generated.
- **Serialization** — `System.Text.Json` and `Newtonsoft.Json` (≥ 13.0.0) converters are generated
  automatically, including `System.Text.Json` dictionary-key support.
- **Entity Framework Core** — value converters for EF Core (≥ 7.0.0) are generated when needed.
- **Swagger / OpenAPI** — schema mappings for Swashbuckle.AspNetCore are generated when referenced,
  for both Microsoft.OpenApi 1.x and 2.x.
- **Runtime reflection helpers** — inspect any type at runtime and get the primitive id behind it.
- **Zero third-party dependencies** — the package ships only the generator/analyzer assembly plus a
  small runtime library; nothing is added to your application's dependency graph.

<a id="requirements"></a>

## Requirements <a href="#table-of-contents" style="float:right">↑ Back to top</a>

| Component                              | Requirement                                                                              |
| -------------------------------------- | ---------------------------------------------------------------------------------------- |
| Target framework of your project       | **`net8.0`, `net10.0`**, or a later compatible framework (the runtime library targets `net8.0` and `net10.0`). |
| .NET SDK                               | **8.0 or later** (the generator targets `netstandard2.0` and needs **Roslyn 4.4+**, which the .NET 8 SDK and later provide). A consuming project must target `net8.0`/`net10.0` or later — see the row above. |
| Newtonsoft.Json                        | ≥ **13.0.0** (only when using the Newtonsoft.Json converter).                            |
| EntityFrameworkCore                    | ≥ **7.0.0** (only when using the EF Core converter).                                     |
| Swashbuckle.AspNetCore.SwaggerGen      | ≥ **6.0.0** (only when generating Swagger schema mappings).                              |

The generated code relies on `IParsable<T>`, `ISpanParsable<T>` and the comparison / equality operator
interfaces in `System.Numerics`, which is why the runtime library starts at `net8.0` rather than following
the generator's much lower `netstandard2.0` baseline.

<a id="getting-started"></a>

## Getting started <a href="#table-of-contents" style="float:right">↑ Back to top</a>

1. Install the package into your application or library:

   ```text
   Package Manager : Install-Package Len.StronglyTypedId
   CLI             : dotnet add package Len.StronglyTypedId
   ```

   One package is enough: `Len.StronglyTypedId` already depends on `Len.StronglyTypedId.Generators`,
   which carries the generator, the analyzer and the code fixes.

2. Declare a strongly typed id with a single primary constructor parameter named `Value`:

   ```csharp
   [StronglyTypedId]
   public partial record struct OrderId(Guid Value);

   // or

   [StronglyTypedId]
   public partial record OrderId(Guid Value);
   ```

   > **`record` vs `record struct`** — `record struct` produces a value type (cannot be `null`,
   > no boxing on interface calls), while `record` produces a reference type (can be `null`).
   > Both are structurally comparable and usable as dictionary keys. Choose the one that matches your
   > nullability and performance expectations; both expose the same generated members.

   The generated type exposes `Value`, a static `Create(...)` factory, and `Parse` / `TryParse`.
   Use it anywhere you would otherwise pass a bare primitive:

   ```csharp
   public class Order
   {
       public OrderId Id { get; set; }
       public UserId Buyer { get; set; }
   }

   var id = OrderId.Create(Guid.NewGuid());          // static factory
   var parsed = OrderId.Parse("...", null);          // IParsable<TSelf>
   OrderId.TryParse("...", null, out var alsoParsed); // returns false instead of throwing
   ```

   The `Value` parameter may keep its `Nullable` context, but it may not be a nullable type:
   `OrderId(Guid? Value)` is rejected by the analyzer ([STIAO006](#diagnostics)).

<a id="supported-primitive-types"></a>

## Supported primitive types <a href="#table-of-contents" style="float:right">↑ Back to top</a>

The wrapped `Value` parameter may be one of:

| C# type    | Example declaration                          |
| ---------- | -------------------------------------------- |
| `Guid`     | `record struct OrderId(Guid Value)`          |
| `string`   | `record UserId(string Value)`                |
| `byte`     | `record struct ByteId(byte Value)`           |
| `sbyte`    | `record struct SByteId(sbyte Value)`         |
| `short`    | `record struct ShortId(short Value)`         |
| `ushort`   | `record struct UShortId(ushort Value)`       |
| `int`      | `record struct ProductId(int Value)`         |
| `uint`     | `record struct UIntId(uint Value)`           |
| `long`     | `record struct LongId(long Value)`           |
| `ulong`    | `record struct ULongId(ulong Value)`         |

The type is matched by its name, so writing `System.Guid` (or an aliased using) works exactly the same.
Only the types above are recognised — a type of your own that merely happens to be called `Guid` is not
supported, and is rejected by [STIAO008](#diagnostics).

<a id="what-gets-generated"></a>

## What gets generated <a href="#table-of-contents" style="float:right">↑ Back to top</a>

For each annotated type the generator emits a `partial` declaration in the **same namespace** as the id —
nested ids are declared back inside their containing types instead (see
[Nested types](#nested-types)) —
your own declaration is never modified. What is emitted depends on what the compilation references:

| Condition in the compilation                                                                        | Generated code                                                                                       |
| --------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------- |
| Always                                                                                              | Core implementation → `<Namespace>.<TypeName>.g.cs`                                                   |
| `System.Text.Json` is referenced                                                                     | Nested `SystemTextJsonConverter` + `[JsonConverter]` → `…SystemTextJson.g.cs`                         |
| `Newtonsoft.Json` ≥ 13.0.0 is referenced                                                             | Nested `NewtonsoftJsonConverter` + `[JsonConverter]` → `…NewtonsoftJson.g.cs`                         |
| `Microsoft.EntityFrameworkCore` ≥ 7.0.0 is referenced **and** a `DbContext` overrides `ConfigureConventions` | Nested `{TypeName}Converter` → `…EntityFrameworkCore.g.cs`, plus `StronglyTypedIds.ApplyTo(ModelConfigurationBuilder)` → `StronglyTypedIds.EntityFrameworkCore.g.cs` |
| `Swashbuckle.AspNetCore.SwaggerGen` ≥ 6.0.0 is referenced                                            | `StronglyTypedIds.ApplyTo(SwaggerGenOptions)` → `StronglyTypedIds.Swagger.g.cs`                       |
| `Dapper` ≥ 2.0.0 is referenced                                                                  | Nested `{TypeName}TypeHandler` → `…Dapper.g.cs`, plus `StronglyTypedIds.ApplyTo(IDbConnection)` → `StronglyTypedIds.Dapper.g.cs` |
| `Microsoft.AspNetCore.OpenApi` ≥ 9.0.0 is referenced                                             | `StronglyTypedIds.ApplyTo(OpenApiOptions)` → `StronglyTypedIds.AspNetCoreOpenApi.g.cs` |

The core implementation adds:

- Implementation of `IStronglyTypedId<TSelf, TPrimitiveId>`, `IParsable<TSelf>`, `ISpanParsable<TSelf>`,
  `IComparable<TSelf>`, `IEqualityOperators<TSelf, TSelf, bool>` and
  `IComparisonOperators<TSelf, TSelf, bool>`.
- `IFormattable` and `ISpanFormattable` as well — except for `string`-backed ids, since `string`
  implements neither, so those members are emitted only for primitives that have them.
- `IUtf8SpanFormattable` (available since net8.0) and `IUtf8SpanParsable<TSelf>` (`Guid` implements it only
  from .NET 10, so it is emitted per primitive) — providing `ReadOnlySpan<byte>` `Parse` / `TryParse` and `TryFormat`.
- A static `Create(TPrimitiveId value)` factory.
- A static `TryCreate(TPrimitiveId value, out TSelf result)` factory — on a failed parse it returns `false` and sets `result` to `default`.
- `Parse` / `TryParse` for both `string` and `ReadOnlySpan<char>`, delegating to the primitive type. For
  `string`-backed ids, `null` and empty strings are not accepted by `TryParse`.
- `CompareTo`, plus the `<`, `>`, `<=` and `>=` operators over the wrapped value.
- `ToString()` returning the text of the wrapped value (not the record's `OrderId { Value = … }` form),
  along with `ToString(string?, IFormatProvider?)` and `TryFormat(...)` where the primitive supports them.
- A nested `XxxTypeConverter` (`[TypeConverter]` attribute) when TypeConverter is enabled per type,
  round-tripping between string and the id through `TypeDescriptor`; a `null` reference-type id resolves back to `default`.

The integration entry points are emitted into a single generated class —
`internal static partial class StronglyTypedIds` in the `Len.StronglyTypedId` namespace — so both
`ApplyTo` overloads live side by side no matter how many ids the project declares. Add
`using Len.StronglyTypedId;` (already required for the attribute itself) to call them.

<a id="value-validation"></a>

## Value validation <a href="#table-of-contents" style="float:right">↑ Back to top</a>

The generator constrains the *shape* of an id, not the *values* it may hold. Point `Validator` at a
static method of the id itself, and the generated entry points will refuse invalid values:

```csharp
[StronglyTypedId(Validator = nameof(Validate))]
public partial record struct OrderId(Guid Value)
{
    private static bool Validate(Guid value) => value != Guid.Empty;
}

OrderId.Create(Guid.Empty);                            // throws ArgumentException
OrderId.TryParse(Guid.Empty.ToString(), null, out _);  // false, no exception
```

The method must be a static member of the id with the signature `static bool Validate(TPrimitiveId value)`,
returning `true` for an acceptable value. It is called from `Create` and from both `TryParse` overloads,
so `TryParse` keeps its contract and returns `false` instead of throwing. The JSON and Entity Framework
Core integrations construct through `Create`, so deserialization rejects invalid values as well.

One thing a source generator cannot do is intercept your primary constructor: `new OrderId(...)` bypasses
the validator. Route values through `Create` (or the parsing members) when they come from outside your
code.

Leaving `Validator` unset emits no validation code at all — the generated members then behave exactly as
if the property did not exist.

<a id="trycreate-and-utf8"></a>

## TryCreate & UTF-8 interfaces <a href="#table-of-contents" style="float:right">↑ Back to top</a>

`Create` throws; `TryCreate` does not — it returns `false` on a failed parse and sets `result` to `default`:

```csharp
OrderId.TryCreate(Guid.NewGuid(), out var id);              // true
OrderId.TryCreate(badValue, out var invalid);               // false, result is default(OrderId)
```

`Guid` implements `IUtf8SpanParsable<Guid>` from .NET 10, so the generator also emits `IUtf8SpanFormattable`
(available since net8.0) and `IUtf8SpanParsable<GuidId>` for `GuidId`, together with `ReadOnlySpan<byte>`
`Parse` / `TryParse`. `string`-backed ids emit neither (because `string` implements neither):

```csharp
var utf8 = Encoding.UTF8.GetBytes(orderId.Value.ToString());
var same = OrderId.Parse(utf8, CultureInfo.InvariantCulture); // IUtf8SpanParsable<TSelf>
```

<a id="type-converter"></a>

## TypeConverter <a href="#table-of-contents" style="float:right">↑ Back to top</a>

By default only JSON converters are generated. When you need round-tripping through `TypeDescriptor` — config
binding, `XmlSerializer`, the Newtonsoft dictionary-key read path, and so on — enable it per type with
`TypeConverter = true`:

```csharp
[StronglyTypedId(TypeConverter = true)]
public partial record struct ConvertibleGuidId(Guid Value);
```

The generated nested `XxxTypeConverter` carries a `[TypeConverter(typeof(XxxTypeConverter))]` attribute, so
`TypeDescriptor.GetConverter(typeof(ConvertibleGuidId))` can convert between string and the id, and a `null`
reference-type id resolves back to `default`.

> This converter covers only the "string ↔ id" path; it does not replace the JSON converters, which remain
> independent and active.

<a id="assembly-defaults"></a>

## Assembly-level defaults (StronglyTypedIdDefaults) <a href="#table-of-contents" style="float:right">↑ Back to top</a>

When many ids in an assembly share the same validator, set a default at the assembly level instead of repeating
it on each one:

```csharp
[assembly: StronglyTypedIdDefaults(Validator = "MyValidators.NonEmpty")]
```

`StronglyTypedIdDefaults.Validator` is only a **default**: an id that declares
`[StronglyTypedId(Validator = ...)]` keeps its own validator; the assembly default is used only when neither
the attribute nor the assembly default is set.

> The validator method must still satisfy the [Value validation](#value-validation) signature convention
> (`static bool Validate(TPrimitiveId value)`), otherwise [STIAO010](#diagnostics) is reported.

<a id="nested-types"></a>

## Nested types <a href="#table-of-contents" style="float:right">↑ Back to top</a>

A strongly typed id may be declared inside a class, struct, record or interface. Since all parts of a
`partial` type must share the same container, the generated declaration is **nested back into the
original containing types** rather than emitted at namespace level:

```csharp
public partial class OrderAggregate          // the container must be partial
{
    [StronglyTypedId]
    public partial record struct OrderId(Guid Value);
}

var id = OrderAggregate.OrderId.Create(value);   // used through its nested name, like any other type
```

The only prerequisite is that **every containing type can be reopened verbatim** by the generated code:
each level must be `partial`, non-generic, and not a `file`-local type. Violations are reported by
[STIAO009](#diagnostics) (missing `partial`, or `file`-local) and [STIAO003](#diagnostics) (generic
container); the former comes with a code fix that adds `partial`.

Limitation: containing types cannot be generic. Reopening `Outer<T>` would require reproducing its type
parameter list and constraints, and a full name such as `Outer<T>.OrderId` contains `<>`, which is not
a legal generated file name.

Nesting does not affect any other capability: comparison and ordering, formatting and span parsing,
both JSON serializers, System.Text.Json dictionary keys, EF Core converters and Swagger `MapType` all
work as usual.

<a id="serialization"></a>

## Serialization <a href="#table-of-contents" style="float:right">↑ Back to top</a>

### System.Text.Json

The converter is applied via a generated `[JsonConverter]` attribute — no extra configuration required.

```csharp
var json = JsonSerializer.Serialize(new OrderId(Guid.NewGuid())); // "..."
var id   = JsonSerializer.Deserialize<OrderId>(json);
```

### Newtonsoft.Json (≥ 13.0.0)

Same zero-config behavior through a generated `[JsonConverter]` attribute.

```csharp
var json = JsonConvert.SerializeObject(new OrderId(Guid.NewGuid()));
var id   = JsonConvert.DeserializeObject<OrderId>(json);
```

### Dictionary keys

A `JsonConverter<T>` does not see dictionary keys by default — `System.Text.Json` only knows the built-in
key types and throws `NotSupportedException` for anything else — so the generated converter overrides
`ReadAsPropertyName` / `WriteAsPropertyName` as well:

```csharp
var cart = new Dictionary<OrderId, int> { [id] = 3 };

var json = JsonSerializer.Serialize(cart);                        // {"3f2c…":3}
var back = JsonSerializer.Deserialize<Dictionary<OrderId, int>>(json);
```

Keys are written as the invariant-culture text of the primitive value, so the same data always produces
the same key no matter the current culture. A `string`-backed id uses the string itself, so an empty
string round-trips; a key that cannot be parsed throws `JsonException`.

Newtonsoft.Json routes dictionary keys through `TypeConverter` / `ToString()` rather than through
`JsonConverter`, so its keys never reach the generated converter. They still round-trip — the generated
`ToString()` returns the text of the primitive value — but that text follows the current culture. Attach
your own `[TypeConverter]` to the id if you need to control the key format.

Both converters serialize the id as its underlying primitive value rather than as an object, so the JSON
payload keeps the same shape it had before the id was introduced. A `null` reference-type id is written
as JSON `null`.

<a id="entity-framework-core"></a>

## Entity Framework Core <a href="#table-of-contents" style="float:right">↑ Back to top</a>

Register the generated value converters for all strongly typed ids (≥ 7.0.0) in your `DbContext`:

```csharp
protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
{
    base.ConfigureConventions(configurationBuilder);

    // Registers a ValueConverter for every strongly typed id in the project.
    StronglyTypedIds.ApplyTo(configurationBuilder);

    // Convention configuration still applies to an id after it has been registered.
    configurationBuilder.Properties<UserId>().HaveMaxLength(100);
}
```

> The EF Core converters are generated only when a `DbContext` overrides `ConfigureConventions`, so
> `StronglyTypedIds.ApplyTo(...)` is available exactly when you can call it. Referencing
> `Microsoft.EntityFrameworkCore` alone is not enough.

Each id gets a `ValueConverter<{Id}, {Primitive}>` nested in the generated `StronglyTypedIds` class, named
`{TypeName}Converter`. If two ids with the same short name exist in different namespaces, the converter
names would collide inside that single class, so only then are they disambiguated as
`{Namespace_With_Underscores}_{TypeName}Converter` (for example `Domain_OrderIdConverter`). The converters
are implementation details of `ApplyTo`, generated as private nested types — you never reference them by
name, and no action is required from you in either case.

<a id="swashbuckle"></a>

## Swashbuckle <a href="#table-of-contents" style="float:right">↑ Back to top</a>

Map every strongly typed id to its underlying primitive schema (Swashbuckle.AspNetCore) in Swagger:

```csharp
services.AddSwaggerGen(options =>
{
    StronglyTypedIds.ApplyTo(options);
});

// method groups work too:
services.AddSwaggerGen(StronglyTypedIds.ApplyTo);
```

The mapping is generated whenever `Swashbuckle.AspNetCore.SwaggerGen` ≥ 6.0.0 is referenced, and it works
with both Microsoft.OpenApi 1.x (Swashbuckle 6.x–9.x) and 2.x (Swashbuckle 10.x and later).

| Primitive  | OpenAPI `type` | `format` |
| ---------- | -------------- | -------- |
| `Guid`     | `string`       | `uuid`   |
| `string`   | `string`       | —        |
| `byte`     | `integer`      | `byte`   |
| `sbyte`    | `integer`      | `sbyte`  |
| `short`    | `integer`      | `int16`  |
| `ushort`   | `integer`      | `uint16` |
| `int`      | `integer`      | `int32`  |
| `uint`     | `integer`      | `uint32` |
| `long`     | `integer`      | `int64`  |
| `ulong`    | `integer`      | `uint64` |

<a id="dapper"></a>

## Dapper <a href="#table-of-contents" style="float:right">↑ Back to top</a>

When `Dapper` ≥ 2.0.0 is referenced, a `TypeHandler` is generated for every strongly typed id, along with a
single entry point:

```csharp
using Dapper;

// Register TypeHandlers for all strongly typed ids on the connection.
StronglyTypedIds.ApplyTo(connection);

connection.Execute("INSERT INTO Orders (Id) VALUES (@Id)", new { Id = OrderId.Create(Guid.NewGuid()) });
```

Each id's `TypeHandler` writes the database value (the `Value` for value types, the id itself for reference
types) into the parameter, and reconstructs the id through `Create` when reading. Ids with the same short name
in different namespaces get a "namespace-underscored" prefix on their `TypeHandler` to avoid collisions (the
same disambiguation used for EF Core converters).

> Referencing `Dapper` with a major version below 2.0.0 emits nothing, to avoid incompatibility with the
> older `SqlMapper.TypeHandler` API.

<a id="aspnetcore-openapi"></a>

## ASP.NET Core OpenAPI <a href="#table-of-contents" style="float:right">↑ Back to top</a>

When `Microsoft.AspNetCore.OpenApi` ≥ 9.0.0 is referenced, OpenAPI schema mappings are generated for every
strongly typed id, in parallel to Swashbuckle's `ApplyTo(SwaggerGenOptions)`:

```csharp
builder.Services.AddOpenApi(options =>
{
    StronglyTypedIds.ApplyTo(options);
});
```

Each id is mapped to the schema of its underlying primitive, with the same `format` values as in the
[Swashbuckle](#swashbuckle) section.

> This integration is independent of Swashbuckle: either, both, or neither may be present, and each generates
> its own `ApplyTo` overload.

<a id="using-the-interface"></a>

## Using the interface <a href="#table-of-contents" style="float:right">↑ Back to top</a>

Every generated id implements `IStronglyTypedId<TSelf, TPrimitiveId>`, so you can write generic
code that works over any strongly typed id:

```csharp
public class Repository<TId>
    where TId : IStronglyTypedId<TId, Guid>
{
    public TId CreateId(Guid value) => TId.Create(value);
}
```

<a id="reflection-helpers"></a>

## Reflection helpers <a href="#table-of-contents" style="float:right">↑ Back to top</a>

The package also exposes `StronglyTypedIdExtensions` for inspecting types at runtime:

- `bool IsStronglyTypedId(this Type type)`
- `Type? GetPrimitiveIdType(this Type type)`
- `bool TryGetPrimitiveIdType(this Type type, out Type? primitiveIdType)`

```csharp
if (typeof(OrderId).IsStronglyTypedId())
{
    Type primitive = typeof(OrderId).GetPrimitiveIdType()!; // typeof(Guid)
}
```

The check is based on the implemented interface, so it also recognises ids generated in other assemblies.
Interfaces, abstract types, enums and arrays are never reported as strongly typed ids.

<a id="diagnostics"></a>

## Diagnostics <a href="#table-of-contents" style="float:right">↑ Back to top</a>

The analyzer enforces the following rules. Each one is reported as a **compile error**, so an invalid
declaration will not compile. Diagnostics are localized for English and Chinese according to the
compiler culture.

| Rule ID  | Description                                                                | Code fix                       |
| -------- | -------------------------------------------------------------------------- | ------------------------------ |
| STIAO000 | The type must be a `record` (or `record struct`).                           | —                              |
| STIAO001 | The type must be `partial`.                                                 | Add `partial`                  |
| STIAO002 | The type cannot be `abstract`.                                              | Remove `abstract`              |
| STIAO003 | The type cannot be `generic`.                                               | Remove the type parameter list |
| STIAO004 | The type must declare a namespace.                                          | —                              |
| STIAO005 | The type must have a single-parameter primary constructor.                  | —                              |
| STIAO006 | The primary constructor parameter cannot be nullable.                       | Remove the `?` suffix          |
| STIAO007 | The primary constructor parameter must be named `Value`.                    | Rename the parameter           |
| STIAO008 | The primary constructor parameter type must be a supported primitive type.  | —                              |
| STIAO009 | The containing type must be reopenable by the generated code: `partial`, and not `file`-local. | Add `partial` to the containing type |
| STIAO010 | `Validator` must point to a method that exists, is static, returns `bool`, and takes the primitive id type as its parameter; otherwise no validation is generated. | — |
| STIAO011 | Do not construct an id with `new XxxId(...)` to bypass `Create` / `TryParse` (the validator will not run). Reported only when the id has a `Validator`; defaults to Info and can be raised via `.editorconfig`. | — |

Code fixes are offered through the usual IDE light bulb and support **Fix all occurrences in document /
project / solution**. A type may be declared across several `partial` blocks: type-level rules are
evaluated once for the whole type, and parameter-level rules only against the block that declares the
primary constructor, so adding members in a separate block is perfectly legal.

<a id="faq"></a>

## FAQ <a href="#table-of-contents" style="float:right">↑ Back to top</a>

**Q: Which package should I install?**

A: Just `Len.StronglyTypedId`. It depends on `Len.StronglyTypedId.Generators`, which contains the
generator, the analyzer and the code fixes; the runtime library is what you reference from code.

**Q: The analyzer didn't pick up my latest changes — why?**

A: This is usually a Visual Studio cache issue. **Clean the solution** and rebuild. The compiled
assembly always contains the latest generated code, so it is safe to ignore in the meantime.

**Q: `StronglyTypedIds.ApplyTo` is not found.**

A: Each `ApplyTo` overload is generated together with its integration, and both live in the
`Len.StronglyTypedId` namespace. Check that (1) `using Len.StronglyTypedId;` is in scope,
(2) for EF Core a `DbContext` overrides `ConfigureConventions`, and (3) for Swagger the project
references `Swashbuckle.AspNetCore.SwaggerGen` ≥ 6.0.0.

**Q: The EF Core converter / column mapping is missing even though EF Core is referenced.**

A: EF Core conversion is gated on a `DbContext` that overrides `ConfigureConventions`. Add the override
and call `StronglyTypedIds.ApplyTo(configurationBuilder)` — the converters are generated at the same time.

**Q: All generated code disappeared and I only see a CS8785 warning.**

A: `CS8785` means the generator threw while running, and every file it produced for that generation was
discarded. Please open an issue with the compiler output and a minimal reproduction.

**Q: Is the id usable as a `Dictionary`/`HashSet` key?**

A: Yes. `record` and `record struct` both generate structural equality, `GetHashCode`, `==` and `!=`
over the wrapped `Value`. Serializing a dictionary keyed by an id is supported as well — see
[Dictionary keys](#serialization).

**Q: Can I order ids, or use them in a `SortedSet<T>`?**

A: Yes. Every id implements `IComparable<TSelf>` plus `<`, `>`, `<=` and `>=` over the wrapped value, so
`OrderBy`, `SortedSet<T>` and range checks work without a custom comparer. Reference-type ids treat
`null` as the smallest value, matching `Comparer<T>.Default`.

**Q: What does `ToString()` print?**

A: The text of the wrapped primitive value (`"42"`, `"3f2c…"`) rather than the record's default
`OrderId { Value = 42 }` form, which keeps ids readable in logs and interpolated strings.

**Q: I set `Validator`, but `new OrderId(...)` still accepts an invalid value.**

A: The primary constructor belongs to your declaration, and a source generator cannot inject code into
it, so only the generated entry points are validated. JSON, EF Core and user input all come in through
`Create` / `Parse` / `TryParse`, where the validator does apply — see [Value validation](#value-validation).

<a id="license"></a>

## License <a href="#table-of-contents" style="float:right">↑ Back to top</a>

This project is licensed under the [MIT License](LICENSE.txt).

[nuget-package]: https://www.nuget.org/packages/Len.StronglyTypedId/
[license-file]: LICENSE.txt
[badge-nuget]: https://img.shields.io/nuget/v/Len.StronglyTypedId.svg
[badge-codecov]: https://codecov.io/github/LenFon/StronglyTypedId/branch/main/graph/badge.svg?token=S3PBV7W190
[badge-license]: https://img.shields.io/badge/License-MIT-blue.svg
