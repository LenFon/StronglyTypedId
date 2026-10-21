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
declared in any assembly work — including ones compiled after the testing package. Every supported
primitive is covered, plus `enum`. Values are deliberately ordinary (finite floats of either sign,
non-empty strings, dates inside 1970–2100) to avoid failures unrelated to the code under test.

- **Reproducible** — `Any.Id<OrderId>(seed: 42)` returns the same value for a given runtime version;
  use one `AnyIdGenerator` instance (or `Any.Ids<T>(count, seed)`) when a whole sequence must repeat.
- **Validator-aware** — generation retries up to `AnyIdOptions.MaxAttempts` (100 by default) and then
  throws with a message pointing at `Any.Register<T>(factory)`. A rejection is recognised by exception
  type alone, so a validator that throws `ArgumentException` for an unrelated reason is retried too.
- **Overridable** — `AnyIdOptions.PrimitiveValueFactory` replaces the value source per primitive type;
  returning `null` falls back to the built-in generator.
- **Resettable** — `Any.Reset()` discards the shared generator together with every `Any.Register<T>(factory)`
  made on it. Call it between tests that register conflicting factories.
- **Configurable text** — `AnyIdOptions.StringLength` (8 by default) sets the length of generated `string`
  primitives; raise it for validators that demand longer input.
- **Boundary-aware on demand** — `AnyIdOptions.IncludeBoundaryValues` mixes in the primitive's
  `MinValue` / `MaxValue` (about one value in sixteen). Off by default: extremes make uniqueness
  assertions flaky, and ordinary values are what most tests want. Integral and `decimal` primitives only —
  the floating-point boundaries are `NaN` / `±Infinity`, which are never generated because they make JSON
  round-trips fail for reasons unrelated to the code under test.

Two more sources deserve a note: an `enum` draws from its declared members, and `char` draws from a
71-character alphabet (letters, digits and `@ . - _ / :`), so a validator that requires a separator can
still succeed inside the retry budget.

For AutoFixture, add `Len.StronglyTypedId.Testing.AutoFixture` and enable the customization:

```csharp
var fixture = new Fixture().WithStronglyTypedIds();

OrderId orderId = fixture.Create<OrderId>();
```

See [TESTING.md](https://github.com/LenFon/StronglyTypedId/blob/main/TESTING.md) for the full guide.

<a id="samples"></a>

## Samples

Every feature described above has a runnable counterpart under
[`samples/`](https://github.com/LenFon/StronglyTypedId/tree/main/samples):

| Project | Target | Demonstrates |
|---|---|---|
| `Len.StronglyTypedId.SampleConsole` | `net8.0;net10.0` | Guided tour of the core surface: `Create` / `TryCreate` / `Parse`, formatting, equality and ordering, deconstruction, validators, named instances, text prefixes, `TypeConverter`, System.Text.Json and Newtonsoft.Json, nesting, generic containers, reflection helpers and the testing package. |
| `Len.StronglyTypedId.SampleIntegrations` | `net8.0` | EF Core (`ApplyTo(ModelConfigurationBuilder)`) and Dapper (`ApplyTo()`) against in-memory SQLite, plus assembly-level `StronglyTypedIdDefaults`. |
| `Len.StronglyTypedId.Sample1` | `net8.0` | MVC model binding and `{id:OrderId}` route constraints, Newtonsoft.Json as the MVC formatter, EF Core (SQL Server) and Swashbuckle 6. Needs a reachable SQL Server to start. |
| `Len.StronglyTypedId.Sample2` | `net10.0` | Minimal API: `IParsable` binding, `[AsParameters]`, route constraints, JSON bodies and the built-in OpenAPI transformer (`AddOpenApi(ApplyTo)`). |
| `Len.StronglyTypedId.Sample3` | `net8.0` | MVC + Swashbuckle 10 (Microsoft.OpenApi 2.x) schema mapping. |
| `Len.StronglyTypedId.Sample4` | `net8.0` | `Microsoft.AspNetCore.OpenApi` pinned to **8.0.0**: the package resolves, the app starts, and the document is produced by Swashbuckle — the built-in schema transformer needs 9.0+. |

### Where each feature lives

| Feature | File |
| --- | --- |
| `Create` / `TryCreate` / `Parse` / `TryParse`, deconstruction, equality | `SampleConsole/Demos/CoreDemo.cs` |
| Validator, normalizer (repair before validating) | `SampleConsole/Demos/ValidationDemo.cs` |
| Text prefix (lenient and strict reads), `JsonShape.Object`, `TypeConverter` | `SampleConsole/Demos/TextDemo.cs` |
| Named instances (`[StronglyTypedIdInstance]`) | `SampleConsole/Demos/InstanceDemo.cs` |
| `ExceptionType` | `SampleConsole/Demos/ExceptionDemo.cs` |
| `[Flags]` enum bit validation | `SampleConsole/Demos/FlagsDemo.cs` |
| Culture invariance (de-DE decimal comma) | `SampleConsole/Demos/CultureDemo.cs` |
| `Integrations = None` | `SampleConsole/Demos/IntegrationsDemo.cs` |
| System.Text.Json, including dictionary keys and explicit registration | `SampleConsole/Demos/SystemTextJsonDemo.cs` |
| Newtonsoft.Json, including dictionary keys and explicit registration | `SampleConsole/Demos/NewtonsoftJsonDemo.cs` |
| Nested types, generic containers, generic factories | `SampleConsole/Demos/StructureDemo.cs` |
| Runtime reflection helpers | `SampleConsole/Demos/ReflectionDemo.cs` |
| `Any.Id` / `Any.Ids` / `Any.Register` / AutoFixture | `SampleConsole/Demos/TestingDemo.cs` |
| EF Core value converters | `SampleIntegrations/EfCoreDemo.cs` |
| Dapper type handlers | `SampleIntegrations/DapperDemo.cs` |
| MVC model binding and route constraints | `Sample1/Controllers/OrderController.cs` |
| Minimal API binding and `[AsParameters]` | `Sample2/Program.cs` |
| OpenAPI / Swagger schema mapping | `Sample2/Program.cs`, `Sample3/Program.cs` |
| OpenAPI 8.0 fallback (document from Swashbuckle) | `Sample4/Program.cs` |

```shell
dotnet run --project samples/Len.StronglyTypedId.SampleConsole
dotnet run --project samples/Len.StronglyTypedId.SampleIntegrations
```

The console samples print each step and exit. The web samples publish their document at
`/openapi/v1.json` (Sample2) or `/swagger/v1/swagger.json` (Sample1, Sample3, Sample4).

### Conventions

- The samples use `record struct` throughout; `SampleConsole/Ids.cs` also carries `record` reference
  types plus the nested and generic-container examples.
- Sample code never calls `new OrderId(...)`: the primary constructor bypasses the validator and the
  normalizer, and `STIA0011` flags it for any id that has a validator configured.
- `SampleConsole` targets both `net8.0` and `net10.0`; its UTF-8 span section compiles only under
  `net10.0`, which is what demonstrates the `IUtf8SpanParsable` availability difference.

[<- Back to the index](index.md) · [中文](zh-CN/testing.md)
