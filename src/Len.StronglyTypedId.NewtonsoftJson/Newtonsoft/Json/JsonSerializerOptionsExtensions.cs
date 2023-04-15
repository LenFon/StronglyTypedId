namespace Newtonsoft.Json;

public static class JsonSerializerOptionsExtensions
{
    public static JsonSerializerSettings AddStronglyTypedId(this JsonSerializerSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings, nameof(settings));

        settings.ContractResolver = new CompositeContractResolver
        {
            new StronglyTypedIdContractResolver()
        };

        return settings;
    }
}