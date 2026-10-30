using System.Globalization;

namespace Len.StronglyTypedId.SampleConsole.Demos;

internal static class StringPrimitiveDemo
{
    public static void Run()
    {
        Demo.Section("string 基元：按序数比较，而不是区域性比较");

        // 软连字符（U+00AD）：区域性比较认为它与不存在等价，序数比较不这么看。
        // 若 CompareTo 走 string.CompareTo，这两个值会「相等却并不 Equals」，SortedSet 会静默丢元素。
        var withSoftHyphen = CustomerId.Create("a\u00ADb");
        var plain = CustomerId.Create("ab");

        Demo.Line($"\"a\\u00ADb\" == \"ab\"               -> {withSoftHyphen == plain}");
        Demo.Line($"CompareTo                          -> {withSoftHyphen.CompareTo(plain)}（非 0，与 Equals 一致）");

        // 区域性比较下 "I" 与 "i" 的顺序会随区域翻转；序数比较恒为 'I'(73) < 'i'(105)。
        var upper = CustomerId.Create("I");
        var lower = CustomerId.Create("i");

        Demo.Line($"\"I\".CompareTo(\"i\")                -> {upper.CompareTo(lower)}（序数：73 - 105）");

        var sorted = new SortedSet<CustomerId> { lower, upper, plain };
        Demo.Line($"SortedSet 顺序                     -> {string.Join(", ", sorted)}");

        Demo.Section("string 基元：TryParse 拒绝 null 与空串，但接受空白");

        Demo.Line($"TryParse(null)                    -> {CustomerId.TryParse(null, CultureInfo.InvariantCulture, out _)}");
        Demo.Line($"TryParse(\"\")                      -> {CustomerId.TryParse(string.Empty, CultureInfo.InvariantCulture, out _)}");
        Demo.Line($"TryParse(\" \")                     -> {CustomerId.TryParse(" ", CultureInfo.InvariantCulture, out _)}");

        Demo.Section("string 基元：空串可以当字典键往返（值本身合法时）");

        // 空串被 TryParse 拒绝，但经 Create 构造的空串 Id 仍能作为字典键往返——
        // 键路径走的是 WriteAsPropertyName / ReadAsPropertyName，与 Parse 的入口不同。
        var empty = CustomerId.Create(string.Empty);
        var json = System.Text.Json.JsonSerializer.Serialize(new Dictionary<CustomerId, int> { [empty] = 1 });

        Demo.Line($"Serialize(Dictionary)              -> {json}");
        Demo.Line($"Deserialize 往返                   -> {System.Text.Json.JsonSerializer.Deserialize<Dictionary<CustomerId, int>>(json)![empty]}");
    }
}
