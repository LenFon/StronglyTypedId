using System;
using AutoFixture;
using FluentAssertions;
using Xunit;

namespace Len.StronglyTypedId.Testing.Tests;

/// <summary>
/// AutoFixture 适配：<c>fixture.Create&lt;OrderId&gt;()</c> 必须走生成的 <c>Create</c>，而不是 AutoFixture
/// 默认的「挑一个构造函数」策略——后者会绕过验证器。
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
}
