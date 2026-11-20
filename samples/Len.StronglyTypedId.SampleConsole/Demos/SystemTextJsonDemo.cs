using System.Text.Json;
using System.Text.Json.Serialization;

namespace Len.StronglyTypedId.SampleConsole.Demos;

internal static class SystemTextJsonDemo
{
    public static void Run()
    {
        Demo.Section("System.Text.Json：零配置（[JsonConverter] 由生成器附加）");

        var orderId = OrderId.Create(Guid.Parse("3f2c1b4e-0000-4000-8000-000000000001"));
        var json = JsonSerializer.Serialize(orderId);
        Demo.Line($"Serialize                         -> {json}");
        Demo.Line($"Deserialize                       -> {JsonSerializer.Deserialize<OrderId>(json)}");

        Demo.Section("System.Text.Json：字典键");

        var cart = new Dictionary<OrderId, int> { [orderId] = 3 };
        var cartJson = JsonSerializer.Serialize(cart);
        Demo.Line($"Serialize(Dictionary<OrderId,int>) -> {cartJson}");
        Demo.Line($"Deserialize                       -> {JsonSerializer.Deserialize<Dictionary<OrderId, int>>(cartJson)![orderId]}");

        Demo.Section("System.Text.Json：显式注册到自定义 options");

        var options = new JsonSerializerOptions();
        options.AddStronglyTypedIds();
        Demo.Line($"options.Converters.Count          -> {options.Converters.Count}");

        Demo.Section("System.Text.Json：JsonNumberHandling");

        // 自定义 JsonConverter<T> 默认看不到 NumberHandling，裸 int 与 Int32Id 会对 "42" 给出不同结论。
        // 生成的转换器补上了这一层：数值基元遵守全局选项，与裸基元逐字节一致。
        var writeAsString = new JsonSerializerOptions { NumberHandling = JsonNumberHandling.WriteAsString };
        var productId = ProductId.Create(42);

        Demo.Line($"WriteAsString：裸 int              -> {JsonSerializer.Serialize(42, writeAsString)}");
        Demo.Line($"WriteAsString：ProductId           -> {JsonSerializer.Serialize(productId, writeAsString)}");

        var allowFromString = new JsonSerializerOptions { NumberHandling = JsonNumberHandling.AllowReadingFromString };
        Demo.Line($"AllowReadingFromString：\"42\"      -> {JsonSerializer.Deserialize<ProductId>("\"42\"", allowFromString)}");

        // 默认（不设选项）时产物不变：数值仍写成 JSON 数字。
        Demo.Line($"未设置选项                         -> {JsonSerializer.Serialize(productId)}");

        // 属性级 [JsonNumberHandling] 用不了：STJ 按 CLR 类型判定该特性，Id 不是数字，序列化时抛异常。
        // 编译期由 STIA0033 提示，修法是改用上面的全局选项。
        Demo.Line("属性级 [JsonNumberHandling]        -> 序列化时抛 InvalidOperationException（见 STIA0033）");
    }
}
