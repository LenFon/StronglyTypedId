namespace Microsoft.Extensions.DependencyInjection
{
    public static class StronglyTypedIdServiceConfigurationExtensions
    {
        private static bool isUseNewtonsoftJson = false;

        public static void AddNewtonsoftJson(this StronglyTypedIdServiceConfiguration config)
        {
            if (isUseNewtonsoftJson) return;

            config.AddConvertHandler((stronglyTypedIdType, primitiveIdType) =>
                new JsonConverterAttribute(typeof(StronglyTypedIdJsonConverter<,>)
                    .MakeGenericType(stronglyTypedIdType, primitiveIdType)));

            isUseNewtonsoftJson = true;
        }
    }
}