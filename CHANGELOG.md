# Changelog

> 📖 中文版：[CHANGELOG.zh-CN.md](CHANGELOG.zh-CN.md)

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Releases up to `1.3.0` are reconstructed from the repository tags: each entry summarises the shipped
change set rather than reproducing every commit.

## [Unreleased]

## [1.4.0] - 2026-10-30

The integrations release: custom primitives (including ULID), protobuf-net and GraphQL integrations, a
debugger proxy on every id, and serialization and analyzer fixes. Several changes are breaking — marked
**Breaking** under **Changed**.

### Added

- **Custom primitives, including ULID.** A type implementing `IParsable<TSelf>`, `IComparable<TSelf>` and
  the non-generic `IComparable`, with public static `Parse` / `TryParse`, can be the wrapped primitive
  (`STIA0008` otherwise). Every serializer stores its canonical text; `System.Uri` is excluded on purpose.
  Cysharp's `Ulid` already qualifies, so ULID needs no extra code.
- **protobuf-net, GraphQL (HotChocolate ≥ 13), MessagePack and MongoDB BSON integrations.** protobuf-net
  needs no registration (the surrogate is discovered from the attribute); GraphQL registers its scalars
  through `AddStronglyTypedIds()`; MessagePack and BSON write the primitive value itself, prefix excluded.
- **A debugger proxy on every id** — `Value` and `Text` (canonical text, prefix included); private, with no
  run-time cost.
- **Testing packages** — `Len.StronglyTypedId.Testing` (`Any.Id<T>()`, seeded sequences,
  `Any.Register<T>(factory)`, `Any.Reset()`), plus AutoFixture and Bogus adapters that respect validators.
- **Nine more primitives** — `char`, `Int128`, `UInt128`, `Half`, `float`, `double`, `DateOnly`, `TimeOnly`
  and `BigInteger` — 24 in total including `enum`.
- **Id options** — `Normalizer`, `ExceptionType`, `RedactValidationMessage`, `Prefix` / `PrefixReadMode`,
  `JsonShape`, `Integrations` (a filter, not a switch), `[StronglyTypedIdInstance]` and
  `IStronglyTypedId<TSelf>`, all with assembly-level defaults in `StronglyTypedIdDefaults`.
- **Registration entry points** — `options.AddStronglyTypedIds()` for System.Text.Json and Newtonsoft.Json,
  and `WithStronglyTypedIds()` for a source-generated `JsonSerializerContext` (Native AOT safe).
- **Diagnostics `STIA0012`–`STIA0025`**, with code fixes for 21 of the 26 rules;
  `<StronglyTypedIdEnableAnalyzers>false</StronglyTypedIdEnableAnalyzers>` mutes the analyzer without
  affecting generation.
- A `record`-to-id refactoring, runnable samples (`SampleConsole`, `SampleIntegrations`, `Sample1`–`Sample4`,
  `SampleGraphQL`), a roadmap page, a build-verified feature index, and a documented "Configuration binding"
  section — an id needs `TypeConverter = true` to bind, and without it the binder fails silently.

### Changed

- **Breaking:** integration entry points are now `AddStronglyTypedIds(...)` on the configured object instead
  of `StronglyTypedIds.ApplyTo(...)`. Replace `StronglyTypedIds.ApplyTo(x)` with `x.AddStronglyTypedIds()`.
- **Breaking:** `Parse` throws `FormatException` instead of `ArgumentException` for invalid text; `Create`
  still throws `ArgumentException` (or the configured `ExceptionType`).
- **Breaking:** diagnostic ids were renamed from `STIAO###` (letter O) to `STIA0###` (digit zero).
- **Breaking for prefixed ids:** `ToString(format, provider)` and `TryFormat` keep the prefix for a non-empty
  `format` too.
- Seeded generation uses SplitMix64, so sequences are reproducible across runtimes — **values produced by an
  existing seed change**.
- Id discovery reads assembly metadata and caches per module MVID (−72% allocations, −54% time); assemblies
  that do not reference `Len.StronglyTypedId` are skipped.
- `Parse` / `TryParse` fall back to the invariant culture when the provider is `null`; `STIA0011` also
  reports `new(...)` and `default` construction and is now a warning.
- Generated code drops the redundant `/// <summary>`; build and tests gained a zero-warning baseline,
  package validation, a Native AOT smoke test and a faster harness (`FluentAssertions` is pinned to 7.2.0,
  the last MIT release).

### Removed

- Per-id-type string interning: equal string-backed ids no longer share one instance. Use `string.Intern` or
  a cache in your own layer if you need deduplication.

### Fixed

- **Serialization** — converters no longer delegate to `JsonSerializer`; `BigInteger`, `Int128`, `UInt128`,
  `Half`, `DateOnly` and `TimeOnly` round-trip correctly under System.Text.Json and Newtonsoft.Json;
  failures throw the serializer's own exception; undefined enum values are rejected on the value path; enum
  dictionary keys honour `DictionaryKeyPolicy`; ids whose assembly lacks Newtonsoft.Json get a converter in
  the consuming assembly.
- **OpenAPI** — schemas match the JSON actually written (numeric, enum and date/time primitives,
  `JsonShape.Object`), Minimal API parameters regain `format`, and route constraints no longer block link
  generation.
- **EF Core and Dapper** — custom primitives map to `string`; Dapper handlers use the non-nullable
  signatures, write the primitive for reference-type ids, convert what the driver returns, and parse enum
  text case-insensitively.
- **Comparison and parsing** — string-backed ids compare ordinally, so `SortedSet` / `SortedDictionary` no
  longer collapse distinct ids; `null` compares as the smallest value; enum parsing rejects undefined
  values; `Half` accepts `NaN`, `±Infinity` and values that round to the largest finite value.
- **Generator** — keyword names, generic containers, nested `TypeConverter` paths, indentation, empty
  registration classes and JSON entry points for assemblies without the JSON library are all fixed.
- **Analyzer and code fixes** — `STIA0016` is no longer O(N²); the analyzer no longer assumes
  `Constructors[0].Parameters[0]`; `STIA0019` covers MessagePack and MongoDB; the inline refactoring only
  removes the real attribute; the `STIA0005` and `STIA0008` fixes are corrected; `file`-local ids report
  `STIA0025`.
- **Native AOT** — MessagePack and BSON work for every primitive, with enum formatters and BSON defaults
  registered explicitly.
- **Testing and runtime helpers** — no crash on ids in generic containers; no pinned collectible
  `AssemblyLoadContext`s; `Any.Id<T>()` covers the full numeric range; `AddStronglyTypedIds(MvcOptions)` is
  idempotent.

## [1.3.0] - 2026-10-07

The modernisation release: the generator moved to an incremental pipeline and the surface grew from
"an id that parses" to a full DDD primitive.

### Added

- 14 primitive types plus any `enum`: `Guid`, `string`, `byte`, `sbyte`, `short`, `ushort`, `int`,
  `uint`, `long`, `ulong`, `decimal`, `DateTime`, `DateTimeOffset` and `TimeSpan`.
- Factories and parsing: `Create` / `TryCreate`, `Parse` / `TryParse` (string and `ReadOnlySpan<char>`),
  `IParsable<TSelf>`, and — when the primitive supports them — `IFormattable`, `ISpanFormattable`
  and the UTF-8 span interfaces.
- Comparison operators, `Deconstruct`, and an opt-in `TypeConverter` (`TypeConverter = true`).
- `Validator` on `[StronglyTypedId]`: one rule shared by `Create`, `TryCreate` and `TryParse`;
  assembly-level `StronglyTypedIdDefaults` supplies it for every id in the assembly.
- Ids may be declared in nested types and in generic containers.
- Integrations: EF Core (`ApplyTo(ModelConfigurationBuilder)` and `ApplyTo(ModelBuilder)`), Dapper
  (`ApplyTo(IDbConnection)`), Swashbuckle and ASP.NET Core OpenAPI schema mapping, MVC model binding
  and `{id:OrderId}` route constraints, System.Text.Json and Newtonsoft.Json converters.
- Per-id-type string interning: equal string-backed ids share one underlying instance without
  leaking into the global intern pool.
- Diagnostics `STIA0000`–`STIA0011` with code fixes, localised (en / zh-CN).

### Changed

- Incremental generator pipeline; deterministic builds, public API baseline, package validation and
  symbol packages in CI.
- `Len.StronglyTypedId` ships as a single package that pulls in the generator package.

### Fixed

- Swashbuckle 7/8/9 are no longer misdetected as Microsoft.OpenApi 2.x, and `Guid`-backed ids
  regained `format: uuid`.
- Ids with the same name in different namespaces no longer collide in EF Core converters.
- Multi-part `partial` declarations no longer raise `STIA0005`.
- The code fix no longer leaves stray `where` constraints or drops attributes and comments.
- `GetModules` returns safely for module-level (`netmodule`) references instead of throwing.

## [1.3.0-preview.11] - 2026-10-02

### Fixed

- Publishing a symbol package twice no longer fails the release: `dotnet nuget push` now passes
  `--skip-duplicate`, turning the 409 into a warning.

## [1.3.0-preview.10] - 2026-10-02

### Changed

- Test coverage for the ASP.NET Core OpenAPI generator, including its accessibility branches.

## [1.3.0-preview.9] - 2026-10-02

_Retag of `1.3.0-preview.8`; no content changes._

## [1.3.0-preview.8] - 2026-10-02

### Added

- `STIA0011` code fix, `Deconstruct` support and a console sample.
- `PACKAGE.md` split out of the README as the NuGet package readme.

## [1.3.0-preview.7] - 2026-09-20

### Added

- ASP.NET Core MVC model binding and `{id:OrderId}` route constraints.

## [1.3.0-preview.6] - 2026-09-20

### Added

- `TryCreate`, UTF-8 span formatting and parsing, `TypeConverter`, Dapper and OpenAPI integrations,
  `STIA0010` / `STIA0011`, assembly-level defaults, ids in nested types, comparison / formatting /
  dictionary-key support and validator hooks.

### Changed

- Public API baseline, package validation, symbol packages and deterministic builds.

### Fixed

- Around twenty generator, analyzer and code-fix defects, including symbol-level primitive
  detection, cross-namespace EF Core converter collisions, missing `uuid` formats in Swagger,
  third-party interfaces suppressing generation, and multi-part `partial` false positives.

## [1.3.0-preview.5] - 2026-09-13

### Fixed

- `Len.StronglyTypedId` now declares its dependency on `Len.StronglyTypedId.Generators`.

## [1.3.0-preview.4] - 2026-09-13

_Retag of `1.3.0-preview.2`; no content changes._

## [1.3.0-preview.3] - 2026-09-13

_Retag of `1.3.0-preview.2`; no content changes._

## [1.3.0-preview.2] - 2026-09-13

### Fixed

- The publish workflow installs both the .NET 8 and .NET 10 SDKs, and the `OutputPath` trailing
  slash that produced NU5019.

## [1.3.0-priview.1] - 2026-09-13

_Published with this spelling; later releases use `preview`._

### Added

- Repository modernisation: .NET 10 target and an expanded test matrix.

### Changed

- Releases use NuGet trusted publishing (OIDC) instead of a long-lived API key.

## [1.2.1] - 2024-02-26

### Added

- `net7.0` target.

### Fixed

- `ModuleInfo.GetTypes` no longer caches, fixing stale results.

## [1.2.0] - 2023-11-27

### Changed

- Targets `net8.0`.

## [1.1.3] - 2023-10-08

### Changed

- Documentation only.

## [1.1.2] - 2023-10-08

### Changed

- `GetTypes` results are cached.

### Fixed

- Namespaces in the generated `InvalidOperationException` message are fully qualified.

## [1.1.1] - 2023-05-20

### Fixed

- Analyzer messages are localisable again.

## [1.1.0] - 2023-05-11

### Added

- Incremental generator pipeline and a syntax analyzer.
- Swagger and EF Core `ValueConverter` generators.

### Fixed

- Warnings emitted for `record`-based ids.

## [1.0.7] - 2023-04-26

### Added

- Localised generator resources (zh-CN).

## [1.0.6] - 2023-04-25

### Added

- Generator diagnostics for invalid id declarations.
- Localised generator error messages.

## [1.0.5] - 2023-04-23

### Added

- `IParsable<TSelf>` and a System.Text.Json converter on generated ids.
- `byte`, `sbyte`, `short` and `ushort` primitives.

### Removed

- `Len.StronglyTypedId.AspNetCore`, superseded by the generated integrations.

### Fixed

- Calling `AddStronglyTypedId` twice no longer registers duplicate resolvers.

[Unreleased]: https://github.com/lenfon/StronglyTypedId/compare/1.4.0...HEAD
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
