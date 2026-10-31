---
layout: default
title: Troubleshooting
---

<a id="troubleshooting"></a>

# Troubleshooting

Symptom first. For what a specific diagnostic means, see [Diagnostics](diagnostics.md#diagnostics); for
conceptual questions, see [FAQ](faq.md#faq).

<a id="nothing-was-generated"></a>

## Nothing was generated

The type compiles but has no `Create`, no `Parse`, no converter.

1. Is the declaration `partial`? A generator can only add to a partial type, and `STIA0001` reports it.
2. Is it a non-abstract, non-generic `record` or `record struct`, inside a namespace, with exactly one
   positional parameter named `Value` whose type is a supported primitive? Each violation has its own
   rule: `STIA0000` and `STIA0002` to `STIA0008`.
3. Does the project reference `Len.StronglyTypedId`? The analyzer travels with it, so a project that only
   consumes `IStronglyTypedId<,>` still gets the diagnostics, but a project that declares ids needs the
   package.

## Everything disappeared and only CS8785 remains

`CS8785` means the generator threw, and every file it produced for that compilation was discarded. The
generator reports the same failure as `STIA0012` when it can. That is a bug in the generator, not in your
declaration: open an issue with the compiler output and a minimal reproduction.

## An integration is missing

Each integration is generated only when its trigger is present. `Integrations` on the id, or
`[assembly: StronglyTypedIdDefaults(...)]`, can also switch it off, so check those first if the package is
clearly referenced.

| Integration | Generated when |
| --- | --- |
| System.Text.Json | `System.Text.Json` is referenced |
| Newtonsoft.Json | `Newtonsoft.Json` ≥ 13.0.0 is referenced |
| EF Core | `Microsoft.EntityFrameworkCore` ≥ 7.0.0 is referenced |
| Swashbuckle | `Swashbuckle.AspNetCore.SwaggerGen` ≥ 6.0.0 is referenced |
| Dapper | `Dapper` ≥ 2.0.0 is referenced |
| MessagePack | `MessagePack` ≥ 2.0.0 is referenced |
| MongoDB | `MongoDB.Bson` ≥ 3.0.0 is referenced |
| HotChocolate (GraphQL) | `HotChocolate.Types` ≥ 13.0.0 is referenced |
| protobuf-net | `protobuf-net` ≥ 3.0.0 is referenced, with no entry point to call: the surrogate is discovered from `[ProtoContract]` |
| ASP.NET Core OpenAPI | `Microsoft.AspNetCore.OpenApi` ≥ 9.0.0 is referenced; 8.0.x has no schema transformer API, so on `net8.0` use Swashbuckle instead |
| ASP.NET Core MVC | `Microsoft.AspNetCore.Mvc` ≥ 2.0.0 is referenced (or its `.Core` / `.Abstractions` facade) |

Two further reasons an integration skips an id: the id is declared inside a **generic** container (the
external-reference integrations cannot reopen it; see [Nested types](declaration.md#nested-types)), or
the assembly-level default turned it off.

## `AddStronglyTypedIds` is not found

The entry points live in `internal static partial class StronglyTypedIds` in the `Len.StronglyTypedId`
namespace, generated alongside the integration that owns them. So:

1. `using Len.StronglyTypedId;` must be in scope.
2. The integration must actually be generated; see the table above. `AddStronglyTypedIds(JsonSerializerOptions)`
   needs System.Text.Json; `AddStronglyTypedIds(JsonSerializerSettings)` needs Newtonsoft.Json ≥ 13.0.0;
   `AddStronglyTypedIds(ModelConfigurationBuilder)` / `AddStronglyTypedIds(ModelBuilder)` need EF Core ≥ 7.0.0;
   `AddStronglyTypedIds(MessagePackSerializerOptions)` needs MessagePack; `RegisterBsonSerializers()` needs
   MongoDB.Bson; `AddStronglyTypedIds(IRequestExecutorBuilder)` needs HotChocolate ≥ 13.0.0. protobuf-net
   has no entry point, so check that the id carries `[ProtoContract(Surrogate = …)]` instead.

## The analyzer reports nothing, or reports stale results

Usually a Visual Studio cache. **Clean the solution and rebuild.** The compiled assembly always contains
the current generated code, so it is safe to ignore in the meantime. If it persists, check that the
diagnostics were not silenced in `.editorconfig`.

## Parsing fails on text that looks right

| Cause | Check |
| --- | --- |
| Culture | A `null` provider means the **invariant** culture, not the current one. `"1,5"` under de-DE is not `1.5`; pass `CultureInfo.CurrentCulture` explicitly if that is what you want |
| Prefix | Reads are lenient by default, so a bare value parses. A *strict* read rejects text without the prefix; make sure you know which mode the id was declared with |
| Empty strings | For `string`-backed ids, `TryParse` rejects `null` and `""` |
| Enum | A plain enum must name a defined member; a `[Flags]` enum may not set a bit no member occupies. `"999"` fails even though `Enum.TryParse` would accept it |

## A value read back from storage no longer matches

Changing `Prefix`, `JsonShape`, the primitive type, or a `Normalizer` changes the **stored form** (the
text form for `Prefix` / `Normalizer`, the JSON shape for `JsonShape`), and that is a data contract, so
rows written before the change will not read back. Treat those as breaking changes and migrate the stored
data; see [Serialization](serialization.md#serialization).

## Configuration binding leaves an id at `default`

`Microsoft.Extensions.Configuration` converts a string to a property's type through `TypeDescriptor`, so an
id needs `TypeConverter = true` to be bindable. Without it the binder does not fail; it leaves the property
at `default`, which for a `Guid`-backed id is `00000000-0000-0000-0000-000000000000` even though the
configuration holds a real value. The scalar overloads (`GetValue<OrderId>(...)`) throw a
`NullReferenceException` from inside the binder instead. Add the converter:

```csharp
[StronglyTypedId(TypeConverter = true)]
public partial record struct OrderId(Guid Value);
```

See [TypeConverter](declaration.md#type-converter).

## JSON throws under Native AOT

`System.Text.Json` disables reflection-based serialization under AOT, so the app must supply a
source-generated `JsonSerializerContext`. That is a `System.Text.Json` requirement, not something specific
to this library. See [Native AOT and trimming](aot.md).

## MongoDB throws while serializing a `Guid`-backed id

Driver 3.0 removed the global default `Guid` representation, so serializing a `Guid` without one registered
throws, and a `Guid`-backed id lands on exactly that path, because its serializer delegates to the `Guid`
serializer. Register one before the first document is written:

```csharp
BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
```

Pick the representation your existing documents already use, because changing it afterwards makes them
unreadable. See [MongoDB](integrations.md#mongodb).

## The Swagger document shows an id as a plain string

If the id sits in a **Minimal API** route or query parameter and the document still shows a bare
`type: string`, the operation filter did not run. It is registered by
`options.AddStronglyTypedIds()`, so that call has to be there:

```csharp
services.AddSwaggerGen(options =>
{
    options.AddStronglyTypedIds();
});
```

`MapType` alone cannot cover those parameters: it is keyed on the CLR type, while `ApiExplorer` reports an
`IParsable` parameter as `string`. The generated operation filter looks the parameter up by its declared
type instead. Details in [Integrations](integrations.md#swashbuckle).

## An id inside a generic container has no JSON converter

Expected: an attribute argument may not reference a converter carrying the container's type parameter
(CS0416), so `[JsonConverter]` cannot be emitted. Register the converter for the closed type yourself:

```csharp
options.Converters.Add(new Outer<int>.InnerId.SystemTextJsonConverter());
```

[<- Back to the index](index.md) · [中文](zh-CN/troubleshooting.md)
