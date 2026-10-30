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

            // 开放泛型（如 Box<T>.BoxedId）无法闭合，MakeGenericMethod 会抛「not fully instantiated」。
            // 自动发现时静默跳过：泛型容器里的 Id 本来就不会作为某个具体类型的属性出现；
            // 显式传入时报错，因为那说明调用方以为它会被填上。
            if (idType.ContainsGenericParameters)
            {
                if (idTypes is null or { Length: 0 })
                {
                    continue;
                }

                throw new ArgumentException(
                    $"'{idType}' is an open generic type, so Bogus cannot create it. "
                    + "Pass a closed type such as Box<int>.BoxedId instead.",
                    nameof(idTypes));
            }

            RegisterTypeRule(faker, idType, effectiveGenerator);
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
    /// 查找当前已加载程序集中的全部强类型 Id 类型。
    /// </summary>
    /// <remarks>
    /// 尚未加载的程序集在此不可见，这正是提供显式类型重载的原因，否则那些 Id 会静默保持默认值。
    /// </remarks>
    private static IEnumerable<Type> DiscoverIdTypes()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (var type in GetTypes(assembly))
            {
                if (type.IsStronglyTypedId())
                {
                    yield return type;
                }
            }
        }
    }

    private static IEnumerable<Type> GetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            // 部分类型加载失败（缺依赖）时，其余类型仍然可用。
            return exception.Types.Where(static type => type is not null).Cast<Type>();
        }
        catch (Exception)
        {
            // 动态程序集与某些受限程序集连类型表都取不到；跳过即可，显式指定类型的那条路仍然可用。
            return [];
        }
    }
}
