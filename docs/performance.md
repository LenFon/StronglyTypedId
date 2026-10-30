---
layout: default
title: Performance
---

# Performance

Two questions matter when you adopt a source generator: what it costs your build, and what the generated
code costs at run time. Both answers are short.

## At run time

The generated members are the code you would have written by hand, so there is no hidden cost:

- `Create` is a static call, plus your validator if you configured one.
- `ToString()` is one call into the primitive.
- `Parse` is one call into the primitive's `Parse`.
- The JSON converters are generated types, not reflection-based ones, so serializing an id costs the same
  as serializing the primitive it wraps.

There is no reflection and no boxing — a `record struct` id is a value type. The one allocation worth
knowing about is a prefixed id: `ToString()` concatenates the prefix with the value, so that path allocates
a string. Leave `Prefix` unset and nothing extra is allocated.

## At build time

The generator only looks at types that implement `IStronglyTypedId<,>`, so its cost grows with the number
of ids you declare, not with the size of your reference set. It also caches what it finds per referenced
assembly, so an IDE that recompiles on every keystroke does not rescan the same assemblies each time.

In practice the generator is rarely what makes a build slow. If you suspect it is, measure your own build
first — the usual causes are other analyzers or a cold restore.

## For maintainers

The repository keeps an allocation-based regression gate for the generator itself. It is a CI concern, not
something consumers configure; the commands and the baseline file are documented in
[CONTRIBUTING](https://github.com/LenFon/StronglyTypedId/blob/main/.github/CONTRIBUTING.md).

[<- Back to the index](index.md) · [中文](zh-CN/performance.md)
