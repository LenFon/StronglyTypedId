using System.Diagnostics.CodeAnalysis;

namespace Len.StronglyTypedId;

public static class StronglyTypedIdExtensions
{
    public static Type? GetPrimitiveIdType(this Type type)
    {
        if (type.TryGetPrimitiveIdType(out var primitiveIdType))
        {
            return primitiveIdType;
        }

        return default;
    }

    public static bool IsStronglyTypedId(this Type type) => type.TryGetPrimitiveIdType(out var _);

    public static bool TryGetPrimitiveIdType(this Type type, [NotNullWhen(true)] out Type? primitiveIdType)
    {
        ArgumentNullException.ThrowIfNull(type, nameof(type));

        if (type.IsAbstract || type.IsInterface || type.IsEnum || type.IsArray)
        {
            primitiveIdType = default;
            return false;
        }

        var interfaceType = type.GetInterfaces()
            .FirstOrDefault(w => w.IsGenericType && w.GetGenericTypeDefinition() == typeof(IStronglyTypedId<>));

        if (interfaceType == null)
        {
            primitiveIdType = default;
            return false;
        }

        primitiveIdType = interfaceType.GetGenericArguments()[0];

        return true;
    }
}