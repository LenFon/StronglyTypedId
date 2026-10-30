using System;
using AutoFixture;
using FluentAssertions;
using Xunit;

namespace Len.StronglyTypedId.Testing.Tests;

/// <summary>
/// AutoFixture 适配：<c>fixture.Create&lt;OrderId&gt;()</c> 必须走生成的 <c>Create</c>，而不是 AutoFixture
/// 默认的「挑一个构造函数」策略，后者会绕过验证器。
/// </summary>
public class AutoFixtureIntegrationTests
{
    [Fact]
    public void Fixture_Should_CreateStronglyTypedId()
    {
        var fixture = new Fixture().WithStronglyTypedIds();

        fixture.Create<GuidId>().Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Fixture_Should_FillIdConstructorParameters()
    {
        var fixture = new Fixture().WithStronglyTypedIds();

        var order = fixture.Create<Order>();

        order.Id.Value.Should().NotBe(Guid.Empty);
        order.Customer.Value.Should().HaveLength(8);
    }

    [Fact]
    public void Fixture_WithSeededGenerator_Should_BeReproducible()
    {
        var first = new Fixture()
            .WithStronglyTypedIds(new AnyIdGenerator(new AnyIdOptions { Seed = 4242 }))
            .Create<GuidId>();

        var second = new Fixture()
            .WithStronglyTypedIds(new AnyIdGenerator(new AnyIdOptions { Seed = 4242 }))
            .Create<GuidId>();

        first.Should().Be(second);
    }

    [Fact]
    public void Fixture_Should_StillCreateOrdinaryTypes()
    {
        var fixture = new Fixture().WithStronglyTypedIds();

        fixture.Create<int>().Should().NotBe(0);
        fixture.Create<string>().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Fixture_Should_RespectValidator()
    {
        var fixture = new Fixture().WithStronglyTypedIds();

        fixture.Create<PositiveIntId>().Value.Should().BePositive();
    }

    [Fact]
    public void Customization_Should_RejectNullGenerator()
    {
        var act = () => new StronglyTypedIdCustomization(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// 无参构造走默认生成器：直接 <c>fixture.Customize(new StronglyTypedIdCustomization())</c>
    /// 与 <c>WithStronglyTypedIds()</c> 等价，两条入口都不能漏。
    /// </summary>
    [Fact]
    public void Customization_Should_UseDefaultGenerator_WhenConstructedWithoutArguments()
    {
        var fixture = new Fixture().Customize(new StronglyTypedIdCustomization());

        fixture.Create<GuidId>().Value.Should().NotBe(Guid.Empty);
    }

    /// <summary>
    /// 自定义项必须插在列表最前面：AutoFixture 按顺序取第一个能处理的 builder，
    /// 排在后面就会被构造函数策略抢先，从而绕过 <c>Create</c> 与验证器。
    /// </summary>
    [Fact]
    public void Customization_Should_BeInsertedBeforeDefaultBuilders()
    {
        var fixture = new Fixture();
        var customization = new StronglyTypedIdCustomization(new AnyIdGenerator(new AnyIdOptions { Seed = 7 }));

        customization.Customize(fixture);

        fixture.Customizations[0].Should().BeOfType<StronglyTypedIdSpecimenBuilder>();
    }
}
