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
    StronglyTypedIds.ApplyTo(configurationBuilder);

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
    StronglyTypedIds.ApplyTo(modelBuilder);
}
```

> Both overloads are generated whenever EF Core ≥ 7.0.0 is referenced — the same gate as every other
> integration, with no requirement that a `DbContext` override `ConfigureConventions`. `ModelConfigurationBuilder`
> only exists from EF Core 7, which is why the version floor is higher here than elsewhere.
>
> `ApplyTo(ModelBuilder)` scans the model as it stands at the moment of the call, so entity types configured
> after it will not be picked up: call it at the **end** of `OnModelCreating`. The conventions overload has no
> such ordering constraint and is the better default.

Each id gets a `ValueConverter<{Id}, {Primitive}>` nested in the generated `StronglyTypedIds` class, named
`{TypeName}Converter`. If two ids with the same short name exist in different namespaces, the converter
names would collide inside that single class, so only then are they disambiguated as
`{Namespace_With_Underscores}_{TypeName}Converter` (for example `Domain_OrderIdConverter`). The converters
are implementation details of `ApplyTo`, generated as private nested types — you never reference them by
name, and no action is required from you in either case.

Because the converter's provider type is the primitive, the column type and the schema EF Core builds are
the ones the primitive would have had on its own: introducing the id migrates nothing, and existing columns
keep working. Reading a row back goes through `Create`, so the id's validators and normalizers run on every
materialisation — a stored value the validator rejects throws instead of producing an id that could never
have been constructed in code.

> `ApplyTo(ModelBuilder)` walks the model as it stands at the moment of the call, so invoke it after your
> entities and `IEntityTypeConfiguration`s have been registered; entities added later are not scanned.

<a id="swashbuckle"></a>

## Swashbuckle

Map every strongly typed id to its underlying primitive schema (Swashbuckle.AspNetCore) in Swagger:

```csharp
services.AddSwaggerGen(options =>
{
    StronglyTypedIds.ApplyTo(options);
});

// method groups work too:
services.AddSwaggerGen(StronglyTypedIds.ApplyTo);
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

The table describes what the converters actually write, not what the CLR type is called: `decimal` and
the floating-point primitives are JSON numbers, an enum is its underlying number, and `Int128` /
`UInt128` / `BigInteger` have no bounded `int64` / `uint64` range to advertise, so they get a bare
`integer` with no `format`. An id with a `Prefix` is always `string` with no `format`, because its
canonical text form is text.

`MapType<T>` registers the schema for the id **type**, not for one endpoint, so there is nothing
per-endpoint to configure. Wherever the framework hands `ApiExplorer` the id type — a property on a DTO, a
response body, a model nested inside another, an MVC controller parameter — the same mapping applies.

One measured caveat, from `samples/Len.StronglyTypedId.Sample4` (`net8.0`, Swashbuckle 6.5.0): a
**Minimal API** route parameter is described by `ApiExplorer` as
`string` — the framework sees an `IParsable` type and reports it as text — so `MapType` never gets a chance
to apply, and that parameter's schema is a bare `type: string` with no `format`. Request and response
bodies, and DTO properties, are mapped as usual, and an MVC controller parameter is mapped too: it reaches
`ApiExplorer` as the id type (`samples/Len.StronglyTypedId.Sample3` renders `/api/products/{id}` as
`type: integer, format: int32`). The built-in [ASP.NET Core OpenAPI](#aspnetcore-openapi) integration does
not have this gap.

<a id="dapper"></a>

## Dapper

When `Dapper` ≥ 2.0.0 is referenced, a `TypeHandler` is generated for every strongly typed id, along with a
single entry point:

```csharp
using Dapper;

// Register TypeHandlers for all strongly typed ids. Dapper keeps them in a process-wide
// registry, so this only needs to run once per process.
StronglyTypedIds.ApplyTo();

connection.Execute("INSERT INTO Orders (Id) VALUES (@Id)", new { Id = OrderId.Create(Guid.NewGuid()) });
```

Each id's `TypeHandler` writes the wrapped primitive into the parameter, and reconstructs the id through
`Create` when reading. Ids with the same short name in different namespaces get a "namespace-underscored"
prefix on their `TypeHandler` to avoid collisions (the same disambiguation used for EF Core converters).

A `null` reference-type id is written as `DBNull.Value` rather than as a null object, so the column sees a
real SQL `NULL`. The handler is registered with `SqlMapper.AddTypeHandler<T>`, which keys it by the id
type, so anonymous parameter objects, `Query<T>` / `QueryAsync<T>` mapping and multi-mapping all pick it up
without per-call configuration.

Drivers rarely hand back the primitive's own CLR type — SQLite returns `long` for an enum column, `string`
for `char` / `Guid` and `double` for `decimal`. The generated `Parse` therefore converts the incoming value
to the primitive before calling `Create` (`System.Convert` for the numeric and date/time primitives,
`Enum.ToObject` / `Enum.Parse` for enums, and the primitive's `IParsable` entry point for `Guid`,
`DateTimeOffset`, `TimeSpan`, `DateOnly`, `TimeOnly`, `Int128`, `UInt128`, `Half` and `BigInteger`). Values
that already arrive as the primitive take a direct path, and `NULL` — either `DBNull` or a bare `null` —
maps to `default`. An enum column may come back as its member name or as the underlying number; both shapes
are accepted.

> Registration is global, not per connection. `ApplyTo(IDbConnection)` is still generated for source
> compatibility, but it ignores its argument and forwards to `ApplyTo()`. Calling either one more than once
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
var options = StronglyTypedIds.ApplyTo(MessagePackSerializerOptions.Standard);

var bytes = MessagePackSerializer.Serialize(OrderId.Create(Guid.NewGuid()), options);
var id    = MessagePackSerializer.Deserialize<OrderId>(bytes, options);
```

The returned options put the generated formatters in a `CompositeResolver` ahead of whatever resolver the
argument already carried, so your own custom formatters keep working — the generated ones only win for ids.

Each id's formatter is named `{TypeName}MessagePackFormatter` and lives in the generated `StronglyTypedIds`
class instead of being attached to the id. That is deliberate: `[MessagePackFormatter]` can only be applied
to a type declared in the current compilation, so an id that comes from a referenced library — the common
"library declares the id, application uses it" case — could never get one. Ids with the same short name in
different namespaces are disambiguated the same way as Dapper's handlers.

The payload is the wrapped primitive value, not the canonical text form: a text prefix decorates the text
form only, and a binary format has no text to decorate. Three primitives have no MessagePack formatter at
all — `BigInteger`, `Int128` and `UInt128` — so those ids fall back to the invariant-culture text form (a
MessagePack string) and are read back through `TryParse`. For them the prefix *does* travel in the payload,
because it is part of the text.

For every other primitive the bytes are exactly what the primitive itself serialises to, so an id field is
indistinguishable from a bare primitive field in an existing payload — swapping one for the other needs no
re-encoding.

A `null` reference-type id is written as `nil` and read back as `null`; a value-type id that receives `nil`
throws `MessagePackSerializationException` rather than silently becoming `default`, so a missing field cannot
be mistaken for a legitimate `Guid.Empty`.

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
per-collection, and calling it twice is harmless — an already registered serializer is left untouched.

Each id's serializer is named `{TypeName}BsonSerializer` and lives in the generated `StronglyTypedIds` class.
`[BsonSerializer]` would have been the shorter route, but it can only be applied to a type declared in the
current compilation, which would leave ids from a referenced library without a serializer.

The payload is the wrapped primitive value. `BigInteger`, `Int128` and `UInt128` have no BSON serializer, so
those ids are stored as a BSON string holding the invariant-culture text form (prefix included) and read back
through `TryParse`. A `null` reference-type id is stored as BSON `null`; a value-type id that meets BSON
`null` throws `BsonSerializationException`.

For every other primitive the stored BSON value is the primitive's own, so existing documents stay readable
and an id field is indistinguishable from a bare primitive field. Because the serializer is registered by
type, LINQ filters and projections (`Builders<Order>.Filter.Eq(x => x.Id, id)`) serialise the id through it
too — there is nothing to configure per query.

<a id="aspnet-core-mvc"></a>

## ASP.NET Core MVC

When `Microsoft.AspNetCore.Mvc` ≥ 2.0.0 is referenced (on .NET 8 / .NET 10 the `Microsoft.AspNetCore.Mvc.Core`
and `Microsoft.AspNetCore.Mvc.Abstractions` assemblies also satisfy the gate), the generator emits a single
model-binder provider plus one route constraint per id, so strongly typed ids bind and validate without any
per-type boilerplate:

```csharp
using Microsoft.Extensions.DependencyInjection;

// Register the model-binder provider and the per-id route constraints.
builder.Services.AddControllers(options => StronglyTypedIds.ApplyTo(options));
builder.Services.AddRouting(options => StronglyTypedIds.ApplyTo(options));
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
providers. Nullable ids are covered too: `[FromQuery] OrderId?` has CLR type `Nullable<OrderId>`, which
implements no interfaces at all, so the provider unwraps it before the interface check. Without that step the
parameter would fall to MVC's `TryParseModelBinder`, which parses with the **current culture** — the same
payload would then bind differently for a nullable and a non-nullable parameter on the same route.

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

Route constraints are shared with MVC: the generated `ApplyTo(RouteOptions)` registers one constraint per id,
so `{id:OrderId}` works in Minimal API templates too (the `Microsoft.AspNetCore.Mvc.*` assemblies ship in the
ASP.NET Core shared framework, so the gate is satisfied in a Minimal API project):

```csharp
builder.Services.AddRouting(options => StronglyTypedIds.ApplyTo(options));

app.MapGet("/orders/v2/{id:OrderId}", (OrderId id) => Results.Ok(id.Value));
```

<a id="aspnetcore-openapi"></a>

## ASP.NET Core OpenAPI

When `Microsoft.AspNetCore.OpenApi` ≥ 9.0.0 is referenced, OpenAPI schema mappings are generated for every
strongly typed id, in parallel to Swashbuckle's `ApplyTo(SwaggerGenOptions)`:

```csharp
builder.Services.AddOpenApi(options =>
{
    StronglyTypedIds.ApplyTo(options);
});
```

Each id is mapped to the schema of its underlying primitive, with the same `format` values as in the
[Swashbuckle](#swashbuckle) section.

The mechanism differs from Swashbuckle's: this one is an `IOpenApiSchemaTransformer` added through
`AddSchemaTransformer`, so it rewrites only `type` and `format` on a schema the framework has already built
and leaves everything else — description, `nullable`, `required`, examples — as it was. Because the framework
keeps that schema as a named component, an id in a body shows up as `$ref: '#/components/schemas/OrderId'`
rather than as an inline object. Swashbuckle's `MapType` instead replaces the schema for the type outright,
with an inline one that carries only `type` and `format`.

The two also differ in where they reach. Measured with `samples/Len.StronglyTypedId.Sample2` (`net10.0`,
`Microsoft.AspNetCore.OpenApi` 10.0.13), route and query parameters do get `type: string, format: uuid`.
Swashbuckle on `net8.0` (`Sample4`) leaves a **Minimal API** route parameter as a bare `type: string`,
because `ApiExplorer` reports an `IParsable` parameter as `string` and `MapType` is keyed on the CLR type —
see the [caveat](#swashbuckle) in that section. Neither integration touches a schema that is not an id.

> This integration is independent of Swashbuckle: either, both, or neither may be present, and each generates
> its own `ApplyTo` overload.

### Why 9.0 and not 8.0

`Microsoft.AspNetCore.OpenApi` 8.0.x ships only the `WithOpenApi()` annotation helpers. The schema-transformer
API — `IOpenApiSchemaTransformer`, `OpenApiSchemaTransformerContext` and `AddSchemaTransformer` — arrives in
9.0.0, so a transformer generated against 8.0.x fails to compile with `CS0246`; the gate cannot be lowered.
`net8.0` has no built-in document generation, so use the [Swashbuckle](#swashbuckle) integration there — it
maps the same ids to the same `type` / `format` pairs. `samples/Len.StronglyTypedId.Sample4` pins the package
to 8.0.0 and is the runnable proof of that fallback: the package resolves, the app starts, and
`/swagger/v1/swagger.json` carries the mapped schemas.

[<- Back to the index](index.md) · [中文](zh-CN/integrations.md)
