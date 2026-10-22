using System.Globalization;

namespace Len.StronglyTypedId.SampleConsole.Demos;

internal static class CultureDemo
{
    public static void Run()
    {
        Demo.Section("文化不变性：规范文本形与解析都不随当前区域漂移");

        var price = PriceId.Create(1234.5m);
        var german = new CultureInfo("de-DE");
        var original = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = german;

            // de-DE 的小数分隔符是逗号：漏掉不变文化，"1234.5" 会被读成 12345。
            // 必须显式 ToString()：插值走 IFormattable + 当前区域，不能按 IDE0071 简化成 {x}。
#pragma warning disable IDE0071 // 插值可以简化：此处显式 ToString() 是刻意的，与插值的 provider 不同
            Demo.Line($"de-DE 下 ToString()                -> {price.ToString()}");
            Demo.Line($"de-DE 下 $\"{{id}}\"（插值走当前区域）  -> {price}");
            Demo.Line($"de-DE 下 Parse(\"1234.5\")           -> {PriceId.Parse("1234.5", null).ToString()}");
            Demo.Line($"显式 provider（de-DE）             -> {PriceId.Parse("1234,5", german).ToString()}");
#pragma warning restore IDE0071
            Demo.Line($"ToString(\"C\", de-DE)              -> {price.ToString("C", german)}");
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
