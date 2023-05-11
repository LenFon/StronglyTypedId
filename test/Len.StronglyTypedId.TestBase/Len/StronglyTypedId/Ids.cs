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
public partial record GuidIdV2(Guid Value);

[StronglyTypedId]
public partial record Int32IdV2(int Value);

[StronglyTypedId]
public partial record UInt32IdV2(uint Value);

[StronglyTypedId]
public partial record Int64IdV2(long Value);

[StronglyTypedId]
public partial record UInt64IdV2(ulong Value);

[StronglyTypedId]
public partial record StringIdV2(string Value);

[StronglyTypedId]
public partial record ByteIdV2(byte Value);

[StronglyTypedId]
public partial record struct StringIdV3(System.String Value);

public partial record NotStronglyTypedId();

