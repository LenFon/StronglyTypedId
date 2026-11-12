# Changelog

> 中文版：[CHANGELOG.zh-CN.md](CHANGELOG.zh-CN.md)

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Releases up to `1.3.0` are reconstructed from the repository tags: each entry summarises the shipped
change set rather than reproducing every commit.

## [Unreleased]

## [1.4.2] - 2026-11-12

### Added

- **`STIA0022` code fix:** a non-PascalCase instance name gets a one-click rename. Only the string literal
  changes; references to the old name surface as `CS0117`. Not offered for `const` references.
- **Non-generic `Any` overloads:** `Any.Create(Type)`, `Any.Ids(Type, count)` and
  `Any.Register(Type, factory)` mirror the generic members for ids known only at run time.
- **Rich validation results:** a `Validator` may return `StronglyTypedIdValidationResult` (a message and an
  optional code) instead of `bool`. `Create` uses the message, and the three-argument `TryCreate` hands the
  whole result to the caller.
- **Testing helpers:** a custom primitive needs no configuration — `Any.Id<T>()` offers seven text shapes to
  `TryParse` in turn, and candidates come from the seeded source, so `Seed` still reproduces the sequence.
- **Integration version matrix:** a probe project compiles and round-trips every integration against a
  version supplied on the command line, and CI runs the newest major of each. `docs/integrations.md` gained
  a "Supported versions" table.
- **`STIA0028`:** an id inside a generic container warns — it still compiles, but every integration is
  skipped and the converter attributes cannot be emitted (`CS0416`). Not reported when every integration is
  turned off.

### Fixed

- **Generator:** a member colliding with a generated one (`CS0111` / `CS0102`) no longer emits code, leaving
  `STIA0029` as the only error; an id without a `Value` property likewise emits nothing instead of hiding
  its diagnostic behind `CS0535`.
- **Generator:** a duplicate attribute spread over two files no longer drops every generated file (the order
  key is the declaration index plus the in-tree offset), and an attribute on a later `partial` segment
  generates again.
- **Generator:** the package ships `roslyn5.9` and `roslyn4.8` builds, so SDK 8, 9 and 10.0.2xx load it
  instead of failing with `CS9057` / `CS1929`; generated output is unchanged.
- **Generated code:** a prefixed `string`-backed id whose value is empty round-trips again — the bare prefix
  (`"ord_"`) reads back, while a bare empty string is still rejected.
- **Generated code:** `ToString(string?, IFormatProvider?)` no longer returns `null` for a custom primitive,
  and the JSON dictionary-key path falls back to `string.Empty`, so `CS8604` is gone.
- **Validation:** a rich-result validator now rejects values on every parsing path (`TryParse`, `Parse` and
  the JSON, Newtonsoft and MVC read paths); a validator or normalizer overload, including an inherited one,
  resolves by argument type, so `STIA0017` no longer misfires.
- **Analyzer:** `STIA0029` is new and lands on the user's own declaration; a `{TypeName}Surrogate` is
  reported only when the generator actually emits one.
- **Analyzer:** a `_` instance name is rejected (`CS0199`), reserved instance names are conditional, and
  `STIA0022` no longer suggests a name `STIA0020` would reject.
- **Analyzer:** a `Validator` or `Normalizer` that cannot be called with one argument is rejected up front
  instead of failing with `CS1620` in the generated code.
- **Refactoring:** "Convert to `record`" no longer produces `static record`, and "Convert to strongly typed
  id" is no longer offered on a `file`-local record (`STIA0025`), a non-`partial` container (`STIA0009`)
  or a nullable parameter (`STIA0006`).
- **Packaging:** the three-argument `TryCreate` is in `CompatibilitySuppressions.xml`, so `dotnet pack`
  passes the `1.3.0-preview.5` baseline again (`CP0006`).
- **Testing helpers:** static caches hold their keys weakly, so a collectible `AssemblyLoadContext` is no
  longer pinned, and the seeded random source honours its bounds.
- **Docs:** the `TryCreate` remark matches what a `bool` validator produces, the English feature index is in
  English again with a CJK guard, and the "Migration from another library" issue link points somewhere.

### Changed

- **Breaking:** `IStronglyTypedId<TSelf, TPrimitiveId>` now declares `TryCreate`, so hand-written
  implementations must add it.
- **Breaking:** `IStronglyTypedId<TSelf>` now extends `IComparable<TSelf>` and
  `IComparisonOperators<TSelf, TSelf, bool>`; hand-written implementations need the five members.
- **Build dependency:** the generator uses `PolySharp` instead of `IndexRange`; both are source-only, so the
  shipped assembly and generated output are unchanged.

## [1.4.1] - 2026-11-06

### Changed

- **GraphQL:** `Int128`, `UInt128` and `BigInteger` ids are strings on both input and output. The literal
  is a `StringValueNode`, matching the response.
- **Canonical text:** `DateTime`, `DateTimeOffset`, `TimeOnly` and `DateOnly` use the `"O"` format, and an
  empty `format` falls back to it. For the first three the default format is lossy: it drops sub-second
  ticks, and `TimeOnly` drops the seconds, so distinct ids could produce the same text and collapse into
  one JSON dictionary key. `DateOnly` is lossless either way; it uses `"O"` because its default short
  date (`01/02/2020`) differs from the ISO form System.Text.Json writes, so the two libraries would
  disagree on the same id.
- **Integrations:** `Integrations = Default` follows the assembly default instead of normalising to `All`,
  which overrode an assembly-level `Integrations = None`.
- **EF Core and Dapper:** `BigInteger`, `Int128`, `UInt128` and `Half` are stored as canonical text. These
  four have no database provider type; every other built-in primitive keeps its native column type.

### Fixed

- **Generator:** A `[StronglyTypedId]` repeated across `partial` declarations threw `CS8785`; only the
  first declaration produces output now, leaving `CS0579`. An id named after a generated member failed with
  `CS0542`; it now reports `STIA0027` and generation is skipped.
- **System.Text.Json:** Object-shape ids rejected a `null` primitive with `JsonException`, so
  `{"value":null,"extra":1}` could not be read back even though the writer emits `{"value":null}` for a
  reference-type primitive. The converter accepts the null now and reads on to `EndObject`.
- **Newtonsoft.Json:** Object-shape ids leaked `FormatException` and `ArgumentException` instead of a
  `JsonException`. `decimal` ids lost precision and rejected `decimal.MaxValue`; they are written as text
  now, and a JSON number is still accepted on read.
- **HotChocolate:** Same-named ids in different namespaces failed schema construction. The GraphQL name
  falls back to the sanitised full name.
- **Generated code:** Ids with a `Prefix` and HotChocolate scalars depended on the consumer's `using
  System;` and failed with `CS1061` under `<ImplicitUsings>disable`. The affected files start with `using
  global::System;`.
- **OpenAPI and Swagger:** The `BigInteger` schema said `integer` while the converter writes text. A
  nullable id lost its schema because the lookup did not unwrap `Nullable<T>`.
- **Nullable ids:** `AnyIdGenerator.Create(typeof(OrderId?))` threw instead of unwrapping the type. Bogus
  left `OrderId?` properties at `null` and threw on an explicitly passed `typeof(OrderId?)`.
- **EF Core:** Converters for non-enum primitives threw `ArgumentException` instead of the documented
  `InvalidOperationException`; they go through `TryCreate` now. `AddStronglyTypedIds(ModelBuilder)` was a
  no-op, because the model never discovers a strongly typed id as a property; it reflects over the entity's
  CLR properties now.
- **Dapper:** `DateOnly` and `TimeOnly` columns returned by SQL Server as `DateTime` and `TimeSpan` were
  rejected.
- **Analyzer:** `STIA0011` reported read-only comparisons such as `id.CompareTo(default)` and
  `id is default`.
- **Testing helpers:** `AnyIdGenerator` threw `IndexOutOfRangeException` on an empty enum.
- **Custom primitives:** An explicit `IComparable<TSelf>.CompareTo` produced five `CS7036` errors.
- **`DateTime`:** Text parsing lost its `DateTimeKind` for text ending in `"Z"`; it uses
  `DateTimeStyles.RoundtripKind` now.
- **protobuf-net:** A `null` reference-type id read back as the primitive's default.
- **Named instances:** Names such as `op_LessThan` produced `CS0102`; they are reserved now. A `DateTime`
  or `DateTimeOffset` value depended on the build machine's time zone; both forms are rejected with
  `STIA0021`.
- **Integration helpers:** Names could collide after namespace disambiguation, because `A.B.OrderId` and
  `A_B.OrderId` both became `A_B_OrderIdConverter`. A short hash is appended when the final names collide.
- **Refactoring:** "Inline strongly typed id" deleted a `global using` and broke other files with `CS0246`.

### Known limitations

- **Newtonsoft.Json:** date-like values are parsed before the converter runs. A `DateTimeOffset` id
  therefore loses its offset, and a `string` id whose value looks like an ISO date-time has its value
  replaced. This is a Newtonsoft limitation, not something the generator can fix: the original text is
  already gone by the time the converter runs. Set `DateParseHandling = DateParseHandling.None` on the
  serializer to keep the raw string; see
  [Serialization](docs/serialization.md#date-like-values-are-parsed-before-the-converter-runs).

## [1.4.0] - 2026-10-30

The integrations release: custom primitives (including ULID), protobuf-net and GraphQL integrations, a
debugger proxy on every id, and serialization and analyzer fixes. Several changes are breaking, marked
**Breaking** under **Changed**.

### Added

- **Custom primitives:** a type implementing `IParsable<TSelf>`, `IComparable<TSelf>` and the non-generic
  `IComparable`, with public static `Parse` / `TryParse`, can be the wrapped primitive (`STIA0008`
  otherwise); `System.Uri` is excluded on purpose. Every serializer stores its canonical text, and
  Cysharp's `Ulid` already qualifies, so ULID needs no extra code.
- **Integrations:** protobuf-net, GraphQL (HotChocolate ≥ 13), MessagePack and MongoDB BSON. protobuf-net
  needs no registration (the surrogate is discovered from the attribute); GraphQL registers its scalars
  through `AddStronglyTypedIds()`; MessagePack and BSON write the primitive value itself, prefix excluded.
- **Debugger proxy:** every id exposes `Value` and `Text` (canonical text, prefix included); private, with
  no run-time cost.
- **Testing packages:** `Len.StronglyTypedId.Testing` (`Any.Id<T>()`, seeded sequences,
  `Any.Register<T>(factory)`, `Any.Reset()`), plus AutoFixture and Bogus adapters that respect validators.
- **Primitives:** nine more — `char`, `Int128`, `UInt128`, `Half`, `float`, `double`, `DateOnly`, `TimeOnly`
  and `BigInteger` — 24 in total including `enum`.
- **Id options:** `Normalizer`, `ExceptionType`, `RedactValidationMessage`, `Prefix` / `PrefixReadMode`,
  `JsonShape`, `Integrations` (a filter, not a switch), `[StronglyTypedIdInstance]` and
  `IStronglyTypedId<TSelf>`, all with assembly-level defaults in `StronglyTypedIdDefaults`.
- **Registration entry points:** `options.AddStronglyTypedIds()` for System.Text.Json and Newtonsoft.Json,
  and `WithStronglyTypedIds()` for a source-generated `JsonSerializerContext` (Native AOT safe).
- **Diagnostics:** `STIA0012`–`STIA0025`, with code fixes for 21 of the 26 rules;
  `<StronglyTypedIdEnableAnalyzers>false</StronglyTypedIdEnableAnalyzers>` mutes the analyzer without
  affecting generation.
- **Tooling and samples:** a `record`-to-id refactoring, runnable samples (`SampleConsole`,
  `SampleIntegrations`, `Sample1`–`Sample4`, `SampleGraphQL`), a roadmap page, a build-verified feature
  index, and a documented "Configuration binding" section — an id needs `TypeConverter = true` to bind,
  and without it the binder fails silently.

### Changed

- **Breaking:** integration entry points are now `AddStronglyTypedIds(...)` on the configured object instead
  of `StronglyTypedIds.ApplyTo(...)`. Replace `StronglyTypedIds.ApplyTo(x)` with `x.AddStronglyTypedIds()`.
- **Breaking:** `Parse` throws `FormatException` instead of `ArgumentException` for invalid text; `Create`
  still throws `ArgumentException` (or the configured `ExceptionType`).
- **Breaking:** diagnostic ids were renamed from `STIAO###` (letter O) to `STIA0###` (digit zero).
- **Breaking for prefixed ids:** `ToString(format, provider)` and `TryFormat` keep the prefix for a non-empty
  `format` too.
- **Testing helpers:** seeded generation uses SplitMix64, so sequences are reproducible across runtimes, and
  **values produced by an existing seed change**.
- **Performance:** id discovery reads assembly metadata and caches per module MVID (−72% allocations, −54%
  time); assemblies that do not reference `Len.StronglyTypedId` are skipped.
- **Parsing:** `Parse` / `TryParse` fall back to the invariant culture when the provider is `null`;
  `STIA0011` also reports `new(...)` and `default` construction and is now a warning.
- **Build:** generated code drops the redundant `/// <summary>`; build and tests gained a zero-warning
  baseline, package validation, a Native AOT smoke test and a faster harness (`FluentAssertions` is pinned
  to 7.2.0, the last MIT release).

### Removed

- **String interning:** per-id-type interning is gone, so equal string-backed ids no longer share one
  instance. Use `string.Intern` or a cache in your own layer if you need deduplication.

### Fixed

- **Serialization:** converters no longer delegate to `JsonSerializer`; `BigInteger`, `Int128`, `UInt128`,
  `Half`, `DateOnly` and `TimeOnly` round-trip correctly under System.Text.Json and Newtonsoft.Json;
  failures throw the serializer's own exception; undefined enum values are rejected on the value path; enum
  dictionary keys honour `DictionaryKeyPolicy`; ids whose assembly lacks Newtonsoft.Json get a converter in
  the consuming assembly.
- **OpenAPI:** schemas match the JSON actually written (numeric, enum and date/time primitives,
  `JsonShape.Object`), Minimal API parameters regain `format`, and route constraints no longer block link
  generation.
- **EF Core and Dapper:** custom primitives map to `string`; Dapper handlers use the non-nullable
  signatures, write the primitive for reference-type ids, convert what the driver returns, and parse enum
  text case-insensitively.
- **Comparison and parsing:** string-backed ids compare ordinally, so `SortedSet` / `SortedDictionary` no
  longer collapse distinct ids; `null` compares as the smallest value; enum parsing rejects undefined
  values; `Half` accepts `NaN`, `±Infinity` and values that round to the largest finite value.
- **Generator:** keyword names, generic containers, nested `TypeConverter` paths, indentation, empty
  registration classes and JSON entry points for assemblies without the JSON library are all fixed.
- **Analyzer:** `STIA0016` is no longer O(N²); the analyzer no longer assumes `Constructors[0].Parameters[0]`;
  `STIA0019` covers MessagePack and MongoDB; the inline refactoring only removes the real attribute; the
  `STIA0005` and `STIA0008` fixes are corrected; `file`-local ids report `STIA0025`.
- **Native AOT:** MessagePack and BSON work for every primitive, with enum formatters and BSON defaults
  registered explicitly.
- **Testing and runtime helpers:** no crash on ids in generic containers; no pinned collectible
  `AssemblyLoadContext`s; `Any.Id<T>()` covers the full numeric range; `AddStronglyTypedIds(MvcOptions)` is
  idempotent.

## [1.3.0] - 2026-10-07

The modernisation release: the generator moved to an incremental pipeline and the surface grew from
"an id that parses" to a full DDD primitive.

### Added

- **Primitives:** 14 types plus any `enum` — `Guid`, `string`, `byte`, `sbyte`, `short`, `ushort`, `int`,
  `uint`, `long`, `ulong`, `decimal`, `DateTime`, `DateTimeOffset` and `TimeSpan`.
- **Factories and parsing:** `Create` / `TryCreate`, `Parse` / `TryParse` (string and
  `ReadOnlySpan<char>`), `IParsable<TSelf>`, and, when the primitive supports them, `IFormattable`,
  `ISpanFormattable` and the UTF-8 span interfaces.
- **Comparison and formatting:** comparison operators, `Deconstruct`, and an opt-in `TypeConverter`
  (`TypeConverter = true`).
- **Validator:** `Validator` on `[StronglyTypedId]` gives `Create`, `TryCreate` and `TryParse` one shared
  rule; assembly-level `StronglyTypedIdDefaults` supplies it for every id in the assembly.
- **Declaration sites:** ids may be declared in nested types and in generic containers.
- **Integrations:** EF Core (`ApplyTo(ModelConfigurationBuilder)` and `ApplyTo(ModelBuilder)`), Dapper
  (`ApplyTo(IDbConnection)`), Swashbuckle and ASP.NET Core OpenAPI schema mapping, MVC model binding
  and `{id:OrderId}` route constraints, System.Text.Json and Newtonsoft.Json converters.
- **String interning:** per-id-type interning gives equal string-backed ids one underlying instance without
  leaking into the global intern pool.
- **Diagnostics:** `STIA0000`–`STIA0011` with code fixes, localised (en / zh-CN).

### Changed

- **Build:** incremental generator pipeline; deterministic builds, public API baseline, package validation
  and symbol packages in CI.
- **Packaging:** `Len.StronglyTypedId` ships as a single package that pulls in the generator package.

### Fixed

- **OpenAPI:** Swashbuckle 7/8/9 are no longer misdetected as Microsoft.OpenApi 2.x, and `Guid`-backed ids
  regained `format: uuid`.
- **EF Core:** ids with the same name in different namespaces no longer collide in converters.
- **Analyzer:** multi-part `partial` declarations no longer raise `STIA0005`.
- **Code fixes:** no longer leave stray `where` constraints or drop attributes and comments.
- **Generator:** `GetModules` returns safely for module-level (`netmodule`) references instead of throwing.

## [1.3.0-preview.11] - 2026-10-02

### Fixed

- **Publishing:** publishing a symbol package twice no longer fails the release — `dotnet nuget push` now
  passes `--skip-duplicate`, turning the 409 into a warning.

## [1.3.0-preview.10] - 2026-10-02

### Changed

- **Tests:** coverage for the ASP.NET Core OpenAPI generator, including its accessibility branches.

## [1.3.0-preview.9] - 2026-10-02

_Retag of `1.3.0-preview.8`; no content changes._

## [1.3.0-preview.8] - 2026-10-02

### Added

- **Analyzer and samples:** `STIA0011` code fix, `Deconstruct` support and a console sample.
- **Packaging:** `PACKAGE.md` split out of the README as the NuGet package readme.

## [1.3.0-preview.7] - 2026-09-20

### Added

- **MVC:** ASP.NET Core model binding and `{id:OrderId}` route constraints.

## [1.3.0-preview.6] - 2026-09-20

### Added

- **Features:** `TryCreate`, UTF-8 span formatting and parsing, `TypeConverter`, Dapper and OpenAPI
  integrations, `STIA0010` / `STIA0011`, assembly-level defaults, ids in nested types, comparison /
  formatting / dictionary-key support and validator hooks.

### Changed

- **Build:** public API baseline, package validation, symbol packages and deterministic builds.

### Fixed

- **Generator, analyzer and code fixes:** around twenty defects, including symbol-level primitive detection,
  cross-namespace EF Core converter collisions, missing `uuid` formats in Swagger, third-party interfaces
  suppressing generation, and multi-part `partial` false positives.

## [1.3.0-preview.5] - 2026-09-13

### Fixed

- **Packaging:** `Len.StronglyTypedId` now declares its dependency on `Len.StronglyTypedId.Generators`.

## [1.3.0-preview.4] - 2026-09-13

_Retag of `1.3.0-preview.2`; no content changes._

## [1.3.0-preview.3] - 2026-09-13

_Retag of `1.3.0-preview.2`; no content changes._

## [1.3.0-preview.2] - 2026-09-13

### Fixed

- **Publishing:** the workflow installs both the .NET 8 and .NET 10 SDKs, and the `OutputPath` trailing
  slash that produced `NU5019`.

## [1.3.0-priview.1] - 2026-09-13

_Published with this spelling; later releases use `preview`._

### Added

- **Repository modernisation:** .NET 10 target and an expanded test matrix.

### Changed

- **Publishing:** releases use NuGet trusted publishing (OIDC) instead of a long-lived API key.

## [1.2.1] - 2024-02-26

### Added

- **Target framework:** `net7.0`.

### Fixed

- **Caching:** `ModuleInfo.GetTypes` no longer caches, fixing stale results.

## [1.2.0] - 2023-11-27

### Changed

- **Target framework:** `net8.0`.

## [1.1.3] - 2023-10-08

### Changed

- **Docs:** documentation only.

## [1.1.2] - 2023-10-08

### Changed

- **Caching:** `GetTypes` results are cached.

### Fixed

- **Generator:** namespaces in the generated `InvalidOperationException` message are fully qualified.

## [1.1.1] - 2023-05-20

### Fixed

- **Analyzer:** messages are localisable again.

## [1.1.0] - 2023-05-11

### Added

- **Generator:** incremental pipeline and a syntax analyzer.
- **Integrations:** Swagger and EF Core `ValueConverter` generators.

### Fixed

- **Generator:** no warnings for `record`-based ids.

## [1.0.7] - 2023-04-26

### Added

- **Localisation:** generator resources (zh-CN).

## [1.0.6] - 2023-04-25

### Added

- **Diagnostics:** for invalid id declarations.
- **Localisation:** generator error messages.

## [1.0.5] - 2023-04-23

### Added

- **Generated code:** `IParsable<TSelf>` and a System.Text.Json converter on generated ids.
- **Primitives:** `byte`, `sbyte`, `short` and `ushort`.

### Removed

- **Package:** `Len.StronglyTypedId.AspNetCore`, superseded by the generated integrations.

### Fixed

- **Integrations:** calling `AddStronglyTypedId` twice no longer registers duplicate resolvers.

[Unreleased]: https://github.com/lenfon/StronglyTypedId/compare/1.4.2...HEAD
[1.4.2]: https://github.com/lenfon/StronglyTypedId/compare/1.4.1...1.4.2
[1.4.1]: https://github.com/lenfon/StronglyTypedId/compare/1.4.0...1.4.1
[1.4.0]: https://github.com/lenfon/StronglyTypedId/compare/1.3.0...1.4.0
[1.3.0]: https://github.com/lenfon/StronglyTypedId/compare/1.3.0-preview.11...1.3.0
[1.3.0-preview.11]: https://github.com/lenfon/StronglyTypedId/compare/1.3.0-preview.10...1.3.0-preview.11
[1.3.0-preview.10]: https://github.com/lenfon/StronglyTypedId/compare/1.3.0-preview.9...1.3.0-preview.10
[1.3.0-preview.9]: https://github.com/lenfon/StronglyTypedId/compare/1.3.0-preview.8...1.3.0-preview.9
[1.3.0-preview.8]: https://github.com/lenfon/StronglyTypedId/compare/1.3.0-preview.7...1.3.0-preview.8
[1.3.0-preview.7]: https://github.com/lenfon/StronglyTypedId/compare/1.3.0-preview.6...1.3.0-preview.7
[1.3.0-preview.6]: https://github.com/lenfon/StronglyTypedId/compare/1.3.0-preview.5...1.3.0-preview.6
[1.3.0-preview.5]: https://github.com/lenfon/StronglyTypedId/compare/1.3.0-preview.2...1.3.0-preview.5
[1.3.0-preview.4]: https://github.com/lenfon/StronglyTypedId/compare/1.3.0-preview.2...1.3.0-preview.4
[1.3.0-preview.3]: https://github.com/lenfon/StronglyTypedId/compare/1.3.0-preview.2...1.3.0-preview.3
[1.3.0-preview.2]: https://github.com/lenfon/StronglyTypedId/compare/1.3.0-priview.1...1.3.0-preview.2
[1.3.0-priview.1]: https://github.com/lenfon/StronglyTypedId/compare/v1.2.1...1.3.0-priview.1
[1.2.1]: https://github.com/lenfon/StronglyTypedId/compare/v1.2.0...v1.2.1
[1.2.0]: https://github.com/lenfon/StronglyTypedId/compare/v1.1.3...v1.2.0
[1.1.3]: https://github.com/lenfon/StronglyTypedId/compare/v1.1.2...v1.1.3
[1.1.2]: https://github.com/lenfon/StronglyTypedId/compare/v1.1.1...v1.1.2
[1.1.1]: https://github.com/lenfon/StronglyTypedId/compare/v1.1.0...v1.1.1
[1.1.0]: https://github.com/lenfon/StronglyTypedId/compare/v1.0.7...v1.1.0
[1.0.7]: https://github.com/lenfon/StronglyTypedId/compare/v1.0.6...v1.0.7
[1.0.6]: https://github.com/lenfon/StronglyTypedId/compare/v1.0.5...v1.0.6
[1.0.5]: https://github.com/lenfon/StronglyTypedId/releases/tag/v1.0.5
