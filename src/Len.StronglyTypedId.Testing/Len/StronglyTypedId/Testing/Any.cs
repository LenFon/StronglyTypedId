namespace Len.StronglyTypedId.Testing;

/// <summary>
/// Static convenience entry point for creating strongly typed ids in tests.
/// </summary>
/// <remarks>
/// <para>
/// The static members share one process-wide <see cref="AnyIdGenerator"/> seeded from the clock. Use
/// <see cref="Reset"/> between tests when a test registers an explicit factory, and prefer an
/// <see cref="AnyIdGenerator"/> instance when a test needs its own seed.
/// </para>
/// <para>
/// For reproducible sequences use the overloads taking a seed: the same seed produces the same values
/// for a given runtime version.
/// </para>
/// </remarks>
public static class Any
{
    private static readonly object _sync = new();

    private static AnyIdGenerator _generator = new();

    /// <summary>
    /// Creates a strongly typed id of type <typeparamref name="TId"/>.
    /// </summary>
    /// <typeparam name="TId">The strongly typed id type.</typeparam>
    /// <returns>A new id wrapping a generated primitive value.</returns>
    public static TId Id<TId>()
        where TId : IStronglyTypedId<TId>
        => Current.Id<TId>();

    /// <summary>
    /// Creates a strongly typed id of type <typeparamref name="TId"/> from a freshly seeded generator.
    /// </summary>
    /// <typeparam name="TId">The strongly typed id type.</typeparam>
    /// <param name="seed">The seed of the generator used for this call.</param>
    /// <returns>A new id wrapping a generated primitive value.</returns>
    /// <remarks>
    /// Because a new generator is created per call, repeating the same seed returns the same value.
    /// Use <see cref="Ids{TId}(int, int)"/> when a reproducible <em>sequence</em> is wanted.
    /// </remarks>
    public static TId Id<TId>(int seed)
        where TId : IStronglyTypedId<TId>
        => new AnyIdGenerator(new AnyIdOptions { Seed = seed }).Id<TId>();

    /// <summary>
    /// Creates <paramref name="count"/> strongly typed ids of type <typeparamref name="TId"/>.
    /// </summary>
    /// <typeparam name="TId">The strongly typed id type.</typeparam>
    /// <param name="count">The number of ids to create.</param>
    /// <returns>The created ids, in generation order.</returns>
    public static IReadOnlyList<TId> Ids<TId>(int count)
        where TId : IStronglyTypedId<TId>
        => Current.Ids<TId>(count);

    /// <summary>
    /// Creates <paramref name="count"/> strongly typed ids of type <typeparamref name="TId"/> from a freshly seeded generator.
    /// </summary>
    /// <typeparam name="TId">The strongly typed id type.</typeparam>
    /// <param name="count">The number of ids to create.</param>
    /// <param name="seed">The seed of the generator used for this call.</param>
    /// <returns>The created ids, in generation order.</returns>
    public static IReadOnlyList<TId> Ids<TId>(int count, int seed)
        where TId : IStronglyTypedId<TId>
        => new AnyIdGenerator(new AnyIdOptions { Seed = seed }).Ids<TId>(count);

    /// <summary>
    /// Registers an explicit factory for <typeparamref name="TId"/> on the shared generator.
    /// </summary>
    /// <typeparam name="TId">The strongly typed id type.</typeparam>
    /// <param name="factory">The factory invoked on every subsequent request for <typeparamref name="TId"/>.</param>
    public static void Register<TId>(Func<TId> factory)
        where TId : IStronglyTypedId<TId>
        => Current.Register(factory);

    /// <summary>
    /// Discards the shared generator, dropping every registration made through <see cref="Register{TId}"/>.
    /// </summary>
    public static void Reset()
    {
        lock (_sync)
        {
            _generator = new AnyIdGenerator();
        }
    }

    private static AnyIdGenerator Current
    {
        get
        {
            lock (_sync)
            {
                return _generator;
            }
        }
    }
}
