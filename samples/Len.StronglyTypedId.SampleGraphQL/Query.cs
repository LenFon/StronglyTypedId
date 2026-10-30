namespace Len.StronglyTypedId.SampleGraphQL;

/// <summary>
/// 示例的查询根：每个字段只回显收到的 Id，让 scalar 的输入与输出两侧都被走到。
/// </summary>
/// <remarks>
/// 方法刻意保持实例成员：HotChocolate 只把实例方法当作字段解析。
/// </remarks>
#pragma warning disable CA1822
public sealed class Query
{
    public OrderDto Order(OrderId id) => new(id);

    public LineDto Line(LineId id) => new(id);

    public StatusDto Status(StatusId id) => new(id);

    public SkuDto Sku(Sku id) => new(id);
}
#pragma warning restore CA1822

public sealed record OrderDto(OrderId Id);

public sealed record LineDto(LineId Id);

public sealed record StatusDto(StatusId Id);

public sealed record SkuDto(Sku Id);
