using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Len.StronglyTypedId;

/// <summary>
/// Provides methods to inspect the primitive type behind a strongly typed id.
/// </summary>
public static class StronglyTypedIdExtensions
{
    // 类型 → 基元类型（null 表示「不是强类型 Id」）。这三个入口常落在模型绑定、EF 转换器、
    // DI 注册这类热路径上，每次判定都要遍历 GetInterfaces()，而对同一个 Type 结果恒定。
    //
    // 键必须弱引用：Type 持有 Assembly，Assembly 持有 AssemblyLoadContext。用普通字典时，
    // 插件热重载 / 测试宿主反复加载程序集，每加载一次就永久钉住一个可回收 ALC 及其全部类型。
    // ConditionalWeakTable 是 ephemeron —— 值只在键可达时可达，键被回收后整条记录随之消失。
    // 值用 StrongBox 是为了能把「不是 Id」这个 null 结果也缓存下来。
    private static readonly ConditionalWeakTable<Type, StrongBox<Type?>> _primitiveIdTypes = [];

    /// <summary>
    /// Gets the primitive type wrapped by the specified strongly typed id.
    /// </summary>
    /// <remarks>
    /// A nullable id is judged by its underlying type, matching the ASP.NET Core model binder and the EF Core converters.
    /// </remarks>
    public static Type? GetPrimitiveIdType([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] this Type type)
        => type.TryGetPrimitiveIdType(out var primitiveIdType) ? primitiveIdType : null;

    /// <summary>
    /// Determines whether the specified type is a strongly typed id.
    /// </summary>
    /// <remarks>
    /// A nullable id counts as a strongly typed id, exactly like its underlying type.
    /// </remarks>
    public static bool IsStronglyTypedId([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] this Type type) => type.TryGetPrimitiveIdType(out _);

    /// <summary>
    /// Attempts to get the primitive type wrapped by the specified strongly typed id.
    /// </summary>
    /// <remarks>
    /// A nullable id reports the same primitive type as its non-nullable form.
    /// </remarks>
    public static bool TryGetPrimitiveIdType([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] this Type type, [NotNullWhen(true)] out Type? primitiveIdType)
    {
        ArgumentNullException.ThrowIfNull(type);

        // 手写 TryGetValue / GetValue 而不是单个 GetValue(key, ResolvePrimitiveIdType)：后者的回调委托
        // 无法携带 DynamicallyAccessedMembers 标注，裁剪分析器会报 IL2091。这里的回调只装箱已算好的结果，
        // 不碰类型成员；并发下两边算出的值相同，谁先写入都一样。
        if (!_primitiveIdTypes.TryGetValue(type, out var boxed))
        {
            var resolved = ResolvePrimitiveIdType(type);
            boxed = _primitiveIdTypes.GetValue(type, _ => new StrongBox<Type?>(resolved));
        }

        primitiveIdType = boxed.Value;
        return primitiveIdType is not null;
    }

    /// <summary>
    /// 计算 <see cref="TryGetPrimitiveIdType"/> 的结果，不做缓存。
    /// </summary>
    private static Type? ResolvePrimitiveIdType([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type type)
    {
        // 可空 Id 按底层 Id 判定：Nullable<T> 的 GetInterfaces() 恒为空数组（它是运行时的特殊类型，
        // 不参与常规接口表）。不先拆就会把 MyId? 判成「不是强类型 Id」，而 MVC 绑定器与 EF 转换器
        // 都按 Id 处理 —— 同一个类型两套答案，使用者按 IsStronglyTypedId 写的分支会走错路。
        var candidate = Nullable.GetUnderlyingType(type) ?? type;

        // 接口、抽象类、枚举与数组都不可能承载强类型 Id 的实现。
        if (candidate.IsAbstract || candidate.IsInterface || candidate.IsEnum || candidate.IsArray)
        {
            return null;
        }

        // 一个类型理论上可以同时实现多个 IStronglyTypedId<,>（例如手写类型或跨版本继承）。
        // 此时应选 TSelf 恰好等于该类型的那个接口 —— 那才是「它自己」的 Id 契约；
        // 直接取 FirstOrDefault 会让结果取决于接口枚举顺序，同一类型在不同运行时可给出不同基元。
        Type? fallback = null;

        foreach (var interfaceType in candidate.GetInterfaces())
        {
            if (!interfaceType.IsGenericType
                || interfaceType.GetGenericTypeDefinition() != typeof(IStronglyTypedId<,>))
            {
                continue;
            }

            var arguments = interfaceType.GetGenericArguments();

            if (arguments[0] == candidate)
            {
                // IStronglyTypedId<TSelf, TPrimitiveId> 的第二个类型实参即底层基元类型。
                return arguments[1];
            }

            fallback ??= arguments[1];
        }

        return fallback;
    }
}
