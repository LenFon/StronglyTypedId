using System.Numerics;

namespace Len.StronglyTypedId;

/// <summary>
/// Defines an interface with a strongly typed id, without exposing the underlying primitive type.
/// </summary>
/// <typeparam name="TSelf">The strongly typed id type that implements this interface.</typeparam>
/// <remarks>
/// <para>
/// This is the primitive-agnostic counterpart of <see cref="IStronglyTypedId{TSelf, TPrimitiveId}"/>: every
/// strongly typed id implements both, so generic code that does not care about the wrapped type can constrain
/// on the shorter form and still accept ids backed by any primitive.
/// </para>
/// <code>
/// static TId RoundTrip&lt;TId&gt;(string text) where TId : IStronglyTypedId&lt;TId&gt; => TId.Parse(text, null);
/// </code>
/// <para>
/// It deliberately declares no members of its own — <see cref="IParsable{TSelf}"/> and
/// <see cref="IEqualityOperators{TSelf, TSelf, TResult}"/> already carry parsing and equality, and
/// <c>Value</c> cannot be exposed without naming the primitive type. Reach for the two-parameter form when
/// you need the underlying value.
/// </para>
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
/// <para>
/// <see cref="IStronglyTypedId{TSelf}"/> deliberately does not require <see cref="IFormattable"/>: ids backed
/// by <c>string</c> have no culture-aware formatting to forward, and ids backed by an <c>enum</c> cannot expose
/// it either (<c>Enum.ToString(string, IFormatProvider)</c> is marked obsolete). This interface is the opt-in
/// marker for the rest, so generic code can format an id without knowing its primitive type:
/// </para>
/// <code>
/// static string Render&lt;TId&gt;(TId id) where TId : IFormattableStronglyTypedId&lt;TId&gt;
///     => id.ToString("D", CultureInfo.InvariantCulture);
/// </code>
/// <para>
/// Generated ids implement it exactly when the wrapped primitive implements <see cref="IFormattable"/>.
/// Constrain on this interface only when the caller is known to pass such an id; constrain on
/// <see cref="IStronglyTypedId{TSelf}"/> when any id must be accepted.
/// </para>
/// </remarks>
public interface IFormattableStronglyTypedId<TSelf> : IStronglyTypedId<TSelf>, IFormattable
    where TSelf : IFormattableStronglyTypedId<TSelf>?, IParsable<TSelf>?, IEqualityOperators<TSelf, TSelf, bool>?
{
}