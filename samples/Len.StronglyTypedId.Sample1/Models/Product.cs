namespace Len.StronglyTypedId.Sample1.Models;

[StronglyTypedId]
public partial record struct ProductId(int Value);

//public record struct ProductId(int Value) : IStronglyTypedId<int>
//{
//    public static IStronglyTypedId<int> Create(int value) => new ProductId(value);
//}

[StronglyTypedId]
public partial record struct UserId(string Value);


public record struct SellerId(string Value) : IStronglyTypedId<string>
{
    public static IStronglyTypedId<string> Create(string value) => new SellerId(value);
}

public class Product
{
    public ProductId Key { get; set; }

    public string Name { get; set; } = default!;
}
