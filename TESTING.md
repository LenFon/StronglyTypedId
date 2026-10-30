# Len.StronglyTypedId.Testing

Test helpers for [Len.StronglyTypedId](https://github.com/lenfon/StronglyTypedId): create strongly typed ids with plausible values
instead of hand-writing `OrderId.Create(Guid.NewGuid())` in every arrange block.

Three packages:

| Package | Purpose | Dependencies |
| --- | --- | --- |
| `Len.StronglyTypedId.Testing` | `Any` / `AnyIdGenerator` — value generation for every supported primitive | none |
| `Len.StronglyTypedId.Testing.AutoFixture` | `fixture.Create<OrderId>()` support | `AutoFixture` |
| `Len.StronglyTypedId.Testing.Bogus` | `faker.Generate<Order>()` support | `Bogus` |

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
// Same seed ⇒ same value on any .NET version and platform.
OrderId first = Any.Id<OrderId>(seed: 42);
OrderId again = Any.Id<OrderId>(seed: 42);   // equal to first

// A reproducible sequence needs one generator, not one call per id.
IReadOnlyList<OrderId> ids = Any.Ids<OrderId>(10, seed: 42);

// Or keep the generator around.
var any = new AnyIdGenerator(new AnyIdOptions { Seed = 42, StringLength = 12 });
CustomerId customerId = any.Id<CustomerId>();
```

Reproducibility is guaranteed across .NET versions and platforms: the value source is a built-in
SplitMix64 rather than `Random`, whose algorithm is an implementation detail that may change between
releases. Golden-file and snapshot tests can therefore assert on exact values. Without a seed the
generator is seeded from the clock, as before.

## Ids with a validator

A validator is an arbitrary predicate, so no generator can guarantee a satisfying value. The generator
retries with fresh values up to `AnyIdOptions.MaxAttempts` (100 by default) and then fails with an
`InvalidOperationException` telling you to register a factory:

```csharp
Any.Register<InvoiceNumber>(() => InvoiceNumber.Create(NextInvoiceNumber()));
```

A rejection is recognised by its type: `ArgumentException` by default, or whatever the id configures
through `ExceptionType` (per id, falling back to `[assembly: StronglyTypedIdDefaults]`). Anything else
thrown by the validator is a bug in the validator, so it propagates instead of being retried.

The exception type is the **only** signal, so a validator that throws `ArgumentException` for an
unrelated reason — a null argument, a failed cast, an invariant it asserts — is indistinguishable from
a rejection: the value is retried until `MaxAttempts` and then reported as "cannot generate". Keep
validators total (return `false` instead of throwing), or register a factory for that id.

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

## Boundary values

Values stay ordinary by default: `int.MaxValue` is reachable in principle but never actually drawn.
Turn on `IncludeBoundaryValues` when the code under test is boundary-sensitive — formatting width,
overflow checks, database column limits:

```csharp
var any = new AnyIdGenerator(new AnyIdOptions { IncludeBoundaryValues = true });
```

Roughly one value in sixteen is then the primitive's `MinValue` or `MaxValue` (integral and `decimal`
types only). Leave it off when a test asserts uniqueness: two ids can then collide on the same extreme.

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

## Bogus

```csharp
using Bogus;
using Len.StronglyTypedId.Testing;

var faker = new Faker<Order>().WithStronglyTypedIds();

Order order = faker.Generate();   // order.Id is a real id
```

Bogus fills a member by its declared type, and a strongly typed id is a distinct type — without this
adapter Bogus cannot produce one, so the member stays at its default value **without any error**. The
adapter registers Bogus' own `RuleForType` rules for every id type, so generation fills them like any
other member.

Bogus instantiates the target type through its parameterless constructor, so the type it fills needs
one: a class with settable properties, not a record with a primary constructor.

The id types are discovered from the assemblies loaded right now. When the assembly declaring an id is
not loaded yet, name the types explicitly:

```csharp
var faker = new Faker<Order>()
    .WithStronglyTypedIds(new AnyIdGenerator(), typeof(OrderId), typeof(CustomerId));
```

Pass a seeded `AnyIdGenerator` for reproducible data. The adapter never overwrites a rule you wrote
yourself — Bogus keeps it.

## Notes

- Values are deliberately ordinary: finite floating-point numbers (either sign), non-empty strings,
  dates inside 1970–2100. A fixture handing out `double.NaN` or `DateTime.MaxValue` mostly produces
  failures unrelated to the code under test — `NaN` is not even valid JSON, so a round-trip test would
  fail for a reason that has nothing to do with the id.
- Strings are drawn from letters, digits and the separators `@ . - _ / :`, so a validator that looks
  for an `@` or a `.` can be satisfied without registering a factory. Anything stricter (a real email
  regex) still needs `Register`.
- `AnyIdGenerator` is thread-safe, but the sequence it produces under parallel access is not
  deterministic even with a fixed seed.
