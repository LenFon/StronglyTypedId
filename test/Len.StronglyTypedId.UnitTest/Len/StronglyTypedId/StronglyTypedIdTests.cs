namespace Len.StronglyTypedId;

public class StronglyTypedIdTests
{
    [Fact]
    public void TryGetPrimitiveIdType()
    {
        var isStronglyTypedId = typeof(GuidId).TryGetPrimitiveIdType(out var primitiveIdType);

        Assert.True(isStronglyTypedId);
        Assert.Equal(typeof(Guid), primitiveIdType);

        var isStronglyTypedId2 = typeof(NotStronglyTypedId).TryGetPrimitiveIdType(out var primitiveIdType2);

        Assert.False(isStronglyTypedId2);
        Assert.Null(primitiveIdType2);

        var isStronglyTypedId3 = typeof(IStronglyTypedId<Guid>).TryGetPrimitiveIdType(out var primitiveIdType3);

        Assert.False(isStronglyTypedId3);
        Assert.Null(primitiveIdType3);

        var isStronglyTypedId4 = typeof(AbstractStronglyTypedId).TryGetPrimitiveIdType(out var primitiveIdType4);

        Assert.False(isStronglyTypedId4);
        Assert.Null(primitiveIdType4);
    }

    [Fact]
    public void TryGetPrimitiveIdType_Type_Is_Null()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
        {
            Type? type = null;

            var isStronglyTypedId = type!.TryGetPrimitiveIdType(out var primitiveIdType);
        });

        Assert.Equal("type", ex.ParamName);
    }

    [Fact]
    public void GetPrimitiveIdType()
    {
        var primitiveIdType = typeof(GuidId).GetPrimitiveIdType();

        Assert.NotNull(primitiveIdType);
        Assert.Equal(typeof(Guid), primitiveIdType);

        var primitiveIdType2 = typeof(NotStronglyTypedId).GetPrimitiveIdType();

        Assert.Null(primitiveIdType2);
    }

    [Fact]
    public void IsStronglyTypedId()
    {
        Assert.True(typeof(GuidId).IsStronglyTypedId());
        Assert.False(typeof(NotStronglyTypedId).IsStronglyTypedId());
    }
}