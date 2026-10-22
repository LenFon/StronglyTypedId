# Contributing to StronglyTypedId

Thanks for taking the time to contribute. This document covers the mechanics — how to build, what the
gates are, and which files must stay in sync when you change behaviour.

By participating you agree to the [Code of Conduct](CODE_OF_CONDUCT.md). For security issues, **do not**
open a public issue — see [SECURITY.md](SECURITY.md).

## Ways to contribute

- **Bug reports** — use the issue template. A minimal repro (the `record` declaration plus the failing
  call) is worth more than a long description.
- **Feature requests** — open an issue first and describe the problem, not just the API you want. Changes
  to the generated surface are breaking changes for every consumer, so they need agreement before code.
- **Pull requests** — small, focused, one concern per PR. If a change touches generated output, expect
  the review to focus on the tests that pin that output.

## Prerequisites

- .NET SDK **10.0.401** — pinned in `global.json` with `rollForward: latestPatch`. A different feature
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
  collection, so splitting is what buys parallelism — a single 3000-line class runs serially no matter
  how many cores the machine has. Keep new analyzer cases in the class matching their rule, and do not
  merge them back into one file.
- **Stop stale test hosts before rebuilding.** On Windows a leftover `Len.StronglyTypedId.*` test process
  keeps the output assemblies locked and the build fails with MSB3026/MSB3027.
- **`0 warnings` is a real gate, not a slogan.** `EnforceCodeStyleInBuild` is on, so IDE rules fail the
  build too. Do not silence a new warning with `NoWarn` — fix it, or raise it in the PR.

## Gates that run in CI

| Gate | Command | What it protects |
| --- | --- | --- |
| Build | `dotnet build Len.StronglyTypedId.slnx -t:Rebuild` | 0 warnings, 0 errors |
| Tests | `dotnet test --solution Len.StronglyTypedId.slnx` | Behaviour, both TFMs |
| Docs | `dotnet run --project tools/Len.StronglyTypedId.DocGen -- --verify` | README tables match the source |
| Changelog | `dotnet run --project tools/Len.StronglyTypedId.DocGen -- --verify-changelog` | Tag ↔ `CHANGELOG*.md` sections |
| Package readme | `dotnet run --project tools/Len.StronglyTypedId.DocGen -- --verify-package-readme` | `PACKAGE.md` matches the source |
| Packaging | `Len.StronglyTypedId.Packaging.Test` | The `.nupkg` actually carries the generator, the readme and the icon |
| API compat | `dotnet pack -c Release` | No accidental breaking change against the published baseline |

## Files that must stay in sync

Most of the friction in this repository comes from the same fact stated five ways. If you change one of
these, check the rest:

| You changed | Also update |
| --- | --- |
| A diagnostic (id, severity, title, message, fixability) | `Descriptors.cs`, `AnalyzerReleases.Shipped.md` / `.Unshipped.md`, and the README diagnostics table (regenerate with DocGen — never hand-edit it) |
| A supported primitive type | `SupportedPrimitiveTypes.DocumentedTypes`, the README primitive table (DocGen), and the serializer gaps noted in `Limitations` |
| An integration's availability condition | The README "What gets generated" table (DocGen) and `PACKAGE.md` |
| Public API of `Len.StronglyTypedId` | `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt` — RS0016/RS0017 are errors |
| Generated code shape | The generator tests that assert on the emitted text, plus `samples/` if the sample output changes |
| Anything user-visible | `CHANGELOG.md` **and** `CHANGELOG.zh-CN.md` — both are verified against the tag |

The README tables live between `<!-- BEGIN GENERATED:… -->` / `<!-- END GENERATED:… -->` markers. Editing
inside those markers by hand will be reverted by the next DocGen run and will fail `--verify` in CI.

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
  "this looks redundant but isn't" — the comment is what stops the next person from "cleaning it up".
- Tests are named `Subject_Should_ExpectedBehaviour_WhenCondition` and use FluentAssertions.

## Review

Expect questions about edge cases rather than style. The interesting failures in this library have all
been at boundaries: nullable ids, culture-sensitive parsing, `[Flags]` enums, nested types, and the
difference between `ToString()` and string interpolation. If your change touches one of those, say so in
the PR description and add a test that pins the behaviour you chose.
