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
    public static TId Id<TId>()
        where TId : IStronglyTypedId<TId>
        => Current.Id<TId>();

    /// <summary>
    /// Creates a strongly typed id of type <typeparamref name="TId"/> from a freshly seeded generator.
    /// </summary>
    public static TId Id<TId>(int seed)
        where TId : IStronglyTypedId<TId>
        => new AnyIdGenerator(new AnyIdOptions { Seed = seed }).Id<TId>();

    /// <summary>
    /// Creates <paramref name="count"/> strongly typed ids of type <typeparamref name="TId"/>.
    /// </summary>
    public static IReadOnlyList<TId> Ids<TId>(int count)
        where TId : IStronglyTypedId<TId>
        => Current.Ids<TId>(count);

    /// <summary>
    /// Creates <paramref name="count"/> strongly typed ids of type <typeparamref name="TId"/> from a freshly
    /// seeded generator.
    /// </summary>
    public static IReadOnlyList<TId> Ids<TId>(int count, int seed)
        where TId : IStronglyTypedId<TId>
        => new AnyIdGenerator(new AnyIdOptions { Seed = seed }).Ids<TId>(count);

    /// <summary>
    /// Registers an explicit factory for <typeparamref name="TId"/> on the shared generator.
    /// </summary>
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
    /// Generation works on a snapshot: racing with <see cref="Reset"/> only loses one reset, and holding the lock would run user factories while locked.
    /// The one operation that needs atomicity is <see cref="Register{TId}"/>, which re-checks the instance after writing.
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
