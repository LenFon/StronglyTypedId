---
layout: default
title: Testing
---

# Testing

<a id="testing"></a>

## Test helpers

`Len.StronglyTypedId.Testing` builds ids for arrange blocks, so tests do not need
`OrderId.Create(Guid.NewGuid())` everywhere:

```csharp
using Len.StronglyTypedId.Testing;

OrderId orderId = Any.Id<OrderId>();
IReadOnlyList<OrderId> batch = Any.Ids<OrderId>(10);
```

Values are derived from the primitive exposed by `IStronglyTypedId<TSelf, TPrimitiveId>`, so ids
declared in any assembly work, including ones compiled after the testing package. Every supported
primitive is covered, plus `enum`. Values are deliberately ordinary (finite floats of either sign,
non-empty strings, dates inside 1970–2100) to avoid failures unrelated to the code under test.

- **Reproducible.** `Any.Id<OrderId>(seed: 42)` returns the same value across .NET versions and
  platforms, not just within one runtime: the value source is a built-in SplitMix64, not `Random`, whose
  algorithm is an implementation detail. Use one `AnyIdGenerator` instance (or `Any.Ids<T>(count, seed)`)
  when a whole sequence must repeat. A long-lived generator takes the same seed through
  `new AnyIdOptions { Seed = 42 }`; without a seed it is seeded from the clock.
- **Validator-aware.** Generation retries up to `AnyIdOptions.MaxAttempts` (100 by default) and then
  throws with a message pointing at `Any.Register<T>(factory)`. A rejection is recognised by exception
  type alone, so a validator that throws `ArgumentException` for an unrelated reason is retried too.
- **Overridable.** `AnyIdOptions.PrimitiveValueFactory` replaces the value source per primitive type;
  returning `null` falls back to the built-in generator.
- **Resettable.** `Any.Reset()` discards the shared generator together with every `Any.Register<T>(factory)`
  made on it. Call it between tests that register conflicting factories.
- **Configurable text.** `AnyIdOptions.StringLength` (8 by default) sets the length of generated `string`
  primitives; raise it for validators that demand longer input.
- **Boundary-aware on demand.** `AnyIdOptions.IncludeBoundaryValues` mixes in the primitive's
  `MinValue` / `MaxValue` (about one value in sixteen). Off by default: extremes make uniqueness
  assertions flaky, and ordinary values are what most tests want. Integral and `decimal` primitives only;
  the floating-point boundaries are `NaN` / `±Infinity`, which are never generated because they make JSON
  round-trips fail for reasons unrelated to the code under test.

Two more sources deserve a note: an `enum` draws from its declared members, and `char` draws from a
68-character alphabet (letters, digits and `@ . - _ / :`), so a validator that requires a separator can
still succeed inside the retry budget.

`AnyIdGenerator.Create(Type)` is the non-generic form of `Any.Id<T>()`, for the cases where the id type is
only known at run time, such as a theory over a `Type[]` or a reflection-driven test. It throws
`ArgumentException` for a type that is not a strongly typed id.

### Checking named instances

A `[StronglyTypedIdInstance]` value that the id's `Validator` rejects is not caught at compile time: the
type initializer throws `TypeInitializationException` the first time the type is touched, which in a test
suite can surface far from the declaration. `StronglyTypedIdConformance` reports every broken instance in
one pass:

```csharp
// Throws with all broken instances listed; use it as a one-line test per assembly.
StronglyTypedIdConformance.AssertAllInstancesValid(typeof(OrderId).Assembly);

// Or inspect the list yourself.
IReadOnlyList<(Type IdType, string Message)> broken =
    StronglyTypedIdConformance.FindBrokenInstances(typeof(OrderId).Assembly);
```

Custom primitives are the case this exists for: the generator runs on `netstandard2.0` and cannot execute
your `Parse`, so their instance values are only validated at run time.

For AutoFixture, add `Len.StronglyTypedId.Testing.AutoFixture` and enable the customization:

```csharp
var fixture = new Fixture().WithStronglyTypedIds();

OrderId orderId = fixture.Create<OrderId>();
```

With Bogus, add `Len.StronglyTypedId.Testing.Bogus` and switch the customization on:

```csharp
var faker = new Faker<Order>().WithStronglyTypedIds();

Order order = faker.Generate();   // order.Id is a real id
```

Bogus fills a member by its declared type, and a strongly typed id is a distinct type, so without this
adapter Bogus cannot produce one and the member stays at its default value **without any error**. The
adapter registers Bogus' own type rules for every id type, so generation fills them like any other member.

The id types are discovered from the assemblies loaded right now. When the assembly declaring an id is
not loaded yet, name the types explicitly:

```csharp
var faker = new Faker<Order>()
    .WithStronglyTypedIds(new AnyIdGenerator(), typeof(OrderId), typeof(CustomerId));
```

See [TESTING.md](https://github.com/LenFon/StronglyTypedId/blob/main/TESTING.md) for the full guide.

<a id="samples"></a>

## Samples

Every feature described above has a runnable counterpart under
[`samples/`](https://github.com/LenFon/StronglyTypedId/tree/main/samples). Pick the one closest to what you
are building:

| Project | Target | Demonstrates |
|---|---|---|
| `Len.StronglyTypedId.SampleConsole` | `net8.0;net10.0` | Guided tour of the core surface: `Create` / `TryCreate` / `Parse`, formatting, equality and ordering, deconstruction, validators, normalizers, named instances, text prefixes (including the formatting path), `TypeConverter`, integration filtering and allow-lists, System.Text.Json and Newtonsoft.Json, JSON edge cases (dictionary keys, undefined enum values, text primitives, `JsonShape.Object`), source-generated `JsonSerializerContext`, the 23-primitive + enum matrix, string-primitive ordinal comparison and empty-string semantics, reference-type ids, custom primitives (including ULID), configuration binding, nesting, generic containers, the interface hierarchy, reflection helpers (including nullable ids), the debugger view and the testing packages (including Bogus). **Start here.** |
| `Len.StronglyTypedId.SampleIntegrations` | `net8.0` | Both EF Core registration entries (`AddStronglyTypedIds(ModelConfigurationBuilder)` and `AddStronglyTypedIds(ModelBuilder)`) and Dapper (`AddStronglyTypedIds()`) against in-memory SQLite, including the driver-returns-a-different-CLR-type conversion path, rejection of invalid values and the NULL semantics of reference-type ids; MessagePack, MongoDB BSON and protobuf-net round-trips (including reference-type null semantics and validator rejection). |
| `Len.StronglyTypedId.SampleDefaults` | `net8.0` | Every option of the assembly-level `[assembly: StronglyTypedIdDefaults(...)]` (including `Integrations`), plus how per-type overrides take precedence (which options can be overridden both ways and which only switch on). |
| `Len.StronglyTypedId.Sample1` | `net8.0` | MVC model binding and `{id:OrderId}` route constraints, Newtonsoft.Json as the MVC formatter, EF Core (SQL Server) and Swashbuckle 6. Needs a reachable SQL Server to start. |
| `Len.StronglyTypedId.Sample2` | `net10.0` | Minimal API: `IParsable` binding, `[AsParameters]`, route constraints, JSON bodies and the built-in OpenAPI transformer (`AddOpenApi(options => options.AddStronglyTypedIds())`). |
| `Len.StronglyTypedId.Sample3` | `net8.0` | MVC + Swashbuckle 10 (Microsoft.OpenApi 2.x) schema mapping. |
| `Len.StronglyTypedId.Sample4` | `net8.0` | `net8.0` with `Microsoft.AspNetCore.OpenApi` 8.0.0: the built-in schema transformer needs 9.0+, so the document is produced by Swashbuckle instead. |
| `Len.StronglyTypedId.SampleGraphQL` | `net8.0` | HotChocolate (`AddStronglyTypedIds()` on `IRequestExecutorBuilder`): the id is a `SCALAR` in the schema, and its literal kind follows the JSON form: `String` for `Guid` and prefixed ids, `Int` for integers and enums (by underlying value), `Float` for floating points. Also shows the variable path (`JsonElement`) and both failure paths. |

Each sample is small and self-contained; the console ones print one section per feature, so you can read
the output next to the code. Run them with:

```shell
dotnet run --project samples/Len.StronglyTypedId.SampleConsole
dotnet run --project samples/Len.StronglyTypedId.SampleIntegrations
dotnet run --project samples/Len.StronglyTypedId.SampleDefaults
dotnet run --project samples/Len.StronglyTypedId.SampleGraphQL
```

The console samples print each step and exit. The web samples publish their document at
`/openapi/v1.json` (Sample2) or `/swagger/v1/swagger.json` (Sample1, Sample3, Sample4).

### Reading the samples

- The samples use `record struct` for ids, which is the recommended default. `SampleConsole` also shows
  nested ids and ids inside a generic container.
- Sample code never calls `new OrderId(...)` on an id that has a validator, because the primary
  constructor bypasses the validator and the normalizer. Route values through `Create` instead.
- `SampleConsole` targets both `net8.0` and `net10.0`; its UTF-8 span section compiles only under
  `net10.0`, which is where `IUtf8SpanParsable` becomes available.

[<- Back to the index](index.md) · [中文](zh-CN/testing.md)
