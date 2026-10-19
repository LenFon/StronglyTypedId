# Len.StronglyTypedId.Testing

Test helpers for [Len.StronglyTypedId](../README.md): create strongly typed ids with plausible values
instead of hand-writing `OrderId.Create(Guid.NewGuid())` in every arrange block.

Two packages:

| Package | Purpose | Dependencies |
| --- | --- | --- |
| `Len.StronglyTypedId.Testing` | `Any` / `AnyIdGenerator` — value generation for every supported primitive | none |
| `Len.StronglyTypedId.Testing.AutoFixture` | `fixture.Create<OrderId>()` support | `AutoFixture` |

## Quick start

```csharp
using Len.StronglyTypedId.Testing;

OrderId orderId = Any.Id<OrderId>();
IReadOnlyList<OrderId> batch = Any.Ids<OrderId>(10);
```

The generator reads the wrapped primitive from the implemented
`IStronglyTypedId<TSelf, TPrimitiveId>` interface, so it works for ids declared in any assembly —
including ones generated after this package was compiled. Every primitive the generator supports is
covered, plus `enum`.

## Reproducible values

```csharp
// Same seed ⇒ same value for a given runtime version.
OrderId first = Any.Id<OrderId>(seed: 42);
OrderId again = Any.Id<OrderId>(seed: 42);   // equal to first

// A reproducible sequence needs one generator, not one call per id.
IReadOnlyList<OrderId> ids = Any.Ids<OrderId>(10, seed: 42);

// Or keep the generator around.
var any = new AnyIdGenerator(new AnyIdOptions { Seed = 42, StringLength = 12 });
CustomerId customerId = any.Id<CustomerId>();
```

Reproducibility is guaranteed for a given runtime version only: the algorithm behind `Random` is an
implementation detail and may change between .NET releases. Pin the runtime in CI if you assert on
exact values.

## Ids with a validator

A validator is an arbitrary predicate, so no generator can guarantee a satisfying value. The generator
retries with fresh values up to `AnyIdOptions.MaxAttempts` (100 by default) and then fails with an
`InvalidOperationException` telling you to register a factory:

```csharp
Any.Register<InvoiceNumber>(() => InvoiceNumber.Create(NextInvoiceNumber()));
```

`Register` affects the shared generator used by the static `Any` members; call `Any.Reset()` between
tests to drop registrations. Prefer a dedicated `AnyIdGenerator` instance when a test needs isolation.

## Custom primitive values

```csharp
var any = new AnyIdGenerator(new AnyIdOptions
{
    PrimitiveValueFactory = type => type == typeof(Guid) ? KnownGuid : null,
});
```

Return `null` to fall back to the built-in generator for that type.

## AutoFixture

```csharp
using AutoFixture;
using Len.StronglyTypedId.Testing;

var fixture = new Fixture().WithStronglyTypedIds();

OrderId orderId = fixture.Create<OrderId>();
```

The customization is inserted at the front of the customization list, so it wins over AutoFixture's
constructor-based strategy — which would otherwise bypass `Create` and therefore the validator. Pass a
seeded `AnyIdGenerator` to `WithStronglyTypedIds` for reproducible fixtures.

## Notes

- Values are deliberately ordinary: finite floating-point numbers, non-empty strings, dates inside
  1970–2100. A fixture handing out `double.NaN` or `DateTime.MaxValue` mostly produces failures
  unrelated to the code under test.
- `AnyIdGenerator` is thread-safe, but the sequence it produces under parallel access is not
  deterministic even with a fixed seed.
