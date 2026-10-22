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

            // de-DE 的小数分隔符是逗号：若生成代码漏掉不变文化，"1234.5" 会被读成 12345。
            // 注意这里必须显式调用 ToString()：字符串插值会走 IFormattable.ToString(null, 当前区域)，
            // 打印出来的是 de-DE 形态，看起来像「规范文本漂移了」，其实只是插值自己换了 provider。
            // 所以下面几行的 `{x.ToString()}` 不能按 IDE0071 简化成 `{x}` —— 那会把不变文化换成
            // 当前区域，正好抹平本示例要展示的差异。
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
