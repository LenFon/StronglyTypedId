---
layout: default
title: Declaring an id
---

# Declaring an id

<a id="the-declaration"></a>

## The declaration

An id is a `partial` record — or `partial record struct` — with exactly one positional parameter named
`Value`:

```csharp
[StronglyTypedId]
public partial record struct OrderId(Guid Value);
```

`record struct` produces a value type: it cannot be `null`, and interface calls do not box. `record`
produces a reference type, which can be `null` — a `default` id is a null reference rather than a
zero-filled value. Both are structurally comparable, both work as dictionary keys, and both expose exactly
the same generated members. Pick by nullability and allocation expectations, not by feature set.

The parameter's type selects the wrapped primitive; the list is closed and lives in
[Supported primitive types](https://github.com/LenFon/StronglyTypedId#supported-primitive-types). Its
nullability annotation may stay in whatever `#nullable` context your project uses, but the type itself may
not be nullable — `OrderId(Guid? Value)` is rejected by the analyzer (`STIA0006`).

Your declaration is never rewritten: the generator emits a sibling `partial` in the same namespace (or
back inside the containing type, for [nested ids](#nested-types)). The full list of shape rules, each with
its diagnostic, is in [Troubleshooting](troubleshooting.md#nothing-was-generated).

<a id="converting-an-existing-record"></a>

## Converting an existing record

If you already have a record that wraps a single value, you do not have to retype it. Put the caret on the
record's declaration header — the line with `record OrderId(...)`, not inside its body — and open the
refactoring menu (the light bulb; `Ctrl+.` in Visual Studio, `Ctrl+Shift+R` in VS Code):

- **Convert to strongly typed id** adds `[StronglyTypedId]`, the `partial` modifier and
  `using Len.StronglyTypedId;`, and nothing else. The new attribute goes above any existing ones, which keep
  their place; an existing `using` is not duplicated; the file's own line endings are preserved.
- **Inline strongly typed id** is the reverse. It removes `[StronglyTypedId]` and `partial`, and drops
  `using Len.StronglyTypedId;` when nothing else in the file needs it — neither another id nor any other
  type from that namespace, such as `IStronglyTypedId<,>` or `StronglyTypedIdIntegrations`. The generated
  members disappear along with the attribute, leaving the plain record you started from.

The forward action is offered only when the result would be a valid id, so it never produces a declaration
that immediately fails the analyzer: exactly one positional parameter, named `Value`, of a
[supported primitive type](https://github.com/LenFon/StronglyTypedId#supported-primitive-types), and the
record is neither `abstract` nor generic. A record with no primary constructor, with several parameters,
with a differently named parameter, or wrapping an unsupported type gets no action.

The reverse action is withheld when the id is declared across several `partial` blocks: removing `partial`
from one of them would be `CS0260`.

Both actions identify `[StronglyTypedId]` by what the name binds to, not by how it is spelled. A
third-party `StronglyTypedIdAttribute` from another namespace — even written with the short name after a
`using` — is left untouched, so "Inline" cannot delete someone else's attribute.

<a id="value-validation"></a>

## Value validation

The generator constrains the *shape* of an id, not the *values* it may hold. Point `Validator` at a
static method of the id itself, and the generated entry points will refuse invalid values:

```csharp
[StronglyTypedId(Validator = nameof(Validate))]
public partial record struct OrderId(Guid Value)
{
    private static bool Validate(Guid value) => value != Guid.Empty;
}

OrderId.Create(Guid.Empty);                            // throws ArgumentException
OrderId.TryParse(Guid.Empty.ToString(), null, out _);  // false, no exception
```

The method must be a static member of the id with the signature `static bool Validate(TPrimitiveId value)`,
returning `true` for an acceptable value. It is called from `Create`, `TryCreate` and every `TryParse`
overload (string, span and UTF-8), so `TryParse` keeps its contract and returns `false` instead of throwing.
The JSON converters and the Entity Framework Core integration both validate on read: EF Core constructs
through `Create`, while the two JSON converters construct through `TryCreate` and rethrow the rejection as
their own framework exception. Deserialization therefore rejects invalid values as well.

One thing a source generator cannot do is intercept your primary constructor: `new OrderId(...)` bypasses
the validator. Route values through `Create` (or the parsing members) when they come from outside your
code.

Leaving `Validator` unset emits no validation code at all — the generated members then behave exactly as
if the property did not exist.

### Choosing the exception type

`Create` throws `ArgumentException` when the validator rejects a value. A domain library that wants its own
error type can point `ExceptionType` at it:

```csharp
public sealed class DomainRuleViolationException(string message) : Exception(message);

[StronglyTypedId(Validator = nameof(Validate), ExceptionType = typeof(DomainRuleViolationException))]
public partial record struct OrderId(Guid Value)
{
    private static bool Validate(Guid value) => value != Guid.Empty;
}

OrderId.Create(Guid.Empty);   // throws DomainRuleViolationException
```

The type must derive from `System.Exception`, must not be abstract, and must expose an accessible constructor
taking a single `string` message; anything else is reported as [STIA0018](diagnostics.md#diagnostics) at the declaration.
Only `Create` uses it — `Parse` throws `FormatException` instead, following the `IParsable<TSelf>` convention
(the same as `int.Parse` / `Guid.Parse`), because its failure also covers text that is not an id at all.
Like the other options it can be set for the whole assembly with
`[assembly: StronglyTypedIdDefaults(ExceptionType = typeof(...))]`.

### Keeping rejected values out of the message

The rejection message interpolates the value that failed validation:
`The value 'alice@example' is not a valid EmailAddress.` That text travels with the exception into logs,
APM traces and error reports — and for an id wrapping `string` it is user input, often rejected precisely
because it is malformed, but still personal data. Set `RedactValidationMessage` to drop it:

```csharp
[StronglyTypedId(Validator = nameof(IsEmail), RedactValidationMessage = true)]
public partial record struct EmailAddress(string Value)
{
    private static bool IsEmail(string value) => value.Contains('@');
}

EmailAddress.Create("alice@example");   // The value is not a valid EmailAddress.
```

The default is `false`, so existing messages are unchanged. `[assembly: StronglyTypedIdDefaults(
RedactValidationMessage = true)]` turns it on for a whole assembly; a type-level `false` cannot undo that,
because redaction is a safety policy rather than a per-type preference. `Parse` already reports
`Could not parse supplied value.` without echoing the input, and the ASP.NET Core model binder echoes the
raw value into `ModelState` — that message is shown back to the caller who sent it, not written to logs.

<a id="value-normalization"></a>

## Value normalization

`Validator` can only *reject* a value. Point `Normalizer` at a static method of the id and the generated
entry points will *repair* it first — trim whitespace, fold case, drop separators — then validate, then
construct:

```csharp
[StronglyTypedId(Normalizer = nameof(Normalize), Validator = nameof(Validate))]
public partial record struct EmailAddress(string Value)
{
    private static string Normalize(string value) => value.Trim().ToLowerInvariant();

    private static bool Validate(string value) => value.Contains('@');
}

EmailAddress.Create("  Alice@Example.COM  ").Value;  // "alice@example.com"
```

The method must be a static member of the id with the signature `static TPrimitiveId Normalize(TPrimitiveId value)`.
The order is fixed and it matters: **normalize → validate → construct**. Validation therefore sees the value
that actually gets stored, and `"  a@b.com  "` is accepted instead of rejected. `Create`, `TryCreate`, `Parse`
and every `TryParse` overload (string, span and UTF-8) go through it.

The normalizer runs exactly once per entry point — the result is kept in a local rather than re-invoked, so a
normalizer that appends a prefix cannot apply it twice.

Leaving `Normalizer` unset emits no normalization code at all. Like `Validator`, it can also be set for the
whole assembly with `[assembly: StronglyTypedIdDefaults(Normalizer = "MyNormalizers.Canonical")]`.

<a id="named-instances"></a>

## Named instances

Some ids have values the domain talks about by name — an unspecified tenant, a system account, an
anonymous user. Declare them with `[StronglyTypedIdInstance]` (repeatable) and the generator emits one
`public static readonly` field per instance:

```csharp
[StronglyTypedId]
[StronglyTypedIdInstance("Unspecified", "-1")]
[StronglyTypedIdInstance("Anonymous", "0")]
public partial record struct TenantId(int Value);

TenantId.Unspecified.Value;                     // -1
TenantId.Unspecified == TenantId.Create(-1);    // true
```

The value is the **primitive** value in its invariant text form, **without** the `Prefix`, because a prefix
decorates the canonical text form while an instance carries a primitive value. It is checked at compile time
and, for most primitives, translated into a literal or a constructor call, so nothing is parsed at run time.
The exceptions are `Int128`, `UInt128` and `BigInteger` — the generator runs on `netstandard2.0`, where those
types do not exist, so it validates the text by hand and emits a run-time `Parse` of the already-checked
string — and custom primitives, whose `Parse` cannot be executed at compile time at all.

Most primitives accept exactly what their own `Parse` accepts, so `[StronglyTypedIdInstance("X", " 1 ")]` is
fine for an `int`. A few accept a stricter form, because their `Parse` is loose enough to make the
declaration ambiguous — `"2020-1-1"` and `"2020-01-01"` are the same day, and accepting both would let the
same instance be written two ways:

| Primitive | Accepted form | Rejected although `Parse` accepts it |
|---|---|---|
| `Int128`, `UInt128`, `BigInteger` | optional sign, then digits only | surrounding whitespace (`" 1 "`) |
| `DateOnly` | exactly `yyyy-MM-dd` | `"2020-1-1"`, `"2020/01/01"` |
| `TimeOnly` | `HH:mm`, `HH:mm:ss`, or `HH:mm:ss.f`–`fffffff` | `"1:02"` (single-digit hour) |
| `Half` | anything `float` accepts, below the 65520 overflow midpoint | `"65520"` (rounds to infinity), `"1,000"` |
| `float`, `double` | exponents, `NaN`, `Infinity`; no thousands separators | `"1,000"` |

Everything else follows its primitive: `char` takes exactly one character, `decimal` accepts thousands
separators, `Guid` / `DateTime` / `DateTimeOffset` / `TimeSpan` take their usual flexible forms, and an enum
takes a member name, a numeric value, or a comma-separated combination of members.

Every instance is built through `Create`, and that is what makes the guarantee hold: an instance is never
a privileged value, so it goes through the same `Normalizer` and `Validator` as any other value, and
`Parse(instance.ToString())` always reads back. A value the validator rejects is not caught at compile
time; the type initializer throws `TypeInitializationException` on first use instead, so a broken instance
fails loudly rather than quietly handing out an id that cannot exist.

Names must be valid identifiers, unique within the id, and must not collide with the id type itself or
with an existing or generated member (`STIA0020`); values must be valid for the primitive (`STIA0021`);
a name that is not PascalCase is reported as `STIA0022` (`Info`).

<a id="trycreate-and-utf8"></a>

## TryCreate & UTF-8 interfaces

`Create` throws; `TryCreate` does not — it returns `false` on a failed parse and sets `result` to `default`:

```csharp
OrderId.TryCreate(Guid.NewGuid(), out var id);              // true
OrderId.TryCreate(badValue, out var invalid);               // false, result is default(OrderId)
```

`Guid` implements `IUtf8SpanParsable<Guid>` from .NET 10, so the generator also emits `IUtf8SpanFormattable`
(available since net8.0) and `IUtf8SpanParsable<GuidId>` for `GuidId`, together with `ReadOnlySpan<byte>`
`Parse` / `TryParse`. `string`-backed ids emit neither (because `string` implements neither):

```csharp
var utf8 = Encoding.UTF8.GetBytes(orderId.Value.ToString());
var same = OrderId.Parse(utf8, CultureInfo.InvariantCulture); // IUtf8SpanParsable<TSelf>
```

<a id="type-converter"></a>

## TypeConverter

By default only JSON converters are generated. When you need round-tripping through `TypeDescriptor` — config
binding, `XmlSerializer`, the Newtonsoft dictionary-key read path, and so on — enable it per type with
`TypeConverter = true`:

```csharp
[StronglyTypedId(TypeConverter = true)]
public partial record struct ConvertibleGuidId(Guid Value);
```

The generated nested `XxxTypeConverter` carries a `[TypeConverter(typeof(XxxTypeConverter))]` attribute, so
`TypeDescriptor.GetConverter(typeof(ConvertibleGuidId))` can convert between string and the id. Converting a
`null` id to `string` yields `null`; anything that is not a string is rejected with `NotSupportedException`.
Both directions use the invariant culture — matching the generated `ToString()` — so a value written under
one culture reads back under another.

> This converter covers only the "string ↔ id" path; it does not replace the JSON converters, which remain
> independent and active.

### Configuration binding

`Microsoft.Extensions.Configuration`'s binder converts a string to a property's type through
`TypeDescriptor`, so **an id without `TypeConverter = true` cannot be bound** — and it fails silently.
Binding an object whose property is such an id leaves the property at `default`: no exception, no warning,
and for a `Guid`-backed id that is `00000000-0000-0000-0000-000000000000` even though the configuration
holds a real value.

```csharp
public sealed class OrderSettings
{
    public OrderId Id { get; set; }   // needs [StronglyTypedId(TypeConverter = true)]
}

var settings = configuration.GetSection("Order").Get<OrderSettings>();
```

The same applies to the scalar overloads (`configuration.GetValue<OrderId>("Order:Id")`), which throw a
`NullReferenceException` from inside the binder rather than returning `default`. Enable the converter and
both shapes work, including the prefix: a prefixed id reads `"ord_3fa85f64-…"` and writes it back the same
way.

<a id="assembly-defaults"></a>

## Assembly-level defaults (StronglyTypedIdDefaults)

When many ids in an assembly share the same validator, set a default at the assembly level instead of repeating
it on each one:

```csharp
[assembly: StronglyTypedIdDefaults(Validator = "MyValidators.NonEmpty")]
```

`StronglyTypedIdDefaults.Validator` is only a **default**: an id that declares
`[StronglyTypedId(Validator = ...)]` keeps its own validator; the assembly default is used only when neither
the attribute nor the assembly default is set.

`Normalizer` follows the same precedence rule:

```csharp
[assembly: StronglyTypedIdDefaults(Normalizer = "MyNormalizers.Canonical")]
```

`ExceptionType` follows it too: `[assembly: StronglyTypedIdDefaults(ExceptionType = typeof(DomainException))]`
applies to every id that does not name its own.

Three more options can be set assembly-wide, for the cases where the whole assembly shares one choice:

```csharp
[assembly: StronglyTypedIdDefaults(
    JsonShape = JsonShape.Object,
    TypeConverter = true,
    PrefixReadMode = PrefixReadMode.Strict)]
```

| Option | Precedence | Notes |
| --- | --- | --- |
| `JsonShape` | per-type wins, including an explicit `JsonShape.Primitive` | An enum, so "not written" is distinguishable from "written as the default value" — a per-type value can override in **both** directions. |
| `PrefixReadMode` | per-type wins, including an explicit `PrefixReadMode.Lenient` | Same enum reasoning. Only decides how a prefixed id is read back; it does not supply a default prefix. |
| `TypeConverter` | **one-way**: the assembly default can turn it on, a per-type `false` cannot turn it off | The attribute property is `bool`, so "not written" and "written `false`" are indistinguishable in metadata. Same rule as `RedactValidationMessage`. |

> `Prefix` itself is deliberately **not** available at assembly level. An assembly-wide prefix would give
> every id in the assembly the same canonical text form, so two different id types could no longer be told
> apart by their text, and changing that prefix later would be a data-contract break for the whole assembly.
> (`STIA0023` is a different check: it reports a prefix that overlaps the primitive's own text.)

> The validator method must still satisfy the [Value validation](#value-validation) signature convention
> (`static bool Validate(TPrimitiveId value)`), otherwise [STIA0010](diagnostics.md#diagnostics) is reported. Likewise a
> normalizer must be `static TPrimitiveId Normalize(TPrimitiveId value)`, otherwise [STIA0017](diagnostics.md#diagnostics)
> is reported.

<a id="integrations-filter"></a>

## Turning integrations off

Every integration is generated only when the compilation references its package, but sometimes that is not
enough: a host application may reference EF Core for its own sake while a particular id should never get a
value converter. `Integrations` narrows the set **per id**:

```csharp
[StronglyTypedId(Integrations = StronglyTypedIdIntegrations.None)]
public partial record struct TenantId(Guid Value);          // core only: no EF, Dapper, JSON, Swagger…

[StronglyTypedId(Integrations = StronglyTypedIdIntegrations.Dapper | StronglyTypedIdIntegrations.SystemTextJson)]
public partial record struct OrderId(Guid Value);           // exactly these two
```

`Integrations` is a **filter, not a switch**: it can only remove integrations that the referenced packages
would otherwise enable, never add one whose package is missing. Requesting an integration whose assembly is
not referenced is reported as [STIA0019](diagnostics.md#diagnostics) so the mistake is not silent.

| Value | Effect |
| --- | --- |
| *(not set)* | Generate everything the referenced packages allow — the behaviour before this option existed |
| `None` | No integration code at all; the core implementation is unaffected |
| `Dapper \| SystemTextJson` | Only the listed integrations (still requires the packages) |
| `All` | Explicitly everything; useful to restore a single id under an assembly default of `None` |

The same option exists on `StronglyTypedIdDefaults`, and the usual precedence applies: the per-type value
wins, otherwise the assembly default, otherwise `All`.

```csharp
[assembly: StronglyTypedIdDefaults(Integrations = StronglyTypedIdIntegrations.None)]
```

> `TypeConverter` is **not** part of `Integrations`. It is an explicit opt-in
> (`TypeConverter = true`), not something a referenced package turns on, so `Integrations = None` leaves it
> alone.

> An id declared in a referenced assembly honours **its own** assembly-level default, not yours. The two
> options a consumer re-reads when it generates integration code are `Integrations` and `JsonShape`: if a
> domain library ships `[assembly: StronglyTypedIdDefaults(Integrations = None)]`, consumers stop generating
> integration code for its ids, and if it ships `JsonShape = JsonShape.Object`, the consumer's Newtonsoft
> converter and OpenAPI schema follow the object shape. `TypeConverter` and `PrefixReadMode` are baked into
> the id by the declaring assembly's core generator, so a consumer inherits them with the type and never
> regenerates them.

<a id="nested-types"></a>

## Nested types

A strongly typed id may be declared inside a class, struct, record or interface. Since all parts of a
`partial` type must share the same container, the generated declaration is **nested back into the
original containing types** rather than emitted at namespace level:

```csharp
public partial class OrderAggregate          // the container must be partial
{
    [StronglyTypedId]
    public partial record struct OrderId(Guid Value);
}

var id = OrderAggregate.OrderId.Create(value);   // used through its nested name, like any other type
```

The only prerequisite is that **every containing type can be reopened verbatim** by the generated code:
each level must be `partial` and not a `file`-local type. Violations are reported by
[STIA0009](diagnostics.md#diagnostics) (missing `partial`, or `file`-local), which comes with a code fix that adds `partial`.

**Generic containers.** The containing type may be generic (for example `Outer<T>`): the generator
reopens it with its type parameter list, so an id declared inside a generic container compiles and keeps
every capability the core generator produces — comparison and ordering, formatting and span parsing, and
System.Text.Json dictionary keys. JSON is the one exception: the converter classes are generated, but the
`[JsonConverter]` attribute cannot be emitted (an attribute argument may not reference a converter that
carries the container's type parameter — CS0416), and the `AddStronglyTypedIds` entry points
skip such ids because an open generic type cannot be passed as a type argument. Register the converter
yourself for the closed type you use:

```csharp
options.Converters.Add(new Outer<int>.InnerId.SystemTextJsonConverter());
```

The external-reference integrations — EF Core, Swagger, Dapper, MessagePack, MongoDB, HotChocolate,
protobuf-net, MVC and OpenAPI — skip ids declared inside generic containers for the same reason; for those,
declare the id at namespace level or in a non-generic container.

Plain (non-generic) nesting affects nothing: comparison and ordering, formatting and span parsing, both
JSON serializers and System.Text.Json dictionary keys all work as usual.

[<- Back to the index](index.md) · [中文](zh-CN/declaration.md)
