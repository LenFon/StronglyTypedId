using System.Collections.Concurrent;
using System.Reflection;

namespace Len.StronglyTypedId.Testing;

/// <summary>
/// Creates strongly typed ids with plausible primitive values, for use in tests.
/// </summary>
public sealed class AnyIdGenerator
{
    private static readonly ConcurrentDictionary<Type, Func<AnyIdGenerator, object>> _factories = new();

    /// <summary>
    /// 每个 Id 类型在验证失败时抛出的异常类型。生成器允许用 <c>ExceptionType</c> 把它换掉，
    /// 而重试循环必须认得这个类型，否则第一次被拒就直接冒泡。
    /// </summary>
    private static readonly ConcurrentDictionary<Type, Type> _rejectionTypes = new();

    private readonly AnyIdOptions _options;

    private readonly PrimitiveValueFactory _values;

    private readonly ConcurrentDictionary<Type, Func<object>> _registered = new();

#if NET9_0_OR_GREATER
    // System.Threading.Lock 仅 net9.0+ 存在；net8.0 目标继续用 object（Monitor）。
    private readonly System.Threading.Lock _sync = new();
#else
    private readonly object _sync = new();
#endif

    /// <summary>
    /// Initializes a new instance with default options.
    /// </summary>
    public AnyIdGenerator()
        : this(new AnyIdOptions())
    {
    }

    /// <summary>
    /// Initializes a new instance with the specified options.
    /// </summary>
    /// <param name="options">The options controlling seeding, string length and value generation.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <see cref="AnyIdOptions.StringLength"/> or <see cref="AnyIdOptions.MaxAttempts"/> is less than 1.
    /// </exception>
    public AnyIdGenerator(AnyIdOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.StringLength, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxAttempts, 1);

        _options = options;
        _values = new PrimitiveValueFactory(
            new Random(options.Seed ?? Random.Shared.Next()),
            options.StringLength,
            options.PrimitiveValueFactory,
            options.IncludeBoundaryValues);
    }

    /// <summary>
    /// Creates a strongly typed id of type <typeparamref name="TId"/>.
    /// </summary>
    /// <typeparam name="TId">The strongly typed id type.</typeparam>
    /// <returns>A new id wrapping a generated primitive value.</returns>
    /// <exception cref="InvalidOperationException">
    /// The id's validator rejected every generated value within <see cref="AnyIdOptions.MaxAttempts"/> attempts.
    /// </exception>
    public TId Id<TId>()
        where TId : IStronglyTypedId<TId>
        => (TId)Create(typeof(TId));

    /// <summary>
    /// Creates <paramref name="count"/> strongly typed ids of type <typeparamref name="TId"/>.
    /// </summary>
    /// <typeparam name="TId">The strongly typed id type.</typeparam>
    /// <param name="count">The number of ids to create.</param>
    /// <returns>The created ids, in generation order.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
    public IReadOnlyList<TId> Ids<TId>(int count)
        where TId : IStronglyTypedId<TId>
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        var ids = new TId[count];

        for (var index = 0; index < count; index++)
        {
            ids[index] = Id<TId>();
        }

        return ids;
    }

    /// <summary>
    /// Creates a strongly typed id of the specified runtime type.
    /// </summary>
    /// <param name="idType">A type implementing <see cref="IStronglyTypedId{TSelf, TPrimitiveId}"/>.</param>
    /// <returns>A new id wrapping a generated primitive value.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="idType"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="idType"/> is not a strongly typed id.</exception>
    public object Create(Type idType)
    {
        ArgumentNullException.ThrowIfNull(idType);

        return _registered.TryGetValue(idType, out var registered)
            ? registered()
            : _factories.GetOrAdd(idType, BuildFactory)(this);
    }

    /// <summary>
    /// Registers an explicit factory for <typeparamref name="TId"/>, overriding value generation.
    /// </summary>
    /// <typeparam name="TId">The strongly typed id type.</typeparam>
    /// <param name="factory">
    /// The factory invoked on every subsequent request for <typeparamref name="TId"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="factory"/> is <see langword="null"/>.
    /// </exception>
    public void Register<TId>(Func<TId> factory)
        where TId : IStronglyTypedId<TId>
    {
        ArgumentNullException.ThrowIfNull(factory);

        _registered[typeof(TId)] = () => factory()!;
    }

    private static Func<AnyIdGenerator, object> BuildFactory(Type idType)
    {
        if (!idType.IsStronglyTypedId())
        {
            throw new ArgumentException(
                $"'{idType}' does not implement IStronglyTypedId<TSelf, TPrimitiveId>, so no value can be generated for it.",
                nameof(idType));
        }

        var primitiveType = idType.GetPrimitiveIdType()!;

        var method = typeof(AnyIdGenerator)
            .GetMethod(nameof(CreateCore), BindingFlags.Instance | BindingFlags.NonPublic)!
            .MakeGenericMethod(idType, primitiveType);

        return (Func<AnyIdGenerator, object>)Delegate.CreateDelegate(typeof(Func<AnyIdGenerator, object>), method);
    }

    /// <summary>
    /// 解析 Id 在验证失败时抛出的异常类型：逐类型 <c>ExceptionType</c> 优先，其次程序集级
    /// <c>StronglyTypedIdDefaults</c>，最后是 <see cref="ArgumentException"/>。优先级与生成器一致。
    /// </summary>
    private static Type ResolveRejectionType(Type idType)
        => _rejectionTypes.GetOrAdd(idType, static type =>
            type.GetCustomAttribute<StronglyTypedIdAttribute>()?.ExceptionType
            ?? type.Assembly.GetCustomAttribute<StronglyTypedIdDefaultsAttribute>()?.ExceptionType
            ?? typeof(ArgumentException));

    /// <summary>
    /// Generates a value and wraps it, retrying while the id's validator rejects it.
    /// </summary>
    private object CreateCore<TId, TPrimitive>()
        where TId : IStronglyTypedId<TId, TPrimitive>
        where TPrimitive : notnull, IComparable
    {
        Exception? lastFailure = null;

        // 拒绝异常可能是 ArgumentException，也可能是 ExceptionType 指定的自定义类型。
        // 只认这两种：其它异常（验证器自身的 NullReferenceException 之类）是真 bug，必须冒泡。
        var rejectionType = ResolveRejectionType(typeof(TId));

        for (var attempt = 0; attempt < _options.MaxAttempts; attempt++)
        {
            TPrimitive value;

            lock (_sync)
            {
                value = (TPrimitive)_values.Create(typeof(TPrimitive))!;
            }

            try
            {
                return TId.Create(value);
            }
            catch (Exception exception) when (rejectionType.IsInstanceOfType(exception))
            {
                lastFailure = exception;
            }
        }

        throw new InvalidOperationException(
            $"Could not generate a value accepted by '{typeof(TId)}' after {_options.MaxAttempts} attempts. "
            + $"Its validator rejects the generated values; register an explicit factory with "
            + $"{nameof(AnyIdGenerator)}.{nameof(Register)}<{typeof(TId).Name}>(...) instead.",
            lastFailure);
    }
}
