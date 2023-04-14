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

public record struct Int32Id(int Value) : IStronglyTypedId<int>
{
    public static IStronglyTypedId<int> Create(int value) => new Int32Id(value);
}

public record struct UInt32Id(uint Value) : IStronglyTypedId<uint>
{
    public static IStronglyTypedId<uint> Create(uint value) => new UInt32Id(value);
}

public record struct Int64Id(long Value) : IStronglyTypedId<long>
{
    public static IStronglyTypedId<long> Create(long value) => new Int64Id(value);
}

public record struct UInt64Id(ulong Value) : IStronglyTypedId<ulong>
{
    public static IStronglyTypedId<ulong> Create(ulong value) => new UInt64Id(value);
}

public record struct StringId(string Value) : IStronglyTypedId<string>
{
    public static IStronglyTypedId<string> Create(string value) => new StringId(value);
}

public record struct ByteId(byte Value) : IStronglyTypedId<byte>
{
    public static IStronglyTypedId<byte> Create(byte value) => new ByteId(value);
}