using System.Text.Json;
using System.Text.Json.Serialization;

namespace Len.StronglyTypedId.SampleConsole.Demos;

internal static class IntegrationsDemo
{
    public static void Run()
    {
        Demo.Section("集成过滤：Integrations = None 只关集成产物");

        Demo.Line($"OrderId 有 [JsonConverter]         -> {HasJsonConverter(typeof(OrderId))}");
        Demo.Line($"PlainGuidId 有 [JsonConverter]     -> {HasJsonConverter(typeof(PlainGuidId))}");

        var guid = Guid.Parse("77777777-8888-9999-aaaa-bbbbbbbbbbbb");
        Demo.Line($"OrderId JSON                      -> {JsonSerializer.Serialize(OrderId.Create(guid))}");
        Demo.Line($"PlainGuidId JSON（无转换器）        -> {JsonSerializer.Serialize(PlainGuidId.Create(guid))}");

        Demo.Section("核心能力不受影响");

        var plain = PlainGuidId.Create(guid);
        Demo.Line($"Parse(ToString()) 往返             -> {PlainGuidId.Parse(plain.ToString(), null) == plain}");
        Demo.Line($"比较                               -> {plain.CompareTo(PlainGuidId.Create(Guid.Empty)) > 0}");
    }

    private static bool HasJsonConverter(Type type) => type.IsDefined(typeof(JsonConverterAttribute), inherit: false);
}
