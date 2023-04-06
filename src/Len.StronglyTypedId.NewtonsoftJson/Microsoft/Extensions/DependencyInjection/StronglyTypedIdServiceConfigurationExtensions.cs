namespace Microsoft.Extensions.DependencyInjection
{
    public static class StronglyTypedIdServiceConfigurationExtensions
    {
        public static void UseNewtonsoftJson(StronglyTypedIdServiceConfiguration config)
        {
            config.AddConvertHandler((stronglyTypedIdType, primitiveIdType) =>
                new JsonConverterAttribute(typeof(StronglyTypedIdJsonConverter<,>)
                    .MakeGenericType(stronglyTypedIdType, primitiveIdType)));
        }
    }
}