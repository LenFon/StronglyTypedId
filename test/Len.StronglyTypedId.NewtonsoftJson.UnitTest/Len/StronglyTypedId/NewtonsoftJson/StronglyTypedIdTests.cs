namespace Len.StronglyTypedId;

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