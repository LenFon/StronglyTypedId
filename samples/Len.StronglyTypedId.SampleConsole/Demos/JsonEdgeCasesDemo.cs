using System.Globalization;
using System.Numerics;
using System.Text.Json;
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;
using StjJsonException = System.Text.Json.JsonException;

namespace Len.StronglyTypedId.SampleConsole.Demos;

internal static class JsonEdgeCasesDemo
{
    public static void Run()
    {
        Demo.Section("JSON：字典键是基元文本，枚举键跟随 DictionaryKeyPolicy");

        var order = OrderId.Create(Guid.Parse("3f2c1b4e-0000-4000-8000-000000000001"));
        Demo.Line($"Dictionary<OrderId,int>           -> {JsonSerializer.Serialize(new Dictionary<OrderId, int> { [order] = 3 })}");

        var permission = PermissionId.Create(Permission.Read | Permission.Write);
        Demo.Line($"Dictionary<PermissionId,int>      -> {JsonSerializer.Serialize(new Dictionary<PermissionId, int> { [permission] = 1 })}");

        // 枚举键是「名字」而非数据，因此 DictionaryKeyPolicy 对它生效；带前缀的 Id 键是数据，policy 不碰它。
        var region = RegionId.Create(Region.North);
        var camel = new JsonSerializerOptions { DictionaryKeyPolicy = JsonNamingPolicy.CamelCase };
        Demo.Line($"枚举键 + CamelCase policy          -> {JsonSerializer.Serialize(new Dictionary<RegionId, int> { [region] = 1 }, camel)}");

        var invoice = InvoiceId.Create(1234);
        Demo.Line($"带前缀的键 + CamelCase policy      -> {JsonSerializer.Serialize(new Dictionary<InvoiceId, int> { [invoice] = 1 }, camel)}");

        Demo.Section("JSON：枚举的未定义数值被拒绝");

        Demo.Line($"Deserialize<PermissionId>(\"999\")   -> {TryDeserialize<PermissionId>("999")}");

        Demo.Section("JSON：序列化器不认识的基元改走规范文本");

        var big = BigIntegerPrimitiveId.Create(BigInteger.Parse("123456789012345678901234567890"));
        Demo.Line($"BigInteger（STJ）                  -> {JsonSerializer.Serialize(big)}");

        var half = HalfPrimitiveId.Create((Half)1.5);
        Demo.Line($"Half（Newtonsoft）                 -> {JsonConvert.SerializeObject(half)}");

        var dateOnly = DateOnlyPrimitiveId.Create(new DateOnly(2024, 1, 2));
        Demo.Line($"DateOnly（Newtonsoft）             -> {JsonConvert.SerializeObject(dateOnly)}");

        Demo.Section("JSON：Newtonsoft 的失败契约是 JsonSerializationException");

        Demo.Line($"Deserialize<PermissionId>(\"999\")   -> {TryNewtonsoftDeserialize<PermissionId>("999")}");
        Demo.Line($"Deserialize<OrderId>(\"null\")       -> {TryNewtonsoftDeserialize<OrderId>("null")}");

        Demo.Section("JSON：JsonShape.Object 的报文形态");

        var ticket = TicketId.Create(Guid.Parse("11111111-2222-3333-4444-555555555555"));
        Demo.Line($"STJ                               -> {JsonSerializer.Serialize(ticket)}");
        Demo.Line($"Newtonsoft                        -> {JsonConvert.SerializeObject(ticket)}");

        // 属性名匹配跟随 options.PropertyNameCaseInsensitive：关掉时只认精确的小写 value。
        Demo.Line($"大小写不敏感读取                    -> {JsonSerializer.Deserialize<TicketId>("{\"Value\":\"11111111-2222-3333-4444-555555555555\"}", _caseInsensitive)}");
        Demo.Line($"默认（区分大小写）读取               -> {TryDeserialize<TicketId>("{\"Value\":\"11111111-2222-3333-4444-555555555555\"}")}");
    }

    // 缓存而非每次新建：CA1869 要求复用 JsonSerializerOptions，且 options 一经使用即冻结。
    private static readonly JsonSerializerOptions _caseInsensitive = new() { PropertyNameCaseInsensitive = true };

    private static string TryDeserialize<T>(string json)
    {
        try
        {
            object? value = JsonSerializer.Deserialize<T>(json);

            return value?.ToString() ?? "<null>";
        }
        catch (StjJsonException ex)
        {
            return $"抛出 {ex.GetType().Name}";
        }
    }

    private static string TryNewtonsoftDeserialize<T>(string json)
    {
        try
        {
            object? value = JsonConvert.DeserializeObject<T>(json);

            return value?.ToString() ?? "<null>";
        }
        catch (Newtonsoft.Json.JsonSerializationException ex)
        {
            return $"抛出 {ex.GetType().Name}";
        }
    }
}
