using System.Reflection;
using Bogus;

namespace Len.StronglyTypedId.Testing;

/// <summary>
/// 把强类型 Id 生成接入 Bogus 的扩展方法。
/// </summary>
/// <remarks>
/// Bogus 按属性的声明类型填充，而强类型 Id 是独立类型，不加干预时属性会被静默留为默认值。
/// 本适配器为发现的每个 Id 类型注册 <c>RuleForType</c> 规则，之后 <c>faker.Generate()</c> 即可填充 Id 成员。
/// 与 AutoFixture 不同，Bogus 事先知道目标类型，接入 Id 是生成前的最后一步。
/// </remarks>
public static class FakerExtensions
{
    /// <summary>
    /// 让 Bogus 能够创建强类型 Id。
    /// </summary>
    /// <remarks>
    /// 默认从当前应用程序域已加载的程序集中发现 Id 类型，取值来自默认的 <see cref="AnyIdGenerator"/>。
    /// 传入 <paramref name="generator"/> 可控制取值方式，传入 <paramref name="idTypes"/> 可显式指定类型
    /// （自动发现的结果不符预期时用这个）。
    /// </remarks>
    /// <typeparam name="T">faker 生成的目标类型。</typeparam>
    /// <param name="faker">要配置的 faker。</param>
    /// <param name="generator">
    /// 生成 Id 取值的生成器；<see langword="null"/> 表示使用默认生成器。
    /// </param>
    /// <param name="idTypes">
    /// Bogus 应能创建的 Id 类型；<see langword="null"/> 表示从已加载的程序集中自动发现。
    /// </param>
    /// <returns>同一个 faker，便于链式调用。</returns>
    public static Faker<T> WithStronglyTypedIds<T>(this Faker<T> faker, AnyIdGenerator? generator = null, params Type[]? idTypes)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(faker);

        var effectiveGenerator = generator ?? new AnyIdGenerator();

        // params 参数省略时是空数组而不是 null，只判 null 会让「自动发现」这条分支永远走不到。
        var effectiveTypes = idTypes is null or { Length: 0 } ? [.. DiscoverIdTypes()] : idTypes;

        foreach (var idType in effectiveTypes)
        {
            ArgumentNullException.ThrowIfNull(idType);

            // 可空 Id（OrderId?）按底层类型注册：Nullable<T> 不实现 IStronglyTypedId<TSelf>，
            // 直接传给 MakeGenericMethod 会报「violates the constraint of type 'TId'」。
            // 与 AnyIdGenerator.Create、AutoFixture 的 specimen builder、MVC 绑定器同一口径。
            var underlyingType = Nullable.GetUnderlyingType(idType) ?? idType;

            // 开放泛型（如 Box<T>.BoxedId）无法闭合，MakeGenericMethod 会抛「not fully instantiated」。
            // 自动发现时静默跳过：泛型容器里的 Id 本来就不会作为某个具体类型的属性出现；
            // 显式传入时报错，因为那说明调用方以为它会被填上。
            if (underlyingType.ContainsGenericParameters)
            {
                if (idTypes is null or { Length: 0 })
                {
                    continue;
                }

                throw new ArgumentException(
                    $"'{underlyingType}' is an open generic type, so Bogus cannot create it. "
                    + "Pass a closed type such as Box<int>.BoxedId instead.",
                    nameof(idTypes));
            }

            RegisterTypeRule(faker, underlyingType, effectiveGenerator);

            // 可空属性（OrderId?）的声明类型是 Nullable<OrderId>，RuleForType(typeof(OrderId)) 匹配不到它，
            // 属性会静默留在 null 上。再补一条针对可空类型的规则，取值来自底层 Id。
            // 自动发现时也要补：发现结果里只有非可空 Id，可空属性同样需要这条规则才填得上。
            RegisterNullableTypeRule(faker, underlyingType, effectiveGenerator);
        }

        return faker;
    }

    /// <summary>
    /// 为一个 Id 类型注册 Bogus 的类型规则。
    /// </summary>
    /// <remarks>
    /// Bogus 的 <c>RuleForType</c> 以值类型为泛型参数，因此必须在闭合到该 Id 类型的泛型方法中调用。
    /// 此处 Id 类型只有运行期才知道，故借助反射。
    /// </remarks>
    private static void RegisterTypeRule<TTarget>(Faker<TTarget> faker, Type idType, AnyIdGenerator generator)
        where TTarget : class
    {
        var method = typeof(FakerExtensions)
            .GetMethod(nameof(RegisterTypeRuleCore), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(typeof(TTarget), idType);

        method.Invoke(null, [faker, generator]);
    }

    private static void RegisterTypeRuleCore<TTarget, TId>(Faker<TTarget> faker, AnyIdGenerator generator)
        where TTarget : class
        where TId : IStronglyTypedId<TId>
        => faker.RuleForType(typeof(TId), _ => generator.Id<TId>());

    /// <summary>
    /// 为可空 Id 类型（<c>Nullable&lt;TId&gt;</c>）注册 Bogus 的类型规则。
    /// </summary>
    /// <remarks>
    /// 只有值类型 Id 才需要：引用类型 Id 的 <c>OrderId?</c> 在运行期就是 <c>OrderId</c>，
    /// <see cref="RegisterTypeRuleCore{TTarget, TId}"/> 那条规则已经覆盖。
    /// </remarks>
    private static void RegisterNullableTypeRule<TTarget>(Faker<TTarget> faker, Type underlyingType, AnyIdGenerator generator)
        where TTarget : class
    {
        // 引用类型 Id 的 T? 与 T 是同一个运行期类型，再注册一条只会重复。
        if (!underlyingType.IsValueType)
        {
            return;
        }

        var method = typeof(FakerExtensions)
            .GetMethod(nameof(RegisterNullableTypeRuleCore), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(typeof(TTarget), underlyingType);

        method.Invoke(null, [faker, generator]);
    }

    private static void RegisterNullableTypeRuleCore<TTarget, TId>(Faker<TTarget> faker, AnyIdGenerator generator)
        where TTarget : class
        where TId : struct, IStronglyTypedId<TId>
        => faker.RuleForType(typeof(TId?), _ => (TId?)generator.Id<TId>());

    /// <summary>
    /// 查找当前已加载程序集中的全部强类型 Id 类型。
    /// </summary>
    /// <remarks>
    /// 尚未加载的程序集在此不可见，这正是提供显式类型重载的原因，否则那些 Id 会静默保持默认值。
    /// </remarks>
    private static IEnumerable<Type> DiscoverIdTypes()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (var type in AssemblyTypeReader.GetLoadableTypes(assembly))
            {
                if (type.IsStronglyTypedId())
                {
                    yield return type;
                }
            }
        }
    }
}
