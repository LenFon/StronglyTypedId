namespace Len.StronglyTypedId.Sample1.Models;

public record struct OrderId(Guid Value) : IStronglyTypedId<Guid>
{
    public static IStronglyTypedId<Guid> Create(Guid value) => new OrderId(value);
}

public class Order
{
    public OrderId Id { get; set; }

    public string Buyer { get; set; } = default!;
    public List<Product> Items { get; set; } = new();
}