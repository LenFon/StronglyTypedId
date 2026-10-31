---
layout: default
title: Features
---

# Features

The complete inventory of what the generator emits, grouped by what you are trying to do. The
[README](https://github.com/LenFon/StronglyTypedId#features) keeps only the highlights; this page is the
full list.

To find *where* a feature can be seen running, use the [feature index](features-index.md); every entry
there points at a runnable sample.

<a id="declaring-an-id"></a>

## Declaring an id

- **One-line declaration.** Annotate a `partial record` (or `partial record struct`) with
  `[StronglyTypedId]`; the generator emits a sibling `partial` and never rewrites yours.
- **Convert an existing record.** Put the caret on a record's declaration header and the refactoring menu
  offers **Convert to strongly typed id**, which adds the attribute, `partial` and the `using` for you; the
  reverse **Inline strongly typed id** undoes it. The forward action appears only when the result would be a
  valid id; see [Converting an existing record](declaration.md#converting-an-existing-record).
- **Compile-time safety.** A built-in analyzer rejects invalid declarations as errors, and twenty-one of
  its twenty-six rules ship a code fix that repairs the declaration for you. See
  [Diagnostics](diagnostics.md#diagnostics).
- **Twenty-three built-in primitives.** Every numeric type including `Int128`, `UInt128` and `Half`, plus
  `Guid`, `string`, `char`, `decimal`, `DateTime`, `DateTimeOffset`, `TimeSpan`, `DateOnly`, `TimeOnly`
  and `BigInteger`. See
  [Supported primitive types](https://github.com/LenFon/StronglyTypedId#supported-primitive-types).
- **Any `enum`.** Enum-backed ids parse by member name and skip the formattable interfaces, because
  `System.Enum` inherits them with `[Obsolete]` members.
- **Custom primitives.** A type of your own works when it implements `IParsable<TSelf>`, `IComparable<TSelf>`
  and the non-generic `IComparable`, and exposes public static `Parse` / `TryParse`. ULID is the worked
  example; see [Custom primitives](https://github.com/LenFon/StronglyTypedId#custom-primitives).
- **Named instances.** `[StronglyTypedIdInstance("Unspecified", "-1")]` declares `public static readonly`
  fields built through `Create`, so they honour the normalizer and validator and always parse back. See
  [Named instances](declaration.md#named-instances).
- **Text prefix.** `[StronglyTypedId(Prefix = "ord_")]` gives an id a canonical textual form
  (`"ord_3f2c…"`) that flows through `ToString`, `Parse`, JSON and every other text path. See
  [Text prefix](serialization.md#serialization).
- **Nested and generic containers.** An id may be declared inside a class, struct, record or interface;
  a generic container compiles but loses the external-reference integrations. See
  [Nested types](declaration.md#nested-types).

<a id="what-gets-generated"></a>

## What gets generated

- **Interfaces.** `IStronglyTypedId<TSelf, TPrimitiveId>`, `IParsable<TSelf>`, `ISpanParsable<TSelf>`,
  `IComparable<TSelf>`, `IEqualityOperators<TSelf, TSelf, bool>` and
  `IComparisonOperators<TSelf, TSelf, bool>`. See [Runtime](runtime.md#using-the-interface).
- **Parsing.** `Parse` / `TryParse` for both `string` and `ReadOnlySpan<char>`. A `null` provider means
  the invariant culture, so `Parse(id.ToString())` always round-trips. See
  [Generated code](generated-code.md).
- **Formatting.** `IFormattable` and `ISpanFormattable` whenever the primitive itself implements them.
  `string` implements neither, and enum-backed ids skip both.
- **UTF-8 interfaces.** `IUtf8SpanFormattable` and `IUtf8SpanParsable<TSelf>` for primitives that expose
  them (`Guid` from .NET 10, for example), enabling `ReadOnlySpan<byte>` parsing and formatting.
- **Comparison and ordering.** `CompareTo` plus `<`, `>`, `<=` and `>=`, so `OrderBy`, `SortedSet<T>`
  and range checks work without a custom comparer. Reference-type ids treat `null` as the smallest value.
- **`Create` and `TryCreate`.** The throwing factory and its non-throwing twin, which returns `false` and
  sets `result` to `default` instead of throwing.
- **Deconstruction.** `id.Deconstruct(out var value)` unwraps the primitive. A single-element
  `var (value) = id;` is not legal C#.
- **`ToString()`.** The invariant-culture text of the primitive (or the prefixed canonical form), not the
  record's `OrderId { Value = … }` shape.
- **`WithStronglyTypedIds()`.** For a source-generated `JsonSerializerContext`, wrap the context's resolver
  so the id keeps its string shape instead of being written as an object. The generated resolver consults
  the id's converter first, without reflection. See [Native AOT and trimming](aot.md).
- **`TypeConverter`.** Opt in with `[StronglyTypedId(TypeConverter = true)]` to round-trip through
  `TypeDescriptor`, covering config binding and `XmlSerializer`. See
  [TypeConverter](declaration.md#type-converter). Without it, `IConfiguration` binding leaves the property
  at `default` instead of failing.
- **Debugger view.** `[DebuggerDisplay]` shows the value when collapsed; `[DebuggerTypeProxy]` shows both
  the raw `Value` and the canonical `Text` (prefix included) when expanded.
- **Reflection helpers.** `IsStronglyTypedId`, `GetPrimitiveIdType` and `TryGetPrimitiveIdType` inspect
  any type at runtime, including ids from other assemblies. See [Runtime](runtime.md#reflection-helpers).

<a id="validation-and-normalization"></a>

## Validation and normalization

- **Value validation.** Point `Validator` at a static method of the id to make `Create`, `TryCreate`,
  `Parse` and `TryParse` reject invalid values. See [Value validation](declaration.md#value-validation).
- **Value normalization.** Point `Normalizer` at a static method to repair incoming values (trim,
  case-fold, strip separators) before they are validated and stored. See
  [Value normalization](declaration.md#value-normalization).
- **Custom failure type.** `ExceptionType` swaps the `ArgumentException` that `Create` throws when the
  validator rejects a value for an exception of your own.
- **Redacted messages.** `RedactValidationMessage` keeps the rejected value out of the exception message,
  for ids that wrap secrets.

<a id="configuration"></a>

## Configuration

- **Assembly-level defaults.** `[assembly: StronglyTypedIdDefaults(...)]` sets a default `Validator`,
  `Normalizer`, `ExceptionType`, `RedactValidationMessage`, `Integrations`, `JsonShape`, `TypeConverter`
  and `PrefixReadMode` for the whole assembly. See
  [Assembly-level defaults](declaration.md#assembly-defaults).
- **Per-id integration filter.** `[StronglyTypedId(Integrations = ...)]` narrows which integration code an
  id gets (`None`, a whitelist, or `All`), with an assembly-level default. See
  [Turning integrations off](declaration.md#integrations-filter).
- **Muting the analyzer in one line.** Set
  `<StronglyTypedIdEnableAnalyzers>false</StronglyTypedIdEnableAnalyzers>` in the project file and the
  analyzer registers no diagnostic action at all. Code generation is unaffected. See
  [Packages](packages.md#transitive-analyzer).

<a id="integrations"></a>

## Integrations

- **Serialization.** `System.Text.Json` and `Newtonsoft.Json` (≥ 13.0.0) converters are generated
  automatically, including dictionary-key support; both expose an explicit registration entry point, and
  the JSON shape is configurable per id. See [Serialization](serialization.md#serialization).
- **Nine more integrations.** Entity Framework Core, Dapper, Swashbuckle, ASP.NET Core MVC (which also
  powers Minimal API route constraints), ASP.NET Core OpenAPI, MessagePack, MongoDB, HotChocolate
  (GraphQL) and protobuf-net. Each is emitted only when its package is referenced. All but protobuf-net
  ship a registration entry point: `AddStronglyTypedIds(...)` for every one except MongoDB, which uses
  `RegisterBsonSerializers()`. protobuf-net needs none, because the surrogate is discovered from the
  attribute. See [Integrations](integrations.md).
- **Binary payloads carry the primitive.** MessagePack, BSON and protobuf-net write the primitive value
  itself; the text prefix belongs to the text form and never reaches the payload.

<a id="testing-and-deployment"></a>

## Testing and deployment

- **Test helpers.** `Any.Id<T>()` and `Any.Ids<T>(count)` build plausible ids for arrange blocks, with
  reproducible seeds and validator-aware retries. `Len.StronglyTypedId.Testing.AutoFixture` and
  `.Bogus` wire the same generator into those libraries. See [Testing](testing.md#testing).
- **Native AOT and trimming.** The runtime library is marked AOT-compatible and loads no types by name,
  so ids work in Native AOT and trimmed applications. `System.Text.Json` still needs a source-generated
  `JsonSerializerContext` under AOT, which is its own requirement rather than this library's. See
  [Native AOT and trimming](aot.md).
- **Zero third-party dependencies.** The package ships only the generator/analyzer assembly plus a small
  runtime library; nothing is added to your application's dependency graph.

[<- Back to the index](index.md) · [中文](zh-CN/features.md)
