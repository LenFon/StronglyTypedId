using System.Reflection;
using FluentAssertions;

namespace Len.StronglyTypedId;

public class StronglyTypedIdTests
{
    [Theory]
    [InlineData(typeof(GuidId), typeof(Guid))]
    [InlineData(typeof(StringId), typeof(string))]
    [InlineData(typeof(Int32Id), typeof(int))]
    public void TryGetPrimitiveIdType_Should_ReturnTrue_When(Type type, Type expectedPrimitiveId)
    {
        type.TryGetPrimitiveIdType(out var actualPrimitiveIdType).Should().BeTrue();
        actualPrimitiveIdType.Should().Be(expectedPrimitiveId);
    }

    [Theory]
    [InlineData(typeof(NotStronglyTypedId), null)]
    [InlineData(typeof(IStronglyTypedId<GuidId, Guid>), null)]
    public void TryGetPrimitiveIdType_Should_ReturnFalse_When(Type type, Type? expectedPrimitiveId)
    {
        type.TryGetPrimitiveIdType(out var actualPrimitiveIdType).Should().BeFalse();
        actualPrimitiveIdType.Should().Be(expectedPrimitiveId);
    }

    [Fact]
    public void TryGetPrimitiveIdType_Should_ThrowArgumentNullException_WhenTypeIsNull()
    {
        Type? type = null;

        var ex = Assert.Throws<ArgumentNullException>(() => type!.TryGetPrimitiveIdType(out _));

        Assert.Equal("type", ex.ParamName);
    }

    [Theory]
    [InlineData(typeof(GuidId), typeof(Guid))]
    [InlineData(typeof(StringId), typeof(string))]
    [InlineData(typeof(Int32Id), typeof(int))]
    public void GetPrimitiveIdType_Should_ReturnType_When(Type type, Type expectedPrimitiveId) => type.GetPrimitiveIdType().Should().Be(expectedPrimitiveId);

    [Theory]
    [InlineData(typeof(NotStronglyTypedId), null)]
    [InlineData(typeof(IStronglyTypedId<GuidId, Guid>), null)]
    public void GetPrimitiveIdType_Should_ReturnNull_When(Type type, Type? expectedPrimitiveId) => type.GetPrimitiveIdType().Should().Be(expectedPrimitiveId);

    [Theory]
    [InlineData(typeof(GuidId))]
    [InlineData(typeof(StringId))]
    [InlineData(typeof(Int32Id))]
    public void IsStronglyTypedId_Should_ReturnTrue_When(Type type) => type.IsStronglyTypedId().Should().BeTrue();

    [Theory]
    [InlineData(typeof(NotStronglyTypedId))]
    [InlineData(typeof(IStronglyTypedId<GuidId, Guid>))]
    [InlineData(typeof(Guid))]
    public void IsStronglyTypedId_Should_ReturnFalse_When(Type type) => type.IsStronglyTypedId().Should().BeFalse();

    /// <summary>
    /// 两个入参为 null 的重载都必须先抛 <see cref="ArgumentNullException"/>，而不是在解引用时抛 NRE。
    /// </summary>
    [Fact]
    public void GetPrimitiveIdType_Should_ThrowArgumentNullException_WhenTypeIsNull()
    {
        Type? type = null;

        var exception = Assert.Throws<ArgumentNullException>(() => type!.GetPrimitiveIdType());

        Assert.Equal("type", exception.ParamName);
    }

    [Fact]
    public void IsStronglyTypedId_Should_ThrowArgumentNullException_WhenTypeIsNull()
    {
        Type? type = null;

        var exception = Assert.Throws<ArgumentNullException>(() => type!.IsStronglyTypedId());

        Assert.Equal("type", exception.ParamName);
    }

    /// <summary>
    /// 特性契约：只能标在 class / struct 上，且不可继承、不允许多次标注。
    /// </summary>
    [Fact]
    public void StronglyTypedIdAttribute_Should_DeclareExpectedUsage()
    {
        var usage = typeof(StronglyTypedIdAttribute).GetCustomAttribute<AttributeUsageAttribute>();

        usage.Should().NotBeNull();
        usage!.ValidOn.Should().Be(AttributeTargets.Class | AttributeTargets.Struct);
        usage.AllowMultiple.Should().BeFalse();
        usage.Inherited.Should().BeFalse();
    }
}