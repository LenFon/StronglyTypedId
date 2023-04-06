using Newtonsoft.Json;

namespace StronglyTypedId.NewtonsoftJson;

public static class JsonSerializerOptionsExtensions
{
    public static JsonSerializerSettings UseStronglyTypedId(this JsonSerializerSettings settings)
    {
        settings.ContractResolver = new CompositeContractResolver
        {
            new StronglyTypedIdContractResolver()
        };

        return settings;
    }
}