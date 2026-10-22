using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;

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
        Check(failures, "ToString / Parse / TryParse", ParseRoundTrip);
        Check(failures, "TryFormat (UTF-16 / UTF-8)", SpanFormatting);
        Check(failures, "Prefix (lenient)", PrefixLenient);
        Check(failures, "System.Text.Json value", JsonValue);
        Check(failures, "System.Text.Json property name", JsonPropertyName);

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
