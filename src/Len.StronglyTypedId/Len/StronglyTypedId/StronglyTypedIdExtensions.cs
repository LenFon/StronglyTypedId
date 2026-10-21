using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Len.StronglyTypedId;

/// <summary>
/// Provides methods to inspect the primitive type behind a strongly typed id.
/// </summary>
public static class StronglyTypedIdExtensions
{
    // 类型 → 基元类型（null 表示「不是强类型 Id」）。这三个入口常落在模型绑定、EF 转换器、
    // DI 注册这类热路径上，每次判定都要 GetInterfaces() + LINQ，而对同一个 Type 结果恒定。
    // Type 本身被运行时根住，字典不会阻止卸载，故直接以 Type 为键。
    private static readonly ConcurrentDictionary<Type, Type?> _primitiveIdTypes = new();

    /// <summary>
    /// Gets the primitive type wrapped by the specified strongly typed id.
    /// </summary>
    /// <param name="type">The strongly typed id type to inspect.</param>
    /// <returns>
    /// The wrapped primitive type, or <see langword="null"/> when <paramref name="type"/> is not a strongly typed id.
    /// </returns>
    /// <remarks>
    /// A nullable strongly typed id (<see cref="Nullable{T}"/>) is judged by its underlying id type:
    /// <c>typeof(OrderId?)</c> yields the same primitive type as <c>typeof(OrderId)</c>. This matches what the
    /// ASP.NET Core model binder and the EF Core converters do with nullable ids.
    /// </remarks>
    public static Type? GetPrimitiveIdType([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] this Type type)
        => type.TryGetPrimitiveIdType(out var primitiveIdType) ? primitiveIdType : null;

    /// <summary>
    /// Determines whether the specified type is a strongly typed id.
    /// </summary>
    /// <param name="type">The type to inspect.</param>
    /// <remarks>
    /// A nullable strongly typed id (<see cref="Nullable{T}"/>) counts as a strongly typed id, exactly like its
    /// underlying id type.
    /// </remarks>
    public static bool IsStronglyTypedId([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] this Type type) => type.TryGetPrimitiveIdType(out _);

    /// <summary>
    /// Attempts to get the primitive type wrapped by the specified strongly typed id.
    /// </summary>
    /// <param name="type">The type to inspect.</param>
    /// <param name="primitiveIdType">
    /// The wrapped primitive type when this method returns <see langword="true"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when <paramref name="type"/> is a strongly typed id; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// A nullable strongly typed id (<see cref="Nullable{T}"/>) is judged by its underlying id type, so it reports
    /// the same primitive type as the non-nullable form.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="type"/> is <see langword="null"/>.</exception>
    public static bool TryGetPrimitiveIdType([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] this Type type, [NotNullWhen(true)] out Type? primitiveIdType)
    {
        ArgumentNullException.ThrowIfNull(type);

        // 手写 TryGetValue / TryAdd 而不是 GetOrAdd：后者的工厂委托无法携带
        // DynamicallyAccessedMembers 标注，裁剪分析器会报 IL2091。并发下两边算出的值相同，谁先写入都一样。
        if (!_primitiveIdTypes.TryGetValue(type, out primitiveIdType))
        {
            primitiveIdType = ResolvePrimitiveIdType(type);
            _primitiveIdTypes.TryAdd(type, primitiveIdType);
        }

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

        var stronglyTypedIdInterface = candidate.GetInterfaces()
            .FirstOrDefault(interfaceType => interfaceType.IsGenericType
                && interfaceType.GetGenericTypeDefinition() == typeof(IStronglyTypedId<,>));

        // IStronglyTypedId<TSelf, TPrimitiveId> 的第二个类型实参即底层基元类型。
        return stronglyTypedIdInterface?.GetGenericArguments()[1];
    }
}
