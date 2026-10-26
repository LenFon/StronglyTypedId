using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Len.StronglyTypedId;

/// <summary>
/// 提供检视强类型 Id 背后基元类型的方法。
/// </summary>
public static class StronglyTypedIdExtensions
{
    // 缓存 Type → 基元类型（null 表示不是强类型 Id）。这几个方法在模型绑定、EF 转换器等热路径上，
    // 每次都要遍历 GetInterfaces()。键用弱引用，避免插件热重载时把可回收的 ALC 钉住；
    // 用 StrongBox 是为了连 null 结果也能缓存。
    private static readonly ConditionalWeakTable<Type, StrongBox<Type?>> _primitiveIdTypes = [];

    /// <summary>
    /// 获取指定强类型 Id 所包装的基元类型；不是强类型 Id 时为 <see langword="null"/>。
    /// </summary>
    /// <remarks>
    /// 可空 Id 按底层类型判定，与 ASP.NET Core 模型绑定器和 EF Core 转换器保持一致。
    /// </remarks>
    public static Type? GetPrimitiveIdType([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] this Type type)
        => type.TryGetPrimitiveIdType(out var primitiveIdType) ? primitiveIdType : null;

    /// <summary>
    /// 判断指定类型是否为强类型 Id。
    /// </summary>
    public static bool IsStronglyTypedId([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] this Type type) => type.TryGetPrimitiveIdType(out _);

    /// <summary>
    /// 尝试获取指定强类型 Id 所包装的基元类型。
    /// </summary>
    public static bool TryGetPrimitiveIdType([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] this Type type, [NotNullWhen(true)] out Type? primitiveIdType)
    {
        ArgumentNullException.ThrowIfNull(type);

        // 不用 GetValue(key, ResolvePrimitiveIdType)：那个回调无法携带 DynamicallyAccessedMembers 标注，
        // 裁剪分析器会报 IL2091。这里先算好再装箱，回调不碰类型成员。
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
        // 可空 Id 按底层类型判定：Nullable<T> 的 GetInterfaces() 恒为空，不拆开就会把 MyId? 判成不是强类型 Id，
        // 与 MVC 绑定器、EF 转换器的结论不一致。
        var candidate = Nullable.GetUnderlyingType(type) ?? type;

        // 接口、抽象类、枚举与数组都不可能承载强类型 Id 的实现。
        if (candidate.IsAbstract || candidate.IsInterface || candidate.IsEnum || candidate.IsArray)
        {
            return null;
        }

        // 一个类型可能实现多个 IStronglyTypedId<,>，此时取 TSelf 等于自身的那个接口；
        // 直接取第一个会让结果取决于接口枚举顺序。
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
