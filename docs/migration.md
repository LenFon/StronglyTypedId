---
layout: default
title: Migrating from 1.3.0 to 1.4.0
---

<a id="migration-1-4-0"></a>

# Migrating from 1.3.0 to 1.4.0

1.4.0 is the integrations release. Four changes require action on upgrade, and four more change behaviour
without breaking the build. Everything else is additive (new primitives, new integrations, new options)
and leaves existing output untouched unless you opt in.

Work through the checklist, then read [Behaviour changes](#behaviour-changes) for the ones that compile
fine but can still alter results at run time.

<a id="checklist"></a>

## Checklist

1. [Replace `StronglyTypedIds.ApplyTo(x)` with `x.AddStronglyTypedIds()`](#integration-entry-points)
2. [Fix any `STIAO###` suppressions or `.editorconfig` entries](#diagnostic-ids-use-digit-zero)
3. [Catch `FormatException` from `Parse`, not `ArgumentException`](#parse-throws-format-exception)
4. [Drop any reliance on string interning](#string-interning-removed)
5. [Check sorted collections of string-backed ids](#ordinal-string-comparison)
6. [Review culture-sensitive formatting and parsing](#invariant-culture)
7. [Re-check `STIA0011` if warnings are errors](#stia0011-warning)

> The changelog also marks two entries as breaking that do not affect a 1.3.0 upgrade: the prefix rule for
> formatted output and the seeded-generation algorithm. Both belong to features that are **new in 1.4.0**
> (`Prefix` and the testing packages did not exist in 1.3.0), so there is nothing to migrate. They are
> breaking only against builds of the unreleased 1.4.0 line, not against any published version.

<a id="integration-entry-points"></a>

## Integration entry points are now extension methods

Every integration used to be a static method on a generated `StronglyTypedIds` class. It is now an
extension method on the object being configured, and it returns that object so calls chain.

| 1.3.0 | 1.4.0 |
| --- | --- |
| `StronglyTypedIds.ApplyTo(configurationBuilder)` | `configurationBuilder.AddStronglyTypedIds()` |
| `StronglyTypedIds.ApplyTo(modelBuilder)` | `modelBuilder.AddStronglyTypedIds()` |
| `StronglyTypedIds.ApplyTo(swaggerGenOptions)` | `swaggerGenOptions.AddStronglyTypedIds()` |
| `StronglyTypedIds.ApplyTo(openApiOptions)` | `openApiOptions.AddStronglyTypedIds()` |
| `StronglyTypedIds.ApplyTo(mvcOptions)` | `mvcOptions.AddStronglyTypedIds()` |
| `StronglyTypedIds.ApplyTo(routeOptions)` | `routeOptions.AddStronglyTypedIds()` |
| `StronglyTypedIds.ApplyTo(connection)` | `connection.AddStronglyTypedIds()` |

The Dapper entry point also gained a parameterless overload, `StronglyTypedIds.AddStronglyTypedIds()`,
because the handler registry is process-wide rather than per connection. The `IDbConnection` overload is
still generated for source compatibility and now ignores its argument.

A method group such as `services.AddSwaggerGen(StronglyTypedIds.ApplyTo)` no longer compiles, so write the
lambda instead:

```csharp
services.AddSwaggerGen(options => options.AddStronglyTypedIds());
```

<a id="diagnostic-ids-use-digit-zero"></a>

## Diagnostic ids use digit zero

The rule ids were spelled with a letter `O` (`STIAO001`); they now use a digit zero (`STIA001`). This
matters wherever the id appears as a string rather than as a compiler symbol:

- `#pragma warning disable STIAO011` and `[SuppressMessage("…", "STIAO011")]`
- `dotnet_diagnostic.STIAO011.severity = none` in `.editorconfig`
- `NoWarn` entries in a project file
- baseline files and CI filters that match on the id

Search the repository for `STIAO` and replace it with `STIA0`. The failure is silent: a stale suppression
does not error, it simply stops suppressing, so the rule reappears.

<a id="parse-throws-format-exception"></a>

## `Parse` throws `FormatException`

`Parse` used to throw `ArgumentException` for text it could not parse. It now throws `FormatException`,
which is what `IParsable<TSelf>` implementations in the BCL do (`int.Parse`, `Guid.Parse`). `Create` is
unchanged: it still throws `ArgumentException`, or the type configured through `ExceptionType`.

```csharp
// 1.3.0
catch (ArgumentException) { /* invalid text */ }

// 1.4.0
catch (FormatException) { /* invalid text */ }
```

`FormatException` has no constructor that takes a parameter name, so the exception no longer carries
`ParamName`. If you matched on it, match on the message or switch to `TryParse`.

<a id="string-interning-removed"></a>

## String interning removed

Equal `string`-backed ids used to share one underlying string instance through a per-id-type cache. That
cache is gone, so two ids with equal values now hold distinct (but equal) strings. This only matters if you
compared by reference or counted on the allocation saving; use `string.Intern` or a cache in your own layer
if you need deduplication.

<a id="behaviour-changes"></a>

## Behaviour changes

These compile without complaint but can change results at run time.

<a id="ordinal-string-comparison"></a>

### String-backed ids compare ordinally

`CompareTo` on a `string`-backed id used `string.CompareTo`, which is culture-sensitive. It now uses
`String.CompareOrdinal`. The visible consequence is in sorted collections: `SortedSet<T>` and
`SortedDictionary<TKey, TValue>` use `CompareTo` to detect duplicates, so two ids that differ only in
case or in characters a culture considers equivalent could collapse into one entry. They no longer do.

The ordering itself also changes for such ids, so a persisted sorted sequence or a golden file built from
the old order needs regenerating. Non-string primitives are unaffected.

<a id="null-ordering"></a>

### Comparison operators are null-safe

For reference-type ids (`record`, not `record struct`), the comparison operators (`<`, `>`, `<=`, `>=`)
now handle a `null` operand instead of throwing. In 1.3.0 they read `left.Value.CompareTo(right.Value)`
directly, so comparing a `null` id raised `NullReferenceException`.

The ordering itself is unchanged: `null` compares as the smallest value, which is what `CompareTo` already
did in 1.3.0. The operators now agree with it. Code that relied on the exception to detect a null id needs
an explicit null check.

<a id="invariant-culture"></a>

### Text form uses the invariant culture

Three paths that used to follow the ambient culture now use the invariant culture:

- `ToString()` on a formattable primitive. A `decimal`-backed id prints `1234.5` under every culture,
  where 1.3.0 printed `1234,5` under de-DE.
- `Parse` / `TryParse` when the `IFormatProvider` is `null`. This makes `Parse(id.ToString())` round-trip
  regardless of the ambient culture.
- The generated `TypeConverter`: `ConvertFrom` parses with the invariant culture and ignores the culture it
  is handed, matching `ToString()`.

If you relied on the current culture being picked up implicitly, pass it explicitly. Note that string
interpolation is not affected: `$"{id}"` still goes through `IFormattable` with the current culture.

<a id="stia0011-warning"></a>

### `STIA0011` is a warning and covers more shapes

`STIA0011` ("strongly typed id constructed directly") was informational and only reported `new Xxx(...)`.
It is now a **warning** and also reports target-typed `new(...)` and `default` / `default(T)`. A project
that treats warnings as errors will start failing on ids that bypass `Create`. Either route construction
through `Create` or set the severity back down in `.editorconfig`.

<a id="analyzer-muting"></a>

## Muting the analyzer

The analyzer can now be switched off without affecting code generation:

```xml
<PropertyGroup>
  <StronglyTypedIdEnableAnalyzers>false</StronglyTypedIdEnableAnalyzers>
</PropertyGroup>
```

This is the blunt instrument for a migration: it silences every `STIA` rule while the generator keeps
emitting the same code. Prefer per-rule `.editorconfig` severities where only one or two rules are in the
way.

<a id="new-capabilities"></a>

## What you can adopt afterwards

Nothing below is required to upgrade; each is opt-in and leaves existing output unchanged when unused.

- **Custom primitives.** A type implementing `IParsable<TSelf>`, `IComparable<TSelf>` and the non-generic
  `IComparable`, with public static `Parse` / `TryParse`, can be the wrapped primitive. Cysharp's `Ulid`
  already qualifies, so ULID needs no extra code. See [Limitations](limitations.md#primitive-types).
- **Nine more primitives.** `char`, `Int128`, `UInt128`, `Half`, `float`, `double`, `DateOnly`, `TimeOnly`
  and `BigInteger`, 24 in total including `enum`.
- **More integrations.** protobuf-net, HotChocolate (GraphQL), MessagePack and MongoDB BSON. See
  [Integrations](integrations.md#protobuf-net).
- **New id options.** `Normalizer`, `ExceptionType`, `RedactValidationMessage`, `Prefix` /
  `PrefixReadMode`, `JsonShape`, `Integrations` and `[StronglyTypedIdInstance]`, all with assembly-level
  defaults. `Prefix` is new in this release, so there is nothing to migrate: leaving it unset keeps the
  generated code byte-for-byte identical. See [Declaring an id](declaration.md#the-declaration).
- **Explicit JSON registration.** `options.AddStronglyTypedIds()` for System.Text.Json and
  Newtonsoft.Json, and `WithStronglyTypedIds()` for a source-generated `JsonSerializerContext`. See
  [Serialization](serialization.md#serialization).
- **Testing packages.** `Len.StronglyTypedId.Testing` plus the AutoFixture and Bogus adapters. These are
  new packages, so there is no 1.3.0 behaviour to migrate; seeded sequences use SplitMix64 and are
  reproducible across runtimes. See [Testing](testing.md#testing).

<a id="not-changed"></a>

## What did not change

- The declaration syntax and the generated member set for an id with no options.
- Database storage for every integration: EF Core, Dapper, MessagePack and MongoDB keep storing the bare
  primitive.
- The package layout and the version pin between `Len.StronglyTypedId` and its generator package.

[<- Back to the index](index.md) · [中文](zh-CN/migration.md)
