using System.ComponentModel;
using System.Globalization;
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
    }
}
