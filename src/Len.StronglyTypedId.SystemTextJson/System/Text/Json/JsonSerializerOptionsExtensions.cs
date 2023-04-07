using Len.StronglyTypedId.SystemTextJson;

namespace System.Text.Json;

public static class JsonSerializerOptionsExtensions
{
    public static JsonSerializerOptions UseStronglyTypedId(this JsonSerializerOptions options)
    {
        options.Converters.Add(new StronglyTypedIdJsonConverterFactory());

        return options;
    }
}