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
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(Guid))]
[JsonSerializable(typeof(string))]
internal sealed partial class AppJsonContext : JsonSerializerContext;

var options = new JsonSerializerOptions { TypeInfoResolver = AppJsonContext.Default };
```

Register the primitive types the ids wrap, plus every type you serialize **as a root** — a context only
serves types reachable from a `[JsonSerializable]` root, so `JsonSerializer.Serialize(orderId, options)`
needs `[JsonSerializable(typeof(OrderId))]` like any other root type. When an id appears as a member of a
registered type it needs no entry of its own: the generated `[JsonConverter]` is picked up from the
attribute.

## MessagePack and MongoDB.Bson under AOT

The generated integration artifacts are reflection-free themselves, and the registration entry points
also cover the **primitive** the ids wrap:

- **MessagePack** — `StronglyTypedIds.ApplyTo(MessagePackSerializerOptions)` adds a
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

## How this is verified

`test/Len.StronglyTypedId.Aot.SmokeTest` is a real `PublishAot` executable, published and run by the
`aot` job in CI — the claim is tested, not asserted. It exercises, on a published AOT binary:

| Area | Covered |
| --- | --- |
| Factories | `Create`, `TryCreate`, and a validator rejecting a value |
| Text | `ToString`, `Parse`, `TryParse` over `string`, `ReadOnlySpan<char>` and `ReadOnlySpan<byte>` |
| Formatting | `TryFormat` to `Span<char>` and `Span<byte>`, plus interpolated `ToString()` |
| Prefix | prefixed `ToString`, round-trip, and the lenient bare-value read |
| JSON | converter `Write` / `Read` round-trip, and `WriteAsPropertyName` / `ReadAsPropertyName` |
| MessagePack | value round-trips over `int` and `enum` primitives, plus rejection of an undefined enum value |
| MongoDB.Bson | value round-trips over `int` and `enum` primitives, plus rejection of an undefined enum value |

The project sets `TrimmerSingleWarn` to `false` so trim and AOT warnings are listed individually instead
of collapsing into a single `IL2104`, and it must compile with **zero** warnings: analyzer-clean plus
runtime-clean is the bar for "works under AOT".

## Checklist for an AOT app

1. Publish with `<PublishAot>true</PublishAot>` (or `<PublishTrimmed>true</PublishTrimmed>`).
2. Provide a source-generated `JsonSerializerContext` and set it as `TypeInfoResolver` if you serialize
   ids — the same context you already need for every other type.
3. Leave the trim analyzer on. If it flags a call site that passes a `Type` into
   `GetPrimitiveIdType` / `IsStronglyTypedId`, annotate that parameter with
   `[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)]` rather than suppressing the
   warning — the annotation is what keeps the metadata alive.
4. Nothing else. There is no AOT-specific configuration switch in the generator.

[<- Back to the index](index.md) · [中文](zh-CN/aot.md)
