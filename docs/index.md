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
| [Packages](packages.md) | The four NuGet packages, why the generator is separate, and how the analyzer reaches every project. |
| [Declaring an id](declaration.md) | Validation, normalization, named instances, exception types, assembly-level defaults, turning integrations off, nested types, `TryCreate` / UTF-8 interfaces and `TypeConverter`. |
| [Generated code](generated-code.md) | What each generated member does: the interfaces, `Parse` and culture, `[Flags]` enum tightening, `ToString()` and the integration entry point. |
| [Serialization](serialization.md) | System.Text.Json, Newtonsoft.Json, dictionary keys, JSON shape, the text prefix, explicit registration, and the primitives the serializers do not know. |
| [Integrations](integrations.md) | Entity Framework Core, Swashbuckle, Dapper, MessagePack, MongoDB, ASP.NET Core MVC, Minimal API and ASP.NET Core OpenAPI. |
| [Runtime](runtime.md) | Constraining generics with `IStronglyTypedId<,>`, and the reflection helpers. |
| [Native AOT and trimming](aot.md) | What survives trimming, the one reflection entry point, and the `JsonSerializerContext` an AOT app must supply. |
| [Testing](testing.md) | `Any.Id<T>()`, the AutoFixture integration, and the sample projects. |
| [Diagnostics](diagnostics.md) | Every rule, the severity bands, and whether it ships a code fix. |
| [Performance](performance.md) | What the generator costs at build time, what the generated code costs at run time, and the allocation-based regression gate. |
| [Troubleshooting](troubleshooting.md) | Symptom-first: nothing generated, a missing integration, stale analyzers, parsing surprises. |
| [Limitations](limitations.md) | What a source generator cannot do — `Value` visibility, the closed primitive list, the unguarded constructor — and the workaround for each. |
| [FAQ](faq.md) | The questions that come up most often. |

## Elsewhere

- [README](https://github.com/LenFon/StronglyTypedId#readme) — features, requirements, getting started,
  the supported primitive types and what gets generated.
- [PACKAGE.md](https://github.com/LenFon/StronglyTypedId/blob/main/PACKAGE.md) — the short version that
  ships with the NuGet package.
- [CHANGELOG](https://github.com/LenFon/StronglyTypedId/blob/main/CHANGELOG.md) — what changed and when.
- [CONTRIBUTING](https://github.com/LenFon/StronglyTypedId/blob/main/CONTRIBUTING.md) — build, test and
  the gates that run in CI.
- [Security policy](https://github.com/LenFon/StronglyTypedId/blob/main/SECURITY.md) — supported
  versions, private reporting, and how to verify what you install.
- [NuGet package](https://www.nuget.org/packages/Len.StronglyTypedId) — install instructions.
