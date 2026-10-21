using System.Globalization;
using System.Text;

namespace Len.StronglyTypedId.SampleConsole.Demos;

internal static class CoreDemo
{
    public static void Run()
    {
        Demo.Section("核心：Create / TryCreate / Parse / TryParse");

        var productId = ProductId.Create(42);
        Demo.Line($"ProductId.Create(42)              -> {productId}");
        Demo.Line($"id.Value                          -> {productId.Value}");

        var created = ProductId.TryCreate(7, out var other);
        Demo.Line($"TryCreate(7)                      -> {created}, {other}");

        var parsed = ProductId.Parse("123", CultureInfo.InvariantCulture);
        Demo.Line($"Parse(\"123\")                      -> {parsed}");

        var parsedOk = ProductId.TryParse("not-a-number", CultureInfo.InvariantCulture, out _);
        Demo.Line($"TryParse(\"not-a-number\")           -> {parsedOk}");

        var spanParsed = ProductId.Parse("456".AsSpan(), CultureInfo.InvariantCulture);
        Demo.Line($"Parse(ReadOnlySpan<char>)         -> {spanParsed}");

        Demo.Section("格式化：IFormattable / ISpanFormattable");

        Demo.Line($"ToString()                        -> {productId}");
        Demo.Line($"ToString(\"D4\")                    -> {productId.ToString("D4", CultureInfo.InvariantCulture)}");

        Span<char> buffer = stackalloc char[16];
        var formatted = ((ISpanFormattable)productId).TryFormat(buffer, out var written, "D4", CultureInfo.InvariantCulture);
        Demo.Line($"TryFormat(Span<char>, \"D4\")       -> {formatted}, \"{buffer[..written]}\"");

#if NET10_0_OR_GREATER
        Demo.Section("UTF-8：IUtf8SpanFormattable / IUtf8SpanParsable（.NET 10）");

        var orderId = OrderId.Create(Guid.Parse("3f2c1b4e-0000-4000-8000-000000000001"));

        Span<byte> utf8 = stackalloc byte[64];
        var utf8Formatted = ((IUtf8SpanFormattable)orderId).TryFormat(utf8, out var utf8Written, default, CultureInfo.InvariantCulture);
        Demo.Line($"TryFormat(Span<byte>)             -> {utf8Formatted}, \"{Encoding.UTF8.GetString(utf8[..utf8Written])}\"");

        var utf8Parsed = OrderId.Parse(utf8[..utf8Written], CultureInfo.InvariantCulture);
        Demo.Line($"Parse(ReadOnlySpan<byte>)         -> {utf8Parsed}");
#endif

        Demo.Section("比较与排序：IComparable<TSelf> + 四个运算符");

        var low = ProductId.Create(1);
        var high = ProductId.Create(2);
        Demo.Line($"low < high                        -> {low < high}");
        Demo.Line($"low.CompareTo(high)               -> {low.CompareTo(high)}");

        var ordered = new SortedSet<ProductId> { high, low };
        Demo.Line($"SortedSet 顺序                     -> {string.Join(", ", ordered)}");

        Demo.Section("解构与相等性");

        productId.Deconstruct(out var value);
        Demo.Line($"Deconstruct                       -> {value}");
        Demo.Line($"Create(5) == Create(5)            -> {ProductId.Create(5) == ProductId.Create(5)}");

        Demo.Section("字符串驻留：相同值的 string Id 共享同一个实例");

        var first = CustomerId.Create("C-001");
        var second = CustomerId.Create("C-001");
        Demo.Line($"ReferenceEquals(a.Value, b.Value) -> {ReferenceEquals(first.Value, second.Value)}");
    }
}
