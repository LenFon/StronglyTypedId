using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Len.StronglyTypedId.Testing;

/// <summary>
/// 用合理的基元值创建强类型 Id，供测试使用。
/// </summary>
public sealed class AnyIdGenerator
{
    // 缓存键是 Type，而 Type 持有 Assembly、Assembly 持有 AssemblyLoadContext。用普通字典做静态缓存时，
    // 插件热重载或测试宿主反复加载程序集，每加载一次就永久钉住一个可回收 ALC 及其全部类型。
    // 与主库 StronglyTypedIdExtensions 同构：ConditionalWeakTable 的键是弱引用，值随键一起回收。
    // 这里不用 StrongBox：两个缓存的值都恒非 null（工厂必有；异常类型回退到 ArgumentException）。
    private static readonly ConditionalWeakTable<Type, Func<AnyIdGenerator, object>> _factories = [];

    /// <summary>
    /// 每个 Id 类型在验证失败时抛出的异常类型。生成器允许用 <c>ExceptionType</c> 把它换掉，
    /// 而重试循环必须认得这个类型，否则第一次被拒就直接冒泡。
    /// </summary>
    private static readonly ConditionalWeakTable<Type, Type> _rejectionTypes = [];

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
    /// 创建 <paramref name="count"/> 个指定运行时类型的强类型 Id。
    /// </summary>
    /// <remarks>
    /// <see cref="Create(Type)"/> 的批量形式，供 Id 类型只在运行期可知的场景使用。
    /// </remarks>
    public IReadOnlyList<object> Ids(Type idType, int count)
    {
        ArgumentNullException.ThrowIfNull(idType);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        var ids = new object[count];

        for (var index = 0; index < count; index++)
        {
            ids[index] = Create(idType);
        }

        return ids;
    }

    /// <summary>
    /// 创建指定运行时类型的强类型 Id。
    /// </summary>
    /// <remarks>
    /// 可空 Id（<c>OrderId?</c>）按底层类型生成，与 <see cref="StronglyTypedIdExtensions.IsStronglyTypedId"/>、
    /// MVC 绑定器、EF 转换器一致。返回的是底层 Id 的装箱实例（<c>OrderId</c>，不是 <c>OrderId?</c>）。
    /// </remarks>
    public object Create(Type idType)
    {
        ArgumentNullException.ThrowIfNull(idType);

        // 先拆可空外壳：Nullable<T> 不实现 IStronglyTypedId<TSelf>，直接传给 MakeGenericMethod 会
        // 报「violates the constraint of type 'TId'」，而 IsStronglyTypedId(OrderId?) 却返回 true。
        var underlyingType = Nullable.GetUnderlyingType(idType) ?? idType;

        // 开放泛型（如 Box<T>.BoxedId）无法构造：MakeGenericMethod 会抛「not fully instantiated」，
        // 而那条消息完全看不出是「这个 Id 声明在泛型容器里」。按调用方实际能做的事报错。
        return underlyingType.ContainsGenericParameters
            ? throw new ArgumentException(
                $"'{underlyingType}' is an open generic type, so no value can be generated for it. "
                + "Declare the id in a non-generic container, or request a closed type such as Box<int>.BoxedId.",
                nameof(idType))
            : _registered.TryGetValue(underlyingType, out var registered)
                ? registered()
                : GetOrAddFactory(underlyingType)(this);
    }

    /// <summary>
    /// 取（必要时构建）指定 Id 类型的工厂。
    /// </summary>
    private static Func<AnyIdGenerator, object> GetOrAddFactory(Type idType)
        => _factories.GetValue(idType, BuildFactory);

    /// <summary>
    /// 为 <typeparamref name="TId"/> 注册显式工厂，覆盖默认的取值生成。
    /// </summary>
    public void Register<TId>(Func<TId> factory)
        where TId : IStronglyTypedId<TId>
    {
        ArgumentNullException.ThrowIfNull(factory);

        _registered[typeof(TId)] = () => factory()!;
    }

    /// <summary>
    /// 为指定运行时类型的强类型 Id 注册显式工厂，覆盖默认的取值生成。
    /// </summary>
    /// <remarks>
    /// <see cref="Register{TId}"/> 的非泛型形式。可空 Id（<c>OrderId?</c>）按底层类型登记，
    /// 与 <see cref="Create(Type)"/> 的拆壳规则一致，否则注册了却永远命中不到。
    /// </remarks>
    public void Register(Type idType, Func<object> factory)
    {
        ArgumentNullException.ThrowIfNull(idType);
        ArgumentNullException.ThrowIfNull(factory);

        _registered[Nullable.GetUnderlyingType(idType) ?? idType] = factory;
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
        => _rejectionTypes.GetValue(idType, static type =>
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
