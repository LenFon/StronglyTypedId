using System.Diagnostics.CodeAnalysis;

namespace Len.StronglyTypedId;

/// <summary>
/// Provides methods to inspect the primitive type behind a strongly typed id.
/// </summary>
public static class StronglyTypedIdExtensions
{
    /// <summary>
    /// Gets the primitive type wrapped by the specified strongly typed id.
    /// </summary>
    /// <param name="type">The strongly typed id type to inspect.</param>
    /// <returns>
    /// The wrapped primitive type, or <see langword="null"/> when <paramref name="type"/> is not a strongly typed id.
    /// </returns>
    public static Type? GetPrimitiveIdType(this Type type)
        => type.TryGetPrimitiveIdType(out var primitiveIdType) ? primitiveIdType : null;

    /// <summary>
    /// Determines whether the specified type is a strongly typed id.
    /// </summary>
    /// <param name="type">The type to inspect.</param>
    public static bool IsStronglyTypedId(this Type type) => type.TryGetPrimitiveIdType(out _);

    /// <summary>
    /// Attempts to get the primitive type wrapped by the specified strongly typed id.
    /// </summary>
    /// <param name="type">The type to inspect.</param>
    /// <param name="primitiveIdType">The wrapped primitive type when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when <paramref name="type"/> is a strongly typed id; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="type"/> is <see langword="null"/>.</exception>
    public static bool TryGetPrimitiveIdType(this Type type, [NotNullWhen(true)] out Type? primitiveIdType)
    {
        ArgumentNullException.ThrowIfNull(type);

        // 接口、抽象类、枚举与数组都不可能承载强类型 Id 的实现。
        if (type.IsAbstract || type.IsInterface || type.IsEnum || type.IsArray)
        {
            primitiveIdType = null;
            return false;
        }

        var stronglyTypedIdInterface = type.GetInterfaces()
            .FirstOrDefault(interfaceType => interfaceType.IsGenericType
                && interfaceType.GetGenericTypeDefinition() == typeof(IStronglyTypedId<,>));

        if (stronglyTypedIdInterface is null)
        {
            primitiveIdType = null;
            return false;
        }

        // IStronglyTypedId<TSelf, TPrimitiveId> 的第二个类型实参即底层基元类型。
        primitiveIdType = stronglyTypedIdInterface.GetGenericArguments()[1];

        return true;
    }
}
