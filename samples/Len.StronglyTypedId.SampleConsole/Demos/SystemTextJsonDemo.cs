using System.Text.Json;

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
        StronglyTypedIds.ApplyTo(options);
        Demo.Line($"options.Converters.Count          -> {options.Converters.Count}");
    }
}
