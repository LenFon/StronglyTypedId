namespace Len.StronglyTypedId;

public class StronglyTypedIdTests
{
    [Fact]
    public void TryGetPrimitiveIdType()
    {
        var isStronglyTypedId = typeof(Id).TryGetPrimitiveIdType(out var primitiveIdType);

        Assert.True(isStronglyTypedId);
        Assert.Equal(typeof(Guid), primitiveIdType);

        var isStronglyTypedId2 = typeof(Id3).TryGetPrimitiveIdType(out var primitiveIdType2);

        Assert.False(isStronglyTypedId2);
        Assert.Null(primitiveIdType2);

        var isStronglyTypedId3 = typeof(IStronglyTypedId<Guid>).TryGetPrimitiveIdType(out var primitiveIdType3);

        Assert.False(isStronglyTypedId3);
        Assert.Null(primitiveIdType3);

        var isStronglyTypedId4 = typeof(Id2).TryGetPrimitiveIdType(out var primitiveIdType4);

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
        var primitiveIdType = typeof(Id).GetPrimitiveIdType();

        Assert.NotNull(primitiveIdType);
        Assert.Equal(typeof(Guid), primitiveIdType);

        var primitiveIdType2 = typeof(Id3).GetPrimitiveIdType();

        Assert.Null(primitiveIdType2);
    }

    [Fact]
    public void IsStronglyTypedId()
    {
        Assert.True(typeof(Id).IsStronglyTypedId());
        Assert.False(typeof(Id3).IsStronglyTypedId());
    }
}

public record struct Id(Guid Value) : IStronglyTypedId<Guid>
{
    public static IStronglyTypedId<Guid> Create(Guid value) => new Id(value);
}

public abstract record Id2(Guid Value) : IStronglyTypedId<Guid>
{
    public static IStronglyTypedId<Guid> Create(Guid value) => default!;
}

public record Id3();