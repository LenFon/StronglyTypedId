namespace Len.StronglyTypedId;

[StronglyTypedId]
public partial record struct GuidId(Guid Value);

[StronglyTypedId]
public partial record struct Int32Id(int Value);

[StronglyTypedId]
public partial record struct UInt32Id(uint Value);

[StronglyTypedId]
public partial record struct Int64Id(long Value);

[StronglyTypedId]
public partial record struct UInt64Id(ulong Value);

[StronglyTypedId]
public partial record struct StringId(string Value);

[StronglyTypedId]
public partial record struct ByteId(byte Value);

[StronglyTypedId]
public abstract record AbstractStronglyTypedId(Guid Value);

public partial record NotStronglyTypedId();

