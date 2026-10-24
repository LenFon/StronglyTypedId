using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using MessagePack;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;

namespace Len.StronglyTypedId.Aot.SmokeTest;

// 三种代表性形态：无验证器值类型、带验证器值类型、带文本前缀。
[StronglyTypedId]
public partial record struct AotProductId(int Value);

[StronglyTypedId(Validator = nameof(IsValid))]
public partial record struct AotOrderId(Guid Value)
{
    private static bool IsValid(Guid value) => value != Guid.Empty;
}

[StronglyTypedId(Prefix = "ord_")]
public partial record struct AotPrefixedId(Guid Value);

// 枚举基元：二进制反序列化入口（MessagePack / BSON）必须经 Enum.IsDefined 收紧，本形态专门压这条分支。
public enum AotStatus
{
    Active = 1,
    Inactive = 2,
}

[StronglyTypedId]
public partial record struct AotStatusId(AotStatus Value);

// ExceptionType：Create 拒绝时抛「指定类型」而非 ArgumentException，生成器走编译期 new（无反射），需在 AOT 下实证。
public sealed class AotDomainException : Exception
{
    public AotDomainException(string message) : base(message)
    {
    }
}

[StronglyTypedId(Validator = nameof(IsPositive), ExceptionType = typeof(AotDomainException))]
public partial record struct AotAmountId(int Value)
{
    private static bool IsPositive(int value) => value > 0;
}

/// <summary>
/// Native AOT / 裁剪环境下的端到端冒烟。
/// </summary>
public static class Program
{
    // AOT 下反射序列化被禁用，必须由源生成的 context 提供 JsonTypeInfo。
    private static readonly JsonSerializerOptions _options = new()
    {
        TypeInfoResolver = SmokeTestJsonContext.Default,
    };

    public static int Main()
    {
        var failures = new List<string>();

        Check(failures, "Create / TryCreate", CreateAndTryCreate);
        Check(failures, "ExceptionType throw", ExceptionTypeThrow);
        Check(failures, "ToString / Parse / TryParse", ParseRoundTrip);
        Check(failures, "TryFormat (UTF-16 / UTF-8)", SpanFormatting);
        Check(failures, "Prefix (lenient)", PrefixLenient);
        Check(failures, "System.Text.Json value", JsonValue);
        Check(failures, "System.Text.Json property name", JsonPropertyName);
        Check(failures, "MessagePack value + enum", MessagePackValue);
        Check(failures, "MongoDB.Bson value + enum", BsonValue);

        if (failures.Count == 0)
        {
            Console.WriteLine("AOT smoke test passed.");
            return 0;
        }

        Console.Error.WriteLine($"AOT smoke test failed ({failures.Count}):");
        foreach (var failure in failures)
        {
            Console.Error.WriteLine($"  - {failure}");
        }

        return 1;
    }

    private static void CreateAndTryCreate(List<string> failures)
    {
        var id = AotProductId.Create(42);
        Expect(failures, id.Value == 42, "AotProductId.Create(42).Value == 42");

        Expect(failures, AotProductId.TryCreate(7, out var other) && other.Value == 7, "AotProductId.TryCreate(7)");

        // 验证器在 AOT 下同样生效（静态方法直调，无反射）。
        Expect(failures, !AotOrderId.TryCreate(Guid.Empty, out _), "AotOrderId.TryCreate(Guid.Empty) == false");

        var valid = AotOrderId.Create(Guid.NewGuid());
        Expect(failures, valid.Value != Guid.Empty, "AotOrderId.Create(valid)");
    }

    private static void ExceptionTypeThrow(List<string> failures)
    {
        // 验证器拒绝时抛 ExceptionType 指定的类型；该类型由生成器以编译期 new 抛出，AOT 下无反射。
        Expect(failures, !AotAmountId.TryCreate(-1, out _), "AotAmountId.TryCreate(-1) == false");

        try
        {
            AotAmountId.Create(-1);
            failures.Add("AotAmountId.Create(-1) should throw AotDomainException");
        }
        catch (AotDomainException)
        {
            // 预期。
        }
        catch (Exception ex)
        {
            failures.Add($"AotAmountId.Create(-1) threw {ex.GetType().Name}, expected AotDomainException");
        }

        var ok = AotAmountId.Create(10);
        Expect(failures, ok.Value == 10, "AotAmountId.Create(10)");
    }

    private static void ParseRoundTrip(List<string> failures)
    {
        var id = AotProductId.Create(123);
        var text = id.ToString();

        Expect(failures, text == "123", $"ToString() == \"123\" (actual: \"{text}\")");
        Expect(failures, AotProductId.Parse(text, CultureInfo.InvariantCulture) == id, "Parse(ToString()) round-trip");

        Expect(failures, AotProductId.TryParse("456".AsSpan(), CultureInfo.InvariantCulture, out var fromSpan) && fromSpan.Value == 456, "TryParse(ReadOnlySpan<char>)");

        Expect(
            failures,
            AotProductId.TryParse(Encoding.UTF8.GetBytes("789").AsSpan(), CultureInfo.InvariantCulture, out var fromUtf8) && fromUtf8.Value == 789,
            "TryParse(ReadOnlySpan<byte>)");
    }

    private static void SpanFormatting(List<string> failures)
    {
        var id = AotProductId.Create(2024);

        Span<char> chars = stackalloc char[16];
        Expect(
            failures,
            id.TryFormat(chars, out var charsWritten, default, CultureInfo.InvariantCulture)
                && chars[..charsWritten].SequenceEqual("2024"),
            "TryFormat(Span<char>)");

        Span<byte> bytes = stackalloc byte[16];
        Expect(
            failures,
            id.TryFormat(bytes, out var bytesWritten, default, CultureInfo.InvariantCulture)
                && bytes[..bytesWritten].SequenceEqual("2024"u8),
            "TryFormat(Span<byte>)");

        // 经 IFormattable 走一遍：结构化日志 / string.Format 的实际路径。
        Expect(failures, $"{id}" == "2024", "interpolated ToString()");
    }

    private static void PrefixLenient(List<string> failures)
    {
        var guid = Guid.NewGuid();
        var id = AotPrefixedId.Create(guid);
        var text = id.ToString();

        Expect(failures, text == "ord_" + guid.ToString("D"), $"prefixed ToString() (actual: \"{text}\")");
        Expect(failures, AotPrefixedId.Parse(text, CultureInfo.InvariantCulture) == id, "prefixed Parse round-trip");

        // 默认宽松：裸值也能读入。
        Expect(failures, AotPrefixedId.TryParse(guid.ToString("D"), CultureInfo.InvariantCulture, out var bare) && bare == id, "lenient TryParse(bare)");
    }

    private static void JsonValue(List<string> failures)
    {
        var id = AotProductId.Create(314);
        var converter = new AotProductId.SystemTextJsonConverter();

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            converter.Write(writer, id, _options);
        }

        var json = Encoding.UTF8.GetString(buffer.WrittenSpan);
        Expect(failures, json == "314", $"STJ write (actual: \"{json}\")");

        var reader = new Utf8JsonReader(buffer.WrittenSpan);
        reader.Read();
        var back = converter.Read(ref reader, typeof(AotProductId), _options);
        Expect(failures, back == id, "STJ read round-trip");
    }

    private static void JsonPropertyName(List<string> failures)
    {
        var id = AotProductId.Create(2718);
        var converter = new AotProductId.SystemTextJsonConverter();

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            converter.WriteAsPropertyName(writer, id, _options);
            writer.WriteNumberValue(1);
            writer.WriteEndObject();
        }

        var json = Encoding.UTF8.GetString(buffer.WrittenSpan);
        Expect(failures, json == "{\"2718\":1}", $"STJ WriteAsPropertyName (actual: \"{json}\")");

        var reader = new Utf8JsonReader(buffer.WrittenSpan);
        reader.Read();
        reader.Read();
        var key = converter.ReadAsPropertyName(ref reader, typeof(AotProductId), _options);
        Expect(failures, key == id, "STJ ReadAsPropertyName round-trip");
    }

    private static void MessagePackValue(List<string> failures)
    {
        // ApplyTo 把本工程全部 Id 的格式化器聚合成一个显式解析器（无反射），正好覆盖 AOT 场景。
        var options = StronglyTypedIds.ApplyTo(MessagePackSerializerOptions.Standard);

        // AOT 前提自检：枚举基元必须命中 ApplyTo 显式注册的零反射格式化器。MessagePack 在 AOT 下的
        // StandardResolver 链里没有任何枚举 resolver（DynamicEnumResolver 走 IL Emit，被排除），
        // 一旦这里拿到的不是 GenericEnumFormatter，AOT 二进制上就会抛「formatter not found」。
        var enumFormatter = options.Resolver.GetFormatter<AotStatus>();
        Expect(
            failures,
            enumFormatter is global::MessagePack.Formatters.GenericEnumFormatter<AotStatus>,
            $"MessagePack enum formatter should be the registered GenericEnumFormatter (actual: {enumFormatter?.GetType().Name ?? "null"})");

        // int 基元往返。
        var product = AotProductId.Create(159);
        var productBytes = MessagePackSerializer.Serialize(product, options);
        var productBack = MessagePackSerializer.Deserialize<AotProductId>(productBytes, options);
        Expect(failures, productBack == product, "MessagePack round-trip (int)");

        // 枚举基元：合法值往返。
        var status = AotStatusId.Create(AotStatus.Active);
        var statusBytes = MessagePackSerializer.Serialize(status, options);
        var statusBack = MessagePackSerializer.Deserialize<AotStatusId>(statusBytes, options);
        Expect(failures, statusBack == status, "MessagePack round-trip (enum)");

        // 枚举基元：未定义数值（999）经二进制入口必须被拒（TryCreate + Enum.IsDefined 分支）。
        var badBytes = MessagePackSerializer.Serialize((AotStatus)999, options);
        try
        {
            MessagePackSerializer.Deserialize<AotStatusId>(badBytes, options);
            failures.Add("MessagePack should reject undefined enum value (999)");
        }
        catch (MessagePackSerializationException)
        {
            // 预期。
        }
        catch (Exception ex)
        {
            failures.Add($"MessagePack undefined enum threw {ex.GetType().Name}, expected MessagePackSerializationException");
        }
    }

    private static void BsonValue(List<string> failures)
    {
        // 进程级注册表里注册本工程全部 Id 的序列化器与各自基元（int / 枚举）的序列化器（均无反射；
        // BSON 的 PrimitiveSerializationProvider 靠反射创建基元序列化器，AOT 下必须由生成产物预注册）。
        StronglyTypedIds.RegisterBsonSerializers();

        // AOT 前提自检：基元序列化器必须来自生成产物的预注册。BSON 的 PrimitiveSerializationProvider
        // 靠 Activator 反射创建它们，AOT 下未被静态引用的构造器被裁剪后会抛 MissingMethodException；
        // 预注册命中缓存，这条反射路径根本不会被走到。
        var intSerializer = global::MongoDB.Bson.Serialization.BsonSerializer.LookupSerializer<int>();
        Expect(
            failures,
            intSerializer is global::MongoDB.Bson.Serialization.Serializers.Int32Serializer,
            $"BSON int serializer should be the pre-registered Int32Serializer (actual: {intSerializer.GetType().Name})");

        var enumSerializer = global::MongoDB.Bson.Serialization.BsonSerializer.LookupSerializer<AotStatus>();
        Expect(
            failures,
            enumSerializer is global::MongoDB.Bson.Serialization.Serializers.EnumSerializer<AotStatus>,
            $"BSON enum serializer should be the pre-registered EnumSerializer (actual: {enumSerializer.GetType().Name})");

        // AOT 前提自检：根反序列化上下文无条件取 BsonDefaults.DynamicArraySerializer（List<object>）与
        // DynamicDocumentSerializer（ExpandoObject），二者首次读取时去注册表查。注册表里没有它们时，
        // provider 会靠 Activator 反射创建，AOT 下抛 MissingMethodException（CI aot job 实证）。
        var dynamicArraySerializer = global::MongoDB.Bson.Serialization.BsonSerializer.LookupSerializer<List<object>>();
        Expect(
            failures,
            dynamicArraySerializer is global::MongoDB.Bson.Serialization.Serializers.EnumerableInterfaceImplementerSerializer<List<object>, object>,
            $"BSON dynamic array serializer should be the pre-registered one (actual: {dynamicArraySerializer.GetType().Name})");

        var dynamicDocumentSerializer = global::MongoDB.Bson.Serialization.BsonSerializer.LookupSerializer<global::System.Dynamic.ExpandoObject>();
        Expect(
            failures,
            dynamicDocumentSerializer is global::MongoDB.Bson.Serialization.Serializers.ExpandoObjectSerializer,
            $"BSON dynamic document serializer should be the pre-registered one (actual: {dynamicDocumentSerializer.GetType().Name})");

        // int 基元往返。
        var product = AotProductId.Create(753);
        var productBack = ReadBson<AotProductId>(WriteBson(product));
        Expect(failures, productBack == product, "Bson round-trip (int)");

        // 枚举基元：合法值往返。
        var status = AotStatusId.Create(AotStatus.Inactive);
        var statusBack = ReadBson<AotStatusId>(WriteBson(status));
        Expect(failures, statusBack == status, "Bson round-trip (enum)");

        // 枚举基元：未定义数值（999）经二进制入口必须被拒。
        var badDoc = new BsonDocument { { "v", 999 } };
        try
        {
            ReadBson<AotStatusId>(badDoc);
            failures.Add("Bson should reject undefined enum value (999)");
        }
        catch (BsonSerializationException)
        {
            // 预期。
        }
        catch (Exception ex)
        {
            failures.Add($"Bson undefined enum threw {ex.GetType().Name}, expected BsonSerializationException");
        }
    }

    // BSON 根必须是文档，裸标量写不进 BsonDocumentWriter，故统一包一层 "v" 字段（与集成测试同构）。
    private static BsonDocument WriteBson<T>(T value)
    {
        var document = new BsonDocument();
        using (var writer = new BsonDocumentWriter(document))
        {
            writer.WriteStartDocument();
            writer.WriteName("v");
            BsonSerializer.Serialize(writer, typeof(T), value!);
            writer.WriteEndDocument();
        }

        return document;
    }

    private static T ReadBson<T>(BsonDocument document)
    {
        using var reader = new BsonDocumentReader(document);
        reader.ReadStartDocument();
        reader.ReadName();
        return BsonSerializer.Deserialize<T>(reader);
    }

    private static void Check(List<string> failures, string name, Action<List<string>> check)
    {
        try
        {
            check(failures);
            Console.WriteLine($"  ok   {name}");
        }
        catch (Exception ex)
        {
            failures.Add($"{name} threw {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void Expect(List<string> failures, bool condition, string description)
    {
        if (!condition)
        {
            failures.Add(description);
        }
    }
}
