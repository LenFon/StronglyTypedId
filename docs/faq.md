---
layout: default
title: FAQ
---

<a id="faq"></a>

# FAQ

**Q: Which package should I install?**

A: Just `Len.StronglyTypedId`. It depends on `Len.StronglyTypedId.Generators`, which contains the
generator, the analyzer and the code fixes; the runtime library is what you reference from code. The other
four packages are optional. See [Packages](packages.md#packages).

**Q: Is the id usable as a `Dictionary`/`HashSet` key?**

A: Yes. `record` and `record struct` both generate structural equality, `GetHashCode`, `==` and `!=`
over the wrapped `Value`. Serializing a dictionary keyed by an id is supported as well. See
[Dictionary keys](serialization.md#serialization).

**Q: Can I order ids, or use them in a `SortedSet<T>`?**

A: Yes. Every id implements `IComparable<TSelf>` plus `<`, `>`, `<=` and `>=` over the wrapped value, so
`OrderBy`, `SortedSet<T>` and range checks work without a custom comparer. Reference-type ids treat
`null` as the smallest value, matching `Comparer<T>.Default`. A `string`-backed id compares ordinally,
so its ordering agrees with `Equals` and does not shift with the machine's culture. See
[generated code](generated-code.md).

**Q: What does `ToString()` print?**

A: The invariant-culture text of the wrapped primitive value (`"42"`, `"3f2c…"`, `"1.5"`), not the
record's default `OrderId { Value = 42 }` form, which keeps ids readable in logs and interpolated
strings. It is culture-independent, so it matches the serialized form and round-trips through
`Parse(id.ToString())`. `Parse` / `TryParse` treat a `null` provider as the invariant culture for exactly
this reason. Ids whose primitive implements `IFormattable` (every supported primitive except `string`
and enum-backed ids) also expose `ToString(string?, IFormatProvider?)` for culture-specific output, and
string interpolation (`$"{id}"`) routes through that member, so it follows the current culture instead of
staying invariant. For `string`- and enum-backed ids neither member exists, and interpolation falls back
to the invariant `ToString()`.

**Q: I set `Validator`, but `new OrderId(...)` still accepts an invalid value.**

A: The primary constructor belongs to your declaration, and a source generator cannot inject code into
it, so only the generated entry points are validated. JSON, EF Core and user input all come in through
`Create` / `Parse` / `TryParse`, where the validator does apply. See [Value validation](declaration.md#value-validation).

**Q: Why can't a strongly typed id be a `file`-local type?**

A: Because the generator has to reopen your `partial` declaration in a file of its own, and every part
of a file-local type must carry `file` itself and stay invisible outside its file, so there is no way to
write that second part from another file. Declaring an id `file partial record struct OrderId(...)`
therefore reports `STIA0025`; remove the `file` modifier (the light bulb offers exactly that). The same
applies to a `file`-local *containing* type, which is reported by the containing-type rule instead, for
the same reason.

Looking for a symptom rather than a question? See [Troubleshooting](troubleshooting.md#troubleshooting).

[<- Back to the index](index.md) · [中文](zh-CN/faq.md)
