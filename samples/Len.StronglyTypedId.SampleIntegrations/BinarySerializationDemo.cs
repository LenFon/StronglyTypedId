using MessagePack;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Len.StronglyTypedId.SampleIntegrations;

/// <summary>
/// 二进制序列化：MessagePack 与 MongoDB BSON。
/// </summary>
/// <remarks>
/// 与 EF Core / Dapper 的区别在「谁决定编码」：那两个把 Id 拆成裸基元交给列类型，这两个库自己编码。
/// 载荷里只有基元值 —— 文本前缀不进二进制，改前缀不会让已落盘的数据读不回来。
/// </remarks>
internal static class BinarySerializationDemo
{
    public static void Run()
    {
        MessagePackSection();
        MongoDbSection();
    }

    private static void MessagePackSection()
    {
        Demo.Section("MessagePack：StronglyTypedIds.ApplyTo(options)");

        // MessagePackSerializerOptions 是不可变的：ApplyTo 返回新实例，必须用它，不能继续用实参。
        var options = StronglyTypedIds.ApplyTo(MessagePackSerializerOptions.Standard);

        var orderId = OrderId.Create(Guid.Parse("77777777-8888-9999-aaaa-bbbbbbbbbbbb"));

        var bytes = MessagePackSerializer.Serialize(orderId, options);
        Demo.Line($"OrderId 序列化                     -> {Convert.ToHexString(bytes)}");

        var restored = MessagePackSerializer.Deserialize<OrderId>(bytes, options);
        Demo.Line($"反序列化                           -> {restored}（相等：{restored == orderId}）");

        // 载荷就是基元本身：与直接序列化 Guid 得到的字节逐字节相同，前缀不会混进来。
        var primitiveBytes = MessagePackSerializer.Serialize(orderId.Value, options);
        Demo.Line($"与裸 Guid 载荷逐字节相同           -> {bytes.AsSpan().SequenceEqual(primitiveBytes)}");

        // 生成的格式化器只认 Id；其它类型照常退回原有解析器 —— ApplyTo 是「先查生成的，再退回原来的」。
        var numbers = new[] { 1, 2, 3 };
        var numberBytes = MessagePackSerializer.Serialize(numbers, options);
        Demo.Line($"退回原有解析器（int[]）            -> {string.Join(',', MessagePackSerializer.Deserialize<int[]>(numberBytes, options))}");
    }

    private static void MongoDbSection()
    {
        Demo.Section("MongoDB BSON：StronglyTypedIds.RegisterBsonSerializers()");

        // driver 3.0 起 Guid 没有全局默认表示形式，未注册时序列化直接抛 BsonSerializationException。
        // 真实应用应按既有数据的表示形式来选（Standard / CSharpLegacy / …）。
        BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

        // 注册进 BsonSerializer 的进程级注册表，只需一次；重复调用无害。
        StronglyTypedIds.RegisterBsonSerializers();

        var entry = new LedgerEntry
        {
            Line = LineId.Create(42),
            Code = Sku.Create("SKU-002"),
            Amount = 100,
        };

        var document = entry.ToBsonDocument();
        Demo.Line($"文档                               -> {document.ToJson()}");

        var restored = BsonSerializer.Deserialize<LedgerEntry>(document);
        Demo.Line($"往返                               -> Line={restored.Line}, Code={restored.Code}, Amount={restored.Amount}");

        // 载荷同样是裸基元：LineId 存的是 int，不是字符串。
        Demo.Line($"Line 字段的 BSON 类型              -> {document["Line"].BsonType}");
    }
}

/// <summary>
/// BSON 的根必须是文档，Id 总是某个文档的字段 —— 这就是真实形态。
/// </summary>
internal sealed class LedgerEntry
{
    public LineId Line { get; set; }

    public Sku Code { get; set; }

    public int Amount { get; set; }
}
