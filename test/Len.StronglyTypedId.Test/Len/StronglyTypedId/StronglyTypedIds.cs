namespace Len.StronglyTypedId;

public record struct OrderId(Guid Value) : IStronglyTypedId<Guid>
{
    public static IStronglyTypedId<Guid> Create(Guid value) => new OrderId(value);
}
