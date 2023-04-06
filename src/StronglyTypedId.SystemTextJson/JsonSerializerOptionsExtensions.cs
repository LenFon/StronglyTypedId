using System.Text.Json;

namespace StronglyTypedId.SystemTextJson;

public static class JsonSerializerOptionsExtensions
{
    public static JsonSerializerOptions UseStronglyTypedId(this JsonSerializerOptions options)
    {
        options.Converters.Add(new StronglyTypedIdJsonConverterFactory());

        return options;
    }
}