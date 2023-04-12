namespace Len.StronglyTypedId.Sample1.Models;

[StronglyTypedId]
public partial record struct OrderId(Guid Value);

//public partial record struct OrderId : IStronglyTypedId<Guid>
//{
//    public static IStronglyTypedId<Guid> Create(Guid value) => new OrderId(value);
//}

public class Order
{
    public OrderId Id { get; set; }

    public UserId Buyer { get; set; } = default!;
    public List<Product> Items { get; set; } = new();
}