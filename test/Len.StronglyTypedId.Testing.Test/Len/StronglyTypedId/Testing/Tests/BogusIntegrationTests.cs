using Bogus;
using FluentAssertions;
using Xunit;

namespace Len.StronglyTypedId.Testing.Tests;

/// <summary>
/// Bogus 适配：<c>faker.Generate()</c> 必须能填出强类型 Id，而不是把它留在默认值。
/// </summary>
/// <remarks>
/// Bogus 按属性的声明类型填值，而强类型 Id 是一个独立的类型 —— 没有适配时，Bogus 无从下手，
/// 属性就停在默认值上，且**不报任何错**。这类失败在测试里表现为「断言里的 Id 恰好是空的」，
/// 很容易被误判成被测代码的 bug。
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

        // 宿主是类而不是 record —— 值相等要逐个字段比。
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
    /// Bogus 只填有规则的成员 —— 没有规则的成员留在默认值上，这是它本来的行为，适配不该改变它。
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
}
