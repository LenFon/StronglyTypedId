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

        Demo.Section("集成白名单：只列出的那几类会生成");

        // 白名单是「过滤器」而非开关：它只能在「已引用对应包」的基础上做减法。
        // 本工程引用了两个 JSON 序列化器，因此这两类照常生成；列出未引用的包会报 STIA0019。
        Demo.Line($"WhitelistedId 有 [JsonConverter]   -> {HasJsonConverter(typeof(WhitelistedId))}");
        Demo.Line($"WhitelistedId JSON                -> {JsonSerializer.Serialize(WhitelistedId.Create(guid))}");
        Demo.Line($"WhitelistedId 核心能力             -> {WhitelistedId.Parse(WhitelistedId.Create(guid).ToString(), null) == WhitelistedId.Create(guid)}");
    }

    private static bool HasJsonConverter(Type type) => type.IsDefined(typeof(JsonConverterAttribute), inherit: false);
}
