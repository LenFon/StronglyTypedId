---
layout: default
title: Integrations
---

# Integrations

Every integration is generated only when its package is referenced, and each one adds its entry point to
the same generated `StronglyTypedIds` class — see
[The integration entry point](generated-code.md#the-integration-entry-point). Nothing at all is emitted for
an integration that is switched off, either per id or through the assembly-level default (see
[Turning integrations off](declaration.md#integrations-filter)), nor when the compilation declares no ids.

The two JSON serializers are integrations in the same sense, but they are covered on their own page —
see [Serialization](serialization.md).

<a id="entity-framework-core"></a>

## Entity Framework Core

Register the generated value converters for all strongly typed ids (≥ 7.0.0) in your `DbContext`:

```csharp
protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
{
    base.ConfigureConventions(configurationBuilder);

    // Registers a ValueConverter for every strongly typed id in the project.
    configurationBuilder.AddStronglyTypedIds();

    // Convention configuration still applies to an id after it has been registered.
    configurationBuilder.Properties<UserId>().HaveMaxLength(100);
}
```

A model-scanning overload is generated alongside it, in case you prefer to register converters from
`OnModelCreating` instead of conventions:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    // Walks every entity's properties and assigns the generated converter wherever the CLR type is a strongly typed id.
    modelBuilder.AddStronglyTypedIds();
}
```

> Both overloads are generated whenever EF Core ≥ 7.0.0 is referenced — the same gate as every other
> integration, with no requirement that a `DbContext` override `ConfigureConventions`. `ModelConfigurationBuilder`
> only exists from EF Core 7, which is why the version floor is higher here than elsewhere.
>
> `AddStronglyTypedIds(ModelBuilder)` scans the model as it stands at the moment of the call, so entity types
> configured after it will not be picked up: call it at the **end** of `OnModelCreating`. The conventions
> overload has no such ordering constraint and is the better default.

Each id gets a `ValueConverter<{Id}, {Primitive}>` nested in the generated `StronglyTypedIds` class, named
`{TypeName}Converter`. If two ids with the same short name exist in different namespaces, the converter
names would collide inside that single class, so only then are they disambiguated as
`{Namespace_With_Underscores}_{TypeName}Converter` (for example `Domain_OrderIdConverter`). The converters
are implementation details of `AddStronglyTypedIds`, generated as private nested types — you never reference them by
name, and no action is required from you in either case.

For a built-in primitive the converter's provider type is that primitive, so the column type and the schema
EF Core builds are the ones the primitive would have had on its own: introducing the id migrates nothing,
and existing columns keep working.

A **custom primitive** is the exception. The database provider does not know your CLR type, so the provider
type is fixed to `string` and the column stores the primitive's canonical text form — the same text the
JSON, binary and Dapper paths use. Without that, EF Core rejects the property with
`The '<Primitive>' property could not be mapped because the database provider does not support this type`.
Built-in primitives are unaffected and their generated output is byte-for-byte unchanged.

Reading a row back goes through `Create`, so the id's validators and normalizers run on every
materialisation — a stored value the validator rejects throws instead of producing an id that could never
have been constructed in code.

> `AddStronglyTypedIds(ModelBuilder)` walks the model as it stands at the moment of the call, so invoke it after your
> entities and `IEntityTypeConfiguration`s have been registered; entities added later are not scanned.

<a id="swashbuckle"></a>

## Swashbuckle

Map every strongly typed id to its underlying primitive schema (Swashbuckle.AspNetCore) in Swagger:

```csharp
services.AddSwaggerGen(options =>
{
    options.AddStronglyTypedIds();
});
```

The mapping is generated whenever `Swashbuckle.AspNetCore.SwaggerGen` ≥ 6.0.0 is referenced, and it works
with both Microsoft.OpenApi 1.x (Swashbuckle 6.x–9.x) and 2.x (Swashbuckle 10.x and later).

| Primitive  | OpenAPI `type` | `format` |
| ---------- | -------------- | -------- |
| `Guid`            | `string`       | `uuid`      |
| `string`, `char`  | `string`       | —           |
| `byte`            | `integer`      | `byte`      |
| `sbyte`           | `integer`      | `sbyte`     |
| `short`           | `integer`      | `int16`     |
| `ushort`          | `integer`      | `uint16`    |
| `int`             | `integer`      | `int32`     |
| `uint`            | `integer`      | `uint32`    |
| `long`            | `integer`      | `int64`     |
| `ulong`           | `integer`      | `uint64`    |
| `Int128`, `UInt128`, `BigInteger` | `integer` | —     |
| `decimal`         | `number`       | —           |
| `double`          | `number`       | `double`    |
| `float`           | `number`       | `float`     |
| `Half`            | `number`       | —           |
| `DateTime`, `DateTimeOffset` | `string` | `date-time` |
| `DateOnly`        | `string`       | `date`      |
| `TimeOnly`        | `string`       | `time`      |
| `TimeSpan`        | `string`       | —           |
| any `enum`        | `integer`      | —           |
| any custom primitive (e.g. `Ulid`) | `string` | —  |

The table describes what the converters actually write, not what the CLR type is called: `decimal` and
the floating-point primitives are JSON numbers, an enum is its underlying number, and `Int128` /
`UInt128` / `BigInteger` have no bounded `int64` / `uint64` range to advertise, so they get a bare
`integer` with no `format`. An id with a `Prefix` is always `string` with no `format`, because its
canonical text form is text. A custom primitive is `string` with no `format` for the same reason: its JSON
form is the canonical text, so there is no `uuid`-style format to advertise.

One limitation of any compile-time mapping: an `enum`-backed id is documented as `integer` because that is
what `System.Text.Json` writes by default. If you register a `JsonStringEnumConverter` globally, the
generated converter delegates to `JsonSerializer` and the actual payload becomes the member name, while the
document still says `integer`. The schema is emitted at compile time and cannot observe runtime
`JsonSerializerOptions`; prefer the default numeric form, or declare the id's primitive as `string`.

An id declared with `JsonShape = JsonShape.Object` is mapped to an object schema instead, because that is
what goes over the wire: `{ "value": <primitive> }`, with `value` marked required. The nested `value`
property follows the same primitive mapping as the table above (including the `Prefix` rule):

```json
{
  "type": "object",
  "properties": { "value": { "type": "string", "format": "uuid" } },
  "required": ["value"]
}
```

`MapType<T>` registers the schema for the id **type**, not for one endpoint, so there is nothing
per-endpoint to configure. Wherever the framework hands `ApiExplorer` the id type — a property on a DTO, a
response body, a model nested inside another, an MVC controller parameter — the same mapping applies.

**Minimal API** parameters have one more wrinkle: `ApiExplorer` describes an `IParsable` parameter as
`string` — the framework sees a type that parses from text — so `MapType`, which is keyed on the CLR type,
never gets a chance to apply. This library therefore also generates an operation filter: it looks the
parameter up by its **declared type** (`ApiParameterDescription.Type` still holds the id type) in the same
schema mapping, and replaces the bare `type: string` with the shape that carries a `format`. Request and
response bodies, and DTO properties, are mapped by `MapType` as usual, and so is an MVC controller
parameter. With the filter in place, a `Guid`-backed id in a Minimal API route is documented as
`type: string, format: uuid` instead of a bare `string`.

The filter is registered together with `AddStronglyTypedIds`, and calling it twice does not grow the filter list. The
built-in [ASP.NET Core OpenAPI](#aspnetcore-openapi) integration does not have this gap — its transformer
inspects the schema the framework already built.

<a id="dapper"></a>

## Dapper

When `Dapper` ≥ 2.0.0 is referenced, a `TypeHandler` is generated for every strongly typed id, along with a
single entry point:

```csharp
using Dapper;

// Register TypeHandlers for all strongly typed ids. Dapper keeps them in a process-wide
// registry, so this only needs to run once per process.
StronglyTypedIds.AddStronglyTypedIds();

connection.Execute("INSERT INTO Orders (Id) VALUES (@Id)", new { Id = OrderId.Create(Guid.NewGuid()) });
```

Each id's `TypeHandler` writes the wrapped primitive into the parameter, and reconstructs the id through
`Create` when reading. Ids with the same short name in different namespaces get a "namespace-underscored"
prefix on their `TypeHandler` to avoid collisions (the same disambiguation used for EF Core converters).

A **custom primitive** is written as its canonical text form instead of the CLR value, because the driver
does not recognise your type and would throw `No mapping exists` while binding the parameter. The text is
the same one the JSON, binary and EF Core paths use, so an id never appears as one shape in one store and
another elsewhere. Reading goes through the primitive's own `Parse`, so the round trip is lossless.

A `null` reference-type id is written as `DBNull.Value` rather than as a null object, so the column sees a
real SQL `NULL`. The handler is registered with `SqlMapper.AddTypeHandler<T>`, which keys it by the id
type, so anonymous parameter objects, `Query<T>` / `QueryAsync<T>` mapping and multi-mapping all pick it up
without per-call configuration.

Drivers rarely hand back the primitive's own CLR type — SQLite returns `long` for an enum column, `string`
for `char` / `Guid` and `double` for `decimal`. The generated `Parse` therefore converts the incoming value
to the primitive before calling `Create` (`System.Convert` for the numeric and date/time primitives,
`Enum.TryParse` for enums, and the primitive's `IParsable` entry point for `Guid`,
`DateTimeOffset`, `TimeSpan`, `DateOnly`, `TimeOnly`, `Int128`, `UInt128`, `Half` and `BigInteger`). Values
that already arrive as the primitive take a direct path, and `NULL` — either `DBNull` or a bare `null` —
maps to `default`. An enum column may come back as its member name or as the underlying number; both shapes
are accepted.

Enum text is matched case-insensitively, because drivers disagree on casing (Oracle returns uppercase,
PostgreSQL returns the stored spelling) while `Enum.Parse` is case-sensitive. It is also checked against the
enum's defined members: `Enum.TryParse` accepts `"999"` and `"1,2"`, which would produce an id matching no
member — a `switch` on it falls through and serialization writes a stray value. Non-`[Flags]` enums must
name a defined member; `[Flags]` enums accept any combination of defined bits, mirroring `Id.TryParse`. Text
that fails either check throws `ArgumentException`, the same exception `Create` raises for a rejected value.

The same check applies to the numeric path, and it has to happen before `Enum.ToObject`: that method neither
range-checks nor rejects fractions. A `300L` handed to a `byte`-backed enum silently becomes `44` — possibly
an existing member — and `1.5d` silently truncates to `0`. The generated handler therefore converts the value
to the enum's underlying type first (an out-of-range value throws `OverflowException`, which is turned into
`ArgumentException`), rejects anything with a fractional part, and only then validates the result against the
defined members. So `999L`, `300L`, `1.5d` and `uint.MaxValue` are all rejected exactly like their textual
counterparts, and a value that arrives already typed as the enum is validated too rather than waved through.

> Registration is global, not per connection. `AddStronglyTypedIds(IDbConnection)` is still generated for source
> compatibility, but it ignores its argument and forwards to `AddStronglyTypedIds()`. Calling either one more than once
> is harmless: Dapper replaces the handler for each type instead of appending, so the registry does not grow.

> Referencing `Dapper` with a major version below 2.0.0 emits nothing, to avoid incompatibility with the
> older `SqlMapper.TypeHandler` API.

<a id="messagepack"></a>

## MessagePack

When `MessagePack` ≥ 2.0.0 is referenced, every strongly typed id gets an `IMessagePackFormatter<TId>`, and a
single entry point assembles them into serializer options:

```csharp
using MessagePack;

// MessagePackSerializerOptions is immutable — the returned instance is the one to use.
var options = MessagePackSerializerOptions.Standard.AddStronglyTypedIds();

var bytes = MessagePackSerializer.Serialize(OrderId.Create(Guid.NewGuid()), options);
var id    = MessagePackSerializer.Deserialize<OrderId>(bytes, options);
```

The returned options put the generated formatters in a `CompositeResolver` ahead of whatever resolver the
argument already carried, so your own custom formatters keep working — the generated ones only win for ids.

Each id's formatter is named `{TypeName}MessagePackFormatter` and lives in the generated `StronglyTypedIds`
class instead of being attached to the id. That placement is what makes ids from a referenced library work:
`[MessagePackFormatter]` can only be applied to a type declared in the current compilation, so the common
"library declares the id, application uses it" case could never get one. Ids with the same short name in
different namespaces are disambiguated the same way as Dapper's handlers.

`AddStronglyTypedIds` also registers a formatter for each distinct **enum** primitive the ids wrap. MessagePack's
AOT-safe resolver chain contains no enum resolver at all — the IL-emitting `DynamicEnumResolver` is
excluded when dynamic code is unavailable — so without that entry an enum-backed id cannot be serialized
under Native AOT. The registered `GenericEnumFormatter<TEnum>` writes the same underlying numeric payload
as the dynamic resolver, so the wire format is unchanged. Non-enum primitives are served by MessagePack's
built-in resolver, which is already reflection-free. See [Native AOT and trimming](aot.md).

The payload is the wrapped primitive value, not the canonical text form: a text prefix decorates the text
form only, and a binary format has no text to decorate. Three primitives are the exception — `BigInteger`,
`Int128` and `UInt128` — and their ids are written as the invariant-culture text form (a MessagePack string)
and read back through `TryParse`. For them the prefix *does* travel in the payload, because it is part of the
text.

**Custom primitives** take the same text path, for a different reason: MessagePack has no formatter for a
type it has never seen, so the id is written as its canonical text and read back through the id's `TryParse`.
A prefix travels in the payload for them too.

The three-primitive exception is stable, not a limitation of the current MessagePack release: 3.x can encode
all three natively, but this library keeps the text form so that payloads written by earlier versions stay
readable. Switching them to native encoding would silently change the wire format for existing data.

For every other primitive the bytes are exactly what the primitive itself serialises to, so an id field is
indistinguishable from a bare primitive field in an existing payload — swapping one for the other needs no
re-encoding.

A `null` reference-type id is written as `nil` and read back as `null`; a value-type id that receives `nil`
throws `MessagePackSerializationException` rather than silently becoming `default`, so a missing field cannot
be mistaken for a legitimate `Guid.Empty`. A value rejected by the id's `Validator` is reported the same way:
the formatter calls `TryCreate` and throws `MessagePackSerializationException` itself, so the
`ArgumentException` from `Create` never reaches the caller.

<a id="mongodb"></a>

## MongoDB

When `MongoDB.Bson` ≥ 3.0.0 is referenced, every strongly typed id gets an `IBsonSerializer<TId>`, plus a
registration entry point:

```csharp
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

// Driver 3.0 has no global default Guid representation. Pick the one your existing
// documents use — otherwise serializing a Guid-backed id throws.
BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

// Registers the generated serializers. Process-wide and idempotent: run it once,
// before the first document is serialized.
StronglyTypedIds.RegisterBsonSerializers();

var collection = database.GetCollection<Order>("orders");
await collection.InsertOneAsync(new Order { Id = OrderId.Create(Guid.NewGuid()) });
```

`BsonSerializer` keeps serializers in a process-wide registry, so registration is global rather than
per-collection, and calling it twice is harmless — the driver sees the same serializer type already
registered and returns `false` instead of adding a second entry.

A serializer registered by someone else for the same id type is a different matter. The driver's
`TryRegisterSerializer` only tolerates a repeat of the *same* serializer; when a *different* one is already
registered for that type it throws `BsonSerializationException: There is already a different serializer
registered`. The generated id registrations call it directly, so `RegisterBsonSerializers()` surfaces that
exception rather than silently deferring to the existing serializer. If you register your own serializer for
an id type, either drop the generated one for that id (`Integrations = None`) or register yours after
`RegisterBsonSerializers()` — the driver will reject the second registration either way, so pick one owner
per type.

Each id's serializer is named `{TypeName}BsonSerializer` and lives in the generated `StronglyTypedIds` class.
`[BsonSerializer]` would have been the shorter route, but it can only be applied to a type declared in the
current compilation, which would leave ids from a referenced library without a serializer.

`RegisterBsonSerializers` also pre-registers a default-configured serializer for every primitive the ids
wrap — `int`, `char`, `decimal`, `string`, enums, and so on. BSON's `PrimitiveSerializationProvider` would
otherwise create those via `Activator`, which throws `MissingMethodException` under Native AOT once the
constructors are trimmed. The pre-registered instances match the provider-created defaults, so nothing
changes for a JIT application. These registrations call the driver directly, so they stay harmless only as
long as the app has not registered a *different* serializer for that primitive — registering, say, a custom
`int` serializer makes the call throw. BSON's dynamic types (`object`, `List<object>`, `ExpandoObject`) are
registered for the same AOT reason: every root serialization context takes
`BsonDefaults.DynamicArraySerializer` and `BsonDefaults.DynamicDocumentSerializer`, which look those types
up in the registry. Those three go through a private helper that swallows a conflict, so an app that already
registered its own keeps it. `Guid` stays excluded: since driver 3.0 it has no global default representation,
and the app must register the one its data uses, as in the snippet above. See
[Native AOT and trimming](aot.md).

The payload is the wrapped primitive value. `BigInteger`, `Int128` and `UInt128` have no BSON serializer, so
those ids are stored as a BSON string holding the invariant-culture text form (prefix included) and read back
through `TryParse`. **Custom primitives** are stored the same way, because the driver has no serializer for a
type it does not know. A `null` reference-type id is stored as BSON `null`; a value-type id that meets BSON
`null` throws `BsonSerializationException`. A value rejected by the id's `Validator` is reported the same
way: the serializer calls `TryCreate` and throws `BsonSerializationException` itself, so the
`ArgumentException` from `Create` never reaches the caller.

For every other primitive the stored BSON value is the primitive's own, so existing documents stay readable
and an id field is indistinguishable from a bare primitive field. Because the serializer is registered by
type, LINQ filters and projections (`Builders<Order>.Filter.Eq(x => x.Id, id)`) serialise the id through it
too — there is nothing to configure per query.

<a id="hotchocolate"></a>

## HotChocolate (GraphQL)

When `HotChocolate` ≥ 13.0.0 is referenced, every strongly typed id gets a `ScalarType<TId>`, and a single
entry point registers them all:

```csharp
using HotChocolate;
using HotChocolate.Execution.Configuration;

builder.Services
    .AddGraphQLServer()
    .AddQueryType<Query>()
    .AddStronglyTypedIds();   // generated extension on IRequestExecutorBuilder
```

The generated entry point is `AddStronglyTypedIds()`, an extension on `IRequestExecutorBuilder` — the same
shape as `AddGraphQLServer()` and `AddQueryType<T>()`, so it chains. It is idempotent — calling it twice
leaves the service collection unchanged — and it registers each scalar with **`BindRuntimeType`**, not just
`AddType`. That distinction matters: without the runtime-type binding HotChocolate still infers the id as an
object type, and schema construction fails with
`The name 'OrderId' was already registered by another type`.

### Representation matches the JSON form

The scalar's literal kind follows the same rule as the JSON converter and the OpenAPI schema, so an id is
never written one way over REST and another over GraphQL:

| Primitive | GraphQL literal | Example |
| --- | --- | --- |
| `Guid`, `string`, `char`, `DateTime`, `DateTimeOffset`, `TimeSpan`, `DateOnly`, `TimeOnly` | `String` | `"3fa85f64-…"` |
| Any id with a `Prefix` | `String` (the canonical text form) | `"ord_3fa85f64-…"` |
| `byte` … `ulong`, `Int128`, `UInt128`, `BigInteger` | `Int` | `42` |
| `enum` | `Int` — the **underlying value**, not the member name | `1` |
| `decimal`, `double`, `float`, `Half` | `Float` | `1.5` |
| any custom primitive (e.g. `Ulid`) | `String` (the canonical text form) | `"01ARZ3NDEKTSV4RRFFQ69G5FAV"` |

An enum-backed id is written as its underlying number, not its member name, so the same id reads `1` in
GraphQL and `1` in JSON. HotChocolate's own enum scalars use the member name instead, which would make the
same value `"Paid"` on one side and `1` on the other. If you want member names in your schema, map the enum
to a GraphQL enum type yourself and keep the id out of that path.

`Int128`, `UInt128` and `BigInteger` exceed what GraphQL's `Int` can carry, so their literals are built from
the invariant-culture text and parsed back through `TryParse` — the value round-trips exactly, it just is not
a 32-bit `Int` on the wire.

### Input, output and failure

- **Output** goes through `TrySerialize`, which returns the raw CLR value (`string`, an integer, a floating
  point) so HotChocolate writes it natively. A `null` reference-type id returns `false`, which the framework
  writes as GraphQL `null`.
- **Input** arrives either as a literal (`ParseLiteral`) or as a variable (`TryDeserialize`). The variable
  path must accept both `string` and `System.Text.Json.JsonElement`: a real HTTP request hands the scalar a
  `JsonElement`, while `QueryRequestBuilder` hands it a `string`. The generated code handles both.
- **Failure** throws `HotChocolate.Types.SerializationException`, not `FormatException`. The framework turns
  the former into a located GraphQL error; the latter would surface as `Unexpected Execution Error` with no
  useful message. Values rejected by the id's `Validator` are reported the same way, because the scalar goes
  through `TryParse` rather than constructing the id directly — so an undefined enum value is rejected too.

Each id's scalar is named `{TypeName}HotChocolateType` and lives in the generated `StronglyTypedIds` class
rather than being attached to the id, so ids that come from a referenced library get a scalar as well. Ids
with the same short name in different namespaces are disambiguated the same way as Dapper's handlers.

<a id="protobuf-net"></a>

## protobuf-net

When `protobuf-net` ≥ 3.0.0 is referenced, every id declared **in the current compilation** gets a surrogate
contract:

```csharp
[StronglyTypedId]
public partial record struct OrderId(Guid Value);
```

```csharp
// generated
[ProtoContract(Surrogate = typeof(OrderId.OrderIdSurrogate))]
partial record struct OrderId
{
    [ProtoContract]
    internal sealed class OrderIdSurrogate
    {
        [ProtoMember(1)] public Guid Value { get; set; }

        public static implicit operator OrderIdSurrogate(OrderId id) => new() { Value = id.Value };
        public static implicit operator OrderId(OrderIdSurrogate surrogate) { /* TryCreate, else ProtoException */ }
    }
}
```

There is no registration call — `Serializer.Serialize(order)` just works, because protobuf-net discovers the
surrogate from the attribute.

### Why an attribute and not a runtime registration

protobuf-net also offers `RuntimeTypeModel.Default.Add(typeof(OrderId), false).SetSurrogate(...)`, which
would let a consuming assembly cover ids from referenced libraries. This library does not use it, because
that API is order-dependent: once a type has been serialised the model is frozen, and registering a
surrogate afterwards throws `The type cannot be changed once a serializer has been generated for OrderId`.
Whether that happens depends on what your application did first, which a generated helper cannot control.
The attribute has no such problem, and it is what protobuf-net documents.

The cost is that **ids from a referenced assembly get no surrogate**. The attribute can only be applied to a
type declared in the same compilation, so if your domain layer declares the id and your serialisation layer
references it, add the surrogate by hand there (or declare the id in the serialising assembly).

### Payload: the primitive value, or its canonical text

protobuf-net has native support for most primitives, and the payload is the primitive value itself — the same
choice MessagePack and BSON make, so an id's wire form does not depend on which serializer you picked.

Five primitives have no native representation and fall back to the invariant-culture text:

| Primitive | Payload |
| --- | --- |
| `Guid`, `string`, `char`, `byte` … `ulong`, `decimal`, `float`, `double`, `DateTime`, `TimeSpan`, `DateOnly`, `TimeOnly`, `enum` | the value itself |
| `Int128`, `UInt128`, `Half`, `DateTimeOffset`, `BigInteger` | canonical text (`string`) |
| any custom primitive (e.g. `Ulid`) | canonical text (`string`) |

`DateTimeOffset` uses the `"O"` format specifically: the default `ToString()` drops sub-second ticks, so a
round-trip would silently truncate the value. `"O"` preserves both the ticks and the offset.

A `Prefix` does **not** appear in the payload — it belongs to the text representation, not to the data. The
same rule applies to MessagePack and BSON.

### Failure and null

- A payload rejected by the id's `Validator` throws `ProtoBuf.ProtoException`, not `ArgumentException`. The
  generated conversion operator calls `TryCreate` and throws the framework's own exception type, because
  protobuf-net wraps anything else and the caller loses the reason.
- A `null` reference-type id round-trips as `null`. protobuf-net writes a root-level `null` as an empty
  payload and reads it back as a surrogate with a `null` field, so the generated operator maps both shapes to
  `null`.
- Ids nested in a generic container are skipped: the attribute argument would contain a type parameter
  (`CS0416`), and without the attribute the surrogate is dead code.
- If you already put `[ProtoContract]` on the id yourself, the generator skips it — attributes cannot be
  repeated (`CS0579`), and your contract wins.

<a id="aspnet-core-mvc"></a>

## ASP.NET Core MVC

When `Microsoft.AspNetCore.Mvc` ≥ 2.0.0 is referenced (on .NET 8 / .NET 10 the `Microsoft.AspNetCore.Mvc.Core`
and `Microsoft.AspNetCore.Mvc.Abstractions` assemblies also satisfy the gate), the generator emits a single
model-binder provider plus one route constraint per id, so strongly typed ids bind and validate without any
per-type boilerplate:

```csharp
using Microsoft.Extensions.DependencyInjection;

// Register the model-binder provider and the per-id route constraints.
builder.Services.AddControllers(options => options.AddStronglyTypedIds());
builder.Services.AddRouting(options => options.AddStronglyTypedIds());
```

```csharp
[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    // "ABC123" from the route or query is bound into OrderId via IParsable<OrderId>.TryParse.
    [HttpGet("{id}")]
    public IActionResult Get([FromRoute] OrderId id) => Ok(id);

    // The {id:OrderId} constraint validates the segment format before the action runs.
    [HttpGet("v2/{id:OrderId}")]
    public IActionResult GetV2([FromRoute] OrderId id) => Ok(id);
}
```

> The binder only takes over for types that implement `IStronglyTypedId<TSelf, TPrimitiveId>`; every other
> type falls through to MVC's default binding chain. The bind itself reuses the `IParsable<TSelf>` member that
> every id already has, so there is no per-request reflection beyond a one-time type check at registration.

The provider is inserted at the front of `ModelBinderProviders`, so it wins over the framework's own
providers. `AddStronglyTypedIds(MvcOptions)` is idempotent: if a `StronglyTypedIdModelBinderProvider` is already in the
list it returns without inserting another, so startup code that registers it from several modules — or a
configuration callback that runs twice — does not grow the provider list or rebuild the binder on every
request. Nullable ids are covered too: `[FromQuery] OrderId?` has CLR type `Nullable<OrderId>`, which
implements no interfaces at all, so the provider unwraps it before the interface check. Without that step the
parameter would fall to MVC's `TryParseModelBinder`, which parses with `ValueProviderResult.Culture` (the
invariant culture for query strings and routes, the current culture for forms) — the same form payload would
then bind differently for a nullable and a non-nullable parameter on the same route.

Binding behaviour, in the three cases that matter:

| Input | Result |
| --- | --- |
| Valid text | Bound through `TryParse` with the invariant culture; a `Prefix` is honoured by the id's own read mode |
| Missing key, or an empty value | Left unbound and **no** model error, so a nullable parameter stays `null` and a non-nullable one gets the framework's usual "required value" handling |
| Text the id rejects | One model error — `The value '<text>' is not a valid strongly typed id.` — which `[ApiController]` turns into a 400 |

Route constraints are registered per id under its **short type name**, so `{id:OrderId}` resolves. Two ids
with the same short name in different namespaces therefore fight over one entry in `ConstraintMap`: the last
registration wins, so only one of them gets a working constraint. The binder has no such limit, because it is
keyed by the parameter's type.

The constraint converts the route value to text before parsing, which is what makes it work in both
directions: on an incoming request the value is the raw segment, and during URL generation it is already an
id instance. So `{id:OrderId}` still produces links through `LinkGenerator` / `Url.Action`. A missing route
value fails the constraint.

<a id="minimal-api"></a>

## Minimal API

Minimal API needs **no extra generation**. Its parameter-binding convention accepts any type with a static
`TryParse(string?, IFormatProvider?, out TSelf)` — which every id already has through `IParsable<TSelf>` — so
route and query parameters bind out of the box:

```csharp
// "ABC123" from the route is bound into OrderId via IParsable<OrderId>.TryParse;
// malformed text yields 400 before the handler runs.
app.MapGet("/orders/{id}", (OrderId id) => Results.Ok(id.Value));

// [AsParameters] binds each id property from the matching route / query value.
app.MapGet("/orders", ([AsParameters] OrderQuery query) => Results.Ok(query.Id.Value));

public record OrderQuery(OrderId Id);
```

No `BindAsync`, no `TryParse` shim and no DI registration are needed — the framework finds the static
`TryParse` on the type itself. Because binding goes through that same member, a `Prefix` and the id's read
mode apply here exactly as they do in MVC.

Route constraints are shared with MVC: the generated `AddStronglyTypedIds(RouteOptions)` registers one constraint per id,
so `{id:OrderId}` works in Minimal API templates too (the `Microsoft.AspNetCore.Mvc.*` assemblies ship in the
ASP.NET Core shared framework, so the gate is satisfied in a Minimal API project):

```csharp
builder.Services.AddRouting(options => options.AddStronglyTypedIds());

app.MapGet("/orders/v2/{id:OrderId}", (OrderId id) => Results.Ok(id.Value));
```

## AutoMapper

AutoMapper needs no generated integration. Two cases cover everything, and only the second needs
configuration.

**Both sides are ids.** Nothing to do — AutoMapper assigns members of the same type directly:

```csharp
cfg.CreateMap<Order, OrderDto>();   // Order.Id (OrderId) -> OrderDto.Id (OrderId)
```

**One side is the bare primitive.** AutoMapper will not guess that `OrderId` should unwrap to `Guid`, so
register the conversion once per id type:

```csharp
cfg.CreateMap<OrderId, Guid>().ConvertUsing(id => id.Value);
cfg.CreateMap<Guid, OrderId>().ConvertUsing(value => OrderId.Create(value));
```

Registering the pair once is enough for every mapping that crosses that boundary, in both directions.

### `ProjectTo` translates to SQL

The conversion survives EF Core's expression translation, so `ProjectTo` stays a server-side query — the
lambda is not evaluated on the client. Mapping an entity to a DTO whose id is a bare `Guid` executes a
single `SELECT` over the entity's columns:

```sql
SELECT "o"."Id", "o"."Name" FROM "Orders" AS "o"
```

`ConvertUsing(id => id.Value)` is eliminated entirely: EF already maps the `OrderId` column to `Guid`
through the generated value converter, and the lambda is just a member access on top of it. The same
holds for the reverse direction and for `MapFrom(source => source.Id.Value)`.

> This is why there is no `AutoMapper` flag on `Integrations` and no `AddStronglyTypedIds(AutoMapperConfiguration)`:
> the common case needs nothing, and the remaining case is two explicit lines that read better than a
> registration you cannot see at the call site.

<a id="aspnetcore-openapi"></a>

## ASP.NET Core OpenAPI

When `Microsoft.AspNetCore.OpenApi` ≥ 9.0.0 is referenced, OpenAPI schema mappings are generated for every
strongly typed id, in parallel to Swashbuckle's `AddStronglyTypedIds(SwaggerGenOptions)`:

```csharp
builder.Services.AddOpenApi(options =>
{
    options.AddStronglyTypedIds();
});
```

Each id is mapped to the schema of its underlying primitive, with the same `format` values as in the
[Swashbuckle](#swashbuckle) section. An id declared with `JsonShape = JsonShape.Object` is rewritten to an
object schema with a single required `value` property, matching the `{ "value": ... }` payload — the
framework builds an empty schema for it, because it cannot see through the custom converter.

The mechanism differs from Swashbuckle's: this one is an `IOpenApiSchemaTransformer` added through
`AddSchemaTransformer`, so it rewrites only `type` and `format` on a schema the framework has already built
and leaves everything else — description, `nullable`, `required`, examples — as it was. Because the framework
keeps that schema as a named component, an id in a body shows up as `$ref: '#/components/schemas/OrderId'`
rather than as an inline object. Swashbuckle's `MapType` instead replaces the schema for the type outright,
with an inline one that carries only `type` and `format`. The exception is `JsonShape.Object`, where both
integrations write the object shape, since the framework has no schema to preserve.

The two also differ in where they reach. The built-in OpenAPI integration covers route and query
parameters as well as bodies and DTO properties, so a `Guid`-backed id in a Minimal API route is documented
as `type: string, format: uuid`. Swashbuckle needs the operation filter described in its own
[section](#swashbuckle) to reach Minimal API parameters, because `ApiExplorer` reports an `IParsable`
parameter as `string` and `MapType` is keyed on the CLR type. Neither integration touches a schema that is
not an id.

> This integration is independent of Swashbuckle: either, both, or neither may be present, and each generates
> its own `AddStronglyTypedIds` overload.

### Why 9.0 and not 8.0

`Microsoft.AspNetCore.OpenApi` 8.0.x ships only the `WithOpenApi()` annotation helpers. The schema-transformer
API — `IOpenApiSchemaTransformer`, `OpenApiSchemaTransformerContext` and `AddSchemaTransformer` — arrives in
9.0.0, so a transformer generated against 8.0.x fails to compile with `CS0246`; the gate cannot be lowered.
`net8.0` has no built-in document generation, so use the [Swashbuckle](#swashbuckle) integration there — it
maps the same ids to the same `type` / `format` pairs.

[<- Back to the index](index.md) · [中文](zh-CN/integrations.md)
