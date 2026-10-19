using System.Numerics;

namespace Len.StronglyTypedId;

/// <summary>
/// Defines an interface with a strongly typed id, without exposing the underlying primitive type.
/// </summary>
/// <typeparam name="TSelf">The strongly typed id type that implements this interface.</typeparam>
/// <remarks>
/// The primitive-agnostic form: every strongly typed id implements both this and
/// <see cref="IStronglyTypedId{TSelf, TPrimitiveId}"/>. It declares no members of its own — parsing and equality
/// come from the base interfaces, and <c>Value</c> cannot be exposed without naming the primitive type.
/// </remarks>
public interface IStronglyTypedId<TSelf> : IParsable<TSelf>, IEqualityOperators<TSelf, TSelf, bool>
    where TSelf : IStronglyTypedId<TSelf>?, IParsable<TSelf>?, IEqualityOperators<TSelf, TSelf, bool>?
{
}

/// <summary>
/// Defines an interface with a strongly typed id.
/// </summary>
/// <typeparam name="TSelf">The strongly typed id type that implements this interface.</typeparam>
/// <typeparam name="TPrimitiveId">The underlying primitive type wrapped by the strongly typed id.</typeparam>
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
    /// <param name="value">The primitive value to wrap.</param>
    /// <returns>A new strongly typed id wrapping <paramref name="value"/>.</returns>
    abstract static TSelf Create(TPrimitiveId value);
}

/// <summary>
/// Defines a strongly typed id whose wrapped primitive supports culture-aware formatting.
/// </summary>
/// <typeparam name="TSelf">The strongly typed id type that implements this interface.</typeparam>
/// <remarks>
/// <see cref="IStronglyTypedId{TSelf}"/> deliberately does not require <see cref="IFormattable"/>: <c>string</c>
/// ids have nothing to forward, and <c>enum</c> ids cannot expose it (<c>Enum.ToString(string, IFormatProvider)</c>
/// is obsolete). Generated ids implement this interface exactly when the wrapped primitive is formattable.
/// </remarks>
public interface IFormattableStronglyTypedId<TSelf> : IStronglyTypedId<TSelf>, IFormattable
    where TSelf : IFormattableStronglyTypedId<TSelf>?, IParsable<TSelf>?, IEqualityOperators<TSelf, TSelf, bool>?
{
}