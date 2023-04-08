namespace Microsoft.Extensions.DependencyInjection;

public static class SwaggerGenOptionsExtensions
{
    public static void AddStronglyTypedId(this SwaggerGenOptions swaggerGenOptions, IEnumerable<Assembly> assemblies)
    {
        if (assemblies == null || !assemblies.Any()) return;

        swaggerGenOptions.AddStronglyTypedId(assemblies.SelectMany(s => s.GetTypes()));
    }

    public static void AddStronglyTypedId(this SwaggerGenOptions swaggerGenOptions, params Assembly[] assemblies)
    {
        if (assemblies == null || !assemblies.Any()) return;

        swaggerGenOptions.AddStronglyTypedId(assemblies.SelectMany(s => s.GetTypes()));
    }

    public static void AddStronglyTypedId(this SwaggerGenOptions swaggerGenOptions, params Type[] stronglyTypedIdTypes)
    {
        if (stronglyTypedIdTypes == null || stronglyTypedIdTypes.Length == 0) return;

        foreach (var stronglyTypedIdType in stronglyTypedIdTypes)
        {
            swaggerGenOptions.AddStronglyTypedId(stronglyTypedIdType);
        }
    }

    public static void AddStronglyTypedId(this SwaggerGenOptions swaggerGenOptions, IEnumerable<Type> stronglyTypedIdTypes)
    {
        if (stronglyTypedIdTypes == null || !stronglyTypedIdTypes.Any()) return;

        foreach (var stronglyTypedIdType in stronglyTypedIdTypes)
        {
            swaggerGenOptions.AddStronglyTypedId(stronglyTypedIdType);
        }
    }

    public static void AddStronglyTypedId(this SwaggerGenOptions swaggerGenOptions, Type stronglyTypedIdType)
    {
        if (stronglyTypedIdType.TryGetPrimitiveIdType(out var primitiveIdType))
        {
            swaggerGenOptions.MapType(stronglyTypedIdType, () => GetOpenApiSchema(primitiveIdType));
        }
    }

    private static OpenApiSchema GetOpenApiSchema(Type type)
    {
        return type switch
        {
            { } t when t == typeof(int) => new OpenApiSchema { Type = "integer", Format = "int32" },
            { } t when t == typeof(long) => new OpenApiSchema { Type = "integer", Format = "int64" },
            { } t when t == typeof(Guid) => new OpenApiSchema { Type = "string", Format = "uuid" },
            _ => new OpenApiSchema { Type = "string" },
        };
    }
}