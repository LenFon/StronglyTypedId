using System.ComponentModel;
using System.Globalization;
using System.Text.Json;

namespace Len.StronglyTypedId.SampleDefaults;

public static class Program
{
    public static void Main()
    {
        Section("程序集默认：验证器 + 归一化 + 自定义异常 + 消息脱敏");
        DefaultsSection();

        Section("逐类型覆盖：验证器 / 异常类型 / JSON 形态 / 前缀读回方式");
        OverridesSection();

        Section("程序集默认：TypeConverter 与 JSON 对象形态");
        ConverterAndShapeSection();

        End();
    }

    private static void DefaultsSection()
    {
        var tenant = TenantId.Create("  ACME  ");
        Line($"Create(\"  ACME  \")                 -> {tenant}（归一化后小写）");

        try
        {
            _ = TenantId.Create("   ");
        }
        catch (DefaultRuleViolationException ex)
        {
            // 程序集默认打开了 RedactValidationMessage，消息里不含被拒原文。
            Line($"Create(\"   \")                    -> 抛出 {ex.GetType().Name}: {ex.Message}");
        }

        Line($"TryCreate(\"   \")                  -> {TenantId.TryCreate("   ", out _)}");
    }

    private static void OverridesSection()
    {
        Line($"EmailId.Create(\"  A@B.COM \")      -> {EmailId.Create("  A@B.COM ")}");

        try
        {
            _ = EmailId.Create("not-an-email");
        }
        catch (DefaultRuleViolationException ex)
        {
            Line($"EmailId.Create(\"not-an-email\")    -> 抛出 {ex.GetType().Name}（逐类型验证器仍用程序集异常类型）");
        }

        try
        {
            _ = SuspendedTenantId.Create("suspended");
        }
        catch (TenantSuspendedException ex)
        {
            Line($"SuspendedTenantId.Create(...)     -> 抛出 {ex.GetType().Name}（逐类型 ExceptionType 覆盖）");
        }

        // 程序集默认 JsonShape.Object，这个 Id 逐类型写回 Primitive。
        Line($"TenantId JSON                     -> {JsonSerializer.Serialize(TenantId.Create("acme"))}");
        Line($"PrimitiveShapeId JSON             -> {JsonSerializer.Serialize(PrimitiveShapeId.Create("acme"))}");

        // 程序集默认 PrefixReadMode.Strict，因此裸值被拒；逐类型写回 Lenient 的那个接受裸值。
        Line($"PrefixedTenantId.TryParse(\"acme\")  -> {PrefixedTenantId.TryParse("acme", CultureInfo.InvariantCulture, out _)}（Strict）");
        Line($"PrefixedTenantId.TryParse(\"tn_acme\") -> {PrefixedTenantId.TryParse("tn_acme", CultureInfo.InvariantCulture, out _)}");
        Line($"LenientPrefixedId.TryParse(\"acme\") -> {LenientPrefixedId.TryParse("acme", CultureInfo.InvariantCulture, out _)}（Lenient）");

        // 程序集默认 Integrations = SystemTextJson，因此默认就有 JSON 转换器；逐类型写 None 的那个没有。
        Line($"TenantId 有 [JsonConverter]        -> {HasJsonConverter(typeof(TenantId))}（程序集默认 SystemTextJson）");
        Line($"CoreOnlyTenantId 有 [JsonConverter] -> {HasJsonConverter(typeof(CoreOnlyTenantId))}（逐类型 None）");
        Line($"CoreOnlyTenantId 核心能力           -> {CoreOnlyTenantId.Parse(CoreOnlyTenantId.Create("acme").ToString(), CultureInfo.InvariantCulture) == CoreOnlyTenantId.Create("acme")}");
    }

    private static bool HasJsonConverter(Type type)
        => type.IsDefined(typeof(System.Text.Json.Serialization.JsonConverterAttribute), inherit: false);

    private static void ConverterAndShapeSection()
    {
        // 程序集默认 TypeConverter = true，因此每个 Id 都能经 TypeDescriptor 往返。
        var converter = TypeDescriptor.GetConverter(typeof(TenantId));
        var text = converter.ConvertToInvariantString(TenantId.Create("acme"))!;

        Line($"TypeDescriptor 往返                -> {text} -> {converter.ConvertFromInvariantString(text)}");

        var prefixedConverter = TypeDescriptor.GetConverter(typeof(PrefixedTenantId));
        var prefixedText = prefixedConverter.ConvertToInvariantString(PrefixedTenantId.Create("acme"))!;

        Line($"带前缀的 TypeConverter             -> {prefixedText} -> {prefixedConverter.ConvertFromInvariantString(prefixedText)}");
    }

    private static void Section(string title)
    {
        Console.WriteLine();
        Console.WriteLine($"── {title} ──");
    }

    private static void Line(string text) => Console.WriteLine($"   {text}");

    private static void End()
    {
        Console.WriteLine();
        Console.WriteLine("全部示例执行完毕。");
    }
}
