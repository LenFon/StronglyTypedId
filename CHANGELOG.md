# Changelog

> 📖 中文版：[CHANGELOG.zh-CN.md](CHANGELOG.zh-CN.md)

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Releases up to `1.3.0` are reconstructed from the repository tags: each entry summarises the shipped
change set rather than reproducing every commit.

## [Unreleased]

### Added

- `Len.StronglyTypedId.Testing`: deterministic id generation for tests — `Any.Id<T>()`,
  `Any.Ids<T>(count)`, seeded sequences, `Any.Register<T>(factory)` overrides and `Any.Reset()`.
  Values rejected by a generated validator are retried instead of surfacing as test failures.
- `Len.StronglyTypedId.Testing.AutoFixture`: `fixture.WithStronglyTypedIds()` fills strongly typed
  ids through the same generator, so AutoFixture-created object graphs respect validators.
- `IStronglyTypedId<TSelf>` — a single-parameter view of the id contract, so generic code can
  constrain on "some strongly typed id" without naming the primitive. `IFormattableStronglyTypedId<TSelf>`
  is generated when the primitive implements `IFormattable`.
- Eight more primitives: `Int128`, `UInt128`, `Half`, `float`, `double`, `DateOnly`, `TimeOnly` and
  `BigInteger` (23 supported types including `enum`).
- `char` as a supported primitive, bringing the total to 24 types including `enum`. Its parsing and
  formatting members are explicit interface implementations, so the generator emits a length check
  and an interface call instead of `char.TryParse` / `char.ToString(format, provider)`.
- `Normalizer` — point it at a `static TPrimitiveId Normalize(TPrimitiveId value)` member to repair
  incoming values (trim, case-fold, drop separators) before they are validated and stored. Every
  entry point runs normalize → validate → construct, and `[assembly: StronglyTypedIdDefaults(Normalizer = ...)]`
  sets an assembly-wide default. A malformed reference is reported as `STIA0017`, with a code fix that
  generates a pass-through stub or drops the argument.
- `ExceptionType` — point it at a custom exception to change what `Create` throws when the validator
  rejects a value. The type must derive from `System.Exception`, be non-abstract and expose an accessible
  constructor taking a single `string` message; anything else is reported as `STIA0018`, with a code fix
  that drops the argument. `Parse` keeps throwing `ArgumentException`, because its failure also covers
  text that is not an id at all. `[assembly: StronglyTypedIdDefaults(ExceptionType = ...)]` sets an
  assembly-wide default.
- Text prefixes: `Prefix` writes a constant prefix in `ToString` and strips it in `Parse`;
  `PrefixReadMode` chooses between accepting a bare value (`Lenient`) and rejecting it (`Strict`).
- `JsonShape` selects the serialized shape: the bare primitive (`Primitive`) or
  `{ "value": ... }` (`Object`).
- Registration entry points for System.Text.Json (`AddStronglyTypedId`) and Newtonsoft.Json
  (`AddStronglyTypedId`), for callers that configure converters explicitly instead of relying on the
  generated `[JsonConverter]` attribute.
- A refactoring provider that turns an existing `record` into a strongly typed id.
- `STIA0012`–`STIA0018` diagnostics, including `STIA0015` (id name should follow the `...Id`
  convention), `STIA0016` (two ids over the same primitive in one assembly), `STIA0017`
  (malformed `Normalizer` reference) and `STIA0018` (unusable `ExceptionType`).
- Runnable samples: `SampleConsole` (guided tour of the whole surface), `SampleIntegrations`
  (EF Core + Dapper against in-memory SQLite), `Sample1` (MVC + Swashbuckle 6 + Newtonsoft.Json),
  `Sample2` (Minimal API + built-in OpenAPI), `Sample3` (MVC + Swashbuckle 10 / Microsoft.OpenApi 2.x).

### Changed

- **Breaking:** diagnostic ids were renamed from `STIAO###` (letter O) to `STIA0###` (digit zero).
  Existing `dotnet_diagnostic.STIAO011.severity` entries in `.editorconfig` stop matching; the
  rename happened before the first stable release.
- `Parse` / `TryParse` now fall back to the invariant culture when the format provider is `null`,
  matching what `ToString()` writes. A `null` provider used to mean the current culture, so
  `Parse(id.ToString())` silently produced wrong values under e.g. `de-DE` (`"1.5"` → `15`). An
  explicitly passed provider is still honoured.
- The README reference tables (primitive types, integrations, diagnostics) are generated from the
  generator source by `tools/Len.StronglyTypedId.DocGen`; CI fails when they drift.
- Performance: generator discovery results are reused across the pipeline, `TryFormat` no longer
  boxes, and the `CompilationProvider` dependency was pushed down.
- Build: `.editorconfig` + `Directory.Build.props` pin a zero-warning baseline (IDE/CA/xUnit rules
  promoted to warnings), package validation runs on `dotnet pack`, and CI gained a Native AOT smoke
  test, package-content assertions and a generator allocation gate.
- `STIA0011` now also reports target-typed `new(...)` and `default` / `default(T)` construction, not
  just `new Xxx(...)`, and its default severity was raised from `Info` to `Warning` so that bypassing
  a validator shows up in build output and CI. It also fires for ids that only declare a `Normalizer`,
  since a directly constructed id is then not in canonical form. A `default` that is only compared
  against (`id == default`) is not reported. Projects building with `TreatWarningsAsErrors` will now
  fail on a bypassed validator; restore the previous behaviour with
  `dotnet_diagnostic.STIA0011.severity = suggestion`.

### Fixed

- OpenAPI parameter schemas now carry the primitive `format` (`/orders/{id}` →
  `type: string, format: uuid`). Route and query parameters describe the primitive in
  `JsonTypeInfo`, so the schema transformer previously missed them; request and response bodies were
  already correct.
- Ids declared inside a generic container no longer emit `[JsonConverter(typeof(...))]`, which is
  illegal in an attribute argument (CS0416). The converter type is still generated and can be
  registered manually for a closed instantiation.
- JSON registration entry points are no longer generated for ids whose declaring assembly does not
  reference the JSON library, removing CS0426 in consumer projects.

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

[Unreleased]: https://github.com/lenfon/StronglyTypedId/compare/1.3.0...HEAD
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
