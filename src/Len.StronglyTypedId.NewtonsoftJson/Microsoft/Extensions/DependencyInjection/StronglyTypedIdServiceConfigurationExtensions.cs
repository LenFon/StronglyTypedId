namespace Microsoft.Extensions.DependencyInjection
{
    public static class StronglyTypedIdServiceConfigurationExtensions
    {
        public static void AddNewtonsoftJson(StronglyTypedIdServiceConfiguration config)
        {
            config.AddConvertHandler((stronglyTypedIdType, primitiveIdType) =>
                new JsonConverterAttribute(typeof(StronglyTypedIdJsonConverter<,>)
                    .MakeGenericType(stronglyTypedIdType, primitiveIdType)));
        }
    }
}