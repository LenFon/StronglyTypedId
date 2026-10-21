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

        Demo.Section("验证失败消息脱敏：RedactValidationMessage");

        // 默认：消息里带着被拒的原文，便于排查，但那可能就是用户的邮箱。
        try
        {
            _ = EmailAddress.Create("not-an-email");
        }
        catch (ArgumentException ex)
        {
            Demo.Line($"未脱敏                             -> {ex.Message}");
        }

        // 打开后：只剩类型名，原文不进日志 / APM / 错误上报。
        try
        {
            _ = RedactedEmailId.Create("not-an-email");
        }
        catch (ArgumentException ex)
        {
            Demo.Line($"已脱敏                             -> {ex.Message}");
        }
    }
}
