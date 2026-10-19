namespace Len.StronglyTypedId.Testing;

/// <summary>
/// Configures how <see cref="AnyIdGenerator"/> produces primitive values.
/// </summary>
public sealed class AnyIdOptions
{
    /// <summary>
    /// Gets or sets the seed passed to <see cref="Random"/>.
    /// </summary>
    /// <value>
    /// <see langword="null"/> (the default) to seed from the clock, or an explicit value to make the
    /// sequence reproducible. Reproducibility is guaranteed for a given runtime version only: the
    /// algorithm behind <see cref="Random"/> is an implementation detail and may change between .NET releases.
    /// </value>
    public int? Seed { get; set; }

    /// <summary>
    /// Gets or sets the length of generated <see cref="string"/> primitives.
    /// </summary>
    /// <value>The number of characters, 8 by default. Must be at least 1.</value>
    public int StringLength { get; set; } = 8;

    /// <summary>
    /// Gets or sets how many random values are tried before giving up on an id whose validator rejects them.
    /// </summary>
    /// <value>The maximum number of attempts, 100 by default. Must be at least 1.</value>
    public int MaxAttempts { get; set; } = 100;

    /// <summary>
    /// Gets or sets a callback that supplies the primitive value for a given primitive type.
    /// </summary>
    /// <value>
    /// A delegate returning the value to wrap, or <see langword="null"/> to fall back to the built-in
    /// generator. <see langword="null"/> is also what the delegate itself returns to mean "not handled".
    /// </value>
    public Func<Type, object?>? PrimitiveValueFactory { get; set; }
}
