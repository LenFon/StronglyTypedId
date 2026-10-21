using AutoFixture;
using Len.StronglyTypedId.Testing;

namespace Len.StronglyTypedId.SampleConsole.Demos;

internal static class TestingDemo
{
    public static void Run()
    {
        Demo.Section("测试辅助：Any.Id / Any.Ids");

        Demo.Line($"Any.Id<OrderId>()                 -> {Any.Id<OrderId>()}");
        Demo.Line($"Any.Ids<OrderId>(3)               -> {string.Join(", ", Any.Ids<OrderId>(3))}");

        Demo.Section("可复现：同一 seed 得到同一序列");

        var first = Any.Ids<OrderId>(3, seed: 42);
        var second = Any.Ids<OrderId>(3, seed: 42);
        Demo.Line($"两次 Ids(3, seed: 42) 相同         -> {first.SequenceEqual(second)}");

        Demo.Section("可覆盖：Register 指定工厂");

        var fixedId = OrderId.Create(Guid.Parse("55555555-6666-7777-8888-999999999999"));
        Any.Register<OrderId>(() => fixedId);
        Demo.Line($"Any.Id<OrderId>()                 -> {Any.Id<OrderId>()}");
        Any.Reset();

        Demo.Section("AutoFixture：fixture.Create<T>() 自动填充 Id");

        var fixture = new Fixture().WithStronglyTypedIds();
        var order = fixture.Create<PurchaseOrder>();
        Demo.Line($"fixture.Create<PurchaseOrder>()   -> Id = {order.Id}, Buyer = {order.Buyer}");
    }
}

internal sealed record PurchaseOrder(OrderId Id, CustomerId Buyer);
