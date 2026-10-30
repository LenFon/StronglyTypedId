using Newtonsoft.Json;

namespace Len.StronglyTypedId.SampleConsole.Demos;

internal static class NewtonsoftJsonDemo
{
    public static void Run()
    {
        Demo.Section("Newtonsoft.Json：零配置（[JsonConverter] 由生成器附加）");

        var orderId = OrderId.Create(Guid.Parse("3f2c1b4e-0000-4000-8000-000000000001"));
        var json = JsonConvert.SerializeObject(orderId);
        Demo.Line($"SerializeObject                   -> {json}");
        Demo.Line($"DeserializeObject                 -> {JsonConvert.DeserializeObject<OrderId>(json)}");

        Demo.Section("Newtonsoft.Json：字典键（读取依赖 TypeConverter）");

        var warehouse = WarehouseId.Create(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));
        var stock = new Dictionary<WarehouseId, int> { [warehouse] = 7 };
        var stockJson = JsonConvert.SerializeObject(stock);
        Demo.Line($"SerializeObject                   -> {stockJson}");
        Demo.Line($"DeserializeObject                 -> {JsonConvert.DeserializeObject<Dictionary<WarehouseId, int>>(stockJson)![warehouse]}");

        Demo.Section("Newtonsoft.Json：显式注册到自定义 settings");

        var settings = new JsonSerializerSettings();
        settings.AddStronglyTypedIds();
        Demo.Line($"settings.Converters.Count         -> {settings.Converters.Count}");
    }
}
