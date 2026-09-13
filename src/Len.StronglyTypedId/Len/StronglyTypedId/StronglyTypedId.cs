using System.Numerics;

namespace Len.StronglyTypedId;

/// <summary>
/// Defines an interface with a strongly typed id.
/// </summary>
/// <typeparam name="TSelf">The strongly typed id type that implements this interface.</typeparam>
/// <typeparam name="TPrimitiveId">The underlying primitive type wrapped by the strongly typed id.</typeparam>
public interface IStronglyTypedId<TSelf, TPrimitiveId> : IParsable<TSelf>, IEqualityOperators<TSelf, TSelf, bool>
    where TSelf : IStronglyTypedId<TSelf, TPrimitiveId>?, IParsable<TSelf>?, IEqualityOperators<TSelf, TSelf, bool>?
    where TPrimitiveId : notnull, IComparable, IComparable<TPrimitiveId>, IEquatable<TPrimitiveId>
{
    /// <summary>
    /// Gets the underlying primitive value wrapped by the strongly typed id.
    /// </summary>
    TPrimitiveId Value { get; }

    /// <summary>
    /// Creates a new strongly typed id that wraps the specified primitive value.
    /// </summary>
    /// <param name="value">The primitive value to wrap.</param>
    /// <returns>A new strongly typed id wrapping <paramref name="value"/>.</returns>
    abstract static TSelf Create(TPrimitiveId value);
}