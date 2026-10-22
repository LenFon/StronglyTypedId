---
layout: default
title: Packages
---

<a id="packages"></a>

# Packages

One package to install. Three more exist, and you may want two of them.

| Package | Targets | Contains | Install when |
| --- | --- | --- | --- |
| `Len.StronglyTypedId` | `net8.0`, `net10.0` | The runtime library: `IStronglyTypedId<,>`, the reflection helpers, and the types the generated code builds on | Always — this is the one you reference from code |
| `Len.StronglyTypedId.Generators` | `netstandard2.0` | The source generator, the analyzer and the code fixes | Never directly: the main package depends on it |
| `Len.StronglyTypedId.Testing` | `net8.0`, `net10.0` | `Any.Id<T>()`, `Any.Ids`, `Any.Register`, `Any.Reset`, `AnyIdGenerator` and `AnyIdOptions` | Test projects that need plausible ids — see [Testing](testing.md#testing) |
| `Len.StronglyTypedId.Testing.AutoFixture` | `net8.0`, `net10.0` | `WithStronglyTypedIds()`, which teaches AutoFixture to fill ids | Test projects already using AutoFixture |

```shell
dotnet add package Len.StronglyTypedId
```

<a id="why-two-packages"></a>

## Why the generator is a separate package

An analyzer has to load inside the compiler, so it targets `netstandard2.0` and is compiled against
**Roslyn 5.9** (`Microsoft.CodeAnalysis` 5.9.0). The hosting compiler must be at least that new: on an
older SDK the build reports `CS9057` and the generator is skipped entirely, which then surfaces as a
missing `Create` method rather than as a clear version error. The floor is therefore whichever SDK ships
Roslyn 5.9 — verified on .NET SDK **10.0.401**; SDK 10.0.201 (Roslyn 5.3) and 9.0.308 (Roslyn 4.14)
both fail.
The runtime library targets `net8.0`/`net10.0` and must not drag Roslyn — or the `System.Collections.Immutable`
and `System.Reflection.Metadata` versions it brings — into your application's dependency graph. Those two
requirements cannot live in one assembly, so they live in two.

The main package declares the dependency, which is why one `dotnet add package` is enough.

<a id="transitive-analyzer"></a>

## The analyzer arrives transitively

Because the dependency is transitive, **every project that references the main package also gets the
analyzer** — including projects that only consume `IStronglyTypedId<,>` and declare no ids of their own.
That is deliberate: an id declared further downstream still needs the generator, and a consumer that
silently lacked it would fail with a puzzling "no `Create` method" error instead of a diagnostic.

A project that wants none of the diagnostics can silence them per project, in `.editorconfig`:

```ini
[*.cs]
dotnet_diagnostic.STIA0011.severity = none
```

or in the project file, which is blunter but survives a missing `.editorconfig`:

```xml
<PropertyGroup>
  <NoWarn>$(NoWarn);STIA0011;STIA0014</NoWarn>
</PropertyGroup>
```

<a id="symbols"></a>

## Debug symbols ship separately

The runtime package publishes a `.snupkg` rather than embedding `.pdb` files in `lib/`. Symbols are fetched
from nuget.org's symbol server on demand, so no consumer's restore folder carries a PDB they never use.

<a id="versioning"></a>

## Versioning

All four packages take their version from `version.props` and are released together. The main package pins
the generator package to that same version, so the generator and the runtime can never drift apart.

[<- Back to the index](index.md) · [中文](zh-CN/packages.md)
