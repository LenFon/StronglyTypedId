---
layout: default
title: Runtime
---

# Runtime

<a id="using-the-interface"></a>

## Using the interface

Every generated id implements `IStronglyTypedId<TSelf, TPrimitiveId>`, so you can write generic
code that works over any strongly typed id:

```csharp
public class Repository<TId>
    where TId : IStronglyTypedId<TId, Guid>
{
    public TId CreateId(Guid value) => TId.Create(value);
}
```

`Create` throws when a `Validator` rejects the value. The interface declares `TryCreate` too, so generic
code can take the non-throwing path without knowing the concrete id type:

```csharp
public bool TryCreateId<TId>(Guid value, out TId id)
    where TId : IStronglyTypedId<TId, Guid>
    => TId.TryCreate(value, out id);
```

Every id also implements the single-parameter `IStronglyTypedId<TSelf>`, so a constraint can say "some
strongly typed id" without naming the primitive, which is useful for logging, caching and validation
helpers that treat the id as an opaque value. That interface extends `IParsable<TSelf>`,
`IComparable<TSelf>` and `IComparisonOperators<TSelf, TSelf, bool>`, so such a constraint can still call
`TId.Parse(...)`, use `==` / `!=`, and order ids with `CompareTo` or `<` / `>` without knowing the
primitive:

```csharp
public static TId Max<TId>(IEnumerable<TId> ids)
    where TId : IStronglyTypedId<TId>
    => ids.Max()!;                       // IComparable<TSelf> is enough for LINQ

public static bool IsBefore<TId>(TId left, TId right)
    where TId : IStronglyTypedId<TId>
    => left < right;                     // IComparisonOperators<TSelf, TSelf, bool>
```

`IFormattableStronglyTypedId<TSelf>` narrows that to ids whose primitive can be formatted.

<a id="reflection-helpers"></a>

## Reflection helpers

The package also exposes `StronglyTypedIdExtensions` for inspecting types at runtime:

- `bool IsStronglyTypedId(this Type type)`
- `Type? GetPrimitiveIdType(this Type type)`
- `bool TryGetPrimitiveIdType(this Type type, out Type? primitiveIdType)`

```csharp
if (typeof(OrderId).IsStronglyTypedId())
{
    Type primitive = typeof(OrderId).GetPrimitiveIdType()!; // typeof(Guid)
}
```

The check is based on the implemented interface, so it also recognises ids generated in other assemblies.
Interfaces, abstract types, enums and arrays are never reported as strongly typed ids. A nullable id
(`OrderId?`) is judged by its underlying id type, so `typeof(OrderId?)` reports the same primitive as
`typeof(OrderId)`, the same answer the MVC model binder and the EF Core converters give.

Results are cached per `Type`, and the cache holds its keys weakly. Types loaded into a collectible
`AssemblyLoadContext` (plugins, hot reload, test hosts that reload assemblies) therefore stay collectible:
once nothing else references the plugin, the context and its types are reclaimed together with their cache
entries. If a type implements several `IStronglyTypedId<TSelf, TPrimitiveId>` interfaces, the one whose
`TSelf` is that type itself wins, so the reported primitive never depends on interface enumeration order.

[<- Back to the index](index.md) · [中文](zh-CN/runtime.md)
