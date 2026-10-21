namespace Len.StronglyTypedId.Testing;

/// <summary>
/// Static convenience entry point for creating strongly typed ids in tests.
/// </summary>
public static class Any
{
#if NET9_0_OR_GREATER
    // System.Threading.Lock 仅 net9.0+ 存在；net8.0 目标继续用 object（Monitor）。
    private static readonly System.Threading.Lock _sync = new();
#else
    private static readonly object _sync = new();
#endif

    private static AnyIdGenerator Generator { get; set; } = new();

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
    /// Creates <paramref name="count"/> strongly typed ids of type <typeparamref name="TId"/> from a freshly
    /// seeded generator.
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
    /// <param name="factory">
    /// The factory invoked on every subsequent request for <typeparamref name="TId"/>.
    /// </param>
    public static void Register<TId>(Func<TId> factory)
        where TId : IStronglyTypedId<TId>
    {
        // 注册必须落在「当前的」共享生成器上。Reset() 可能在「读到引用」与「写进字典」之间换掉实例，
        // 那样注册会留在被丢弃的旧实例里 —— 之后 Any.Id<TId>() 静默不生效，且没有任何报错。
        // 用「写后复查」而不是「持锁执行」：后者会在锁内调用用户工厂，工厂跨线程再进 Any 就死锁。
        while (true)
        {
            var generator = Current;

            generator.Register(factory);

            lock (_sync)
            {
                if (ReferenceEquals(Generator, generator))
                {
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Discards the shared generator, dropping every registration made through <see cref="Register{TId}"/>.
    /// </summary>
    public static void Reset()
    {
        lock (_sync)
        {
            Generator = new AnyIdGenerator();
        }
    }

    /// <summary>
    /// Gets a snapshot of the shared generator.
    /// </summary>
    /// <remarks>
    /// Generation deliberately works on a snapshot: racing with <see cref="Reset"/> may use the instance that
    /// was just discarded, which only loses one reset — the produced ids stay valid. Holding the lock across the
    /// whole call would instead run user-supplied factories while locked. The one operation that does need
    /// atomicity is <see cref="Register{TId}"/>, which re-checks the instance after writing.
    /// </remarks>
    private static AnyIdGenerator Current
    {
        get
        {
            lock (_sync)
            {
                return Generator;
            }
        }
    }
}
