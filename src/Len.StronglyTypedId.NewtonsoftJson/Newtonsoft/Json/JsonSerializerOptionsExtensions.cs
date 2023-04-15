namespace Newtonsoft.Json;

public static class JsonSerializerOptionsExtensions
{
    public static JsonSerializerSettings AddStronglyTypedId(this JsonSerializerSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings, nameof(settings));

        if (settings.ContractResolver is null)
        {
            settings.ContractResolver = new CompositeContractResolver
            {
                new StronglyTypedIdContractResolver()
            };
        }
        else if (settings.ContractResolver.GetType() != typeof(CompositeContractResolver))
        {
            var resolver = new CompositeContractResolver(settings.ContractResolver)
            {
                new StronglyTypedIdContractResolver()
            };
            settings.ContractResolver = resolver;
        }

        return settings;
    }
}