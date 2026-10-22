using System.Numerics;

namespace Len.StronglyTypedId;

/// <summary>
/// Defines an interface with a strongly typed id, without exposing the underlying primitive type.
/// </summary>
public interface IStronglyTypedId<TSelf> : IParsable<TSelf>, IEqualityOperators<TSelf, TSelf, bool>
    where TSelf : IStronglyTypedId<TSelf>?, IParsable<TSelf>?, IEqualityOperators<TSelf, TSelf, bool>?
{
}

/// <summary>
/// Defines an interface with a strongly typed id.
/// </summary>
public interface IStronglyTypedId<TSelf, TPrimitiveId> : IStronglyTypedId<TSelf>
    where TSelf : IStronglyTypedId<TSelf, TPrimitiveId>?, IParsable<TSelf>?, IEqualityOperators<TSelf, TSelf, bool>?
    where TPrimitiveId : notnull, IComparable
{
    /// <summary>
    /// Gets the underlying primitive value wrapped by the strongly typed id.
    /// </summary>
    TPrimitiveId Value { get; }

    /// <summary>
    /// Creates a new strongly typed id that wraps the specified primitive value.
    /// </summary>
    abstract static TSelf Create(TPrimitiveId value);
}

/// <summary>
/// Defines a strongly typed id whose wrapped primitive supports culture-aware formatting.
/// </summary>
public interface IFormattableStronglyTypedId<TSelf> : IStronglyTypedId<TSelf>, IFormattable
    where TSelf : IFormattableStronglyTypedId<TSelf>?, IParsable<TSelf>?, IEqualityOperators<TSelf, TSelf, bool>?
{
}