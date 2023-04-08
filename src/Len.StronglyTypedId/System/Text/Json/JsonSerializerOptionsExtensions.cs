namespace System.Text.Json;

public static class JsonSerializerOptionsExtensions
{
    public static JsonSerializerOptions AddStronglyTypedId(this JsonSerializerOptions options)
    {
        options.Converters.Add(new StronglyTypedIdJsonConverterFactory());

        return options;
    }
}