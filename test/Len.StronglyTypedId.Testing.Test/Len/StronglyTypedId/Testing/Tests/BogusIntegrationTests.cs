using System.Reflection;
using System.Reflection.Emit;
using Bogus;
using FluentAssertions;
using Xunit;

namespace Len.StronglyTypedId.Testing.Tests;

/// <summary>
/// Bogus 适配：<c>faker.Generate()</c> 必须能填出强类型 Id，而不是把它留在默认值。
/// </summary>
/// <remarks>
/// Bogus 按属性的声明类型填值，而强类型 Id 是独立类型，没有适配时属性就停在默认值上且不报任何错。
/// 这类失败在测试里表现为「断言里的 Id 恰好是空的」，容易被误判成被测代码的 bug。
/// </remarks>
public class BogusIntegrationTests
{
    [Fact]
    public void Faker_Should_FillStronglyTypedIdProperty()
    {
        var faker = new Faker<BogusOrder>().WithStronglyTypedIds();

        var order = faker.Generate();

        order.Id.Value.Should().NotBe(Guid.Empty);
        order.Customer.Value.Should().HaveLength(8);
    }

    [Fact]
    public void Faker_Should_FillStronglyTypedIdProperty_WithExplicitIdTypes()
    {
        var faker = new Faker<BogusOrder>()
            .WithStronglyTypedIds(new AnyIdGenerator(), typeof(GuidId), typeof(StringId));

        var order = faker.Generate();

        order.Id.Value.Should().NotBe(Guid.Empty);
        order.Customer.Value.Should().HaveLength(8);
    }

    [Fact]
    public void Faker_WithSeededGenerator_Should_BeReproducible()
    {
        var first = new Faker<BogusOrder>()
            .WithStronglyTypedIds(new AnyIdGenerator(new AnyIdOptions { Seed = 4242 }))
            .Generate();

        var second = new Faker<BogusOrder>()
            .WithStronglyTypedIds(new AnyIdGenerator(new AnyIdOptions { Seed = 4242 }))
            .Generate();

        // 宿主是类而不是 record，值相等要逐个字段比。
        first.Id.Should().Be(second.Id);
        first.Customer.Should().Be(second.Customer);
    }

    [Fact]
    public void Faker_Should_RespectValidator()
    {
        var faker = new Faker<BogusPositiveOrder>().WithStronglyTypedIds();

        var order = faker.Generate();

        order.Id.Value.Should().BePositive();
    }

    /// <summary>
    /// Id 的适配不能挤掉使用者自己写的规则。
    /// </summary>
    /// <remarks>
    /// Bogus 只填有规则的成员，没有规则的成员留在默认值上，这是它本来的行为，适配不该改变它。
    /// </remarks>
    [Fact]
    public void Faker_Should_KeepTheCallersOwnRules()
    {
        var faker = new Faker<BogusOrder>().WithStronglyTypedIds();
        faker.RuleFor(x => x.Quantity, 42);

        var order = faker.Generate();

        order.Quantity.Should().Be(42);
        order.Id.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Faker_Should_RejectNullArguments()
    {
        var act = () => ((Faker<BogusOrder>)null!).WithStronglyTypedIds();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Faker_Should_RejectNullIdType()
    {
        // 省略 idTypes 是「自动发现」的合法用法；显式传一个含 null 的数组才是非法的。
        var act = () => new Faker<BogusOrder>().WithStronglyTypedIds(null, [null!]);

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// 显式传入可空 Id 类型（<c>OrderId?</c>）必须按底层类型注册，而不是把 <c>Nullable&lt;OrderId&gt;</c>
    /// 直接交给 <c>MakeGenericMethod</c> 抛「violates the constraint of type 'TId'」。
    /// </summary>
    [Fact]
    public void Faker_Should_AcceptExplicitNullableIdType()
    {
        var faker = new Faker<BogusNullableOrder>()
            .WithStronglyTypedIds(new AnyIdGenerator(), typeof(GuidId?), typeof(GuidId));

        var order = faker.Generate();

        order.Id.Value.Should().NotBe(Guid.Empty);
        order.OptionalId.Should().NotBeNull("可空 Id 属性必须被填上，而不是留在 null");
        order.OptionalId!.Value.Value.Should().NotBe(Guid.Empty);
    }

    /// <summary>
    /// 自动发现只看到非可空 Id，但可空属性（<c>GuidId?</c>）的声明类型是 <c>Nullable&lt;GuidId&gt;</c>，
    /// 只注册 <c>RuleForType(typeof(GuidId))</c> 匹配不到它，属性会静默留在 null 上。
    /// </summary>
    [Fact]
    public void Faker_Should_FillNullableIdProperty_WhenDiscovering()
    {
        var faker = new Faker<BogusNullableOrder>().WithStronglyTypedIds();

        var order = faker.Generate();

        order.OptionalId.Should().NotBeNull("自动发现也必须为可空 Id 属性注册规则");
        order.OptionalId!.Value.Value.Should().NotBe(Guid.Empty);
    }

    /// <summary>
    /// 自动发现会看到泛型容器里的开放泛型 Id（<c>Box&lt;T&gt;.BoxedId</c>），它无法闭合。
    /// 适配必须静默跳过，而不是让 <c>MakeGenericMethod</c> 抛「not fully instantiated」。
    /// </summary>
    [Fact]
    public void Faker_Should_SkipOpenGenericIds_WhenDiscovering()
    {
        var faker = new Faker<BogusOrder>().WithStronglyTypedIds();

        var order = faker.Generate();

        order.Id.Value.Should().NotBe(Guid.Empty);
    }

    /// <summary>
    /// 显式传入开放泛型时报错：调用方显然以为它会被填上，静默跳过只会让属性停在默认值。
    /// </summary>
    [Fact]
    public void Faker_Should_RejectExplicitOpenGenericIdType()
    {
        var openType = typeof(Box<>).GetNestedType(nameof(Box<>.BoxedId))!;

        var act = () => new Faker<BogusOrder>().WithStronglyTypedIds(null, openType);

        act.Should().Throw<ArgumentException>().WithParameterName("idTypes");
    }

    /// <summary>
    /// 自动发现要扫描 AppDomain 里的全部程序集，其中可能有加载不了的类型（缺依赖）。
    /// 这条路径必须与 <c>StronglyTypedIdConformance.FindBrokenInstances</c> 共用同一份保护，
    /// 否则同一个程序集在两个 API 上表现不同 —— 那正是这层保护曾经只存在于一侧的原因。
    /// </summary>
    [Fact]
    public void Faker_Should_NotThrow_WhenSomeLoadedAssembliesHaveUnloadableTypes()
    {
        // 造一个基类无法解析的程序集并留在 AppDomain 里，让自动发现必然扫到它。
        var dependency = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("BogusMissingDependency"),
            AssemblyBuilderAccess.Run);
        var dependencyType = dependency
            .DefineDynamicModule("BogusMissingDependency.dll")
            .DefineType("Missing.Widget", TypeAttributes.Public | TypeAttributes.Class);

        var consumer = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("BogusConsumer"),
            AssemblyBuilderAccess.Run);
        var consumerType = consumer
            .DefineDynamicModule("BogusConsumer.dll")
            .DefineType("Consumer.Broken", TypeAttributes.Public | TypeAttributes.Class, dependencyType);

        try
        {
            consumerType.CreateType();
        }
        catch (NotSupportedException)
        {
            // 基类未创建时 CreateType 会失败，但类型已进入模块的类型表 —— 这正是要的形态。
        }

        var act = () => new Faker<BogusOrder>().WithStronglyTypedIds().Generate();

        act.Should().NotThrow();
    }
}
