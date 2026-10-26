namespace Len.StronglyTypedId.Testing;

/// <summary>
/// 在测试中创建强类型 Id 的静态便捷入口。
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
    /// 创建 <typeparamref name="TId"/> 类型的强类型 Id。
    /// </summary>
    public static TId Id<TId>()
        where TId : IStronglyTypedId<TId>
        => Current.Id<TId>();

    /// <summary>
    /// 使用新播种的生成器创建 <typeparamref name="TId"/> 类型的强类型 Id。
    /// </summary>
    public static TId Id<TId>(int seed)
        where TId : IStronglyTypedId<TId>
        => new AnyIdGenerator(new AnyIdOptions { Seed = seed }).Id<TId>();

    /// <summary>
    /// 创建 <paramref name="count"/> 个 <typeparamref name="TId"/> 类型的强类型 Id。
    /// </summary>
    public static IReadOnlyList<TId> Ids<TId>(int count)
        where TId : IStronglyTypedId<TId>
        => Current.Ids<TId>(count);

    /// <summary>
    /// 使用新播种的生成器创建 <paramref name="count"/> 个 <typeparamref name="TId"/> 类型的强类型 Id。
    /// </summary>
    public static IReadOnlyList<TId> Ids<TId>(int count, int seed)
        where TId : IStronglyTypedId<TId>
        => new AnyIdGenerator(new AnyIdOptions { Seed = seed }).Ids<TId>(count);

    /// <summary>
    /// 在共享生成器上为 <typeparamref name="TId"/> 注册显式工厂。
    /// </summary>
    public static void Register<TId>(Func<TId> factory)
        where TId : IStronglyTypedId<TId>
    {
        // 注册必须落在当前的共享生成器上：Reset() 可能在「读到引用」与「写进字典」之间换掉实例，
        // 那样注册会留在被丢弃的旧实例里，之后 Any.Id<TId>() 静默不生效。
        // 用「写后复查」而非「持锁执行」，后者会在锁内调用用户工厂，工厂再进 Any 就死锁。
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
    /// 丢弃共享生成器，同时清除通过 <see cref="Register{TId}"/> 建立的全部注册。
    /// </summary>
    public static void Reset()
    {
        lock (_sync)
        {
            Generator = new AnyIdGenerator();
        }
    }

    /// <summary>
    /// 获取共享生成器的快照。
    /// </summary>
    /// <remarks>
    /// 生成基于快照：与 <see cref="Reset"/> 竞争最多丢失一次重置，而持锁会在锁内运行用户工厂。
    /// 唯一需要原子性的是 <see cref="Register{TId}"/>，它在写入后复查实例。
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
