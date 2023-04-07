﻿namespace Len.StronglyTypedId;

/// <summary>
/// 强类型Id接口
/// </summary>
/// <typeparam name="TPrimitiveId"></typeparam>
public interface IStronglyTypedId<TPrimitiveId>
    where TPrimitiveId : struct, IComparable, IComparable<TPrimitiveId>, IEquatable<TPrimitiveId>, ISpanParsable<TPrimitiveId>

{
    TPrimitiveId Value { get; }

    abstract static IStronglyTypedId<TPrimitiveId> Create(TPrimitiveId value);
}