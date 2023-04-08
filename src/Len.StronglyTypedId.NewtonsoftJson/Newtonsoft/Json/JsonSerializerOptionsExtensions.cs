namespace Newtonsoft.Json;

public static class JsonSerializerOptionsExtensions
{
    public static JsonSerializerSettings AddStronglyTypedId(this JsonSerializerSettings settings)
    {
        settings.ContractResolver = new CompositeContractResolver
        {
            new StronglyTypedIdContractResolver()
        };

        return settings;
    }
}