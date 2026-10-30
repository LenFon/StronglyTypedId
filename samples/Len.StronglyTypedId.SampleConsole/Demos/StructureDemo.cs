using System.Globalization;

namespace Len.StronglyTypedId.SampleConsole.Demos;

internal static class StructureDemo
{
    public static void Run()
    {
        Demo.Section("嵌套类型：Id 声明在 partial 容器内");

        var aggregateId = OrderAggregate.AggregateId.Create(Guid.Parse("22222222-3333-4444-5555-666666666666"));
        Demo.Line($"OrderAggregate.AggregateId        -> {aggregateId}");

        Demo.Section("泛型容器：核心能力齐全");

        var boxed = Box<int>.BoxedId.Create(Guid.Parse("33333333-4444-5555-6666-777777777777"));
        Demo.Line($"Box<int>.BoxedId                  -> {boxed}");
        Demo.Line($"Parse(ToString()) 往返             -> {Box<int>.BoxedId.Parse(boxed.ToString(), CultureInfo.InvariantCulture) == boxed}");

        Demo.Section("泛型工厂：面向 IStronglyTypedId<TSelf, TPrimitive> 编程");

        var orderId = IdFactory<OrderId, Guid>.Create(Guid.Parse("44444444-5555-6666-7777-888888888888"));
        Demo.Line($"IdFactory<OrderId, Guid>.Create   -> {orderId}");

        Demo.Section("单参数接口：只表达「某个强类型 Id」，不必指明基元");

        Demo.Line($"Describe(OrderId)                 -> {IdDescriber.Describe(orderId)}");
        Demo.Line($"Describe(ProductId)               -> {IdDescriber.Describe(ProductId.Create(42))}");
        Demo.Line($"Describe(RegionId)                -> {IdDescriber.Describe(RegionId.Create(Region.North))}");

        Demo.Section("IFormattableStronglyTypedId：基元可格式化时才生成");

        Demo.Line($"OrderId 实现                       -> {ImplementsFormattable(typeof(OrderId))}");
        Demo.Line($"CustomerId（string 基元）实现       -> {ImplementsFormattable(typeof(CustomerId))}");
        Demo.Line($"RegionId（enum 基元）实现           -> {ImplementsFormattable(typeof(RegionId))}");
        Demo.Line($"Render(OrderId)                   -> {FormattableRenderer.Render(orderId)}");

        Demo.Section("接口清单：一个 Guid 基元的 Id 实现了哪些契约");

        foreach (var @interface in typeof(OrderId).GetInterfaces().OrderBy(name => name.Name, StringComparer.Ordinal))
        {
            Demo.Line(@interface.Name);
        }
    }

    private static bool ImplementsFormattable(Type type)
        => type.GetInterfaces().Any(@interface =>
            @interface.IsGenericType
            && @interface.GetGenericTypeDefinition() == typeof(IFormattableStronglyTypedId<>));
}

internal static class IdFactory<TId, TPrimitive>
    where TId : IStronglyTypedId<TId, TPrimitive>
    where TPrimitive : notnull, IComparable
{
    public static TId Create(TPrimitive value) => TId.Create(value);
}

/// <summary>
/// 只约束「某个强类型 Id」：不知道基元类型，也能用 <c>Parse</c> 与 <c>==</c>。
/// </summary>
internal static class IdDescriber
{
    public static string Describe<TId>(TId id)
        where TId : IStronglyTypedId<TId>
        => $"{typeof(TId).Name} = {id}, 往返 = {TId.Parse(id.ToString()!, CultureInfo.InvariantCulture) == id}";
}

/// <summary>
/// 约束到「基元可格式化」的 Id：因此可以统一调用 <c>ToString(format, provider)</c>。
/// </summary>
internal static class FormattableRenderer
{
    public static string Render<TId>(TId id)
        where TId : IFormattableStronglyTypedId<TId>
        => id.ToString("N", CultureInfo.InvariantCulture);
}
