using System.Collections.Concurrent;
using System.Reflection;

namespace Len.StronglyTypedId.Testing;

/// <summary>
/// 用合理的基元值创建强类型 Id，供测试使用。
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
    /// 使用默认选项初始化新实例。
    /// </summary>
    public AnyIdGenerator()
        : this(new AnyIdOptions())
    {
    }

    /// <summary>
    /// 使用指定选项初始化新实例。
    /// </summary>
    public AnyIdGenerator(AnyIdOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.StringLength, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxAttempts, 1);

        _options = options;
        // 未指定种子时用时钟熵播种，行为与之前一样不可复现；指定种子时序列跨运行时、跨平台一致。
        _values = new PrimitiveValueFactory(
            new DeterministicRandom(options.Seed ?? Random.Shared.Next()),
            options.StringLength,
            options.PrimitiveValueFactory,
            options.IncludeBoundaryValues);
    }

    /// <summary>
    /// 创建 <typeparamref name="TId"/> 类型的强类型 Id。
    /// </summary>
    public TId Id<TId>()
        where TId : IStronglyTypedId<TId>
        => (TId)Create(typeof(TId));

    /// <summary>
    /// 创建 <paramref name="count"/> 个 <typeparamref name="TId"/> 类型的强类型 Id。
    /// </summary>
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
    /// 创建指定运行时类型的强类型 Id。
    /// </summary>
    public object Create(Type idType)
    {
        ArgumentNullException.ThrowIfNull(idType);

        // 开放泛型（如 Box<T>.BoxedId）无法构造：MakeGenericMethod 会抛「not fully instantiated」，
        // 而那条消息完全看不出是「这个 Id 声明在泛型容器里」。按调用方实际能做的事报错。
        return idType.ContainsGenericParameters
            ? throw new ArgumentException(
                $"'{idType}' is an open generic type, so no value can be generated for it. "
                + "Declare the id in a non-generic container, or request a closed type such as Box<int>.BoxedId.",
                nameof(idType))
            : _registered.TryGetValue(idType, out var registered)
                ? registered()
                : _factories.GetOrAdd(idType, BuildFactory)(this);
    }

    /// <summary>
    /// 为 <typeparamref name="TId"/> 注册显式工厂，覆盖默认的取值生成。
    /// </summary>
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
    /// 生成一个值并包装成 Id；若 Id 的验证器拒绝则重试。
    /// </summary>
    private object CreateCore<TId, TPrimitive>()
        where TId : IStronglyTypedId<TId, TPrimitive>
        where TPrimitive : notnull, IComparable
    {
        Exception? lastFailure = null;

        // 拒绝异常可能是 ArgumentException，也可能是 ExceptionType 指定的自定义类型。
        // 其它异常（如验证器自身的 NullReferenceException）是真 bug，必须冒泡。
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
