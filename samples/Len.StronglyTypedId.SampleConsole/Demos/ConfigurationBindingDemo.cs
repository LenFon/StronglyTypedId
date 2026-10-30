using System.ComponentModel;
using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace Len.StronglyTypedId.SampleConsole.Demos;

internal static class ConfigurationBindingDemo
{
    public static void Run()
    {
        Demo.Section("配置绑定：只有开了 TypeConverter 的 Id 才能被绑定");

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Order:Warehouse"] = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                ["Order:Id"] = "3f2c1b4e-0000-4000-8000-000000000001",
            })
            .Build();

        var settings = configuration.GetSection("Order").Get<OrderSettings>()!;

        Demo.Line($"WarehouseId（TypeConverter = true） -> {settings.Warehouse}");
        // 不开 TypeConverter 时 binder 静默失败：不抛异常、不报警告，属性停在 default。
        Demo.Line($"OrderId（未开 TypeConverter）       -> {settings.Id}（静默留在 default）");

        Demo.Section("配置绑定：标量重载在未开 TypeConverter 时抛 NullReferenceException");

        Demo.Line($"GetValue<WarehouseId>             -> {configuration.GetValue<WarehouseId>("Order:Warehouse")}");

        try
        {
            _ = configuration.GetValue<OrderId>("Order:Id");
        }
        catch (NullReferenceException)
        {
            Demo.Line("GetValue<OrderId>                 -> 抛出 NullReferenceException（binder 内部）");
        }

        Demo.Section("TypeConverter：TypeDescriptor 往返与拒绝非字符串输入");

        var converter = TypeDescriptor.GetConverter(typeof(WarehouseId));
        var warehouse = WarehouseId.Create(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));
        var text = converter.ConvertToInvariantString(warehouse)!;

        Demo.Line($"ConvertToInvariantString          -> {text}");
        Demo.Line($"ConvertFromInvariantString        -> {converter.ConvertFromInvariantString(text)}");

        try
        {
            _ = converter.ConvertFrom(null, CultureInfo.InvariantCulture, 42);
        }
        catch (NotSupportedException)
        {
            Demo.Line("ConvertFrom(42)                   -> 抛出 NotSupportedException");
        }
    }
}

/// <summary>
/// 配置绑定的目标类型：一个开了 <c>TypeConverter</c> 的 Id，一个没开的作对照。
/// </summary>
internal sealed class OrderSettings
{
    public WarehouseId Warehouse { get; set; }

    public OrderId Id { get; set; }
}
