using System.ComponentModel;

namespace Microsoft.Extensions.DependencyInjection;

public static class MvcBuilderExtensions
{
    public static IMvcBuilder AddStronglyTypedId(
        this IMvcBuilder builder,
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

            var attribute = new TypeConverterAttribute(typeof(StronglyTypedIdTypeConverter<,>)
                .MakeGenericType(type, primitiveIdType));

            TypeDescriptor.AddAttributes(type, attribute);
        }

        return builder;
    }
}