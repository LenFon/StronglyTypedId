---
layout: default
title: Integrations
---

# Integrations

Every integration is generated only when its package is referenced, and each one adds its entry point to
the same generated `StronglyTypedIds` class; see
[The integration entry point](generated-code.md#the-integration-entry-point). Nothing at all is emitted for
an integration that is switched off, either per id or through the assembly-level default (see
[Turning integrations off](declaration.md#integrations-filter)), nor when the compilation declares no ids.

The two JSON serializers are integrations in the same sense, but they are covered on their own page. See
[Serialization](serialization.md).

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

    // Reflects over each entity's CLR properties and maps every strongly typed id it finds.
    modelBuilder.AddStronglyTypedIds();
}
```

> Both overloads are generated whenever EF Core ≥ 7.0.0 is referenced, the same gate as every other
> integration, with no requirement that a `DbContext` override `ConfigureConventions`. `ModelConfigurationBuilder`
> only exists from EF Core 7, which is why the version floor is higher here than elsewhere.
>
> `AddStronglyTypedIds(ModelBuilder)` reflects over each entity's CLR properties rather than walking the
> model's discovered properties: EF Core's conventions do not add a property whose CLR type is neither a
> supported primitive nor a valid entity type, and a strongly typed id is exactly that, so a model walk
> would find nothing. Nullable id properties (`OrderId?`) are mapped too, and properties excluded with
> `[NotMapped]` or a fluent `Ignore` are left alone. Entity types configured after the call are still not
> picked up, so invoke it at the end of `OnModelCreating`. The conventions overload has no such ordering
> constraint and is the better default.

Each id gets a `ValueConverter<{Id}, {Primitive}>` nested in the generated `StronglyTypedIds` class, named
`{TypeName}Converter`. If two ids with the same short name exist in different namespaces, the converter
names would collide inside that single class, so only then are they disambiguated as
`{Namespace_With_Underscores}_{TypeName}Converter` (for example `Domain_OrderIdConverter`). Because that
replacement turns `.` into `_`, two ids can still end up with the same name — `A.B.OrderId` and
`A_B.OrderId` both become `A_B_OrderIdConverter`; in that case a short stable hash of the id's full name is
appended, so the two stay distinct. The same hash is appended when an id whose short name is unique happens
to be called `A_B_OrderId`: its plain `A_B_OrderIdConverter` would otherwise be byte-identical to the
disambiguated name of `A.B.OrderId`. The converters are implementation details of `AddStronglyTypedIds`,
generated as private nested types, so you never reference them by name and no action is required from you in
either case.

For a built-in primitive the converter's provider type is that primitive, so the column type and the schema
EF Core builds are the ones the primitive would have had on its own: introducing the id migrates nothing,
and existing columns keep working.

A **custom primitive** is the exception. The database provider does not know your CLR type, so the provider
type is fixed to `string` and the column stores the primitive's canonical text form, the same text the
JSON, binary and Dapper paths use. Without that, EF Core rejects the property with
`The '<Primitive>' property could not be mapped because the database provider does not support this type`.

The same applies to four built-in primitives that have no provider type at all: `BigInteger`, `Int128`,
`UInt128` and `Half`. Their ids also store the canonical text (`string`), because using the primitive itself
as the provider type makes EF Core reject the whole property with the message above. The other built-in
primitives — including `DateOnly`, `TimeOnly`, `decimal` and `char` — keep their native column type.

Reading a row back runs the id's validators and normalizers on every materialisation, and a stored value the
validator rejects throws instead of producing an id that could never have been constructed in code. Every
converter — not just the enum-backed ones — calls `TryCreate` from a generated helper method and throws
`InvalidOperationException` itself, so the `ArgumentException` from `Create` never reaches the caller. The
helper exists because the converter's lambda is compiled into an expression tree, and expression trees cannot
contain `throw`.

An **enum**-backed id is checked against the enum's defined members as well, the same rule the other
integrations apply: a non-`[Flags]` enum must name a defined member, a `[Flags]` enum may combine defined
bits but not undefined ones. A stored `999` is rejected rather than materialised as `(Status)999`, which no
`switch` would ever match. The check lives in a generated helper method rather than in the converter's
lambda, because that lambda is compiled into an expression tree and expression trees cannot contain `throw`.

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
| `Int128`, `UInt128` | `integer`      | —           |
| `BigInteger`      | `string`       | —           |
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
the floating-point primitives are JSON numbers, an enum is its underlying number, and `Int128` / `UInt128`
have no bounded `int64` / `uint64` range to advertise, so they get a bare `integer` with no `format`.
`BigInteger` is `string` because System.Text.Json has no built-in converter for it: writing it by value
would emit `{"IsPowerOfTwo":…}`, so the generated converter writes the canonical text instead. An id with
a `Prefix` is always `string` with no `format`, because its
canonical text form is text. A custom primitive is `string` with no `format` for the same reason: its JSON
form is the canonical text, so there is no `uuid`-style format to advertise.

The mapping follows System.Text.Json. Under Newtonsoft.Json, `Int128`, `UInt128`, `Half` and `decimal` are
written as strings instead (see [Serialization](serialization.md#primitives-the-serializers-do-not-know)),
so a document served to a Newtonsoft client is one step off for those four primitives. The schema is
emitted at compile time and cannot observe which serializer the host registers.

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
per-endpoint to configure. Wherever the framework hands `ApiExplorer` the id type (a property on a DTO, a
response body, a model nested inside another, an MVC controller parameter), the same mapping applies.

**Minimal API** parameters have one more wrinkle: `ApiExplorer` describes an `IParsable` parameter as
`string`, because the framework sees a type that parses from text, so `MapType`, which is keyed on the CLR
type, never gets a chance to apply. This library therefore also generates an operation filter: it looks the
parameter up by its **declared type** (`ApiParameterDescription.Type` still holds the id type) in the same
schema mapping, and replaces the bare `type: string` with the shape that carries a `format`. Request and
response bodies, and DTO properties, are mapped by `MapType` as usual, and so is an MVC controller
parameter. With the filter in place, a `Guid`-backed id in a Minimal API route is documented as
`type: string, format: uuid` instead of a bare `string`.

The filter is registered together with `AddStronglyTypedIds`, and calling it twice does not grow the filter list. The
built-in [ASP.NET Core OpenAPI](#aspnetcore-openapi) integration does not have this gap, because its
transformer inspects the schema the framework already built.

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

The four built-in primitives with no provider type — `BigInteger`, `Int128`, `UInt128` and `Half` — take the
same text path for the same reason. Every other built-in primitive is written as its CLR value.

A `null` reference-type id is written as `DBNull.Value` rather than as a null object, so the column sees a
real SQL `NULL`. The same applies to a value-type id whose primitive is a reference type (a custom `class`):
a `null` primitive is written as `DBNull.Value` instead of throwing. The handler is registered with
`SqlMapper.AddTypeHandler<T>`, which keys it by the id
type, so anonymous parameter objects, `Query<T>` / `QueryAsync<T>` mapping and multi-mapping all pick it up
without per-call configuration.

Drivers rarely hand back the primitive's own CLR type: SQLite returns `long` for an enum column, `string`
for `char` / `Guid` and `double` for `decimal`. The generated `Parse` therefore converts the incoming value
to the primitive before calling `Create` (`System.Convert` for the numeric and date/time primitives,
`Enum.TryParse` for enums, and the primitive's `IParsable` entry point for `Guid`,
`DateTimeOffset`, `TimeSpan`, `Int128`, `UInt128`, `Half` and `BigInteger`). `DateOnly` and `TimeOnly` also
accept the driver's own CLR shape: SQL Server returns `DateTime` for a `DATE` column and `TimeSpan` for a
`TIME` column, and their `Parse` rejects text that carries the other component, so the generated handler
converts from those types first. Values that already arrive as the primitive take a direct path, and `NULL`,
either `DBNull` or a bare `null`,
maps to `default`. An enum column may come back as its member name or as the underlying number; both shapes
are accepted.

Enum text is matched case-insensitively, because drivers disagree on casing (Oracle returns uppercase,
PostgreSQL returns the stored spelling) while `Enum.Parse` is case-sensitive. It is also checked against the
enum's defined members: `Enum.TryParse` accepts `"999"` and `"1,2"`, which would produce an id matching no
member, so a `switch` on it falls through and serialization writes a stray value. Non-`[Flags]` enums must
name a defined member; `[Flags]` enums accept any combination of defined bits, mirroring `Id.TryParse`. Text
that fails either check throws `ArgumentException`, the same exception `Create` raises for a rejected value.

The same check applies to the numeric path, and it has to happen before `Enum.ToObject`: that method neither
range-checks nor rejects fractions. A `300L` handed to a `byte`-backed enum silently becomes `44`, possibly
an existing member, and `1.5d` silently truncates to `0`. The generated handler therefore converts the value
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
argument already carried, so your own custom formatters keep working; the generated ones only win for ids.

Each id's formatter is named `{TypeName}MessagePackFormatter` and lives in the generated `StronglyTypedIds`
class instead of being attached to the id. That placement is what makes ids from a referenced library work:
`[MessagePackFormatter]` can only be applied to a type declared in the current compilation, so the common
"library declares the id, application uses it" case could never get one. Ids with the same short name in
different namespaces are disambiguated the same way as Dapper's handlers.

`AddStronglyTypedIds` also registers a formatter for each distinct **enum** primitive the ids wrap. MessagePack's
AOT-safe resolver chain contains no enum resolver at all (the IL-emitting `DynamicEnumResolver` is
excluded when dynamic code is unavailable), so without that entry an enum-backed id cannot be serialized
under Native AOT. The registered `GenericEnumFormatter<TEnum>` writes the same underlying numeric payload
as the dynamic resolver, so the wire format is unchanged. Non-enum primitives are served by MessagePack's
built-in resolver, which is already reflection-free. See [Native AOT and trimming](aot.md).

The payload is the wrapped primitive value, not the canonical text form: a text prefix decorates the text
form only, and a binary format has no text to decorate. Three primitives are the exception (`BigInteger`,
`Int128` and `UInt128`), and their ids are written as the invariant-culture text form (a MessagePack string)
and read back through `TryParse`. For them the prefix *does* travel in the payload, because it is part of the
text.

**Custom primitives** take the same text path, for a different reason: MessagePack has no formatter for a
type it has never seen, so the id is written as its canonical text and read back through the id's `TryParse`.
A prefix travels in the payload for them too.

The three-primitive exception is stable, not a limitation of the current MessagePack release: 3.x can encode
all three natively, but this library keeps the text form so that payloads written by earlier versions stay
readable. Switching them to native encoding would silently change the wire format for existing data.

For every other primitive the bytes are exactly what the primitive itself serialises to, so an id field is
indistinguishable from a bare primitive field in an existing payload, and swapping one for the other needs no
re-encoding.

A `null` reference-type id is written as `nil` and read back as `null`; a value-type id that receives `nil`
throws `MessagePackSerializationException` rather than silently becoming `default`, so a missing field cannot
be mistaken for a legitimate `Guid.Empty`. An id whose wrapped primitive is `null` (a `string`-backed id or
one backed by a custom `class`) is written as `nil` too, matching what System.Text.Json writes, instead of
the empty string the text form would otherwise produce — and read back as `default`, so the write and the
read stay symmetric whether the id itself is a `record struct` or a `record class`. A value rejected by the
id's `Validator` is reported the same way: the formatter calls `TryCreate` and throws
`MessagePackSerializationException` itself, so the `ArgumentException` from `Create` never reaches the caller.

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
per-collection, and calling it twice is harmless: the driver sees the same serializer type already
registered and returns `false` instead of adding a second entry.

A serializer registered by someone else for the same id type is a different matter. The driver's
`TryRegisterSerializer` only tolerates a repeat of the *same* serializer; when a *different* one is already
registered for that type it throws `BsonSerializationException: There is already a different serializer
registered`. The generated id registrations call it directly, so `RegisterBsonSerializers()` surfaces that
exception rather than silently deferring to the existing serializer. If you register your own serializer for
an id type, either drop the generated one for that id (`Integrations = None`) or register yours after
`RegisterBsonSerializers()`; the driver will reject the second registration either way, so pick one owner
per type.

Each id's serializer is named `{TypeName}BsonSerializer` and lives in the generated `StronglyTypedIds` class.
`[BsonSerializer]` would have been the shorter route, but it can only be applied to a type declared in the
current compilation, which would leave ids from a referenced library without a serializer.

`RegisterBsonSerializers` also pre-registers a default-configured serializer for every primitive the ids
wrap (`int`, `char`, `decimal`, `string`, enums, and so on). BSON's `PrimitiveSerializationProvider` would
otherwise create those via `Activator`, which throws `MissingMethodException` under Native AOT once the
constructors are trimmed. The pre-registered instances match the provider-created defaults, so nothing
changes for a JIT application. These registrations call the driver directly, so they stay harmless only as
long as the app has not registered a *different* serializer for that primitive; registering, say, a custom
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
`null` throws `BsonSerializationException`. An id whose wrapped primitive is `null` (a `string`-backed id or
one backed by a custom `class`) is stored as BSON `null` too, matching what System.Text.Json writes, instead
of the empty string the text form would otherwise produce — and read back as `default`, so the write and the
read stay symmetric whether the id itself is a `record struct` or a `record class`. A value rejected by the
id's `Validator` is reported the same way: the serializer calls `TryCreate` and throws
`BsonSerializationException` itself, so the `ArgumentException` from `Create` never reaches the caller.

For every other primitive the stored BSON value is the primitive's own, so existing documents stay readable
and an id field is indistinguishable from a bare primitive field. Because the serializer is registered by
type, LINQ filters and projections (`Builders<Order>.Filter.Eq(x => x.Id, id)`) serialise the id through it
too, with nothing to configure per query.

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

The generated entry point is `AddStronglyTypedIds()`, an extension on `IRequestExecutorBuilder` with the same
shape as `AddGraphQLServer()` and `AddQueryType<T>()`, so it chains. It is idempotent (calling it twice
leaves the service collection unchanged) and it registers each scalar with **`BindRuntimeType`**, not just
`AddType`. Without the runtime-type binding HotChocolate still infers the id as an object type, and schema
construction fails with
`The name 'OrderId' was already registered by another type`.

### Representation matches the JSON form

The scalar's literal kind follows the same rule as the JSON converter and the OpenAPI schema, so an id is
never written one way over REST and another over GraphQL:

| Primitive | GraphQL literal | Example |
| --- | --- | --- |
| `Guid`, `string`, `char`, `DateTime`, `DateTimeOffset`, `TimeSpan`, `DateOnly`, `TimeOnly` | `String` | `"3fa85f64-…"` |
| Any id with a `Prefix` | `String` (the canonical text form) | `"ord_3fa85f64-…"` |
| `byte` … `ulong` | `Int` | `42` |
| `Int128`, `UInt128`, `BigInteger` | `String` (the invariant-culture text form) | `"42"` |
| `enum` | `Int`, the **underlying value**, not the member name | `1` |
| `decimal`, `double`, `float`, `Half` | `Float` | `1.5` |
| any custom primitive (e.g. `Ulid`) | `String` (the canonical text form) | `"01ARZ3NDEKTSV4RRFFQ69G5FAV"` |

An enum-backed id is written as its underlying number, not its member name, so the same id reads `1` in
GraphQL and `1` in JSON. HotChocolate's own enum scalars use the member name instead, which would make the
same value `"Paid"` on one side and `1` on the other. If you want member names in your schema, map the enum
to a GraphQL enum type yourself and keep the id out of that path.

`Int128`, `UInt128` and `BigInteger` exceed what GraphQL's `Int` can carry, and HotChocolate's serializer
does not recognise them either, so both the literal and the response are the invariant-culture text:
`"42"` in, `"42"` out. Input and output are symmetric — what the server returns can be sent back verbatim.
This differs from JSON, where `Int128` and `UInt128` are numbers; `BigInteger` is text in both, because
System.Text.Json has no built-in converter for it.

### Input, output and failure

- **Output** goes through `TrySerialize`, which returns the raw CLR value (`string`, an integer, a floating
  point) so HotChocolate writes it natively. A `null` reference-type id returns `false`, which the framework
  writes as GraphQL `null`; so does an id whose wrapped primitive is `null` (a `string`-backed id or one
  backed by a custom `class`), matching what the other serializers write instead of the empty string the
  text form would produce. A prefixed id is the exception, since its text is the prefix plus the empty
  string and is never `null`.
- **Input** arrives either as a literal (`ParseLiteral`) or as a variable (`TryDeserialize`). The variable
  path must accept both `string` and `System.Text.Json.JsonElement`: a real HTTP request hands the scalar a
  `JsonElement`, while `QueryRequestBuilder` hands it a `string`. The generated code handles both.
- **Failure** throws `HotChocolate.Types.SerializationException`, not `FormatException`. The framework turns
  the former into a located GraphQL error; the latter would surface as `Unexpected Execution Error` with no
  useful message. Values rejected by the id's `Validator` are reported the same way, because the scalar goes
  through `TryParse` rather than constructing the id directly, so an undefined enum value is rejected too.

Each id's scalar is named `{TypeName}HotChocolateType` and lives in the generated `StronglyTypedIds` class
rather than being attached to the id, so ids that come from a referenced library get a scalar as well. Ids
with the same short name in different namespaces are disambiguated the same way as Dapper's handlers.

The GraphQL type name is a separate matter: GraphQL has one global type table, so two `OrderId` ids in
different namespaces cannot both be called `OrderId` even though their C# names are distinct. When a short
name is shared, the GraphQL name becomes the sanitised full name (`Domain_OrderId`), and a stable hash is
appended if that still collides with another id's name. A unique short name is used unchanged.

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

There is no registration call: `Serializer.Serialize(order)` just works, because protobuf-net discovers the
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

protobuf-net has native support for most primitives, and the payload is the primitive value itself, the same
choice MessagePack and BSON make, so an id's wire form does not depend on which serializer you picked.

Five primitives have no native representation and fall back to the invariant-culture text:

| Primitive | Payload |
| --- | --- |
| `Guid`, `string`, `char`, `byte` … `ulong`, `decimal`, `float`, `double`, `DateTime`, `TimeSpan`, `DateOnly`, `TimeOnly`, `enum` | the value itself |
| `Int128`, `UInt128`, `Half`, `DateTimeOffset`, `BigInteger` | canonical text (`string`) |
| any custom primitive (e.g. `Ulid`) | canonical text (`string`) |

`DateTimeOffset` uses the `"O"` format specifically: the default `ToString()` drops sub-second ticks, so a
round-trip would silently truncate the value. `"O"` preserves both the ticks and the offset. The same
format is used for `DateTime` and `TimeOnly` everywhere their canonical text is written, for the same
reason.

A `Prefix` does **not** appear in the payload; it belongs to the text representation, not to the data.
MessagePack and BSON follow the same rule for primitives with a native representation, but not for the
text-form ones (`BigInteger`, `Int128`, `UInt128`): those are written as the id's `ToString()`, which
carries the prefix.

### Failure and null

- A payload rejected by the id's `Validator` throws `ProtoBuf.ProtoException`, not `ArgumentException`. The
  generated conversion operator calls `TryCreate` and throws the framework's own exception type, because
  protobuf-net wraps anything else and the caller loses the reason.
- A `null` reference-type id round-trips as `null`. protobuf-net writes a root-level `null` as an empty
  payload and reads it back as a surrogate with a `null` field, so the generated operator maps both shapes to
  `null`. For a reference-type id the surrogate's field is always nullable, including when the primitive is a
  value type: a non-nullable field would make `null` and the primitive's default value (`Guid.Empty`, `0`, the
  first enum member) produce the same empty payload, and `null` would silently come back as that default.
- An id whose wrapped primitive is `null` (a `string`-backed id or one backed by a custom `class`) round-trips
  as `default`, whether the id itself is a `record struct` or a `record class`. The empty payload is what the
  write side produces for such an id, so the read side maps it back instead of throwing; this matches
  System.Text.Json, Newtonsoft.Json, MessagePack and BSON.
- Ids nested in a generic container are skipped: the attribute argument would contain a type parameter
  (`CS0416`), and without the attribute the surrogate is dead code.
- If you already put `[ProtoContract]` on the id yourself, the generator skips it; attributes cannot be
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
list it returns without inserting another, so startup code that registers it from several modules, or a
configuration callback that runs twice, does not grow the provider list or rebuild the binder on every
request. Nullable ids are covered too: `[FromQuery] OrderId?` has CLR type `Nullable<OrderId>`, which
implements no interfaces at all, so the provider unwraps it before the interface check. Without that step the
parameter would fall to MVC's `TryParseModelBinder`, which parses with `ValueProviderResult.Culture` (the
invariant culture for query strings and routes, the current culture for forms), and the same form payload
would then bind differently for a nullable and a non-nullable parameter on the same route.

Binding behaviour, in the three cases that matter:

| Input | Result |
| --- | --- |
| Valid text | Bound through `TryParse` with the invariant culture; a `Prefix` is honoured by the id's own read mode |
| Missing key, or an empty value | Left unbound and **no** model error, so a nullable parameter stays `null` and a non-nullable one gets the framework's usual "required value" handling |
| Text the id rejects | One model error, `The value '<text>' is not a valid strongly typed id.`, which `[ApiController]` turns into a 400 |

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
`TryParse(string?, IFormatProvider?, out TSelf)`, which every id already has through `IParsable<TSelf>`, so
route and query parameters bind out of the box:

```csharp
// "ABC123" from the route is bound into OrderId via IParsable<OrderId>.TryParse;
// malformed text yields 400 before the handler runs.
app.MapGet("/orders/{id}", (OrderId id) => Results.Ok(id.Value));

// [AsParameters] binds each id property from the matching route / query value.
app.MapGet("/orders", ([AsParameters] OrderQuery query) => Results.Ok(query.Id.Value));

public record OrderQuery(OrderId Id);
```

No `BindAsync`, no `TryParse` shim and no DI registration are needed; the framework finds the static
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

**Both sides are ids.** Nothing to do, because AutoMapper assigns members of the same type directly:

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

The conversion survives EF Core's expression translation, so `ProjectTo` stays a server-side query and the
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
object schema with a single required `value` property, matching the `{ "value": ... }` payload; the
framework builds an empty schema for it, because it cannot see through the custom converter.

The mechanism differs from Swashbuckle's: this one is an `IOpenApiSchemaTransformer` added through
`AddSchemaTransformer`, so it rewrites only `type` and `format` on a schema the framework has already built
and leaves everything else (description, `nullable`, `required`, examples) as it was. Because the framework
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
API (`IOpenApiSchemaTransformer`, `OpenApiSchemaTransformerContext` and `AddSchemaTransformer`) arrives in
9.0.0, so a transformer generated against 8.0.x fails to compile with `CS0246`; the gate cannot be lowered.
`net8.0` has no built-in document generation, so use the [Swashbuckle](#swashbuckle) integration there; it
maps the same ids to the same `type` / `format` pairs.

<a id="supported-versions"></a>

## Supported versions

Each integration is gated on a major version, and that gate is a promise: "this version or newer works".
The table records what the promise rests on. **Minimum** is the gate in the generator. **Verified** lists
the versions a probe actually compiled against and round-tripped, either in the regular test projects or
in `test/Len.StronglyTypedId.IntegrationMatrix`.

| Integration | Minimum | Verified |
| --- | --- | --- |
| System.Text.Json | any | the framework's own version on `net8.0` and `net10.0` |
| Newtonsoft.Json | 13.0.0 | 13.0.4 |
| Entity Framework Core | 7.0.0 | 8.0.32, 10.0.13 |
| Dapper | 2.0.0 | 2.1.89 |
| Swashbuckle | 6.0.0 | 6.9.0, 7.2.0, 10.2.3 |
| MessagePack | 2.0.0 | 2.5.302, 3.1.12 |
| MongoDB.Bson | 3.0.0 | 3.4.0, 3.12.0 |
| HotChocolate | 13.0.0 | 13.9.14, 14.3.0, 15.1.8 |
| protobuf-net | 3.0.0 | 3.2.30 |
| ASP.NET Core MVC | 2.0.0 | the framework's own version on `net8.0` and `net10.0` |
| ASP.NET Core OpenAPI | 9.0.0 | 10.0.13 |

The matrix probe runs in CI for the newest major of each integration, so a breaking change upstream turns
the build red before it reaches a user. Swashbuckle 6 and 7 depend on `Microsoft.OpenApi` 1.x while
Swashbuckle 10 and ASP.NET Core OpenAPI 9+ depend on 2.x; the two cannot be in one compilation, so the
probe covers them as separate matrix entries.

[<- Back to the index](index.md) · [中文](zh-CN/integrations.md)
