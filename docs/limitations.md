---
layout: default
title: Limitations
---

<a id="limitations"></a>

# Limitations

Four things the generator cannot do for you. Each has a workaround, and none of them is a bug: they
follow from what a source generator is allowed to touch.

<a id="value-is-public"></a>

## `Value` is always public

The property comes from the positional record's primary constructor, and a source generator cannot change
the accessibility of a member you declared. Making it `internal` would mean hand-writing the type and
giving up the one-line declaration that is the point of this library.

Treat `Value` as the escape hatch it is: reach for it in persistence mappings and interop, keep it out of
domain signatures. When you need a hard boundary, declare the id `internal` and expose only the operations
your application actually needs.

`STIA0014` flags externally visible ids whose validator `new` can bypass. It is off by default: the
trade-off is acceptable in most codebases, and the diagnostic is noisy wherever tests construct ids
directly.

<a id="primitive-types"></a>

## Only the listed primitives are supported by default

The built-in list is closed and lives in
[Supported primitive types](https://github.com/LenFon/StronglyTypedId#supported-primitive-types).
It is extensible, though: a type of your own is accepted when it implements `IParsable<TSelf>`,
`IComparable<TSelf>` and the non-generic `IComparable`, and exposes public static `Parse` / `TryParse`.
See [Custom primitives](https://github.com/LenFon/StronglyTypedId#custom-primitives). Anything that does
not satisfy that contract is rejected by `STIA0008`.

`Uri` is deliberately absent even though it satisfies the contract. `Uri.Equals` ignores the fragment and
normalises case and default ports, so two different URIs would compare equal and silently collapse into the
same id, which is the bug a strongly typed id exists to prevent. Wrap such a value in a supported primitive
(usually `string`) and validate the format instead:

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
`TryParse` enforce them. See [Value normalization](declaration.md#value-normalization). A source generator
cannot inject code into a constructor you declared.

In practice this rarely matters, because everything crossing a boundary already goes through the generated
entry points: JSON, EF Core, Dapper, MVC binding and user input all end up in `Create` or `TryParse`.
Construction inside your own domain code is the one path that skips validation, which is usually what you
want, since that is where the invariant is known to hold.

<a id="generic-containers"></a>

## An id inside a generic container gets no external-reference integrations

`Outer<T>.InnerId` is generated, but it cannot be wired up automatically: an attribute argument may not
reference a converter that carries the container's type parameter (CS0416), so `[JsonConverter]` cannot be
emitted, and the `AddStronglyTypedIds` entry points skip it because an open generic type cannot
be passed as a type argument. The converter classes themselves are generated; register the one for the
closed type you use by hand, see [Nested types](declaration.md#nested-types).

`[DebuggerTypeProxy]` is skipped for the same reason, and unlike the converters its proxy class is not
generated either: it is private, so there is no way to wire it up by hand, and the id simply shows its
record members when expanded in the debugger.

[<- Back to the index](index.md) · [中文](zh-CN/limitations.md)
