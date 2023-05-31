using System.Numerics;

namespace Len.StronglyTypedId;

/// <summary>
/// Defines an interface with a strongly typed id.
/// </summary>
/// <typeparam name="TSelf">The type that implements this interface.</typeparam>
/// <typeparam name="TPrimitiveId">The original type that implements this interface.</typeparam>
public interface IStronglyTypedId<TSelf, TPrimitiveId> : IParsable<TSelf>, IEqualityOperators<TSelf, TSelf, bool>
    where TSelf : IStronglyTypedId<TSelf, TPrimitiveId>?, IParsable<TSelf>?, IEqualityOperators<TSelf, TSelf, bool>?
    where TPrimitiveId : notnull, IComparable, IComparable<TPrimitiveId>, IEquatable<TPrimitiveId>
{
    /// <summary>
    /// This is the actual value of a strongly typed id.
    /// </summary>
    TPrimitiveId Value { get; }

    /// <summary>
    /// Create a new strongly typed id.
    /// </summary>
    /// <param name="value">This is the actual value of a strongly typed id.</param>
    /// <returns></returns>
    abstract static TSelf Create(TPrimitiveId value);
}