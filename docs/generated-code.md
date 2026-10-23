---
layout: default
title: Generated code
---

# Generated code

The generator emits a `partial` declaration in the same namespace as the id (nested ids are declared
back inside their containing types — see [Nested types](declaration.md#nested-types)), and your own
declaration is never modified. **Which** files appear depends on what the compilation references:

<!-- BEGIN GENERATED:generated-files -->
| Condition in the compilation                                                                                       | Generated files                                                          |
| ------------------------------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------ |
| Always                                                                                                             | `<Namespace>.<TypeName>.g.cs`                                            |
| `System.Text.Json` is referenced                                                                                   | `…SystemTextJson.g.cs`, `StronglyTypedIds.SystemTextJson.g.cs`           |
| `Newtonsoft.Json` ≥ 13.0.0 is referenced                                                                           | `…NewtonsoftJson.g.cs`, `StronglyTypedIds.NewtonsoftJson.g.cs`           |
| `Microsoft.EntityFrameworkCore` ≥ 7.0.0 is referenced                                                              | `…EntityFrameworkCore.g.cs`, `StronglyTypedIds.EntityFrameworkCore.g.cs` |
| `Swashbuckle.AspNetCore.SwaggerGen` ≥ 6.0.0 is referenced                                                          | `StronglyTypedIds.Swagger.g.cs`                                          |
| `Dapper` ≥ 2.0.0 is referenced                                                                                     | `…Dapper.g.cs`, `StronglyTypedIds.Dapper.g.cs`                           |
| `MessagePack` ≥ 2.0.0 is referenced                                                                                | `…MessagePack.g.cs`, `StronglyTypedIds.MessagePack.g.cs`                 |
| `MongoDB.Bson` ≥ 3.0.0 is referenced                                                                               | `…MongoDb.g.cs`, `StronglyTypedIds.MongoDb.g.cs`                         |
| `Microsoft.AspNetCore.OpenApi` ≥ 9.0.0 is referenced                                                               | `StronglyTypedIds.AspNetCoreOpenApi.g.cs`                                |
| `Microsoft.AspNetCore.Mvc` ≥ 2.0.0 is referenced (or its `Microsoft.AspNetCore.Mvc.Core` / `.Abstractions` facade) | `StronglyTypedIds.AspNetCoreMvc.g.cs`                                    |
<!-- END GENERATED:generated-files -->

The rest of this page covers what each generated member actually does. The [README](https://github.com/LenFon/StronglyTypedId#what-gets-generated)
carries the same inventory without the file names, as a one-glance list.

## The core implementation

- Implementation of `IStronglyTypedId<TSelf, TPrimitiveId>`, `IParsable<TSelf>`, `ISpanParsable<TSelf>`,
  `IComparable<TSelf>`, `IEqualityOperators<TSelf, TSelf, bool>` and
  `IComparisonOperators<TSelf, TSelf, bool>`.
- `IFormattableStronglyTypedId<TSelf>` (which extends `IFormattable`) and `ISpanFormattable` as well,
  emitted per primitive: `string` implements neither, and enum-backed ids skip both because `System.Enum`
  inherits them with `[Obsolete]` members. The marker interface exists so generic code can format an id
  without naming its primitive: `static string Render<TId>(TId id) where TId : IFormattableStronglyTypedId<TId>`.
  `IStronglyTypedId<TSelf>` deliberately stays free of `IFormattable`, so it still accepts every id.
- `IUtf8SpanFormattable` (available since net8.0) and `IUtf8SpanParsable<TSelf>`, also per primitive:
  `Guid` implements the latter only from .NET 10, and `BigInteger` is excluded even though it implements
  it, because its `TryParse` is an explicit interface implementation that cannot be called by type name.
  Together they provide `ReadOnlySpan<byte>` `Parse` / `TryParse` and `TryFormat`.
- A `[DebuggerDisplay("{Value}")]` attribute, so the debugger shows the wrapped value rather than the
  record's expanded form.
- A static `Create(TPrimitiveId value)` factory. With a `Validator` configured it refuses an unacceptable
  value by throwing `ArgumentException` — `The value '…' is not a valid OrderId.` — or the `ExceptionType`
  you pointed it at, so an invalid id cannot be constructed through it.
- A static `TryCreate(TPrimitiveId value, out TSelf result)` factory — the non-throwing twin of `Create`:
  it returns `false` and sets `result` to `default` instead of throwing.
- A `public static readonly TSelf` field per `[StronglyTypedIdInstance]`, initialized through `Create`.
- `Deconstruct(out TPrimitiveId value)`, so `id.Deconstruct(out var value);` unwraps the primitive.
  (A single-element `var (value) = id;` is not legal C# — deconstruction declarations need at least two elements.)
- `Parse` / `TryParse` for both `string` and `ReadOnlySpan<char>`, delegating to the primitive type. A
  `null` `provider` means the **invariant culture**, not the current one, so `Parse(id.ToString())` always
  round-trips regardless of the ambient culture; pass an explicit provider for culture-specific input.
  For `string`-backed ids, `null` and empty strings are not accepted by `TryParse`. `Parse` reports
  malformed text with `FormatException` (`Could not parse supplied value.`), following the
  `IParsable<TSelf>` convention rather than `ArgumentException`.
- `CompareTo`, plus the `<`, `>`, `<=` and `>=` operators over the wrapped value. On a `record` id the
  parameter and both operands are nullable and `null` sorts lowest: `CompareTo(null)` returns `1` and
  `null < id` is `true`. The operators delegate to a private `CompareValues` helper, so a `null` operand
  never dereferences `Value`.
  A `string`-backed id compares **ordinally** (`string.CompareOrdinal`), not with
  `string.CompareTo(string)`: the latter is culture-sensitive, so it disagrees with the record's
  `Equals` — `"a\u00ADb"` and `"ab"` compare equal yet are not equal — and the ordering of `"I"` and
  `"i"` flips between cultures. Because `SortedSet<T>` and `SortedDictionary<TKey, TValue>` use
  `CompareTo` to detect duplicates, a culture-sensitive comparison silently drops elements there. Ordinal
  comparison keeps ordering consistent with equality and with the invariant text the serializers write.
- `ToString()` returning the invariant-culture text of the wrapped value (not the record's
  `OrderId { Value = … }` form), so it matches what the serializers write and round-trips through
  `Parse(id.ToString())`; use `ToString(string?, IFormatProvider?)` and
  `TryFormat(...)` (where the primitive supports them) for culture-specific formatting.
  A non-empty `format` is forwarded to the primitive and the text keeps its shape otherwise: on a
  `Guid`-backed id `ToString("N")` yields the dash-less GUID, and on a prefixed id the prefix is still
  there (`ord_d8ac85d4…`), so formatting never falls back to the bare primitive.
  String interpolation is **not** `ToString()`: `$"{id}"` goes through
  `IFormattable.ToString(null, CultureInfo.CurrentCulture)` and therefore follows the ambient culture
  (a `decimal`-backed id prints `1234,5` under de-DE), while `id.ToString()` does not.
- A nested `XxxTypeConverter` (`[TypeConverter]` attribute) when TypeConverter is enabled per type,
  round-tripping between string and the id through `TypeDescriptor`: `ConvertFrom` runs `Parse` with the
  invariant culture, `ConvertTo` runs `ToString()`, and converting a `null` id to `string` yields `null`.
  A value that is not a string — including `null` — is rejected with `NotSupportedException`.

## Enum-backed ids

Enum-backed ids keep `IComparable<TSelf>`, `IParsable<TSelf>` and `ISpanParsable<TSelf>` (parsing uses
`Enum.TryParse<TEnum>`), but deliberately skip `IFormattable`/`ISpanFormattable` because `Enum`'s
`ToString(string, IFormatProvider)` is marked obsolete — their `ToString()` simply returns the
underlying value's text.

`Enum.TryParse` accepts any numeric text, so the generated `TryParse` tightens it: a plain enum must name a
defined member (`Enum.IsDefined`), while a `[Flags]` enum may combine members but must not set a bit that no
member occupies — `Read | Write` parses, `999` does not. The bit mask is computed at compile time from the
members' constants and trimmed to the enum's underlying type, so it costs one bitwise-and at run time; an enum
that defines every bit (typically `All = -1`) gets no check at all.

Beyond that check the semantics are `Enum.TryParse`'s own: surrounding whitespace and numeric text are
accepted as long as the resulting value passes the check above, comma-separated combinations are accepted
for `[Flags]` enums, and member names are matched **case-sensitively**. So `" 1 "` and `"01"` parse when
`1` is a defined member, and `"Read, Write"` parses on a flags enum; `ToString()` is what writes the
canonical member name.

## The integration entry point

The integration entry points are emitted into a single generated class —
`internal static partial class StronglyTypedIds` in the `Len.StronglyTypedId` namespace — so both
`ApplyTo` overloads live side by side no matter how many ids the project declares. Add
`using Len.StronglyTypedId;` (already required for the attribute itself) to call them.

[<- Back to the index](index.md) · [中文](zh-CN/generated-code.md)
