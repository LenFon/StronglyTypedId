---
layout: default
title: Serialization
---

<a id="serialization"></a>

# Serialization

## System.Text.Json

The converter is applied via a generated `[JsonConverter]` attribute — no extra configuration required.

```csharp
var json = JsonSerializer.Serialize(new OrderId(Guid.NewGuid())); // "..."
var id   = JsonSerializer.Deserialize<OrderId>(json);
```

## Newtonsoft.Json (≥ 13.0.0)

Same zero-config behavior through a generated `[JsonConverter]` attribute.

```csharp
var json = JsonConvert.SerializeObject(new OrderId(Guid.NewGuid()));
var id   = JsonConvert.DeserializeObject<OrderId>(json);
```

## Primitives the serializers do not know

Six primitives have no native JSON representation in one of the two libraries, so their converters write
the invariant-culture text instead of the raw value — the same text `ToString()` produces:

| Primitive                                     | Library          | Raw value would…                                              |
| --------------------------------------------- | ---------------- | ------------------------------------------------------------- |
| `BigInteger`                                  | System.Text.Json | serialize as `{"IsPowerOfTwo":false,…,"Sign":1}` and read back as `default` |
| `Int128`, `UInt128`, `Half`, `DateOnly`, `TimeOnly` | Newtonsoft.Json | throw `JsonWriterException: Unsupported type`                  |

```csharp
JsonSerializer.Serialize(BigIntegerId.Create(42));       // "42"
JsonConvert.SerializeObject(HalfId.Create((Half)1.5));   // "1.5"
```

Both directions go through the same canonical text, so the round trip is lossless; the only consequence
is that these ids appear as JSON strings rather than numbers. Every other primitive keeps its natural
shape (numbers stay numbers, `Guid` stays a string, enums stay numbers).

## Dictionary keys

A `JsonConverter<T>` does not see dictionary keys by default — `System.Text.Json` only knows the built-in
key types and throws `NotSupportedException` for anything else — so the generated converter overrides
`ReadAsPropertyName` / `WriteAsPropertyName` as well:

```csharp
var cart = new Dictionary<OrderId, int> { [id] = 3 };

var json = JsonSerializer.Serialize(cart);                        // {"3f2c…":3}
var back = JsonSerializer.Deserialize<Dictionary<OrderId, int>>(json);
```

Keys are written as the invariant-culture text of the primitive value, so the same data always produces
the same key no matter the current culture. A `string`-backed id uses the string itself, so an empty
string round-trips; a key that cannot be parsed throws `JsonException`.

An `enum`-backed id follows `System.Text.Json`'s own convention for enum keys: the key is the member name
(`"Active"`), while the value is the underlying number (`1`) unless your `JsonSerializerOptions` say
otherwise. The asymmetry is inherent to JSON — object keys can only be strings — and it is exactly what a
plain `Dictionary<MyEnum, MyEnum>` produces, so ids and raw enums stay interchangeable in the same document.
Enum keys are also the one case where `options.DictionaryKeyPolicy` applies: the key is a member name
rather than data, so camel-casing it matches the built-in enum-key handling, and the read side parses
case-insensitively to accept the rewritten key. Every other primitive keeps its key text verbatim — that
text *is* the value, and rewriting it would make the document unreadable — and a prefixed id is excluded
too, because the policy would mangle the prefix. Writing a `null` id as a property name throws
`NotSupportedException`.

Newtonsoft.Json routes dictionary keys through `TypeConverter` / `ToString()` rather than through
`JsonConverter`, so its keys never reach the generated converter. Enable `TypeConverter = true` on the id so
Newtonsoft can restore keys on read. The key text is the same invariant-culture text the converter writes:
the generated `ToString()` is invariant, and the generated `TypeConverter` reads and writes invariantly too, so
keys stay stable across cultures.

Both converters serialize the id as its underlying primitive value rather than as an object, so the JSON
payload keeps the same shape it had before the id was introduced. A `null` reference-type id is written
as JSON `null`, and reading that `null` back yields `null` — but only for a `record` id. A `record struct`
id cannot represent `null`, so both converters reject the token instead of silently producing `default`
(`JsonException` / `JsonSerializationException`).

An `enum`-backed id validates its value on read exactly like its text form does: a number that names no
member is rejected with the same framework exception, so `{"status": 999}` cannot produce an id whose
value is `(Status)999`. This is the check `Id.TryParse("999")` already applies — and that the Dapper
paths apply — closing the one entry point that used to accept it. `[Flags]` enums are checked against the
undefined-bits mask instead, so any combination of defined members passes while `999` does not.

## Validation failures

A `Validator` rejects values that parse but violate a domain rule. Both converters honour it on read, and
they report the rejection through their own exception type rather than letting the `ArgumentException`
from `Create` escape:

| Serializer | Exception on a rejected value |
| --- | --- |
| `System.Text.Json` | `JsonException` |
| `Newtonsoft.Json` | `JsonSerializationException` |

This is the exception both libraries document for a failed conversion, so a caller that wraps
deserialization in `catch (JsonException)` sees a malformed payload and a domain violation the same way,
and never has to expect an `ArgumentException` from a serializer.

`BigInteger`-backed ids read their payload as text, so a payload that is not a number is reported the same
way — as `JsonException`, not as the `FormatException` that `BigInteger.Parse` would raise.

EF Core is the exception: its `ValueConverter` calls `Create`, so a rejected value surfaces as
`ArgumentException`. Materializing an entity from a database row is a data-integrity boundary, where that
exception describes the problem more precisely than an ORM-specific wrapper would.

## JSON shape

By default an id is **shape-compatible with its primitive**: `OrderId` backed by `Guid` serializes to the
same JSON string a bare `Guid` would, so introducing the id does not change the wire format. When an
existing contract has already promised the object form — typically a client that sends
`{ "value": "3f2c…" }` — opt into it per id:

```csharp
[StronglyTypedId(JsonShape = JsonShape.Object)]
public partial record struct OrderId(Guid Value);

// JsonShape.Primitive (default): "3f2c…"
// JsonShape.Object:             {"value":"3f2c…"}
```

The setting applies to both `System.Text.Json` and `Newtonsoft.Json`, and to the id's own value only —
dictionary keys are always the primitive text, because a JSON object cannot be a key. `JsonShape.Object`
reads and writes a single `value` property; a payload that is not an object, or an object without
`value`, throws (`JsonException` / `JsonSerializationException`), and any other property is skipped.

The property name is matched case-insensitively on both sides, which is what each serializer already does
for an ordinary object: `{"value":…}`, `{"Value":…}` and `{"VALUE":…}` all read the same. Under
`System.Text.Json` that follows `options.PropertyNameCaseInsensitive` — turn it off and only the exact
lowercase `value` is accepted. Under `Newtonsoft.Json` it is unconditional, because Newtonsoft's own
POCO binding is case-insensitive and a converter that were stricter would reject payloads the rest of the
document accepts. Because the primitive form is the default, existing payloads are unaffected unless you ask
for the object form.

## Text prefix

Opt into a textual prefix per id. The prefix becomes part of the id's **canonical text form** —
`ToString()` writes it, `Parse` / `TryParse` strip it — so every text-based path picks it up
automatically:

```csharp
[StronglyTypedId(Prefix = "ord_")]
public partial record struct OrderId(Guid Value);

var id = OrderId.Create(guid);

id.ToString();                              // "ord_3f2c…"
id.ToString("N");                           // "ord_3f2c…"  (dash-less GUID, prefix kept)
OrderId.Parse("ord_3f2c…");                 // OrderId
OrderId.Parse("3f2c…");                     // OrderId  (lenient read, the default)
JsonSerializer.Serialize(id);                // "ord_3f2c…"
```

The prefix is part of every text form, not just `ToString()`: `ToString(format, provider)` and
`TryFormat(...)` prepend it too, so `$"{id:N}"` and structured-logging output stay recognisable as ids.

Because the prefix lives in the canonical text form, integrations that go through text need no extra
configuration: MVC model binding, route constraints and the generated `TypeConverter` all accept and emit
the prefixed form. Integrations that go through the primitive **value** — EF Core, Dapper, MessagePack and
MongoDB — are unaffected and keep storing the bare primitive, so existing columns, rows and documents stay
valid. The one exception is the three primitives MessagePack and BSON have no native representation for
(`BigInteger`, `Int128`, `UInt128`): those are stored as text, so their payload does carry the prefix.

Reading is **lenient by default**: `TryParse` accepts both the prefixed and the bare form, so existing
payloads and stored text keep round-tripping while new text is always written with the prefix. Switch to
strict per id when you want the prefix to be mandatory:

```csharp
[StronglyTypedId(Prefix = "ord_", PrefixReadMode = PrefixReadMode.Strict)]
public partial record struct OrderId(Guid Value);

OrderId.TryParse("ord_3f2c…", null, out _);  // true
OrderId.TryParse("3f2c…", null, out _);       // false
```

> **Breaking change when opted in.** For numeric, enum and date primitives the JSON representation changes
> from a JSON number to a JSON string, because the prefixed form is text. Database storage is unaffected.
> Leave `Prefix` unset and the generated code is byte-for-byte identical to before.

An empty prefix is treated as "no prefix" and reported by nothing — the generated code is identical to
leaving `Prefix` unset. A whitespace-only prefix is treated as "no prefix" too, but reported as `STIA0013`,
because it is almost certainly a typo.

## Explicit registration

The `[JsonConverter]` attribute covers the common case, but it is ignored when you supply your own
`JsonSerializerOptions` / `JsonSerializerSettings` and want this library's converters attached to them anyway.
Both serializers therefore expose a registration entry point, in
parallel to the `AddStronglyTypedIds` family generated for the other integrations — see
[Integrations](integrations.md) for those, including the binary formats (MessagePack, MongoDB BSON), which
are registered the same way:

```csharp
// System.Text.Json
var options = new JsonSerializerOptions();
options.AddStronglyTypedIds();               // adds every id's converter to options.Converters

// Newtonsoft.Json
var settings = new JsonSerializerSettings();
settings.AddStronglyTypedIds();              // adds every id's converter to settings.Converters
```

Each entry point is generated only when the corresponding serializer is referenced, and only when the
compilation declares at least one id.

Both are idempotent: a converter that is already in the list is left alone, so calling either one from
several modules — or twice from the same one — does not grow the list. Converters you added yourself stay
where they are. Under `System.Text.Json` this also means a second call after the options have been used is a
no-op rather than an `InvalidOperationException`; `JsonSerializerOptions` freezes on first use, and the
entry point never touches the collection when everything is already registered.

### Source-generated `JsonSerializerContext`

`AddStronglyTypedIds` registers converters in `options.Converters`, but a source-generated context does not consult
that list for a type it was asked to generate metadata for. In `JsonSourceGenerationMode.Metadata` — the
recommended mode for AOT — the context writes the id by its properties, so the same id serializes as
`{"Value":"…"}` through the context and `"…"` through reflection.

Wrap the context with `WithStronglyTypedIds()` to make the two agree:

```csharp
var options = new JsonSerializerOptions
{
    TypeInfoResolver = AppJsonContext.Default.WithStronglyTypedIds(),
};
```

The method puts an id-only `IJsonTypeInfoResolver` in front of your context. Order is what makes it work:
`JsonTypeInfoResolver.Combine` takes the first resolver that returns a non-null `JsonTypeInfo`, and the
context does return one for the id. Types other than ids fall through to the context unchanged. The
resolver builds each `JsonTypeInfo` from the generated converter, so it is reflection-free and works under
Native AOT.

`AddStronglyTypedIds` and `WithStronglyTypedIds()` are independent: use `AddStronglyTypedIds` when you configure
`options.Converters`, and `WithStronglyTypedIds()` when you set `TypeInfoResolver` to a context. Setting
both is harmless.

### When the id is declared in another assembly

The two serializers differ in where they put the converter:

- The `System.Text.Json` converter is nested in the id, but `System.Text.Json.dll` lives in the
  `Microsoft.NETCore.App` shared framework, so every net8.0+ project references it — **the assembly that
  declares the id always carries the converter**, and consumers never have to think about it.
- `Newtonsoft.Json` is a separate NuGet package. If the assembly that declares the id (typically a domain
  layer) does not reference it, that id has no Newtonsoft converter at all: a nested converter can only be
  generated in the assembly that declares the id, and a consumer adding one of its own would hit `CS0426`
  (the type already exists).

  The consumer (the host app that does reference Newtonsoft) generates a **top-level** converter for such
  ids and registers it through `AddStronglyTypedIds`. Newtonsoft's `[JsonConverter]` attribute, however,
  takes precedence over `JsonSerializerSettings.Converters`, and an attribute can only sit on the type
  itself — so **across assemblies you must call `AddStronglyTypedIds`**. Without it the id is written as an
  object (`{"Value":"…"}`) while `System.Text.Json` writes a string: the same data in two shapes, with no
  compiler warning and no exception at runtime.

  When both the domain layer and the host app reference Newtonsoft, the nested converter in the declaring
  assembly travels with the type and wins by attribute precedence; `AddStronglyTypedIds` is then optional.

[<- Back to the index](index.md) · [中文](zh-CN/serialization.md)
