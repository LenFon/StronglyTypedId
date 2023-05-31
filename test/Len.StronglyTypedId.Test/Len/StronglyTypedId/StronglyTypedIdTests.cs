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
    public void TryGetPrimitiveIdType_Should_ReturnFalse_WhenTypeIsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
        {
            Type? type = null;

            var isStronglyTypedId = type!.TryGetPrimitiveIdType(out var primitiveIdType);
        });

        Assert.Equal("type", ex.ParamName);
    }

    [Theory]
    [InlineData(typeof(GuidId), typeof(Guid))]
    [InlineData(typeof(StringId), typeof(string))]
    [InlineData(typeof(Int32Id), typeof(int))]
    public void GetPrimitiveIdType_Should_ReturnType_When(Type type, Type expectedPrimitiveId)
    {
        type.GetPrimitiveIdType().Should().Be(expectedPrimitiveId);
    }

    [Theory]
    [InlineData(typeof(NotStronglyTypedId), null)]
    [InlineData(typeof(IStronglyTypedId<GuidId, Guid>), null)]
    public void GetPrimitiveIdType_Should_ReturnNull_When(Type type, Type expectedPrimitiveId)
    {
        type.GetPrimitiveIdType().Should().Be(expectedPrimitiveId);
    }

    [Theory]
    [InlineData(typeof(GuidId))]
    [InlineData(typeof(StringId))]
    [InlineData(typeof(Int32Id))]
    public void IsStronglyTypedId_Should_ReturnTrue_When(Type type)
    {
        type.IsStronglyTypedId().Should().BeTrue();
    }

    [Theory]
    [InlineData(typeof(NotStronglyTypedId))]
    [InlineData(typeof(IStronglyTypedId<GuidId, Guid>))]
    [InlineData(typeof(Guid))]
    public void IsStronglyTypedId_Should_ReturnFalse_When(Type type)
    {
        type.IsStronglyTypedId().Should().BeFalse();
    }
}