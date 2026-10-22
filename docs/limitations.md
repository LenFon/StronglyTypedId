---
layout: default
title: Limitations
---

<a id="limitations"></a>

# Limitations

Four things the generator cannot do for you. Each has a workaround, and none of them is a bug — they
follow from what a source generator is allowed to touch.

<a id="value-is-public"></a>

## `Value` is always public

The property comes from the positional record's primary constructor, and a source generator cannot change
the accessibility of a member you declared. Making it `internal` would mean hand-writing the type —
giving up the one-line declaration that is the point of this library.

Treat `Value` as the escape hatch it is: reach for it in persistence mappings and interop, keep it out of
domain signatures. When you need a hard boundary, declare the id `internal` and expose only the operations
your application actually needs.

`STIA0014` flags externally visible ids whose validator `new` can bypass. It is off by default: the
trade-off is acceptable in most codebases, and the diagnostic is noisy wherever tests construct ids
directly.

<a id="primitive-types"></a>

## Only the listed primitives are supported

Arbitrary custom types are not accepted — the list is closed and lives in
[Supported primitive types](https://github.com/LenFon/StronglyTypedId#supported-primitive-types).

`Uri` is deliberately absent. `Uri.Equals` ignores the fragment and normalises case and default ports, so
two different URIs would compare equal and silently collapse into the same id — exactly the bug a strongly
typed id exists to prevent. Wrap such a value in a supported primitive (usually `string`) and validate the
format instead:

```csharp
[StronglyTypedId(Validator = nameof(Validate))]
public partial record struct ResourceUri(string Value)
{
    private static bool Validate(string value) => Uri.TryCreate(value, UriKind.Absolute, out _);
}
```

<a id="primary-constructor"></a>

## The primary constructor is not guarded

`new OrderId(...)` bypasses both `Normalizer` and `Validator`; only `Create`, `TryCreate`, `Parse` and
`TryParse` enforce them — see [Value normalization](declaration.md#value-normalization). A source generator
cannot inject code into a constructor you declared.

In practice this rarely matters, because everything crossing a boundary already goes through the generated
entry points: JSON, EF Core, Dapper, MVC binding and user input all end up in `Create` or `TryParse`.
Construction inside your own domain code is the one path that skips validation — which is usually what you
want, since that is where the invariant is known to hold.

<a id="generic-containers"></a>

## An id inside a generic container gets no external-reference integrations

`Outer<T>.InnerId` is generated, but it cannot be wired up automatically: an attribute argument may not
reference a converter that carries the container's type parameter (CS0416), so `[JsonConverter]` cannot be
emitted, and the `ApplyTo` / `AddStronglyTypedId` entry points skip it because an open generic type cannot
be passed as a type argument. The converter classes themselves are generated — register the one for the
closed type you use by hand, see [Nested types](declaration.md#nested-types).

<a id="swashbuckle-minimal-api-parameters"></a>

## Swashbuckle does not reach Minimal API parameters

`MapType` is keyed on the CLR type, but a Minimal API handler's parameter reaches `ApiExplorer` as `string` —
the framework recognises `IParsable` and describes it as text — so a route or query parameter declared as an
id ends up as a bare `type: string` with no `format` in the Swagger document. Bodies and DTO properties are
unaffected, and an MVC controller parameter is fine: it reaches `ApiExplorer` as the id type, so
`/api/products/{id}` comes out as `type: integer, format: int32` for an `int`-backed id
(`samples/Len.StronglyTypedId.Sample3`, Swashbuckle 10). The Minimal API gap was measured on `net8.0` with
Swashbuckle 6.5.0 (`samples/Len.StronglyTypedId.Sample4`).

The built-in [ASP.NET Core OpenAPI](integrations.md#aspnetcore-openapi) integration does not have this gap:
its transformer inspects the schema the framework built for the parameter, which is why `/orders/{id}` comes
out as `type: string, format: uuid` there.

[<- Back to the index](index.md) · [中文](zh-CN/limitations.md)
