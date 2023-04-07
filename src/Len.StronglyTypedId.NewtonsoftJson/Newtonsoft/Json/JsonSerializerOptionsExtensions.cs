using Len.StronglyTypedId.NewtonsoftJson;

namespace Newtonsoft.Json;

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