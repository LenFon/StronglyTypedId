# StronglyTypedId

> 中文文档：[README.zh-CN.md](README.zh-CN.md) · Docs site: [lenfon.github.io/StronglyTypedId](https://lenfon.github.io/StronglyTypedId/)

[![codecov][badge-codecov]](https://app.codecov.io/gh/LenFon/StronglyTypedId/tree/main)
[![NuGet][badge-nuget]][nuget-package]
[![License: MIT][badge-license]][license-file]

A source generator, analyzer and code fix provider. Annotate a one-line `record` declaration and it
becomes a complete strongly typed id: the `IStronglyTypedId<TSelf, TPrimitiveId>` implementation,
`IParsable<TSelf>` support, and the JSON, Entity Framework Core and Swagger integrations. A `Guid` can
then no longer be passed where an `OrderId` is expected, or the reverse.

<a id="table-of-contents"></a>

## Table of contents

- [Features](#features)
- [Getting started](#getting-started)
- [Supported primitive types](#supported-primitive-types)
- [What gets generated](#what-gets-generated)
- [Documentation](#documentation)
- [Limitations](#limitations)
- [License](#license)

<a id="features"></a>

## Features <a href="#table-of-contents" style="float:right">↑ Back to top</a>

- **One line to declare.** Annotate a `partial record` (or `partial record struct`) with
  `[StronglyTypedId]` and the type gains parsing, formatting, comparison, `Create` / `TryCreate`,
  deconstruction, JSON converters and a debugger view.
- **A `Guid` can never stand in for an `OrderId`.** The compiler rejects the swap, and a built-in
  analyzer rejects invalid declarations as errors; twenty-one of its twenty-six rules ship a code fix.
- **Twenty-three primitives, or your own.** Every numeric type, `Guid`, `string`, dates and
  `BigInteger` are built in, along with any `enum` and any type that implements `IParsable<TSelf>` and
  `IComparable`. ULID works unchanged.
- **Validation where the value enters.** Point `Validator` and `Normalizer` at static methods, and every
  generated entry point enforces them: `Create`, `Parse`, JSON, EF Core, Dapper and model binding.
- **Eleven integrations, generated only when referenced.** System.Text.Json, Newtonsoft.Json, EF Core,
  Dapper, Swashbuckle, ASP.NET Core MVC and OpenAPI, MessagePack, MongoDB, HotChocolate and protobuf-net.
- **Native AOT, trimming, and no third-party dependencies.** The runtime library loads no types by name,
  and the package adds nothing to your dependency graph.

The full inventory, covering every generated member, configuration option and integration, is in
[Features](docs/features.md).

<a id="getting-started"></a>

## Getting started <a href="#table-of-contents" style="float:right">↑ Back to top</a>

1. Install the package into your application or library:

   ```text
   Package Manager : Install-Package Len.StronglyTypedId
   CLI             : dotnet add package Len.StronglyTypedId
   ```

   One package is enough: `Len.StronglyTypedId` depends on `Len.StronglyTypedId.Generators`, which carries
   the generator, the analyzer and the code fixes. The dependency is transitive, so **every project that
   references the main package also gets the analyzer**. See [Packages](docs/packages.md) for why, and for
   how to silence the diagnostics in a project that wants none of them.

2. Declare a strongly typed id with a single primary constructor parameter named `Value`:

   ```csharp
   [StronglyTypedId]
   public partial record struct OrderId(Guid Value);

   // or

   [StronglyTypedId]
   public partial record OrderId(Guid Value);
   ```

   > **`record` vs `record struct`.** A value type versus a reference type, with the same generated
   > members either way. See [The declaration](docs/declaration.md#the-declaration) for the trade-off.

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

The type is matched by its name, so writing `System.Guid` (or an aliased using) works the same. Only the
types above are recognised. A type of your own that happens to be called `Guid` is not supported and is
rejected by [STIA0008](docs/diagnostics.md#diagnostics). Any `enum` is supported as well; how enum ids
parse and which interfaces they skip is in
[Enum-backed ids](docs/generated-code.md#enum-backed-ids).

<a id="custom-primitives"></a>

### Custom primitives

A type of your own is accepted as the primitive when it satisfies this contract:

- implements `IParsable<TSelf>` and `IComparable<TSelf>` with itself as the type argument;
- implements the non-generic `System.IComparable`, which is the actual constraint on `TPrimitiveId` in
  `IStronglyTypedId<TSelf, TPrimitiveId>`; `IComparable<T>` does not imply it;
- exposes **public static** `Parse(string, IFormatProvider)` and
  `TryParse(string, IFormatProvider, out TSelf)`. Explicit interface implementations do not count,
  because the generated code calls them by type name;
- is a non-abstract, non-static, non-generic struct or class, and is not `System.Uri`.

```csharp
public readonly record struct Email(string Value)
    : IParsable<Email>, IComparable<Email>, IComparable
{
    public static Email Parse(string s, IFormatProvider? provider)
        => TryParse(s, provider, out var result) ? result : throw new FormatException(s);

    public static bool TryParse(string? s, IFormatProvider? provider, out Email result)
    {
        result = new Email(s ?? string.Empty);
        return s is not null;
    }

    public int CompareTo(Email other) => string.CompareOrdinal(Value, other.Value);

    public int CompareTo(object? obj) => obj is Email other ? CompareTo(other) : throw new ArgumentException();

    public override string ToString() => Value;
}

[StronglyTypedId]
public partial record struct EmailId(Email Value);
```

- **`ToString()` must produce text your own `Parse` can read back.** Every serialization path writes the
  canonical text of the primitive: System.Text.Json, Newtonsoft.Json, MessagePack, BSON and Dapper all
  use it, so an id never appears as one shape over REST and another in a database. Handing a custom type
  to a serializer would instead serialize its public properties and read back `default`.
- **Named instances are validated at run time, not at compile time.** The generator runs on
  `netstandard2.0` and cannot execute your `Parse`, so `[StronglyTypedIdInstance("Unknown", "…")]` only
  fails when the type is first touched. The failure is an `InvalidOperationException` whose message names
  the instance and the id type; `StronglyTypedIdConformance.AssertAllInstancesValid(assembly)` from
  `Len.StronglyTypedId.Testing` reports every broken instance at once.

**ULID works with no code from this library.** Cysharp's [`Ulid`](https://www.nuget.org/packages/Ulid)
is a third-party package you add yourself; this library does not depend on it and generates nothing
special for it. It implements `IParsable<Ulid>`, `IComparable<Ulid>` and the non-generic `IComparable`,
so it already satisfies the contract above:

```shell
dotnet add package Ulid
```

```csharp
[StronglyTypedId]
public partial record struct OrderId(Ulid Value);
```

Its canonical text is the 26-character Crockford Base32 form, so JSON, MessagePack, BSON, EF Core and
Dapper all store it as that string. ULIDs generated within the same millisecond are only ordered by their
random component, which is not monotonic: "ULID is sortable" holds across milliseconds, not within one.

<a id="what-gets-generated"></a>

## What gets generated <a href="#table-of-contents" style="float:right">↑ Back to top</a>

For each annotated type the generator emits a `partial` declaration in the **same namespace** as the id
(nested ids are declared back inside their containing types; see
[Nested types](docs/declaration.md#nested-types)), and your own declaration is never modified. What is
emitted depends on what the compilation references:

<!-- BEGIN GENERATED:generated-code -->
| Condition in the compilation                                                                                       | Generated code                                                                                                                                                           |
| ------------------------------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Always                                                                                                             | Core implementation                                                                                                                                                      |
| `System.Text.Json` is referenced                                                                                   | Nested `SystemTextJsonConverter` + `[JsonConverter]`, plus `AddStronglyTypedIds()` on `JsonSerializerOptions` and `WithStronglyTypedIds()` for source-generated contexts |
| `Newtonsoft.Json` ≥ 13.0.0 is referenced                                                                           | Nested `NewtonsoftJsonConverter` + `[JsonConverter]`, plus `AddStronglyTypedIds()` on `JsonSerializerSettings`                                                           |
| `Microsoft.EntityFrameworkCore` ≥ 7.0.0 is referenced                                                              | Nested `{TypeName}Converter`, plus `AddStronglyTypedIds()` on `ModelConfigurationBuilder` and `ModelBuilder`                                                             |
| `Swashbuckle.AspNetCore.SwaggerGen` ≥ 6.0.0 is referenced                                                          | `AddStronglyTypedIds()` on `SwaggerGenOptions`                                                                                                                           |
| `Dapper` ≥ 2.0.0 is referenced                                                                                     | Nested `{TypeName}TypeHandler`, plus `AddStronglyTypedIds()` (also on `IDbConnection`)                                                                                   |
| `MessagePack` ≥ 2.0.0 is referenced                                                                                | `{TypeName}MessagePackFormatter`, plus `AddStronglyTypedIds()` on `MessagePackSerializerOptions`                                                                         |
| `MongoDB.Bson` ≥ 3.0.0 is referenced                                                                               | `{TypeName}BsonSerializer`, plus `StronglyTypedIds.RegisterBsonSerializers()`                                                                                            |
| `HotChocolate` ≥ 13.0.0 is referenced                                                                              | `{TypeName}HotChocolateType` scalar, plus `AddStronglyTypedIds()` on `IRequestExecutorBuilder`                                                                           |
| `protobuf-net` ≥ 3.0.0 is referenced                                                                               | `[ProtoContract(Surrogate = …)]` on the id plus a nested `{TypeName}Surrogate` with conversion operators                                                                 |
| `Microsoft.AspNetCore.OpenApi` ≥ 9.0.0 is referenced                                                               | `AddStronglyTypedIds()` on `OpenApiOptions`                                                                                                                              |
| `Microsoft.AspNetCore.Mvc` ≥ 2.0.0 is referenced (or its `Microsoft.AspNetCore.Mvc.Core` / `.Abstractions` facade) | `AddStronglyTypedIds()` on `MvcOptions` and `RouteOptions` (also powers Minimal API route constraints)                                                                   |
<!-- END GENERATED:generated-code -->

The table above is the inventory. The generated file names and the behaviour of each generated member
(the interfaces it implements, how `Parse` treats a `null` provider, how `[Flags]` enums are tightened,
what `ToString()` prints) are documented in [Generated code](docs/generated-code.md).

<a id="documentation"></a>

## Documentation <a href="#table-of-contents" style="float:right">↑ Back to top</a>

Everything past the quick start lives in `docs/`, and is published at
[lenfon.github.io/StronglyTypedId](https://lenfon.github.io/StronglyTypedId/):

| Page | What it covers |
| --- | --- |
| [Features](docs/features.md) | The full inventory: every generated member, every configuration option, every integration. |
| [Feature index](docs/features-index.md) | Every feature with its runnable sample and the page that explains it; the table is generated from source and verified on every build. |
| [Packages](docs/packages.md) | The five NuGet packages, why the generator is separate, and how the analyzer reaches every project. |
| [Declaring an id](docs/declaration.md) | Validation, normalization, named instances, exception types, assembly-level defaults, turning integrations off, nested types, `TryCreate` / UTF-8 interfaces and `TypeConverter`. |
| [Generated code](docs/generated-code.md) | What each generated member does: the interfaces, `Parse` and culture, `[Flags]` enum tightening, `ToString()` and the integration entry point. |
| [Serialization](docs/serialization.md) | System.Text.Json, Newtonsoft.Json, dictionary keys, JSON shape, the text prefix, explicit registration, and the primitives the serializers do not know. |
| [Integrations](docs/integrations.md) | Entity Framework Core, Swashbuckle, Dapper, MessagePack, MongoDB, HotChocolate (GraphQL), protobuf-net, ASP.NET Core MVC, Minimal API and ASP.NET Core OpenAPI. |
| [Runtime](docs/runtime.md) | Constraining generics with `IStronglyTypedId<,>`, and the reflection helpers. |
| [Native AOT and trimming](docs/aot.md) | What survives trimming, the reflection helpers, and the `JsonSerializerContext` an AOT app must supply. |
| [Testing](docs/testing.md) | `Any.Id<T>()`, the AutoFixture and Bogus integrations, and the sample projects. |
| [Diagnostics](docs/diagnostics.md) | Every rule, its severity, and whether it ships a code fix. |
| [Performance](docs/performance.md) | What the generator costs at build time, and what the generated code costs at run time. |
| [Troubleshooting](docs/troubleshooting.md) | Symptom-first: nothing generated, a missing integration, stale analyzers, parsing surprises. |
| [Limitations](docs/limitations.md) | What a source generator cannot do (`Value` visibility, the closed primitive list, the unguarded constructor) and the workaround for each. |
| [FAQ](docs/faq.md) | The questions that come up most often. |
| [Migrating from 1.3.0](docs/migration.md) | The four changes that need action in 1.4.0, the four that change behaviour without breaking the build, and what to adopt afterwards. |
| [Future plans](docs/roadmap.md) | None at the moment; every planned direction has shipped. |

<a id="limitations"></a>

## Limitations <a href="#table-of-contents" style="float:right">↑ Back to top</a>

**`Value` is always public.** The property comes from the positional record's primary constructor, and a
source generator cannot change the accessibility of a member you declared. Declare the id `internal` when
you need a hard boundary; `STIA0014` flags externally visible ids whose validator `new` can bypass.

**Only the listed primitives are supported out of the box.** A type of your own is accepted when it
satisfies the [custom-primitive contract](#custom-primitives); anything else is rejected. `Uri` is not
supported even though it meets that contract: `Uri.Equals` ignores the fragment and normalises case and
default ports, so two different URIs would compare equal and silently collapse into the same id.

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
