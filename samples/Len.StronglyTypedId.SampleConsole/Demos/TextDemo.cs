using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Len.StronglyTypedId.SampleConsole.Demos;

internal static class TextDemo
{
    public static void Run()
    {
        Demo.Section("文本前缀：ToString 写出，Parse 剥离");

        var invoice = InvoiceId.Create(1234);
        Demo.Line($"InvoiceId.Create(1234)            -> {invoice}");
        Demo.Line($"Parse(\"inv_1234\")                  -> {InvoiceId.Parse("inv_1234", CultureInfo.InvariantCulture)}");
        Demo.Line($"Parse(\"1234\")（宽松读取）           -> {InvoiceId.Parse("1234", CultureInfo.InvariantCulture)}");
        Demo.Line($"JSON                              -> {JsonSerializer.Serialize(invoice)}");

        Demo.Section("严格前缀：裸值被拒绝");

        var sku = SkuId.Create("ABC");
        Demo.Line($"SkuId.Create(\"ABC\")                -> {sku}");
        Demo.Line($"TryParse(\"sku_ABC\")                -> {SkuId.TryParse("sku_ABC", CultureInfo.InvariantCulture, out _)}");
        Demo.Line($"TryParse(\"ABC\")                    -> {SkuId.TryParse("ABC", CultureInfo.InvariantCulture, out _)}");

        Demo.Section("前缀 + 格式化：前缀贯穿 ToString(format) 与 TryFormat");

        // 前缀属于规范文本形，因此格式化路径不会退化成裸基元：数字格式只作用于基元，前缀照旧保留。
        Demo.Line($"ToString()                        -> {invoice}");
        Demo.Line($"ToString(\"D6\")                    -> {invoice.ToString("D6", CultureInfo.InvariantCulture)}");
        Demo.Line($"ToString(\"N\")                     -> {invoice.ToString("N", CultureInfo.InvariantCulture)}");

        Span<char> formatted = stackalloc char[32];

        if (invoice.TryFormat(formatted, out var formattedWritten, "D6", CultureInfo.InvariantCulture))
        {
            Demo.Line($"TryFormat(\"D6\")                   -> {new string(formatted[..formattedWritten])}");
        }

        Demo.Section("JSON 形状：JsonShape.Object");

        var ticket = TicketId.Create(Guid.Parse("11111111-2222-3333-4444-555555555555"));
        Demo.Line($"JsonShape.Object                  -> {JsonSerializer.Serialize(ticket)}");

        Demo.Section("TypeConverter：TypeDescriptor 往返");

        var warehouse = WarehouseId.Create(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));
        var converter = TypeDescriptor.GetConverter(typeof(WarehouseId));
        var text = converter.ConvertToInvariantString(warehouse)
            ?? throw new InvalidOperationException("TypeConverter 未能把 Id 转成字符串。");
        Demo.Line($"ConvertToInvariantString          -> {text}");
        Demo.Line($"ConvertFromInvariantString        -> {converter.ConvertFromInvariantString(text)}");

        Demo.Section("Span 格式化：写进调用方缓冲区，不产生中间字符串");

        // ISpanFormattable / IUtf8SpanFormattable：直接写进栈上缓冲区，适合高频日志与协议编码。
        Span<char> chars = stackalloc char[64];

        if (invoice.TryFormat(chars, out var charsWritten, default, CultureInfo.InvariantCulture))
        {
            Demo.Line($"TryFormat(Span<char>)             -> {new string(chars[..charsWritten])}");
        }

        Span<byte> utf8 = stackalloc byte[64];

        if (invoice.TryFormat(utf8, out var bytesWritten, default, CultureInfo.InvariantCulture))
        {
            Demo.Line($"TryFormat(Span<byte>)             -> {Encoding.UTF8.GetString(utf8[..bytesWritten])}");
        }

        // IUtf8SpanParsable：从 UTF-8 字节直接读回，同样不经过字符串。
        if (InvoiceId.TryParse(utf8[..bytesWritten], CultureInfo.InvariantCulture, out var reparsed))
        {
            Demo.Line($"TryParse(ReadOnlySpan<byte>)      -> {reparsed}");
        }
    }
}
