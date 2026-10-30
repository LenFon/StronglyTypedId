---
layout: default
title: StronglyTypedId
---

# StronglyTypedId

**English** · [中文](zh-CN/index.md)

A source generator, analyzer and code fix provider that turns a one-line `record` declaration into a
fully featured strongly typed id. It generates the `IStronglyTypedId<TSelf, TPrimitiveId>` implementation,
`IParsable<TSelf>` support, and the JSON / Entity Framework Core / Swagger integrations for you — so a
`Guid` cannot be passed where an `OrderId` is expected, and vice versa.

```csharp
[StronglyTypedId]
public partial record struct OrderId(Guid Value);
```

## Guides

| Page | What it covers |
| --- | --- |
| [Features](features.md) | The full inventory: every generated member, every configuration option, every integration. |
| [Feature index](features-index.md) | Every feature with its runnable sample and the page that explains it; the table is generated from source and verified on every build. |
| [Packages](packages.md) | The five NuGet packages, why the generator is separate, and how the analyzer reaches every project. |
| [Declaring an id](declaration.md) | Validation, normalization, named instances, exception types, assembly-level defaults, turning integrations off, nested types, `TryCreate` / UTF-8 interfaces and `TypeConverter`. |
| [Generated code](generated-code.md) | What each generated member does: the interfaces, `Parse` and culture, `[Flags]` enum tightening, `ToString()` and the integration entry point. |
| [Serialization](serialization.md) | System.Text.Json, Newtonsoft.Json, dictionary keys, JSON shape, the text prefix, explicit registration, and the primitives the serializers do not know. |
| [Integrations](integrations.md) | Entity Framework Core, Swashbuckle, Dapper, MessagePack, MongoDB, HotChocolate (GraphQL), protobuf-net, ASP.NET Core MVC, Minimal API and ASP.NET Core OpenAPI — plus how to wire up AutoMapper by hand. |
| [Runtime](runtime.md) | Constraining generics with `IStronglyTypedId<,>`, and the reflection helpers. |
| [Native AOT and trimming](aot.md) | What survives trimming, the three reflection entry points, and the `JsonSerializerContext` an AOT app must supply. |
| [Testing](testing.md) | `Any.Id<T>()`, the AutoFixture and Bogus integrations, `StronglyTypedIdConformance`, and the sample projects. |
| [Diagnostics](diagnostics.md) | Every rule, the severity bands, whether it ships a code fix, and how to configure the naming suffix. |
| [Performance](performance.md) | What the generator costs at build time, and what the generated code costs at run time. |
| [Troubleshooting](troubleshooting.md) | Symptom-first: nothing generated, a missing integration, stale analyzers, parsing surprises. |
| [Limitations](limitations.md) | What a source generator cannot do — `Value` visibility, the closed primitive list, the unguarded constructor — and the workaround for each. |
| [FAQ](faq.md) | The questions that come up most often. |
| [Future plans](roadmap.md) | None at the moment — every planned direction has shipped. |

## Elsewhere

- [README](https://github.com/LenFon/StronglyTypedId#readme) — features, getting started,
  the supported primitive types and what gets generated.
- [PACKAGE.md](https://github.com/LenFon/StronglyTypedId/blob/main/PACKAGE.md) — the short version that
  ships with the NuGet package.
- [CHANGELOG](https://github.com/LenFon/StronglyTypedId/blob/main/CHANGELOG.md) — what changed and when.
- [CONTRIBUTING](https://github.com/LenFon/StronglyTypedId/blob/main/.github/CONTRIBUTING.md) — build, test and
  the gates that run in CI.
- [Security policy](https://github.com/LenFon/StronglyTypedId/blob/main/.github/SECURITY.md) — supported
  versions, private reporting, and how to verify what you install.
- [NuGet package](https://www.nuget.org/packages/Len.StronglyTypedId) — install instructions.
