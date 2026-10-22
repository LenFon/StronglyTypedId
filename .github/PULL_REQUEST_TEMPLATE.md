<!--
  Keep the summary short and the checklist honest — an unchecked box is a question the reviewer
  would otherwise have to ask.
-->

## Summary

<!-- What changes, and why. Link the issue if there is one: "Closes #123". -->

## Motivation

<!-- The problem this solves. If it is a bug, describe the failing scenario, not just the symptom. -->

## Checklist

- [ ] `dotnet build Len.StronglyTypedId.slnx -t:Rebuild` reports **0 warnings, 0 errors**
- [ ] `dotnet test --solution Len.StronglyTypedId.slnx` passes
- [ ] `DocGen --verify` passes (README tables regenerated, not hand-edited)
- [ ] `DocGen --verify-changelog` / `--verify-package-readme` pass, if docs or `PACKAGE.md` changed
- [ ] `CHANGELOG.md` **and** `CHANGELOG.zh-CN.md` updated for user-visible changes
- [ ] Diagnostic changes also touch `AnalyzerReleases.Shipped.md` / `.Unshipped.md`
- [ ] Public API changes also touch `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt`
- [ ] Tests added or updated for the new behaviour

## Breaking change?

<!--
  "No" is the expected answer. If yes, explain the blast radius: which generated members disappear or
  change shape, what breaks at compile time versus at run time, and whether persisted data can still be
  read back. Changes to the canonical text form are the expensive ones.
-->

## Notes for the reviewer

<!-- Edge cases you deliberately left alone, alternatives you rejected, anything surprising in the diff. -->
