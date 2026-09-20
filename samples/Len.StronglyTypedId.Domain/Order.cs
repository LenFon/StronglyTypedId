namespace Len.StronglyTypedId.Domain;

[StronglyTypedId]
public partial record struct OrderId(Guid Value);

public class Order
{
    public OrderId Id { get; set; }

    public UserId Buyer { get; set; } = default!;

    public List<Product> Items { get; set; } = [];

    public UserId? ModifierId { get; set; }
}