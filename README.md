# StronglyTypedId

> 📖 中文文档：[README.zh-CN.md](README.zh-CN.md)

[![codecov][badge-codecov]](https://app.codecov.io/gh/LenFon/StronglyTypedId/tree/main)
[![NuGet][badge-nuget]][nuget-package]
[![License: MIT][badge-license]][license-file]

A source generator and analyzer that turns a one-line `record` declaration into a fully featured strongly
typed id. It generates the `IStronglyTypedId<TSelf, TPrimitiveId>` implementation, `IParsable<TSelf>` support,
and the JSON / Entity Framework Core / Swagger integrations for you.

<a id="table-of-contents"></a>

## Table of contents

- [Features](#features)
- [Requirements](#requirements)
- [Getting started](#getting-started)
- [Supported primitive types](#supported-primitive-types)
- [What gets generated](#what-gets-generated)
- [Serialization](#serialization)
- [Entity Framework Core](#entity-framework-core)
- [Swashbuckle](#swashbuckle)
- [Using the interface](#using-the-interface)
- [Reflection helpers](#reflection-helpers)
- [Constraints](#constraints)
- [FAQ](#faq)
- [License](#license)

<a id="features"></a>

## Features <a href="#table-of-contents" style="float:right">↑ Back to top</a>

- **Tiny to declare** — annotate a `partial record` (or `partial record struct`) with `[StronglyTypedId]`.
- **Compile-time safety** — a built-in analyzer rejects invalid declarations as errors (see [Constraints](#constraints)).
- **Parsing & formatting** — `IParsable<TSelf>` support (`Parse` / `TryParse`) is generated automatically.
- **Serialization** — `System.Text.Json` and `Newtonsoft.Json` (≥ 13.0.0) converters are generated automatically.
- **Entity Framework Core** — value converters for EF Core (≥ 7.0.0) are generated when needed.
- **Swagger / OpenAPI** — schema mappings for Swashbuckle.AspNetCore are generated when referenced.
- **Zero runtime dependencies** — the package ships only the generator/analyzer assembly plus a small runtime library.

<a id="requirements"></a>

## Requirements <a href="#table-of-contents" style="float:right">↑ Back to top</a>

| Component           | Requirement                                                          |
| ------------------- | ------------------------------------------------------------------- |
| .NET SDK            | Any modern .NET SDK (the generator targets `netstandard2.0`).       |
| C# source generator | Roslyn **4.4+** (any SDK that ships it, e.g. .NET 6.0.3xx or later). |
| Runtime library     | Targets **.NET 8.0** and **.NET 10.0**.                             |
| Newtonsoft.Json     | ≥ **13.0.0** (only when using the Newtonsoft.Json converter).       |
| EntityFrameworkCore | ≥ **7.0.0** (only when using the EF Core converter).                |
| Swashbuckle.AspNetCore | ≥ **6.0.0** (only when generating Swagger schema mappings).     |

<a id="getting-started"></a>

## Getting started <a href="#table-of-contents" style="float:right">↑ Back to top</a>

1. Install the package into your application or library:

   ```text
   Package Manager : Install-Package Len.StronglyTypedId
   CLI             : dotnet add package Len.StronglyTypedId
   ```

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
   > Choose the one that matches your nullability and performance expectations; both expose the
   > same generated members.

   The generated type exposes `Value`, a static `Create(...)` factory, and `Parse` / `TryParse`.
   Use it anywhere you would otherwise pass a bare primitive:

   ```csharp
   public class Order
   {
       public OrderId Id { get; set; }
       public UserId Buyer { get; set; }
   }
   ```

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
| `ushort`   | `record struct UShortId(ushort Value)`        |
| `int`      | `record struct ProductId(int Value)`         |
| `uint`     | `record struct UIntId(uint Value)`            |
| `long`     | `record struct LongId(long Value)`           |
| `ulong`    | `record struct ULongId(ulong Value)`         |

<a id="what-gets-generated"></a>

## What gets generated <a href="#table-of-contents" style="float:right">↑ Back to top</a>

For each annotated type the generator emits:

- Implementation of `IStronglyTypedId<TSelf, TPrimitiveId>`, `IParsable<TSelf>`,
  and `IEqualityOperators<TSelf, TSelf, bool>`.
- A static `Create(TPrimitiveId value)` factory and `Parse` / `TryParse` methods.
- A nested `SystemTextJsonConverter` when `System.Text.Json` is referenced.
- A nested `NewtonsoftJsonConverter` when `Newtonsoft.Json` ≥ 13.0.0 is referenced.
- A nested `{TypeName}Converter` (EF Core `ValueConverter`) **only** when the compilation also
  contains a `DbContext` that overrides `ConfigureConventions`.
- Swagger `MapType` registrations **only** when `Swashbuckle.AspNetCore.SwaggerGen` is referenced.

<a id="serialization"></a>

## Serialization <a href="#table-of-contents" style="float:right">↑ Back to top</a>

### System.Text.Json

The converter is applied via a generated `[JsonConverter]` attribute — no extra configuration required.

```csharp
var json = JsonSerializer.Serialize(new OrderId(Guid.NewGuid()));
var id   = JsonSerializer.Deserialize<OrderId>(json);
```

### Newtonsoft.Json (≥ 13.0.0)

Same zero-config behavior through a generated `[JsonConverter]` attribute.

```csharp
var json = JsonConvert.SerializeObject(new OrderId(Guid.NewGuid()));
var id   = JsonConvert.DeserializeObject<OrderId>(json);
```

<a id="entity-framework-core"></a>

## Entity Framework Core <a href="#table-of-contents" style="float:right">↑ Back to top</a>

Register the generated value converters for all strongly typed ids (≥ 7.0.0) in your `DbContext`:

```csharp
protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
{
    base.ConfigureConventions(configurationBuilder);

    // Registers a ValueConverter for every strongly typed id in the project.
    StronglyTypedIds.ApplyTo(configurationBuilder);
}
```

> The EF Core converters are generated only when a `DbContext` overrides `ConfigureConventions`, so
> `StronglyTypedIds.ApplyTo(...)` is available exactly when you can call it.

<a id="swashbuckle"></a>

## Swashbuckle <a href="#table-of-contents" style="float:right">↑ Back to top</a>

Map every strongly typed id to its underlying primitive schema (Swashbuckle.AspNetCore) in Swagger:

```csharp
services.AddSwaggerGen(options =>
{
    StronglyTypedIds.ApplyTo(options);
});
```

This works with both Microsoft.OpenApi 1.x (Swashbuckle 6.x–9.x) and 2.x (Swashbuckle 10.x and later).

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

<a id="constraints"></a>

## Constraints <a href="#table-of-contents" style="float:right">↑ Back to top</a>

The analyzer enforces the following rules. Each one is reported as a **compile error**, so an invalid
declaration will not compile:

| Rule ID  | Description                                                                 |
| -------- | --------------------------------------------------------------------------- |
| STIAO000 | The type must be a `record` (or `record struct`).                            |
| STIAO001 | The type must be `partial`.                                                 |
| STIAO002 | The type cannot be `abstract`.                                              |
| STIAO003 | The type cannot be `generic`.                                               |
| STIAO004 | The type cannot be nested and must declare a namespace.                     |
| STIAO005 | The type must have a single-parameter primary constructor.                  |
| STIAO006 | The primary constructor parameter cannot be nullable.                       |
| STIAO007 | The primary constructor parameter must be named `Value`.                    |
| STIAO008 | The primary constructor parameter type must be a supported primitive type.  |

<a id="faq"></a>

## FAQ <a href="#table-of-contents" style="float:right">↑ Back to top</a>

**Q: The analyzer didn't pick up my latest changes — why?**

A: This is usually a Visual Studio cache issue. **Clean the solution** and rebuild. The compiled
assembly always contains the latest generated code, so it is safe to ignore in the meantime.

<a id="license"></a>

## License <a href="#table-of-contents" style="float:right">↑ Back to top</a>

This project is licensed under the [MIT License](LICENSE.txt).

[nuget-package]: https://www.nuget.org/packages/Len.StronglyTypedId/
[license-file]: LICENSE.txt
[badge-nuget]: https://img.shields.io/nuget/v/Len.StronglyTypedId.svg
[badge-codecov]: https://codecov.io/github/LenFon/StronglyTypedId/branch/main/graph/badge.svg?token=S3PBV7W190
[badge-license]: https://img.shields.io/badge/License-MIT-blue.svg
