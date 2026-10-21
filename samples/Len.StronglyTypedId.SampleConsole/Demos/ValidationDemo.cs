using System.Globalization;

namespace Len.StronglyTypedId.SampleConsole.Demos;

internal static class ValidationDemo
{
    public static void Run()
    {
        Demo.Section("验证器：Create / TryCreate / TryParse 共用同一条规则");

        var quantity = QuantityId.Create(5);
        Demo.Line($"QuantityId.Create(5)              -> {quantity}");

        try
        {
            _ = QuantityId.Create(0);
        }
        catch (ArgumentException ex)
        {
            Demo.Line($"QuantityId.Create(0)              -> 抛出 {ex.GetType().Name}");
        }

        var created = QuantityId.TryCreate(0, out var invalid);
        Demo.Line($"TryCreate(0)                      -> {created}, result = {invalid}");

        var parsed = QuantityId.TryParse("-1", CultureInfo.InvariantCulture, out _);
        Demo.Line($"TryParse(\"-1\")                     -> {parsed}");

        var parsedOk = QuantityId.TryParse("12", CultureInfo.InvariantCulture, out var parsedId);
        Demo.Line($"TryParse(\"12\")                     -> {parsedOk}, {parsedId}");

        Demo.Section("没有验证器的 Id 接受任意值");

        Demo.Line($"ProductId.Create(-1)              -> {ProductId.Create(-1)}");
    }
}
