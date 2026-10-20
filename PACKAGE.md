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
var parsed = OrderId.Parse("...", null);           // IParsable<TSelf>; a null provider means the invariant culture
OrderId.TryParse("...", null, out var alsoParsed); // false instead of throwing
```

Supported primitive types: `Guid`, `string`, `char`, `byte`, `sbyte`, `short`, `ushort`, `int`, `uint`,
`long`, `ulong`, `Int128`, `UInt128`, `decimal`, `Half`, `float`, `double`, `DateTime`, `DateTimeOffset`,
`TimeSpan`, `DateOnly`, `TimeOnly`, `BigInteger`, and any `enum`. Use `record struct` for a value type,
`record` for a reference type.

Point `Validator` at a static method to reject invalid values:

```csharp
[StronglyTypedId(Validator = nameof(Validate))]
public partial record struct OrderId(Guid Value)
{
    private static bool Validate(Guid value) => value != Guid.Empty;
}
```

Point `Normalizer` at a static method to repair values before they are validated and stored — trim
whitespace, fold case, drop separators:

```csharp
[StronglyTypedId(Normalizer = nameof(Normalize), Validator = nameof(IsEmail))]
public partial record struct EmailAddress(string Value)
{
    private static string Normalize(string value) => value.Trim().ToLowerInvariant();

    private static bool IsEmail(string value) => value.Contains('@');
}

EmailAddress.Create("  Alice@Example.COM  ").Value; // "alice@example.com"
```

Swap the exception `Create` throws when the validator rejects a value, and declare named static instances:

```csharp
[StronglyTypedId(Validator = nameof(Validate), ExceptionType = typeof(DomainRuleViolationException))]
[StronglyTypedIdInstance("Unspecified", "-1")]
public partial record struct TenantId(int Value)
{
    private static bool Validate(int value) => value != 0;
}

var tenant = TenantId.Unspecified;   // public static readonly, built through Create
```

Instances are constructed through `Create`, so they honour the normalizer and validator and
`Parse(instance.ToString())` always reads back.

Give an id a canonical text form with `Prefix` (`"ord_3f2c…"`), choose the JSON shape with `JsonShape`,
and narrow the generated integrations per id with `Integrations`:

```csharp
[StronglyTypedId(Prefix = "ord_", JsonShape = JsonShape.Object,
                 Integrations = StronglyTypedIdIntegrations.Dapper | StronglyTypedIdIntegrations.SystemTextJson)]
public partial record struct OrderId(Guid Value);
```

## Integrations

Generated only when the matching package is referenced; `Integrations` can narrow the set per id, and
`[assembly: StronglyTypedIdDefaults(...)]` sets assembly-wide defaults:

- **Serialization** — System.Text.Json and Newtonsoft.Json converters via a generated `[JsonConverter]`.
- **EF Core** — `StronglyTypedIds.ApplyTo(configurationBuilder)` from `ConfigureConventions`, plus a
  model-scanning `StronglyTypedIds.ApplyTo(modelBuilder)` from `OnModelCreating`. Both overloads are
  generated together whenever a `DbContext` overrides `ConfigureConventions` and EF Core ≥ 7.0.0 is referenced.
- **Dapper** — `StronglyTypedIds.ApplyTo()` registers `TypeHandler`s in Dapper's process-wide registry,
  so it only needs to run once. The `ApplyTo(IDbConnection)` overload is kept for source compatibility and
  ignores its argument.
- **Swashbuckle / OpenAPI** — `StronglyTypedIds.ApplyTo(options)` for `AddSwaggerGen` / `AddOpenApi`.
- **ASP.NET Core MVC** — `StronglyTypedIds.ApplyTo(options)` enables `[FromRoute] OrderId id` and `{id:OrderId}`.

## Testing

`Len.StronglyTypedId.Testing` builds ids for arrange blocks (`Any.Id<OrderId>()`, `Any.Ids<OrderId>(10)`),
and `Len.StronglyTypedId.Testing.AutoFixture` wires the same generator into AutoFixture
(`fixture.WithStronglyTypedIds()`). Both respect generated validators.

## Native AOT & trimming

The runtime library is marked `<IsAotCompatible>true</IsAotCompatible>`: the core types and every
generated converter avoid dynamic code generation and loading types by name, so strongly typed ids
work in Native AOT and trimmed applications. The only reflection entry point,
`StronglyTypedIdExtensions.TryGetPrimitiveIdType` / `GetPrimitiveIdType` / `IsStronglyTypedId`,
is annotated with `[DynamicallyAccessedMembers(Interfaces)]` so the interface metadata that identifies
a strongly typed id is preserved under trimming.

## Changelog

Every release, including previews, is documented in
[CHANGELOG.md](https://github.com/lenfon/StronglyTypedId/blob/main/CHANGELOG.md).

## License

[MIT License](https://github.com/lenfon/StronglyTypedId/blob/main/LICENSE.txt).

[nuget-package]: https://www.nuget.org/packages/Len.StronglyTypedId/
[license-file]: https://github.com/lenfon/StronglyTypedId/blob/main/LICENSE.txt
[badge-nuget]: https://badgen.net/nuget/v/Len.StronglyTypedId
[badge-license]: https://badgen.net/badge/license/MIT/blue
