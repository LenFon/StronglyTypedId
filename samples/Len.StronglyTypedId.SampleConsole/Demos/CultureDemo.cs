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
            Demo.Line($"de-DE 下 ToString()                -> {price}");
            Demo.Line($"de-DE 下 Parse(\"1234.5\")           -> {PriceId.Parse("1234.5", null)}");
            Demo.Line($"显式 provider（de-DE）             -> {PriceId.Parse("1234,5", german)}");
            Demo.Line($"ToString(\"C\", de-DE)              -> {price.ToString("C", german)}");
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
