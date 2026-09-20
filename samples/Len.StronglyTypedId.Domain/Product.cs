namespace Len.StronglyTypedId.Domain;

[StronglyTypedId]
public partial record struct ProductId(int Value);

[StronglyTypedId]
public partial record struct UserId(string Value);

[StronglyTypedId]
public partial record SellerId(string Value);

public class Product
{
    public ProductId Key { get; set; }

    public string Name { get; set; } = default!;
}
