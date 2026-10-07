# StronglyTypedId

[![NuGet][badge-nuget]][nuget-package]
[![License: MIT][badge-license]][license-file]

`Len.StronglyTypedId` is a Roslyn source generator (with analyzer + code fixes) that turns a one-line
`record` declaration into a fully featured strongly typed identifier. It generates the
`IStronglyTypedId<TSelf, TPrimitiveId>` and `IParsable<TSelf>` implementation plus System.Text.Json /
Newtonsoft.Json / EF Core / Dapper / Swashbuckle / ASP.NET Core integrations — so a `Guid` can never be
passed where an `OrderId` is expected, and vice versa.

## Install

```text
dotnet add package Len.StronglyTypedId
```

One package is enough — it already depends on `Len.StronglyTypedId.Generators`. Requires .NET 8.0+ SDK
and a target framework `net8.0` / `net10.0`. Integrations are generated only when the matching package
is referenced.

## Quick start

```csharp
[StronglyTypedId]
public partial record struct OrderId(Guid Value);

var id = OrderId.Create(Guid.NewGuid());           // static factory
var parsed = OrderId.Parse("...", null);           // IParsable<TSelf>
OrderId.TryParse("...", null, out var alsoParsed); // false instead of throwing
```

Supported primitive types: `Guid`, `string`, `byte`, `sbyte`, `short`, `ushort`, `int`, `uint`,
`long`, `ulong`, `decimal`, `DateTime`, `DateTimeOffset`, `TimeSpan`, and any `enum`. Use
`record struct` for a value type, `record` for a reference type.

String-backed ids are **interned**: equal string values share a single underlying instance, so a
repeated id costs almost no extra memory. Interning is scoped per id type (not the global
`string.Intern` pool), so it cannot leak memory for unbounded value sets.

Point `Validator` at a static method to reject invalid values:

```csharp
[StronglyTypedId(Validator = nameof(Validate))]
public partial record struct OrderId(Guid Value)
{
    private static bool Validate(Guid value) => value != Guid.Empty;
}
```

## Integrations

All opt-in, generated only when the matching package is referenced:

- **Serialization** — System.Text.Json and Newtonsoft.Json converters via a generated `[JsonConverter]`.
- **EF Core** — `StronglyTypedIds.ApplyTo(configurationBuilder)` from `ConfigureConventions`, plus a
  model-scanning `StronglyTypedIds.ApplyTo(modelBuilder)` from `OnModelCreating`. Both overloads are
  generated together whenever a `DbContext` overrides `ConfigureConventions` and EF Core ≥ 7.0.0 is referenced.
- **Dapper** — `StronglyTypedIds.ApplyTo(connection)` registers `TypeHandler`s.
- **Swashbuckle / OpenAPI** — `StronglyTypedIds.ApplyTo(options)` for `AddSwaggerGen` / `AddOpenApi`.
- **ASP.NET Core MVC** — `StronglyTypedIds.ApplyTo(options)` enables `[FromRoute] OrderId id` and `{id:OrderId}`.

## Native AOT & trimming

The runtime library is marked `<IsAotCompatible>true</IsAotCompatible>`: the core types and every
generated converter avoid dynamic code generation and loading types by name, so strongly typed ids
work in Native AOT and trimmed applications. The only reflection entry point,
`StronglyTypedIdExtensions.TryGetPrimitiveIdType` / `GetPrimitiveIdType` / `IsStronglyTypedId`,
is annotated with `[DynamicallyAccessedMembers(Interfaces)]` so the interface metadata that identifies
a strongly typed id is preserved under trimming.

## License

[MIT License](https://github.com/lenfon/StronglyTypedId/blob/main/LICENSE.txt).

[nuget-package]: https://www.nuget.org/packages/Len.StronglyTypedId/
[license-file]: https://github.com/lenfon/StronglyTypedId/blob/main/LICENSE.txt
[badge-nuget]: https://badgen.net/nuget/v/Len.StronglyTypedId
[badge-license]: https://badgen.net/badge/license/MIT/blue
