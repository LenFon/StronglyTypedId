namespace Len.StronglyTypedId;

/// <summary>
/// 强类型Id接口
/// </summary>
/// <typeparam name="TPrimitiveId"></typeparam>
public interface IStronglyTypedId<TPrimitiveId>
    where TPrimitiveId : notnull, IComparable, IComparable<TPrimitiveId>, IEquatable<TPrimitiveId>

{
    TPrimitiveId Value { get; }

    abstract static IStronglyTypedId<TPrimitiveId> Create(TPrimitiveId value);
}