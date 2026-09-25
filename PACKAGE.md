# StronglyTypedId

[![NuGet][badge-nuget]][nuget-package]
[![License: MIT][badge-license]][license-file]
[![codecov][badge-codecov]][codecov]

`Len.StronglyTypedId` is a Roslyn source generator, analyzer, and code-fix provider that turns a
one-line `record` (or `record struct`) declaration into a fully featured strongly typed identifier. It
generates the `IStronglyTypedId<TSelf, TPrimitiveId>` implementation, `IParsable<TSelf>` support, and the
System.Text.Json / Newtonsoft.Json / Entity Framework Core / Dapper / Swashbuckle / ASP.NET Core
integrations for you — so a `Guid` can never be passed where an `OrderId` is expected, and vice versa.

A built-in analyzer rejects invalid declarations as compile errors, and most of the rules come with a
code fix that repairs the declaration for you.

## Getting started

### Install

```text
Package Manager : Install-Package Len.StronglyTypedId
CLI             : dotnet add package Len.StronglyTypedId
```

One package is enough: `Len.StronglyTypedId` already depends on `Len.StronglyTypedId.Generators`, which
carries the generator, the analyzer, and the code fixes.

### Prerequisites

| Component                       | Requirement                                                                                  |
| ------------------------------- | -------------------------------------------------------------------------------------------- |
| Target framework of your project | **`net8.0`, `net10.0`**, or a later compatible framework.                                  |
| .NET SDK                        | **8.0 or later** (the generator needs Roslyn 4.4+, provided by the .NET 8 SDK and later).    |
| Newtonsoft.Json                 | ≥ **13.0.0** (only when using the Newtonsoft.Json converter).                                |
| Entity Framework Core           | ≥ **7.0.0** (only when using the EF Core converter).                                         |
| Swashbuckle.AspNetCore          | ≥ **6.0.0** (only when generating Swagger schema mappings).                                  |
| Dapper                          | ≥ **2.0.0** (only when using the Dapper integration).                                        |
| ASP.NET Core MVC                | ≥ **2.0.0** (only when using the MVC model-binding / route-constraint integration).          |
| ASP.NET Core OpenAPI            | ≥ **9.0.0** (only when using the OpenAPI integration).                                       |

The JSON, EF Core, Dapper, Swashbuckle, MVC, and OpenAPI integrations are generated only when the
corresponding package is referenced, so nothing is added to your application's dependency graph unless
you opt in.

### Declare an id

```csharp
[StronglyTypedId]
public partial record struct OrderId(Guid Value);

// or, as a reference type:
[StronglyTypedId]
public partial record OrderId(Guid Value);
```

`record struct` produces a value type (no `null`, no boxing on interface calls); `record` produces a
reference type (can be `null`). Both expose the same generated members and are usable as dictionary
keys.

The generated type exposes `Value`, a static `Create(...)` factory, and `Parse` / `TryParse`:

```csharp
var id = OrderId.Create(Guid.NewGuid());           // static factory
var parsed = OrderId.Parse("...", null);           // IParsable<TSelf>
OrderId.TryParse("...", null, out var alsoParsed); // returns false instead of throwing
```

The `Value` parameter may keep its nullability context but may not be a nullable type
(`OrderId(Guid? Value)` is rejected by the analyzer).

## Usage

### Supported primitive types

The wrapped `Value` parameter may be one of:

`Guid`, `string`, `byte`, `sbyte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`.

### Value validation

Point `Validator` at a static method on the id to reject invalid values in `Create` and `TryParse`:

```csharp
[StronglyTypedId(Validator = nameof(Validate))]
public partial record struct OrderId(Guid Value)
{
    private static bool Validate(Guid value) => value != Guid.Empty;
}

OrderId.Create(Guid.Empty);                            // throws ArgumentException
OrderId.TryParse(Guid.Empty.ToString(), null, out _);  // false, no exception
```

`new OrderId(...)` bypasses the validator because the primary constructor belongs to your declaration;
route untrusted values through `Create` / `Parse` / `TryParse`.

### Serialization

System.Text.Json and Newtonsoft.Json (≥ 13.0.0) converters are generated automatically and applied via
a generated `[JsonConverter]` attribute — no configuration required. Dictionary-key support for
System.Text.Json is included as well.

### Entity Framework Core

```csharp
protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
{
    base.ConfigureConventions(configurationBuilder);
    StronglyTypedIds.ApplyTo(configurationBuilder); // registers a ValueConverter for every id
}
```

Converters are generated only when a `DbContext` overrides `ConfigureConventions`.

### Dapper

```csharp
using Dapper;

StronglyTypedIds.ApplyTo(connection); // registers TypeHandlers for every id
```

### Swashbuckle / OpenAPI

```csharp
services.AddSwaggerGen(options => StronglyTypedIds.ApplyTo(options));   // Swashbuckle
// and/or
builder.Services.AddOpenApi(options => StronglyTypedIds.ApplyTo(options)); // ASP.NET Core OpenAPI
```

### ASP.NET Core MVC

```csharp
builder.Services.AddControllers(options => StronglyTypedIds.ApplyTo(options));
builder.Services.AddRouting(options => StronglyTypedIds.ApplyTo(options));
```

This enables `[FromRoute] OrderId id` / `[FromQuery] OrderId id` model binding and the `{id:OrderId}`
route constraint.

## Additional documentation

- Full documentation, all diagnostics (STIAO000–STIAO011), and FAQ:
  [README.md](https://github.com/lenfon/StronglyTypedId/blob/main/README.md) (English) ·
  [README.zh-CN.md](https://github.com/lenfon/StronglyTypedId/blob/main/README.zh-CN.md) (中文)
- Source repository: <https://github.com/lenfon/StronglyTypedId>

## Feedback

Questions, bug reports, and feature requests are welcome on the
[GitHub issue tracker](https://github.com/lenfon/StronglyTypedId/issues).

## License

This package is licensed under the
[MIT License](https://github.com/lenfon/StronglyTypedId/blob/main/LICENSE.txt).

[nuget-package]: https://www.nuget.org/packages/Len.StronglyTypedId/
[license-file]: https://github.com/lenfon/StronglyTypedId/blob/main/LICENSE.txt
[codecov]: https://app.codecov.io/gh/LenFon/StronglyTypedId/tree/main
[badge-nuget]: https://badgen.net/nuget/v/Len.StronglyTypedId
[badge-license]: https://badgen.net/badge/license/MIT/blue
[badge-codecov]: https://badgen.net/codecov/c/github/LenFon/StronglyTypedId
