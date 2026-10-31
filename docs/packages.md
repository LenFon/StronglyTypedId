---
layout: default
title: Packages
---

<a id="packages"></a>

# Packages

One package to install. Four more exist, and you may want three of them.

| Package | Targets | Contains | Install when |
| --- | --- | --- | --- |
| `Len.StronglyTypedId` | `net8.0`, `net10.0` | The runtime library: `IStronglyTypedId<,>`, the reflection helpers, and the types the generated code builds on | Always, because this is the one you reference from code |
| `Len.StronglyTypedId.Generators` | `netstandard2.0` | The source generator, the analyzer and the code fixes | Never directly: the main package depends on it |
| `Len.StronglyTypedId.Testing` | `net8.0`, `net10.0` | `Any.Id<T>()`, `Any.Ids`, `Any.Register`, `Any.Reset`, `AnyIdGenerator` and `AnyIdOptions` | Test projects that need plausible ids; see [Testing](testing.md#testing) |
| `Len.StronglyTypedId.Testing.AutoFixture` | `net8.0`, `net10.0` | `WithStronglyTypedIds()`, which teaches AutoFixture to fill ids | Test projects already using AutoFixture |
| `Len.StronglyTypedId.Testing.Bogus` | `net8.0`, `net10.0` | `WithStronglyTypedIds()`, which teaches Bogus to fill ids | Test projects already using Bogus |

```shell
dotnet add package Len.StronglyTypedId
```

<a id="why-two-packages"></a>

## Why the generator is a separate package

An analyzer has to load inside the compiler, so it targets `netstandard2.0` and is compiled against
`Microsoft.CodeAnalysis`. The runtime library targets `net8.0`/`net10.0` and must not drag Roslyn, or the
`System.Collections.Immutable` and `System.Reflection.Metadata` versions it brings, into your application's
dependency graph. Those two requirements cannot live in one assembly, so they live in two.

The main package declares the dependency, which is why one `dotnet add package` is enough.

> **Building this repository** needs .NET SDK 10.0.401, pinned in `global.json`. That is a requirement of the
> source tree, not of the package: a project that consumes `Len.StronglyTypedId` only needs a target framework
> of `net8.0` or `net10.0`.

<a id="transitive-analyzer"></a>

## The analyzer arrives transitively

Because the dependency is transitive, **every project that references the main package also gets the
analyzer**, including projects that only consume `IStronglyTypedId<,>` and declare no ids of their own.
That is intentional: an id declared further downstream still needs the generator, and a consumer that
silently lacked it would fail with a puzzling "no `Create` method" error instead of a diagnostic.

A project that wants none of the diagnostics can silence the whole analyzer with one property in the
project file:

```xml
<PropertyGroup>
  <StronglyTypedIdEnableAnalyzers>false</StronglyTypedIdEnableAnalyzers>
</PropertyGroup>
```

The property travels from MSBuild to the analyzer through the analyzer config the SDK writes for the
compilation (`build_property.StronglyTypedIdEnableAnalyzers`); when it reads "no" the analyzer registers
**no diagnostic action at all**, so none of the 26 rules report, and the code fixes disappear with them
(there is nothing left to fix).

The three states:

| Property value | Analyzer |
| --- | --- |
| `false` (any casing), `0`, `no`, `off` | fully muted |
| `true` | reports as usual |
| not set | reports as usual (the default) |

**Only the diagnostics are muted, not code generation.** The generator and the analyzer are separate
components, so a muted project still gets complete, fully usable strongly typed ids.

### Checking that the switch took effect

A muted build looks exactly like a clean build, so "no diagnostics" proves nothing on its own. Verify with a
rule that is reported at `Warning` or higher: `STIA0011` (direct construction) is a warning, and the
advisory rules such as `STIA0015` are `Info`, which `dotnet build` does not print by default. A test that
only looks at `Info`-level output will report "nothing" in both states and prove nothing.

The reliable check is to make the build fail if anything is reported:

```shell
dotnet build -warnaserror
```

With the property set to `false` the build stays green even though the code violates a rule; remove the
property and the same build fails. If it fails in both cases, the property never reached the analyzer, most
often because the `buildTransitive` props did not flow to the project (see the note below), or because the
value was written somewhere the SDK does not read, such as a `Directory.Build.targets` that runs after the
analyzer config is generated.

To silence individual rules instead, `.editorconfig` still works:

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

> **Why the property works without any extra setup**: the SDK does not put arbitrary MSBuild properties
> into `build_property.*`, only the two dozen it defines itself (`TargetFramework`, `RootNamespace`, …).
> A custom property reaches that section only when it is explicitly allowed through
> `<CompilerVisibleProperty Include="…" />`. This library already does that for you, and the file that
> carries it flows down the dependency chain to the final consumer, so **setting the property is all a
> project that references the main package has to do**.

<a id="symbols"></a>

## Debug symbols ship separately

The runtime package publishes a `.snupkg` rather than embedding `.pdb` files in `lib/`. Symbols are fetched
from nuget.org's symbol server on demand, so your restore folder does not carry a PDB you never use.

<a id="versioning"></a>

## Versioning

All five packages are released together and share one version. The main package pins the generator package
to that same version, so the generator and the runtime can never drift apart.

[<- Back to the index](index.md) · [中文](zh-CN/packages.md)
