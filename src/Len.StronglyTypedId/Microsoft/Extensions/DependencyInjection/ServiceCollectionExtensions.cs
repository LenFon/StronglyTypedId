using System.ComponentModel;

namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddStronglyTypedId(
        this IServiceCollection services,
        Action<StronglyTypedIdServiceConfiguration> configuration)
    {
        var serviceConfig = new StronglyTypedIdServiceConfiguration();

        configuration.Invoke(serviceConfig);

        if (!serviceConfig.AssembliesToRegister.Any())
        {
            throw new ArgumentException("至少提供一个程序集用于注册转换器。");
        }

        foreach (var type in serviceConfig.AssembliesToRegister.SelectMany(s => s.GetTypes()))
        {
            if (!type.TryGetPrimitiveIdType(out var primitiveIdType)) continue;

            var attributes = serviceConfig.ConvertHandles
                .Select(s => s.Invoke(type, primitiveIdType))
                .ToArray();

            TypeDescriptor.AddAttributes(type, attributes);
        }

        return services;
    }
}