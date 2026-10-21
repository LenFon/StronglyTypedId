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

        Demo.Section("泛型工厂：面向 IStronglyTypedId<TSelf, TPrimitive> 编程");

        var orderId = IdFactory<OrderId, Guid>.Create(Guid.Parse("44444444-5555-6666-7777-888888888888"));
        Demo.Line($"IdFactory<OrderId, Guid>.Create   -> {orderId}");
    }
}

internal static class IdFactory<TId, TPrimitive>
    where TId : IStronglyTypedId<TId, TPrimitive>
    where TPrimitive : notnull, IComparable
{
    public static TId Create(TPrimitive value) => TId.Create(value);
}
