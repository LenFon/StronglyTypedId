using ProtoBuf;

namespace Len.StronglyTypedId.SampleIntegrations;

/// <summary>
/// protobuf-net：surrogate 契约由生成器附加，不需要任何注册调用。
/// </summary>
/// <remarks>
/// 与其余集成的最大区别在「谁发现谁」：MessagePack / BSON 要调用注册入口，protobuf-net 则从
/// <c>[ProtoContract(Surrogate = ...)]</c> 特性里发现。代价是特性只能加在本次编译声明的类型上，
/// 引用程序集里的 Id 拿不到 surrogate。
/// </remarks>
internal static class ProtobufDemo
{
    public static void Run()
    {
        RoundTripSection();
        PayloadSection();
        FailureSection();
    }

    private static void RoundTripSection()
    {
        Demo.Section("protobuf-net：零注册，[ProtoContract(Surrogate)] 由生成器附加");

        var entry = new LedgerEntry
        {
            Id = LedgerId.Create(Guid.Parse("77777777-8888-9999-aaaa-bbbbbbbbbbbb")),
            Kind = KindId.Create(LedgerKind.Credit),
            Amount = PriceId.Create(new Money(12.5m, "USD")),
            PostedAt = PostedAtId.Create(new DateTimeOffset(2024, 1, 2, 3, 4, 5, 678, TimeSpan.FromHours(8)).AddTicks(9)),
        };

        using var stream = new MemoryStream();
        Serializer.Serialize(stream, entry);

        Demo.Line($"载荷                               -> {Convert.ToHexString(stream.ToArray())}");

        stream.Position = 0;
        var restored = Serializer.Deserialize<LedgerEntry>(stream);

        Demo.Line($"往返                               -> Id={restored.Id}, Kind={restored.Kind}, Amount={restored.Amount}, PostedAt={restored.PostedAt}");
        Demo.Line($"DateTimeOffset 保留 ticks 与 offset -> {restored.PostedAt.Value.Ticks == entry.PostedAt.Value.Ticks}, {restored.PostedAt.Value.Offset}");
    }

    private static void PayloadSection()
    {
        Demo.Section("protobuf-net：载荷是基元值本身，前缀不进载荷");

        // 与 MessagePack / BSON 同一口径：前缀属于文本表示，不是数据本身。
        var prefixed = new PrefixedLedger { Id = PrefixedLedgerId.Create(Guid.Parse("77777777-8888-9999-aaaa-bbbbbbbbbbbb")) };

        using var prefixedStream = new MemoryStream();
        Serializer.Serialize(prefixedStream, prefixed);

        prefixedStream.Position = 0;
        var prefixedBack = Serializer.Deserialize<PrefixedLedger>(prefixedStream);

        Demo.Line($"ToString()                         -> {prefixedBack.Id}");
        Demo.Line($"Value 是裸 Guid                    -> {prefixedBack.Id.Value}");

        Demo.Section("protobuf-net：自定义基元没有原生表示，载荷是规范文本");

        // Money 没有 protobuf 原生表示，因此按 UTF-8 文本写出；字节里能直接看到 "USD 12.5"。
        var custom = new CustomPrimitiveLedger { Amount = PriceId.Create(new Money(12.5m, "USD")) };

        using var customStream = new MemoryStream();
        Serializer.Serialize(customStream, custom);

        var customBytes = customStream.ToArray();

        // 载荷前面是 protobuf 的长度前缀等帧字节，直接打印会带控制字符；这里只摘出可读的文本段。
        Demo.Line($"载荷（十六进制）                    -> {Convert.ToHexString(customBytes)}");
        Demo.Line($"载荷里的文本                       -> {BinaryPayloadText.Extract(customBytes)}");

        customStream.Position = 0;
        Demo.Line($"往返                               -> {Serializer.Deserialize<CustomPrimitiveLedger>(customStream).Amount}");

        Demo.Section("protobuf-net：引用类型 Id 的 null 往返");

        var withSession = new SessionLedger { Session = SessionId.Create(Guid.Parse("11111111-1111-1111-1111-111111111111")) };
        var withoutSession = new SessionLedger { Session = null };

        using var withStream = new MemoryStream();
        Serializer.Serialize(withStream, withSession);
        withStream.Position = 0;

        using var withoutStream = new MemoryStream();
        Serializer.Serialize(withoutStream, withoutSession);
        withoutStream.Position = 0;

        Demo.Line($"有值往返                           -> {Serializer.Deserialize<SessionLedger>(withStream).Session}");
        Demo.Line($"null 往返                          -> {Serializer.Deserialize<SessionLedger>(withoutStream).Session?.ToString() ?? "<null>"}");
    }

    private static void FailureSection()
    {
        Demo.Section("protobuf-net：验证器拒绝的载荷抛 ProtoException");

        // 空载荷意味着字段缺失，surrogate 的 Value 停在 Guid.Empty；验证器拒绝它，
        // 生成代码走 TryCreate 再抛 ProtoException，而不是让 Create 的 ArgumentException 穿透。
        try
        {
            _ = Serializer.Deserialize<LedgerId>(new MemoryStream());
        }
        catch (ProtoException ex)
        {
            Demo.Line($"空载荷（Guid.Empty）                -> 抛出 {ex.GetType().Name}: {ex.Message}");
        }
    }
}

/// <summary>
/// protobuf-net 的根类型必须自己声明契约：surrogate 只解决「Id 怎么装进字段」，
/// 不替宿主类型推断成员。
/// </summary>
[ProtoContract]
internal sealed class LedgerEntry
{
    [ProtoMember(1)]
    public LedgerId Id { get; set; }

    [ProtoMember(2)]
    public KindId Kind { get; set; }

    [ProtoMember(3)]
    public PriceId Amount { get; set; }

    [ProtoMember(4)]
    public PostedAtId PostedAt { get; set; }
}

[ProtoContract]
internal sealed class PrefixedLedger
{
    [ProtoMember(1)]
    public PrefixedLedgerId Id { get; set; }
}

[ProtoContract]
internal sealed class CustomPrimitiveLedger
{
    [ProtoMember(1)]
    public PriceId Amount { get; set; }
}

[ProtoContract]
internal sealed class SessionLedger
{
    [ProtoMember(1)]
    public SessionId? Session { get; set; }
}
