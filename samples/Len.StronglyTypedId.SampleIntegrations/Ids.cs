using Len.StronglyTypedId;

// 程序集级默认验证器：本程序集内所有 Id 都套用它，除非类型自己声明了 Validator。
// 每个 Id 仍须提供同名静态方法，否则 STIA0010 会报错。
[assembly: StronglyTypedIdDefaults(Validator = "Validate")]

namespace Len.StronglyTypedId.SampleIntegrations;

[StronglyTypedId]
public partial record struct OrderId(Guid Value)
{
    private static bool Validate(Guid value) => value != Guid.Empty;
}

[StronglyTypedId]
public partial record struct LineId(int Value)
{
    private static bool Validate(int value) => value > 0;
}

[StronglyTypedId]
public partial record struct Sku(string Value)
{
    private static bool Validate(string value) => !string.IsNullOrWhiteSpace(value);
}

internal sealed class Order
{
    public OrderId Id { get; set; }

    public LineId Line { get; set; }

    public Sku Code { get; set; }

    public int Quantity { get; set; }
}
