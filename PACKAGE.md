# StronglyTypedId

[![NuGet][badge-nuget]][nuget-package]
[![License: MIT][badge-license]][license-file]

`Len.StronglyTypedId` is a Roslyn source generator (with analyzer + code fixes) that turns a one-line
`record` declaration into a fully featured strongly typed identifier. It generates the
`IStronglyTypedId<TSelf, TPrimitiveId>` and `IParsable<TSelf>` implementation plus System.Text.Json /
Newtonsoft.Json / EF Core / Dapper / Swashbuckle / ASP.NET Core / MessagePack / MongoDB / HotChocolate /
protobuf-net integrations — so a `Guid` can never be passed where an `OrderId` is expected, and vice versa.

## Install

```text
dotnet add package Len.StronglyTypedId
```

One package is enough — it already depends on `Len.StronglyTypedId.Generators`. Its target frameworks are
`net8.0` and `net10.0`, so the package works in any project targeting either one. Integrations are generated
only when the matching package is referenced.

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
`[assembly: StronglyTypedIdDefaults(...)]` sets assembly-wide defaults. The flags are
`EntityFrameworkCore`, `Dapper`, `SystemTextJson`, `NewtonsoftJson`, `Swagger`, `AspNetCoreOpenApi`,
`AspNetCoreMvc`, `MessagePack`, `MongoDb`, `HotChocolate` and `Protobuf`:

- **Serialization** — `SystemTextJson` and `NewtonsoftJson` converters via a generated `[JsonConverter]`.
- **Binary serialization** — `MessagePack` and `MongoDb`: a generated `IMessagePackFormatter<TId>` /
  `IBsonSerializer<TId>`, wired up by `MessagePackSerializerOptions.AddStronglyTypedIds()` and
  `StronglyTypedIds.RegisterBsonSerializers()`. Both write the primitive value itself — the text prefix
  belongs to the text form (`ToString()` / JSON) and does not reach the payload. The exception is
  `BigInteger`, `Int128` and `UInt128`: they have no native MessagePack / BSON representation, so those
  ids are stored as their invariant-culture text, prefix included.
- **EF Core** — `EntityFrameworkCore`: `configurationBuilder.AddStronglyTypedIds()` from
  `ConfigureConventions`, plus a model-scanning `modelBuilder.AddStronglyTypedIds()` from
  `OnModelCreating` for contexts that do not override `ConfigureConventions`. Both overloads are
  generated whenever EF Core ≥ 7.0.0 is referenced.
- **Dapper** — `Dapper`: `StronglyTypedIds.AddStronglyTypedIds()` registers `TypeHandler`s in Dapper's process-wide
  registry, so it only needs to run once. The `AddStronglyTypedIds(IDbConnection)` overload is kept for source
  compatibility and ignores its argument.
- **Swashbuckle / OpenAPI** — `Swagger` and `AspNetCoreOpenApi`: `options.AddStronglyTypedIds()` for
  `AddSwaggerGen` / `AddOpenApi`.
- **ASP.NET Core MVC** — `AspNetCoreMvc`: `options.AddStronglyTypedIds()` enables `[FromRoute] OrderId id`
  and `{id:OrderId}`.
- **GraphQL** — `HotChocolate`: a generated `ScalarType<TId>` per id, wired up by
  `AddStronglyTypedIds()` on `IRequestExecutorBuilder`. The representation matches the JSON form, so an id
  is not written one way over REST and another over GraphQL: text primitives (including prefixed ids) are
  GraphQL `String`, integers and enums are `Int` (enums by underlying value, not member name), and floating
  points are `Float`. The entry point is idempotent and uses `BindRuntimeType`, without which HotChocolate
  would infer the id as an object type and the schema would fail to build.
- **protobuf-net** — `Protobuf`: `[ProtoContract(Surrogate = …)]` on the id plus a nested
  `{TypeName}Surrogate`, so `Serializer.Serialize(order)` works with no registration call. The payload is
  the primitive value itself; `Int128`, `UInt128`, `Half`, `DateTimeOffset`, `BigInteger` and custom
  primitives have no native protobuf representation and are stored as canonical text. Ids from a
  referenced assembly get no surrogate — the attribute can only be applied in the declaring compilation.

## Testing

`Len.StronglyTypedId.Testing` builds ids for arrange blocks (`Any.Id<OrderId>()`, `Any.Ids<OrderId>(10)`),
and `Len.StronglyTypedId.Testing.AutoFixture` / `Len.StronglyTypedId.Testing.Bogus` wire the same
generator into AutoFixture (`fixture.WithStronglyTypedIds()`) and Bogus (`faker.WithStronglyTypedIds()`).
All of them respect generated validators.

## Native AOT & trimming

The runtime library is marked `<IsAotCompatible>true</IsAotCompatible>`: the core types and every
generated converter avoid dynamic code generation and loading types by name, so strongly typed ids
work in Native AOT and trimmed applications. The only reflection entry points —
`StronglyTypedIdExtensions.TryGetPrimitiveIdType`, `GetPrimitiveIdType` and `IsStronglyTypedId` — are
annotated with `[DynamicallyAccessedMembers(Interfaces)]` so the interface metadata that identifies
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
