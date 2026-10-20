using System.Globalization;

namespace Len.StronglyTypedId.SampleConsole.Demos;

internal static class ExceptionDemo
{
    public static void Run()
    {
        Demo.Section("验证失败异常类型：ExceptionType");

        Demo.Line($"StockLevelId.Create(3)            -> {StockLevelId.Create(3)}");

        try
        {
            _ = StockLevelId.Create(0);
        }
        catch (StockLevelOutOfRangeException ex)
        {
            Demo.Line($"Create(0)                         -> 抛出 {ex.GetType().Name}");
        }

        try
        {
            _ = StockLevelId.Parse("0", CultureInfo.InvariantCulture);
        }
        catch (FormatException ex)
        {
            Demo.Line($"Parse(\"0\")                        -> 抛出 {ex.GetType().Name}（解析失败始终是 FormatException）");
        }

        Demo.Line($"TryCreate(0)                      -> {StockLevelId.TryCreate(0, out _)}");
        Demo.Line($"TryParse(\"0\")                      -> {StockLevelId.TryParse("0", CultureInfo.InvariantCulture, out _)}");
    }
}
