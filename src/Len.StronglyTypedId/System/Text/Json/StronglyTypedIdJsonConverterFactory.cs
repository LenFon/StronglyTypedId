using System.Text.Json.Serialization;

namespace System.Text.Json;

internal class StronglyTypedIdJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsStronglyTypedId();

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        if (!typeToConvert.TryGetPrimitiveIdType(out var primitiveIdType))
            throw new InvalidOperationException($"Cannot create converter for '{typeToConvert}'");

        var type = typeof(StronglyTypedIdJsonConverter<,>).MakeGenericType(typeToConvert, primitiveIdType);
        return (JsonConverter)Activator.CreateInstance(type)!;
    }
}