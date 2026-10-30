---
layout: default
title: Native AOT and trimming
---

# Native AOT and trimming

Strongly typed ids are ordinary generated types, so they survive Native AOT publishing and trimming
without a source generator running at publish time. The runtime library is marked
`<IsAotCompatible>true</IsAotCompatible>`, and nothing it emits — or that the generator emits — relies
on dynamic code generation or on loading a type by name.

## What the library guarantees

- **No `Reflection.Emit`, no `Activator.CreateInstance` by name, no `Type.GetType(string)`.** Every
  generated member is a plain static or instance method over the wrapped primitive.
- **Validators and normalizers are called directly.** They are static methods on your id, resolved at
  compile time, so they work under AOT exactly as they do on the JIT.
- **Serializers are generated, not reflected.** The System.Text.Json, Newtonsoft.Json, MessagePack and
  MongoDB converters are emitted as nested types; the library never reflects over your id to build one.
- **Trim-safe reflection helpers.** `Type.GetPrimitiveIdType()`, `Type.TryGetPrimitiveIdType(out Type?)`
  and `Type.IsStronglyTypedId()` are the only reflection entry points, and each parameter carries
  `[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)]` so the interface metadata
  that identifies a strongly typed id is preserved when the app is trimmed.

## System.Text.Json under AOT

`System.Text.Json` disables its reflection-based serializer under Native AOT, so an AOT app must supply
a source-generated `JsonSerializerContext`. **This is a `System.Text.Json` requirement, not something
specific to this library** — the generated converter plugs into that context like any other converter:

```csharp
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(OrderId))]
internal sealed partial class AppJsonContext : JsonSerializerContext;

var options = new JsonSerializerOptions { TypeInfoResolver = AppJsonContext.Default };
```

Register every type you serialize **as a root** — a context only serves types reachable from a
`[JsonSerializable]` root, so `JsonSerializer.Serialize(orderId, options)` needs
`[JsonSerializable(typeof(OrderId))]` like any other root type.

**The primitive an id wraps does not need an entry.** The generated converter reads and writes the
primitive directly through `Utf8JsonWriter` / `Utf8JsonReader` (`Guid` goes through
`WriteStringValue(Guid)`, numbers through `WriteNumberValue`) instead of going through
`JsonSerializer`, so no `JsonTypeInfo` for the primitive is required. That matters: a source-generated
context only serves registered types, and a missing entry surfaces as the framework's own
`NotSupportedException: JsonTypeInfo metadata for type 'System.Guid' was not provided` — from which
it is hard to work out that a *primitive* type went missing.

A few primitives have no direct form (`Int128`, `UInt128`, `Half`, `TimeSpan`, `DateOnly`, `TimeOnly`,
and reading a single `char`); those still go through `JsonSerializer`, so the context needs an entry
for them. If it is missing, the library turns the framework's `NotSupportedException` into a
`JsonException` that names the `[JsonSerializable]` to add.

### The two `GenerationMode` values differ

`JsonSourceGenerationMode.Metadata` emits metadata only, `Default` also emits the serialization logic.
The difference that matters here is **whether the `[JsonConverter]` attribute on the type is picked up**:

- In `Default` mode the source generator expands the type's `[JsonConverter]` into a converter.
- In `Metadata` mode it takes the "serialize by properties" path, where the attribute is not picked up —
  the id is then written as a plain object (`{"Value":"…"}`) rather than a string.

`Metadata` is the recommended configuration for AOT (smaller artifacts), so in that mode the converter
has to be registered into `options.Converters` through
`options.AddStronglyTypedIds()` — a path that does not depend on what the source
generator decides. Under `Default` the attribute is already enough and `AddStronglyTypedIds` is optional.

### Making the context write the same shape

`AddStronglyTypedIds` fixes the shape for converters the serializer resolves through `options.Converters`, but a
source-generated context does not consult that list for a type it was told to generate metadata for: it
emits its own `JsonTypeInfo` for the id and writes it by properties. The result is `{"Value":"…"}` from
the context and `"…"` from reflection — the same id, two shapes, no error.

`WithStronglyTypedIds()` closes that gap. It wraps your resolver so the id's `JsonTypeInfo` — built
around the generated converter, without reflection — is consulted first:

```csharp
var options = new JsonSerializerOptions
{
    TypeInfoResolver = AppJsonContext.Default.WithStronglyTypedIds(),
};
```

The order matters and the method handles it: `JsonTypeInfoResolver.Combine` returns the first resolver
that produces a non-null `JsonTypeInfo`, and the context does produce one for the id. Putting the id
resolver first is what makes the converter win; every other type still falls through to your context.

`AddStronglyTypedIds` and `WithStronglyTypedIds()` are independent — use the one that matches how you configure
serialization. If you set `TypeInfoResolver` to a context, use `WithStronglyTypedIds()`; if you rely on
`options.Converters`, use `AddStronglyTypedIds`. Setting both is harmless.

Both target frameworks are covered: the `System.Text.Json` source generator ships in the net8.0 reference
pack as well as the net10.0 one, so the same gap and the same fix apply on either.

## MessagePack and MongoDB.Bson under AOT

The generated integration artifacts are reflection-free themselves, and the registration entry points
also cover the **primitive** the ids wrap:

- **MessagePack** — `MessagePackSerializerOptions.AddStronglyTypedIds()` adds a
  `GenericEnumFormatter<TEnum>` for each distinct enum primitive. MessagePack's AOT-safe resolver chain
  (`StandardResolver` when dynamic code is unavailable) contains no enum resolver at all — the
  IL-emitting `DynamicEnumResolver` is excluded — so without this entry an enum-backed id cannot be
  serialized. Non-enum primitives (`int`, `Guid`, …) are served by the built-in resolver, which is
  reflection-free. The formatter writes the same underlying numeric payload as the dynamic resolver, so
  the wire format is unchanged.
- **MongoDB.Bson** — `StronglyTypedIds.RegisterBsonSerializers()` pre-registers default-configured
  serializers for every binary-path primitive (`int`, `char`, `decimal`, `string`, enums, …). BSON's
  `PrimitiveSerializationProvider` would otherwise create `Int32Serializer`, `EnumSerializer<T>` and
  friends via `Activator`, which throws `MissingMethodException` under AOT once the constructors are
  trimmed. The registered instances match the provider-created defaults, so behavior is unchanged.
  It also registers BSON's dynamic types — `object`, `List<object>` and `ExpandoObject` — because every
  root serialization context takes `BsonDefaults.DynamicArraySerializer` and
  `BsonDefaults.DynamicDocumentSerializer`, and those look the types up in the registry.
  **`Guid` is the exception**: since driver 3.0 there is no global default representation, so an app
  must register its own `GuidSerializer` with the representation its data uses.

## What is covered

The library's own AOT smoke test publishes a real `PublishAot` executable and runs it in CI, so the claim
is tested rather than asserted. On a published AOT binary it exercises:

| Area | Covered |
| --- | --- |
| Factories | `Create`, `TryCreate`, and a validator rejecting a value |
| Text | `ToString`, `Parse`, `TryParse` over `string`, `ReadOnlySpan<char>` and `ReadOnlySpan<byte>` |
| Formatting | `TryFormat` to `Span<char>` and `Span<byte>`, plus interpolated `ToString()` |
| Prefix | prefixed `ToString`, round-trip, and the lenient bare-value read |
| JSON | converter `Write` / `Read` round-trip, and `WriteAsPropertyName` / `ReadAsPropertyName` |
| JSON via context | `JsonSerializer` through a source-generated `JsonSerializerContext` + `WithStronglyTypedIds()`, including the prefixed and validator-rejection paths |
| MessagePack | value round-trips over `int` and `enum` primitives, plus rejection of an undefined enum value |
| MongoDB.Bson | value round-trips over `int` and `enum` primitives, plus rejection of an undefined enum value |

You do not need to reproduce any of this in your own app; it is listed so you know which paths have been
proven to work under AOT. The smoke project is a CI concern — see
[CONTRIBUTING](https://github.com/LenFon/StronglyTypedId/blob/main/.github/CONTRIBUTING.md) if you are working on
the library itself.

## Checklist for an AOT app

1. Publish with `<PublishAot>true</PublishAot>` (or `<PublishTrimmed>true</PublishTrimmed>`).
2. Provide a source-generated `JsonSerializerContext` and set it as `TypeInfoResolver` if you serialize
   ids — the same context you already need for every other type. Wrap it with
   `.WithStronglyTypedIds()` so ids keep the string shape instead of being written as objects.
3. Leave the trim analyzer on. If it flags a call site that passes a `Type` into
   `GetPrimitiveIdType` / `IsStronglyTypedId`, annotate that parameter with
   `[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)]` rather than suppressing the
   warning — the annotation is what keeps the metadata alive.
4. Nothing else. There is no AOT-specific configuration switch in the generator.

[<- Back to the index](index.md) · [中文](zh-CN/aot.md)
