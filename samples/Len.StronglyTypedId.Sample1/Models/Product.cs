namespace Len.StronglyTypedId.Sample1.Models;

public record struct ProductId(int Value) : IStronglyTypedId<int>
{
    public static IStronglyTypedId<int> Create(int value) => new ProductId(value);
}

public class Product
{
    public ProductId Key { get; set; }

    public string Name { get; set; } = default!;
}
