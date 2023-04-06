# StronglyTypedId [![NuGet][badge-nuget]][nuget-package]
A base implementation of strongly typed ids.
# Usage
Use record to define strongly-typed ids:
```C#
public record struct OrderId(Guid Value) : IStronglyTypedId<Guid>
{
    public static IStronglyTypedId<Guid> Create(Guid value) => new OrderId(value);
}
```
or
```C#
public record OrderId(Guid Value) : IStronglyTypedId<Guid>
{
    public static IStronglyTypedId<Guid> Create(Guid value) => new OrderId(value);
}
```


[nuget-package]: https://www.nuget.org/packages/Len.StronglyTypedId/
[badge-nuget]: https://img.shields.io/nuget/v/Len.StronglyTypedId.svg