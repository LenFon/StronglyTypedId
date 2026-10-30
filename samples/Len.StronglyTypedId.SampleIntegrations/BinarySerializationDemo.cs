using System.Text;
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
/// 载荷里只有基元值，文本前缀不进二进制，改前缀不会让已落盘的数据读不回来。
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
        Demo.Section("MessagePack：options.AddStronglyTypedIds()");

        // MessagePackSerializerOptions 是不可变的：AddStronglyTypedIds 返回新实例，必须用它，不能继续用实参。
        var options = MessagePackSerializerOptions.Standard.AddStronglyTypedIds();

        var orderId = OrderId.Create(Guid.Parse("77777777-8888-9999-aaaa-bbbbbbbbbbbb"));

        var bytes = MessagePackSerializer.Serialize(orderId, options);
        Demo.Line($"OrderId 序列化                     -> {Convert.ToHexString(bytes)}");

        var restored = MessagePackSerializer.Deserialize<OrderId>(bytes, options);
        Demo.Line($"反序列化                           -> {restored}（相等：{restored == orderId}）");

        // 载荷就是基元本身：与直接序列化 Guid 得到的字节逐字节相同，前缀不会混进来。
        var primitiveBytes = MessagePackSerializer.Serialize(orderId.Value, options);
        Demo.Line($"与裸 Guid 载荷逐字节相同           -> {bytes.AsSpan().SequenceEqual(primitiveBytes)}");

        // 生成的格式化器只认 Id；其它类型照常退回原有解析器，AddStronglyTypedIds 是「先查生成的，再退回原来的」。
        var numbers = new[] { 1, 2, 3 };
        var numberBytes = MessagePackSerializer.Serialize(numbers, options);
        Demo.Line($"退回原有解析器（int[]）            -> {string.Join(',', MessagePackSerializer.Deserialize<int[]>(numberBytes, options))}");

        Demo.Section("MessagePack：枚举与自定义基元");

        // 枚举基元的格式化器由 AddStronglyTypedIds 一并注册：AOT 下解析器链里没有枚举 resolver，
        // 不补这一项，枚举基元的 Id 连序列化都过不去。
        var kind = KindId.Create(LedgerKind.Credit);
        var kindBytes = MessagePackSerializer.Serialize(kind, options);
        Demo.Line($"KindId                             -> {Convert.ToHexString(kindBytes)}，往返 {MessagePackSerializer.Deserialize<KindId>(kindBytes, options)}");

        // 自定义基元没有原生表示，载荷退回规范文本（MessagePack string）。
        var price = PriceId.Create(new Money(12.5m, "USD"));
        var priceBytes = MessagePackSerializer.Serialize(price, options);
        Demo.Line($"PriceId 载荷（十六进制）            -> {Convert.ToHexString(priceBytes)}");
        Demo.Line($"PriceId 载荷里的文本                -> {BinaryPayloadText.Extract(priceBytes)}");
        Demo.Line($"PriceId 往返                       -> {MessagePackSerializer.Deserialize<PriceId>(priceBytes, options)}");

        Demo.Section("MessagePack：值类型 Id 收到 nil 会抛异常，而不是落成 default");

        try
        {
            _ = MessagePackSerializer.Deserialize<OrderId>(new byte[] { 0xC0 }, options);
        }
        catch (MessagePackSerializationException ex)
        {
            Demo.Line($"Deserialize(nil)                  -> 抛出 {ex.GetType().Name}");
        }

        Demo.Section("MessagePack：引用类型 Id 的 null 写成 nil，读回 null");

        // 与值类型相反：引用类型 Id 的 null 是合法取值，写成 MessagePack nil、读回 null。
        var session = SessionId.Create(Guid.Parse("22222222-3333-4444-5555-666666666666"));
        var sessionBytes = MessagePackSerializer.Serialize(session, options);
        var nullBytes = MessagePackSerializer.Serialize<SessionId?>(null, options);

        Demo.Line($"有值往返                           -> {MessagePackSerializer.Deserialize<SessionId>(sessionBytes, options)}");
        Demo.Line($"null 的载荷                        -> {Convert.ToHexString(nullBytes)}（nil）");
        Demo.Line($"null 往返                          -> {MessagePackSerializer.Deserialize<SessionId?>(nullBytes, options)?.ToString() ?? "<null>"}");
    }

    private static void MongoDbSection()
    {
        Demo.Section("MongoDB BSON：StronglyTypedIds.RegisterBsonSerializers()");

        // driver 3.0 起 Guid 没有全局默认表示形式，未注册时序列化直接抛 BsonSerializationException。
        // 真实应用应按既有数据的表示形式来选（Standard / CSharpLegacy / …）。
        BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

        // 注册进 BsonSerializer 的进程级注册表，只需一次；重复调用无害。
        StronglyTypedIds.RegisterBsonSerializers();

        var entry = new BsonLedgerEntry
        {
            Line = LineId.Create(42),
            Code = Sku.Create("SKU-002"),
            Amount = 100,
        };

        var document = entry.ToBsonDocument();
        Demo.Line($"文档                               -> {document.ToJson()}");

        var restored = BsonSerializer.Deserialize<BsonLedgerEntry>(document);
        Demo.Line($"往返                               -> Line={restored.Line}, Code={restored.Code}, Amount={restored.Amount}");

        // 载荷同样是裸基元：LineId 存的是 int，不是字符串。
        Demo.Line($"Line 字段的 BSON 类型              -> {document["Line"].BsonType}");

        Demo.Section("MongoDB BSON：自定义基元存成 BSON string");

        var priceDocument = new BsonPriceEntry { Amount = PriceId.Create(new Money(12.5m, "USD")) }.ToBsonDocument();
        Demo.Line($"文档                               -> {priceDocument.ToJson()}");
        Demo.Line($"Amount 字段的 BSON 类型            -> {priceDocument["Amount"].BsonType}");
        Demo.Line($"往返                               -> {BsonSerializer.Deserialize<BsonPriceEntry>(priceDocument).Amount}");

        Demo.Section("MongoDB BSON：引用类型 Id 的 null 存成 BSON null");

        // 与值类型相反：引用类型 Id 的 null 是合法取值，存成 BSON null、读回 null。
        var sessionDocument = new BsonSessionEntry
        {
            Id = SessionId.Create(Guid.Parse("22222222-3333-4444-5555-666666666666")),
            Previous = null,
        }.ToBsonDocument();

        Demo.Line($"文档                               -> {sessionDocument.ToJson()}");
        Demo.Line($"Previous 字段的 BSON 类型          -> {sessionDocument["Previous"].BsonType}");
        Demo.Line($"往返                               -> Id={BsonSerializer.Deserialize<BsonSessionEntry>(sessionDocument).Id}, Previous={BsonSerializer.Deserialize<BsonSessionEntry>(sessionDocument).Previous?.ToString() ?? "<null>"}");
    }
}

/// <summary>
/// BSON 的根必须是文档，Id 总是某个文档的字段，这就是真实形态。
/// </summary>
internal sealed class BsonLedgerEntry
{
    public LineId Line { get; set; }

    public Sku Code { get; set; }

    public int Amount { get; set; }
}

internal sealed class BsonPriceEntry
{
    public PriceId Amount { get; set; }
}

internal sealed class BsonSessionEntry
{
    public SessionId Id { get; set; } = null!;

    public SessionId? Previous { get; set; }
}

/// <summary>
/// 从二进制载荷里摘出可打印的文本段，供示例输出展示。
/// </summary>
/// <remarks>
/// MessagePack 的字符串前面是类型字节与长度，直接解码会让整行输出看起来是乱码或空的。
/// 这里把不可打印字节换成空格，再压掉连续空格与首尾空格，只留下文本本身。
/// </remarks>
internal static class BinaryPayloadText
{
    public static string Extract(byte[] payload)
    {
        var builder = new StringBuilder(payload.Length);

        foreach (var value in payload)
        {
            builder.Append(value is >= 0x20 and < 0x7F ? (char)value : ' ');
        }

        return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
