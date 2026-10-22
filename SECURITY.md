# Security Policy

## Supported versions

| Version | Supported |
| --- | --- |
| 1.3.x (current pre-release line) | ✅ |
| 1.2.x and earlier | ❌ |

Only the latest published version receives fixes. Because a strongly typed id ends up in persisted data,
upgrading is expected to be cheap — the generated text form is stable across patch versions, and any
change to it is treated as a breaking change and called out in the changelog.

## Reporting a vulnerability

**Please do not open a public issue for a security problem.**

Use GitHub's private reporting: go to the [Security tab](https://github.com/lenfon/StronglyTypedId/security)
and choose **Report a vulnerability**. That opens a private advisory visible only to the maintainer.

If private reporting is unavailable to you, contact the maintainer ([@LenFon](https://github.com/LenFon))
through GitHub and ask for an alternative channel — do not include exploit details in a public thread.

Please include:

- The affected version and target framework.
- A minimal reproduction: the `record` declaration, the attribute arguments, and the call that misbehaves.
- What you expected versus what happened.
- Your assessment of impact, if you have one.

## What to expect

This is a single-maintainer project, so timelines are best effort rather than contractual:

| Step | Target |
| --- | --- |
| Acknowledgement | within 7 days |
| Initial assessment (in scope / out of scope, severity) | within 14 days |
| Fix for a confirmed high-severity issue | as soon as practical, released as a patch |

You will be credited in the advisory and the changelog unless you ask otherwise.

## Threat model

Understanding what this library does — and does not do — makes triage faster.

**In scope**

- **Generated-code correctness with security consequences.** A prefix that makes two distinct values
  collapse into one id, a parser that accepts text it should reject, a validator that can be bypassed
  through a path the generated code forgot to guard. These are data-integrity bugs and are treated as
  security issues when they can be triggered by untrusted input.
- **Information disclosure through generated messages.** Validation failures must not echo the rejected
  value unless the consumer opted in (`RedactValidationMessage`).
- **Supply chain.** The build and release workflows pin every action to a commit SHA and the SDK to an
  exact version. A report showing a mutable reference, an unpinned tool, or a path where a tag name
  reaches a shell is in scope.
- **Analyzer / code-fix damage.** A code fix that silently changes the meaning of user code (for example
  turning a `struct` into a reference type, or deleting an unrelated attribute) is in scope.

**Out of scope**

- Vulnerabilities in *your* validator or normalizer. The library calls what you point it at; it cannot
  make a weak predicate strong.
- Denial of service through pathological input sizes (a 100 MB string handed to `Parse`). Bound your
  inputs at the transport layer.
- Missing hardening that requires an attacker who can already modify your source or build.
- Reports that only reproduce against a version older than the supported line.

## Verifying what you install

- Packages are **not** NuGet-signed. Provenance is established through the tagged commit, SourceLink
  (symbols resolve back to the exact source on GitHub) and the deterministic build used in CI.
- Every release is produced by the `publish-nuget.yml` workflow from a tag, with a manual approval gate.
  The version in the package matches the tag; `--skip-duplicate` means a re-run cannot overwrite an
  existing version.
- If you need stronger guarantees, build from the tagged commit yourself:
  `dotnet pack src/Len.StronglyTypedId/Len.StronglyTypedId.csproj -c Release`.

## Dependency vulnerabilities

`NuGetAuditMode` is `all` for the four shipped projects, so a vulnerable transitive dependency fails
restore rather than sitting unnoticed. If you believe a dependency advisory applies but restore stays
green, open a regular issue — that is a build-configuration bug, not a vulnerability report.
