using AutoFixture;
using Bogus;
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

        Demo.Section("AnyIdOptions：StringLength / IncludeBoundaryValues / PrimitiveValueFactory");

        var longStrings = new AnyIdGenerator(new AnyIdOptions { Seed = 7, StringLength = 20 });
        Demo.Line($"StringLength = 20                 -> {longStrings.Id<CustomerId>().Value}");

        // 边界值约每 16 个掺一个；断言唯一性时不要开，这里只是展示它确实会产出极值。
        var boundary = new AnyIdGenerator(new AnyIdOptions { Seed = 7, IncludeBoundaryValues = true });
        Demo.Line($"IncludeBoundaryValues 取样          -> {string.Join(", ", boundary.Ids<QuantityId>(6))}");

        // 返回 null 表示「这个基元我不处理」，回落到内置生成逻辑。
        var custom = new AnyIdGenerator(new AnyIdOptions
        {
            Seed = 7,
            PrimitiveValueFactory = type => type == typeof(Guid) ? Guid.Parse("77777777-7777-7777-7777-777777777777") : null,
        });
        Demo.Line($"PrimitiveValueFactory             -> {custom.Id<OrderId>()}");

        Demo.Section("非泛型入口：AnyIdGenerator.Create(Type)");

        var generator = new AnyIdGenerator(new AnyIdOptions { Seed = 7 });
        Demo.Line($"Create(typeof(ProductId))         -> {generator.Create(typeof(ProductId))}");

        try
        {
            _ = generator.Create(typeof(string));
        }
        catch (ArgumentException ex)
        {
            Demo.Line($"Create(typeof(string))            -> 抛出 {ex.GetType().Name}");
        }

        Demo.Section("命名实例体检：StronglyTypedIdConformance");

        // 本程序集里有一个故意写坏的实例（RejectedInstanceId.Zero），它会在首次触碰时抛
        // TypeInitializationException；FindBrokenInstances 一次列出全部坏实例，而不是遇到第一个就停。
        var broken = StronglyTypedIdConformance.FindBrokenInstances(typeof(Program).Assembly);
        Demo.Line($"FindBrokenInstances 数量           -> {broken.Count}");

        foreach (var (idType, message) in broken)
        {
            Demo.Line($"{idType.Name,-28}-> {message}");
        }

        Demo.Section("AutoFixture：fixture.Create<T>() 自动填充 Id");

        var fixture = new Fixture().WithStronglyTypedIds();
        var order = fixture.Create<PurchaseOrder>();
        Demo.Line($"fixture.Create<PurchaseOrder>()   -> Id = {order.Id}, Buyer = {order.Buyer}");

        Demo.Section("Bogus：faker.Generate() 自动填充 Id");

        var faker = new Faker<BogusOrder>().WithStronglyTypedIds();
        var bogusOrder = faker.Generate();
        Demo.Line($"faker.Generate()                  -> Id = {bogusOrder.Id}, Buyer = {bogusOrder.Buyer}");

        // 没有适配时 Bogus 把 Id 留在默认值上，且不报任何错——这是最容易误判成业务 bug 的形态。
        var unadapted = new Faker<BogusOrder>().Generate();
        Demo.Line($"未适配时 Id.Value                  -> {unadapted.Id.Value}（静默留在 default）");
    }
}

internal sealed record PurchaseOrder(OrderId Id, CustomerId Buyer);

/// <summary>
/// Bogus 的宿主类型：Bogus 默认走无参构造，因此用「类 + 可写属性」而不是 record。
/// </summary>
internal sealed class BogusOrder
{
    public OrderId Id { get; set; }

    public CustomerId Buyer { get; set; }
}
