using System.Text.Json;

namespace Len.StronglyTypedId.SampleConsole;

// 不带验证器的 Id：任何取值都合法，Create 恒成功。
[StronglyTypedId]
public partial record struct ProductId(int Value);

// 带验证器的 Id：Create / TryCreate / TryParse 都会先校验，非法值被拒。
[StronglyTypedId(Validator = nameof(IsValid))]
public partial record struct OrderId(Guid Value)
{
    private static bool IsValid(Guid value) => value != Guid.Empty;
}

public static class Program
{
    public static void Main()
    {
        DemonstrateUnconstrainedId();
        DemonstrateValidatedId();
        DemonstrateDeconstruction();
        DemonstrateSerialization();
    }

    private static void DemonstrateUnconstrainedId()
    {
        var id = ProductId.Create(42);
        Console.WriteLine($"ProductId.Create(42)       -> {id}");
        Console.WriteLine($"id.Value                  -> {id.Value}");

        // TryCreate：非抛异常版本
        if (ProductId.TryCreate(7, out var other))
        {
            Console.WriteLine($"ProductId.TryCreate(7)     -> {other}");
        }

        // 解析（string 与 span 重载由生成器透出）
        if (ProductId.TryParse("123", null, out var parsed))
        {
            Console.WriteLine($"ProductId.TryParse(\"123\") -> {parsed}");
        }
    }

    private static void DemonstrateValidatedId()
    {
        // 合法值
        var valid = OrderId.Create(Guid.NewGuid());
        Console.WriteLine($"OrderId.Create(valid)      -> {valid}");

        // 非法值：Create 抛 ArgumentException
        try
        {
            OrderId.Create(Guid.Empty);
        }
        catch (ArgumentException ex)
        {
            Console.WriteLine($"OrderId.Create(empty)     -> 抛异常：{ex.Message}");
        }

        // TryCreate：非法值返回 false，不抛异常
        if (!OrderId.TryCreate(Guid.Empty, out _))
        {
            Console.WriteLine("OrderId.TryCreate(empty)  -> false（校验拦截）");
        }

        // 比较与排序
        var a = OrderId.Create(Guid.NewGuid());
        var b = OrderId.Create(Guid.NewGuid());
        Console.WriteLine($"a < b ?                    -> {a < b}");
    }

    private static void DemonstrateDeconstruction()
    {
        var id = ProductId.Create(99);

        // 生成器提供的解构：把基元值拆出来
        id.Deconstruct(out var value);
        Console.WriteLine($"id.Deconstruct(out value)    -> {value}");
    }

    private static void DemonstrateSerialization()
    {
        var id = ProductId.Create(5);
        var json = JsonSerializer.Serialize(id);
        Console.WriteLine($"JsonSerializer.Serialize    -> {json}");

        var back = JsonSerializer.Deserialize<ProductId>(json);
        Console.WriteLine($"JsonSerializer.Deserialize  -> {back}");
    }
}
