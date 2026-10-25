using System.Reflection;
using Bogus;

namespace Len.StronglyTypedId.Testing;

/// <summary>
/// Extension methods that wire strongly typed id generation into Bogus.
/// </summary>
/// <remarks>
/// <para>
/// Bogus fills a property by the property's declared type. A strongly typed id is a distinct type, so
/// without help Bogus cannot produce one — the property is simply left at its default value, silently.
/// This adapter registers Bogus' <c>RuleForType</c> rules for every id type it finds, after which
/// <c>faker.Generate()</c> fills id-typed members just like any other member.
/// </para>
/// <para>
/// Unlike AutoFixture, Bogus knows the target type up front: <c>new Faker&lt;Order&gt;()</c> already
/// says what is being produced, so wiring the ids in is the last step before generating.
/// </para>
/// </remarks>
public static class FakerExtensions
{
    /// <summary>
    /// Teaches Bogus to create strongly typed ids.
    /// </summary>
    /// <remarks>
    /// <para>
    /// By default the id types are discovered from the assemblies loaded in the current application
    /// domain, and the values come from a default <see cref="AnyIdGenerator"/>. Pass
    /// <paramref name="generator"/> to control how the values are produced, and <paramref name="idTypes"/>
    /// to state the id types explicitly.
    /// </para>
    /// <para>
    /// Stating the types explicitly is the way out when the discovery does not match what the test
    /// needs — an id type in an assembly that is not loaded yet, or an id type that should stay out of
    /// the faker.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">The type the faker produces.</typeparam>
    /// <param name="faker">The faker to configure.</param>
    /// <param name="generator">
    /// The generator producing the id values; <see langword="null"/> to use a default generator.
    /// </param>
    /// <param name="idTypes">
    /// The id types Bogus should be able to create; <see langword="null"/> to discover them from the
    /// loaded assemblies.
    /// </param>
    /// <returns>The same faker, so calls can be chained.</returns>
    public static Faker<T> WithStronglyTypedIds<T>(this Faker<T> faker, AnyIdGenerator? generator = null, params Type[]? idTypes)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(faker);

        var effectiveGenerator = generator ?? new AnyIdGenerator();

        // params 参数省略时是**空数组**而不是 null —— 只判 null 会让「自动发现」这条分支永远走不到。
        var effectiveTypes = idTypes is null or { Length: 0 } ? [.. DiscoverIdTypes()] : idTypes;

        foreach (var idType in effectiveTypes)
        {
            ArgumentNullException.ThrowIfNull(idType);

            RegisterTypeRule(faker, idType, effectiveGenerator);
        }

        return faker;
    }

    /// <summary>
    /// Registers Bogus' type rule for one id type.
    /// </summary>
    /// <remarks>
    /// Bogus' <c>RuleForType</c> is generic in the value type, so the call has to be made from a
    /// generic method closed over the id type. The id type is only known at run time here, hence the
    /// reflection.
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
    /// Finds every strongly typed id type in the assemblies loaded right now.
    /// </summary>
    /// <remarks>
    /// An assembly that is not loaded yet is invisible here. That is why the overload taking explicit
    /// types exists: an id type declared in an assembly the test never touched would otherwise stay
    /// unfilled — silently, since the property is simply left at its default value.
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
            // 动态程序集与某些受限程序集连类型表都取不到；跳过即可 —— 显式指定类型的那条路仍然可用。
            return [];
        }
    }
}
