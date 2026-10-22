# StronglyTypedId

> 📖 中文文档：[README.zh-CN.md](README.zh-CN.md) · 🌐 Docs site: [lenfon.github.io/StronglyTypedId](https://lenfon.github.io/StronglyTypedId/)

[![codecov][badge-codecov]](https://app.codecov.io/gh/LenFon/StronglyTypedId/tree/main)
[![NuGet][badge-nuget]][nuget-package]
[![License: MIT][badge-license]][license-file]

A source generator, analyzer and code fix provider that turns a one-line `record` declaration into a
fully featured strongly typed id. It generates the `IStronglyTypedId<TSelf, TPrimitiveId>` implementation,
`IParsable<TSelf>` support, and the JSON / Entity Framework Core / Swagger integrations for you — so a
`Guid` cannot be passed where an `OrderId` is expected, and vice versa.

<a id="table-of-contents"></a>

## Table of contents

- [Features](#features)
- [Requirements](#requirements)
- [Getting started](#getting-started)
- [Supported primitive types](#supported-primitive-types)
- [What gets generated](#what-gets-generated)
- [Documentation](#documentation)
- [Limitations](#limitations)
- [License](#license)

<a id="features"></a>

## Features <a href="#table-of-contents" style="float:right">↑ Back to top</a>

- **Tiny to declare** — annotate a `partial record` (or `partial record struct`) with `[StronglyTypedId]`.
- **Compile-time safety** — a built-in analyzer rejects invalid declarations as errors, and sixteen of
  its twenty-five rules come with a code fix that repairs the declaration for you (see [Diagnostics](docs/diagnostics.md#diagnostics)).
- **Parsing & formatting** — `IParsable<TSelf>` and `ISpanParsable<TSelf>` support (`Parse` / `TryParse`
  for both `string` and `ReadOnlySpan<char>`) is generated automatically, together with `IFormattable`
  and `ISpanFormattable` whenever the primitive itself implements them — `string` does not, and
  enum-backed ids skip both because `System.Enum`'s inherited members are `[Obsolete]`.
- **Comparison & ordering** — each id implements `IComparable<TSelf>` and the four comparison operators,
  so `OrderBy`, `SortedSet<T>` and ranges work without a custom comparer.
- **Value validation** — point `Validator` at a static method of the id to make `Create` and `TryParse`
  reject invalid values (see [Value validation](docs/declaration.md#value-validation)).
- **Custom failure type** — `ExceptionType` swaps the `ArgumentException` that `Create` throws when the
  validator rejects a value for an exception of your own.
- **Value normalization** — point `Normalizer` at a static method to repair incoming values (trim, case-fold,
  strip separators) before they are validated and stored (see [Value normalization](docs/declaration.md#value-normalization)).
- **Named instances** — `[StronglyTypedIdInstance("Unspecified", "-1")]` declares `public static readonly`
  fields that are built through `Create`, so they honour the normalizer and validator and always parse
  back (see [Named instances](docs/declaration.md#named-instances)).
- **`TryCreate` factory** — the non-throwing counterpart of `Create`: on a failed parse it returns `false`
  and sets `result` to `default` instead of throwing.
- **UTF-8 interfaces** — for primitives that expose them (`Guid` from .NET 10, for example),
  `IUtf8SpanFormattable` and `IUtf8SpanParsable<TSelf>` are generated automatically, enabling
  `ReadOnlySpan<byte>` parsing and formatting.
- **Per-type TypeConverter** — set `[StronglyTypedId(TypeConverter = true)]` to emit a nested `TypeConverter`
  that round-trips between string and the id through `TypeDescriptor`, covering config binding /
  `XmlSerializer` scenarios.
- **Assembly-level defaults** — use `[assembly: StronglyTypedIdDefaults(Validator = ...)]` to set a default
  validator for the whole assembly.
- **Per-id integration filter** — `[StronglyTypedId(Integrations = ...)]` narrows which integration code an
  id gets (`None`, a whitelist, or `All`), with an assembly-level default
  (see [Turning integrations off](docs/declaration.md#integrations-filter)).
- **Seven more integrations** — Entity Framework Core, Dapper, Swashbuckle, ASP.NET Core MVC (which
  also powers Minimal API route constraints), ASP.NET Core OpenAPI, MessagePack and MongoDB. Each is emitted
  only when its package is referenced, and each ships a `StronglyTypedIds.ApplyTo(...)` entry point
  (see [Integrations](docs/integrations.md)).
- **Serialization** — `System.Text.Json` and `Newtonsoft.Json` (≥ 13.0.0) converters are generated
  automatically, including dictionary-key support; both expose an explicit registration entry point, and
  the JSON shape is configurable per id (see [Serialization](docs/serialization.md#serialization)).
- **Text prefix** — `[StronglyTypedId(Prefix = "ord_")]` gives an id a canonical textual form
  (`"ord_3f2c…"`) that flows through `ToString`, `Parse`, JSON and every other text path
  (see [Text prefix](docs/serialization.md#serialization)).
- **Runtime reflection helpers** — inspect any type at runtime and get the primitive id behind it.
- **Native AOT & trimming** — the runtime library is marked AOT-compatible and loads no types by name, so
  ids work in Native AOT and trimmed applications. `System.Text.Json` still needs a source-generated
  `JsonSerializerContext` under AOT, which is its own requirement rather than this library's
  (see [Native AOT and trimming](docs/aot.md)).
- **Zero third-party dependencies** — the package ships only the generator/analyzer assembly plus a
  small runtime library; nothing is added to your application's dependency graph.

<a id="requirements"></a>

## Requirements <a href="#table-of-contents" style="float:right">↑ Back to top</a>

| Component                              | Requirement                                                                              |
| -------------------------------------- | ---------------------------------------------------------------------------------------- |
| Target framework of your project       | **`net8.0`, `net10.0`**, or a later compatible framework (the runtime library targets `net8.0` and `net10.0`). |
| .NET SDK                               | **A compiler that provides Roslyn 5.9 or newer.** The generator is compiled against `Microsoft.CodeAnalysis` 5.9, so an older compiler cannot load it: the build reports `CS9057` and then fails with a missing `Create` method. Verified with .NET SDK **10.0.401**; SDK 10.0.201 (Roslyn 5.3) and 9.0.308 (Roslyn 4.14) both fail. |
| Newtonsoft.Json                        | ≥ **13.0.0** (only when using the Newtonsoft.Json converter).                            |
| EntityFrameworkCore                    | ≥ **7.0.0** (only when using the EF Core converter).                                     |
| Swashbuckle.AspNetCore.SwaggerGen      | ≥ **6.0.0** (only when generating Swagger schema mappings).                              |

The generated code relies on `IParsable<T>`, `ISpanParsable<T>` and the comparison / equality operator
interfaces in `System.Numerics`, which is why the runtime library starts at `net8.0` rather than following
the generator's much lower `netstandard2.0` baseline.

<a id="getting-started"></a>

## Getting started <a href="#table-of-contents" style="float:right">↑ Back to top</a>

1. Install the package into your application or library:

   ```text
   Package Manager : Install-Package Len.StronglyTypedId
   CLI             : dotnet add package Len.StronglyTypedId
   ```

   One package is enough: `Len.StronglyTypedId` depends on `Len.StronglyTypedId.Generators`, which carries
   the generator, the analyzer and the code fixes. The dependency is transitive, so **every project that
   references the main package also gets the analyzer** — see [Packages](docs/packages.md) for why, and for
   how to silence the diagnostics in a project that wants none of them.

2. Declare a strongly typed id with a single primary constructor parameter named `Value`:

   ```csharp
   [StronglyTypedId]
   public partial record struct OrderId(Guid Value);

   // or

   [StronglyTypedId]
   public partial record OrderId(Guid Value);
   ```

   > **`record` vs `record struct`** — a value type versus a reference type, with exactly the same
   > generated members either way. See [The declaration](docs/declaration.md#the-declaration) for the
   > trade-off.

   The generated type exposes `Value`, a static `Create(...)` factory, and `Parse` / `TryParse`.
   Use it anywhere you would otherwise pass a bare primitive:

   ```csharp
   public class Order
   {
       public OrderId Id { get; set; }
       public UserId Buyer { get; set; }
   }

   var id = OrderId.Create(Guid.NewGuid());          // static factory
   var parsed = OrderId.Parse("...", null);          // IParsable<TSelf>
   OrderId.TryParse("...", null, out var alsoParsed); // returns false instead of throwing
   ```

   The `Value` parameter may keep its `Nullable` context, but it may not be a nullable type:
   `OrderId(Guid? Value)` is rejected by the analyzer ([STIA0006](docs/diagnostics.md#diagnostics)).

<a id="supported-primitive-types"></a>

## Supported primitive types <a href="#table-of-contents" style="float:right">↑ Back to top</a>

The wrapped `Value` parameter may be one of:

<!-- BEGIN GENERATED:primitive-types -->
| C# type          | Example declaration                                    |
| ---------------- | ------------------------------------------------------ |
| `Guid`           | `record struct GuidId(Guid Value)`                     |
| `string`         | `record StringId(string Value)`                        |
| `char`           | `record struct CharId(char Value)`                     |
| `byte`           | `record struct ByteId(byte Value)`                     |
| `sbyte`          | `record struct SByteId(sbyte Value)`                   |
| `short`          | `record struct Int16Id(short Value)`                   |
| `ushort`         | `record struct UInt16Id(ushort Value)`                 |
| `int`            | `record struct Int32Id(int Value)`                     |
| `uint`           | `record struct UInt32Id(uint Value)`                   |
| `long`           | `record struct Int64Id(long Value)`                    |
| `ulong`          | `record struct UInt64Id(ulong Value)`                  |
| `Int128`         | `record struct Int128Id(Int128 Value)`                 |
| `UInt128`        | `record struct UInt128Id(UInt128 Value)`               |
| `decimal`        | `record struct DecimalId(decimal Value)`               |
| `Half`           | `record struct HalfId(Half Value)`                     |
| `float`          | `record struct SingleId(float Value)`                  |
| `double`         | `record struct DoubleId(double Value)`                 |
| `DateTime`       | `record struct DateTimeId(DateTime Value)`             |
| `DateTimeOffset` | `record struct DateTimeOffsetId(DateTimeOffset Value)` |
| `TimeSpan`       | `record struct TimeSpanId(TimeSpan Value)`             |
| `DateOnly`       | `record struct DateOnlyId(DateOnly Value)`             |
| `TimeOnly`       | `record struct TimeOnlyId(TimeOnly Value)`             |
| `BigInteger`     | `record struct BigIntegerId(BigInteger Value)`         |
| any `enum`       | `record StatusId(Status Value)`                        |
<!-- END GENERATED:primitive-types -->

The type is matched by its name, so writing `System.Guid` (or an aliased using) works exactly the same.
Only the types above are recognised — a type of your own that merely happens to be called `Guid` is not
supported, and is rejected by [STIA0008](docs/diagnostics.md#diagnostics). Any `enum` is supported as well;
how enum ids parse and which interfaces they skip is in
[Enum-backed ids](docs/generated-code.md#enum-backed-ids).

<a id="what-gets-generated"></a>

## What gets generated <a href="#table-of-contents" style="float:right">↑ Back to top</a>

For each annotated type the generator emits a `partial` declaration in the **same namespace** as the id —
nested ids are declared back inside their containing types instead (see
[Nested types](docs/declaration.md#nested-types)) —
your own declaration is never modified. What is emitted depends on what the compilation references:

<!-- BEGIN GENERATED:generated-code -->
| Condition in the compilation                                                                                       | Generated code                                                                                                                        |
| ------------------------------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------- |
| Always                                                                                                             | Core implementation                                                                                                                   |
| `System.Text.Json` is referenced                                                                                   | Nested `SystemTextJsonConverter` + `[JsonConverter]`, plus `StronglyTypedIds.ApplyTo(JsonSerializerOptions)`                          |
| `Newtonsoft.Json` ≥ 13.0.0 is referenced                                                                           | Nested `NewtonsoftJsonConverter` + `[JsonConverter]`, plus `StronglyTypedIds.AddStronglyTypedId(this JsonSerializerSettings)`         |
| `Microsoft.EntityFrameworkCore` ≥ 7.0.0 is referenced                                                              | Nested `{TypeName}Converter`, plus `StronglyTypedIds.ApplyTo(ModelConfigurationBuilder)` and `StronglyTypedIds.ApplyTo(ModelBuilder)` |
| `Swashbuckle.AspNetCore.SwaggerGen` ≥ 6.0.0 is referenced                                                          | `StronglyTypedIds.ApplyTo(SwaggerGenOptions)`                                                                                         |
| `Dapper` ≥ 2.0.0 is referenced                                                                                     | Nested `{TypeName}TypeHandler`, plus `StronglyTypedIds.ApplyTo()`                                                                     |
| `MessagePack` ≥ 2.0.0 is referenced                                                                                | `{TypeName}MessagePackFormatter`, plus `StronglyTypedIds.ApplyTo(MessagePackSerializerOptions)`                                       |
| `MongoDB.Bson` ≥ 3.0.0 is referenced                                                                               | `{TypeName}BsonSerializer`, plus `StronglyTypedIds.RegisterBsonSerializers()`                                                         |
| `Microsoft.AspNetCore.OpenApi` ≥ 9.0.0 is referenced                                                               | `StronglyTypedIds.ApplyTo(OpenApiOptions)`                                                                                            |
| `Microsoft.AspNetCore.Mvc` ≥ 2.0.0 is referenced (or its `Microsoft.AspNetCore.Mvc.Core` / `.Abstractions` facade) | `StronglyTypedIds.ApplyTo(MvcOptions)` + `ApplyTo(RouteOptions)` (also powers Minimal API route constraints)                          |
<!-- END GENERATED:generated-code -->

The table above is the inventory. The generated file names, and what each generated member actually
does — the interfaces it implements, how `Parse` treats a `null` provider, how `[Flags]` enums are
tightened, what `ToString()` prints — are documented in [Generated code](docs/generated-code.md).

<a id="documentation"></a>

## Documentation <a href="#table-of-contents" style="float:right">↑ Back to top</a>

Everything past the quick start lives in `docs/`, and is published at
[lenfon.github.io/StronglyTypedId](https://lenfon.github.io/StronglyTypedId/):

| Page | What it covers |
| --- | --- |
| [Packages](docs/packages.md) | The four NuGet packages, why the generator is separate, and how the analyzer reaches every project. |
| [Declaring an id](docs/declaration.md) | Validation, normalization, named instances, exception types, assembly-level defaults, turning integrations off, nested types, `TryCreate` / UTF-8 interfaces and `TypeConverter`. |
| [Generated code](docs/generated-code.md) | What each generated member does: the interfaces, `Parse` and culture, `[Flags]` enum tightening, `ToString()` and the integration entry point. |
| [Serialization](docs/serialization.md) | System.Text.Json, Newtonsoft.Json, dictionary keys, JSON shape, the text prefix, explicit registration, and the primitives the serializers do not know. |
| [Integrations](docs/integrations.md) | Entity Framework Core, Swashbuckle, Dapper, MessagePack, MongoDB, ASP.NET Core MVC, Minimal API and ASP.NET Core OpenAPI. |
| [Runtime](docs/runtime.md) | Constraining generics with `IStronglyTypedId<,>`, and the reflection helpers. |
| [Native AOT and trimming](docs/aot.md) | What survives trimming, the reflection helpers, and the `JsonSerializerContext` an AOT app must supply. |
| [Testing](docs/testing.md) | `Any.Id<T>()`, the AutoFixture integration, and the sample projects. |
| [Diagnostics](docs/diagnostics.md) | Every rule, its severity, and whether it ships a code fix. |
| [Performance](docs/performance.md) | What the generator costs at build time, what the generated code costs at run time, and the allocation-based regression gate. |
| [Troubleshooting](docs/troubleshooting.md) | Symptom-first: nothing generated, a missing integration, stale analyzers, parsing surprises. |
| [Limitations](docs/limitations.md) | What a source generator cannot do — `Value` visibility, the closed primitive list, the unguarded constructor — and the workaround for each. |
| [FAQ](docs/faq.md) | The questions that come up most often. |

<a id="limitations"></a>

## Limitations <a href="#table-of-contents" style="float:right">↑ Back to top</a>

**`Value` is always public.** The property comes from the positional record's primary constructor, and a
source generator cannot change the accessibility of a member you declared. Declare the id `internal` when
you need a hard boundary; `STIA0014` flags externally visible ids whose validator `new` can bypass.

**Only the listed primitives are supported.** Arbitrary custom types are rejected, and `Uri` is
deliberately absent: `Uri.Equals` ignores the fragment and normalises case and default ports, so two
different URIs would compare equal and silently collapse into the same id.

**The primary constructor is not guarded.** `new OrderId(...)` bypasses both `Normalizer` and `Validator`;
only `Create`, `Parse` and `TryParse` enforce them.

Workarounds for all three, plus the generic-container caveat, are in [Limitations](docs/limitations.md).

<a id="license"></a>

## License <a href="#table-of-contents" style="float:right">↑ Back to top</a>

This project is licensed under the [MIT License](LICENSE.txt).

[nuget-package]: https://www.nuget.org/packages/Len.StronglyTypedId/
[license-file]: LICENSE.txt
[badge-nuget]: https://img.shields.io/nuget/v/Len.StronglyTypedId.svg
[badge-codecov]: https://codecov.io/github/LenFon/StronglyTypedId/branch/main/graph/badge.svg?token=S3PBV7W190
[badge-license]: https://img.shields.io/badge/License-MIT-blue.svg
