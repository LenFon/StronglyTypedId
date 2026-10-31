# Contributing to StronglyTypedId

Thanks for taking the time to contribute. This document covers the mechanics: how to build, what the
gates are, and which files must stay in sync when you change behaviour.

By participating you agree to the [Code of Conduct](CODE_OF_CONDUCT.md). For security issues, **do not**
open a public issue; see [SECURITY.md](SECURITY.md).

## Ways to contribute

- **Bug reports.** Use the issue template. A minimal repro (the `record` declaration plus the failing
  call) is worth more than a long description.
- **Feature requests.** Open an issue first and describe the problem, not just the API you want. Changes
  to the generated surface are breaking changes for every consumer, so they need agreement before code.
- **Pull requests.** Small, focused, one concern per PR. If a change touches generated output, expect
  the review to focus on the tests that pin that output.

## Prerequisites

- .NET SDK **10.0.401**, pinned in `global.json` with `rollForward: latestPatch`. A different feature
  band will not be picked up.
- Nothing else. The repository builds offline after the first restore.

## Build and test

```bash
# Full rebuild. The acceptance bar is 0 warnings, 0 errors.
dotnet build Len.StronglyTypedId.slnx -t:Rebuild

# Full test run (Microsoft.Testing.Platform, both TFMs).
dotnet test --solution Len.StronglyTypedId.slnx

# A single test class, when iterating.
dotnet test --solution Len.StronglyTypedId.slnx --filter-class "*StronglyTypedIdAnalyzer*Tests"
```

Two practical notes:

- **Analyzer / code-fix suites are split by rule family on purpose.** Each test class is its own xUnit
  collection, so splitting is what buys parallelism; a single 3000-line class runs serially no matter
  how many cores the machine has. Keep new analyzer cases in the class matching their rule, and do not
  merge them back into one file.
- **Stop stale test hosts before rebuilding.** On Windows a leftover `Len.StronglyTypedId.*` test process
  keeps the output assemblies locked and the build fails with MSB3026/MSB3027.
- **`0 warnings` is a real gate, not a slogan.** `EnforceCodeStyleInBuild` is on, so IDE rules fail the
  build too. Do not silence a new warning with `NoWarn`; fix it, or raise it in the PR.
- **Generated-code snapshots are plain text files.** `GeneratorSnapshotTests` compares each scenario's
  full output against `test/Len.StronglyTypedId.Generators.Test/Snapshots/`. A mismatch writes a
  `.received.txt` next to the baseline and fails with the first differing line. When the change is
  intended, regenerate with `STRONGLY_TYPED_ID_UPDATE_SNAPSHOTS=1 dotnet test --project
  test/Len.StronglyTypedId.Generators.Test` and review the diff; the baselines are the review artifact.
  The reference set is pinned to net8.0, so one baseline serves both TFMs.

## Gates that run in CI

| Gate | Command | What it protects |
| --- | --- | --- |
| Build | `dotnet build Len.StronglyTypedId.slnx -t:Rebuild` | 0 warnings, 0 errors |
| Tests | `dotnet test --solution Len.StronglyTypedId.slnx` | Behaviour, both TFMs |
| Docs | `dotnet run --project tools/Len.StronglyTypedId.DocGen -- --verify` | README tables match the source |
| Package readme | `dotnet run --project tools/Len.StronglyTypedId.DocGen -- --verify-package-readme` | `PACKAGE.md` matches the source |
| Packaging | `Len.StronglyTypedId.Packaging.Test` | The `.nupkg` actually carries the generator, the readme and the icon |
| API compat | `dotnet pack -c Release` | No accidental breaking change against the published baseline |

### The generator performance gate

`bench/Len.StronglyTypedId.Benchmarks` holds four benchmark groups (`GeneratorRunBenchmarks`,
`DiscoveryBenchmarks`, `ColdDiscoverBenchmarks`, `GetTypesBenchmarks`) for local investigation via
BenchmarkDotNet:

```bash
dotnet run -c Release --project bench/Len.StronglyTypedId.Benchmarks -- --filter *Discovery*
```

CI runs the deterministic gate instead, because a single BDN run takes minutes and wall-clock noise on a
shared runner routinely exceeds ±20%:

```bash
dotnet run -c Release --project bench/Len.StronglyTypedId.Benchmarks -- --gate
```

**Allocated bytes is the primary criterion.** The same code on the same path allocates the same number of
bytes regardless of machine speed, and almost every Roslyn-side regression (enumerating symbols twice,
concatenating one more string, adding a LINQ layer) shows up as allocation. Elapsed time is only an
order-of-magnitude backstop (10× the baseline), there to catch "allocations unchanged but complexity
exploded".

| Knob | Value | Why |
| --- | --- | --- |
| Allocation tolerance | +10% | Consecutive runs on one machine differ by about 0.005%, so 10% is ~2000× the observed noise. It cannot go to 20%: reverting a real regression in this repository's history (one enumeration turned into five) raised allocations by only 16.3%, which a 20% threshold would have waved through |
| Elapsed tolerance | 10× baseline | Wall clock is not comparable across machines: the same code and baseline measured 13.97 ms on a developer machine and 59.75 ms on `windows-latest`, while allocations differed by 0.011% |
| Runs | 3 warm-up + 10 measured, median | The compilation and driver are rebuilt every iteration; reusing a driver would hit the incremental cache and stop measuring a build |

The baseline lives in `bench/Len.StronglyTypedId.Benchmarks/baselines/generator-run.baseline.json` and
records the runtime version alongside the numbers. If you change performance characteristics on purpose,
rewrite it and commit it with the change:

```bash
dotnet run -c Release --project bench/Len.StronglyTypedId.Benchmarks -- --gate --update-baseline
```

The gate self-checks: every measured run must produce the same number of generated files as the first,
otherwise the numbers are not comparable and it fails loudly instead of reporting a delta.

## Files that must stay in sync

Most of the friction in this repository comes from the same fact stated five ways. If you change one of
these, check the rest:

| You changed | Also update |
| --- | --- |
| A diagnostic (id, severity, title, message, fixability) | `Descriptors.cs`, `AnalyzerReleases.Shipped.md` / `.Unshipped.md`, and the README diagnostics table (regenerate with DocGen; never hand-edit it) |
| A supported primitive type | `SupportedPrimitiveTypes.DocumentedTypes`, the README primitive table (DocGen), and the serializer gaps noted in `Limitations` |
| An integration's availability condition | The README "What gets generated" table (DocGen) and `PACKAGE.md` |
| A `build` / `buildTransitive` `.props` or `.targets` file | Its file name must match the package ID exactly, because NuGet only auto-imports files named after the package. A mismatch ships the file without importing it, so the property silently never reaches the analyzer |
| Public API of `Len.StronglyTypedId` | `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt`; RS0016/RS0017 are errors |
| Generated code shape | The generator tests that assert on the emitted text, the snapshots under `test/Len.StronglyTypedId.Generators.Test/Snapshots/`, plus `samples/` if the sample output changes |
| Anything user-visible | `CHANGELOG.md` **and** `CHANGELOG.zh-CN.md`; both are verified against the tag |

The README tables live between `<!-- BEGIN GENERATED:… -->` / `<!-- END GENERATED:… -->` markers. Editing
inside those markers by hand will be reverted by the next DocGen run and will fail `--verify` in CI.

## Adding an integration

An integration touches seven places. All eleven existing ones were built this way, so follow the pattern
rather than inventing a shorter path:

| # | File | What changes |
| --- | --- | --- |
| 1 | `src/Len.StronglyTypedId/…/StronglyTypedIdIntegrations.cs` | Add the public enum bit and fold it into `All` |
| 2 | `src/Len.StronglyTypedId.Generators/…/IntegrationFlags.cs` | Add the **mirrored** constant. The generator does not reference the runtime assembly, so it matches by integer bit, and the two must agree bit for bit |
| 3 | `src/Len.StronglyTypedId.Generators/…/Generators/<Xxx>CodeGenerator.cs` | The new generator, implementing `IIntegrationCodeGenerator` (`Order` decides execution order) |
| 4 | `…/Generators/StronglyTypedIdGenerator.cs` | One `CodeGeneratorRegistration` in `_generatorCatalog` (assembly gate + English and Chinese `DocText`), a `GenerateXxxCode` method, and one `RegisterSourceOutput` line |
| 5 | `…/Generators/PrimitiveIdSchemaMap.cs` | If the integration exposes an OpenAPI schema, the mapping goes here; Swashbuckle and the built-in OpenAPI share it, so the two cannot drift |
| 6 | `docs/integrations.md` + `docs/zh-CN/integrations.md` | The integration's section. DocGen `--verify` checks the README table against the registration entries |
| 7 | `test/…/Generators/` + `test/Len.StronglyTypedId.Integration.Test/` | Generator unit tests plus an end-to-end round-trip |

Enum bits currently occupy bit 0–11 (`None` plus the eleven integrations); the next free one is `1 << 12`.

Two conventions the gate and the entry point must follow:

- **Gate** with `modules.HasModule("Xxx.dll", minimumMajorVersion)`, comparing `module.Version.Major`.
- **Registration entry points are idempotent.** The System.Text.Json and Newtonsoft.Json `AddStronglyTypedIds`
  collect already-registered converter types into a `HashSet` before adding, so a second call neither grows
  the list nor throws `InvalidOperationException` on a frozen `JsonSerializerOptions`. MessagePack's
  `AddStronglyTypedIds` returns a **new** instance instead, because `MessagePackSerializerOptions` is
  immutable.

## Changing a diagnostic

Diagnostics are the most rule-bound part of the codebase:

1. New ids go into `AnalyzerReleases.Unshipped.md` under `## Release <next version>`. A missing entry fails
   RS2000; a mismatched severity fails RS2001; a diagnostic that is off by default must be listed as
   `Disabled`.
2. The `DiagnosticDocumentation` table and `FixableDiagnosticIds` are cross-checked by tests. If a
   diagnostic gains or loses a code fix, both sides change together.
3. Prefer a diagnostic over silent behaviour. Several past bugs were "the generator quietly did nothing";
   the fix was a new id, not a comment.

## Commit messages

[Conventional Commits](https://www.conventionalcommits.org/): `fix(generators): …`, `feat(analyzer): …`,
`test(packaging): …`, `chore: …`, `ci: …`. `DocGen --draft-changelog` groups commits by these prefixes,
so an unconventional prefix means a missing changelog line.

Keep the subject in the imperative mood and under ~72 characters. Explain *why* in the body when the
change is not obvious from the diff.

## Style

- `.editorconfig` is authoritative; the build enforces it. Run `dotnet format` before pushing if your
  editor does not apply it on save.
- Comments explain **why**, especially where the code looks wrong on purpose. This repository has a lot of
  "this looks redundant but isn't", and the comment is what stops the next person from "cleaning it up".
- Tests are named `Subject_Should_ExpectedBehaviour_WhenCondition` and use FluentAssertions.

## Review

Expect questions about edge cases rather than style. The interesting failures in this library have all
been at boundaries: nullable ids, culture-sensitive parsing, `[Flags]` enums, nested types, and the
difference between `ToString()` and string interpolation. If your change touches one of those, say so in
the PR description and add a test that pins the behaviour you chose.
