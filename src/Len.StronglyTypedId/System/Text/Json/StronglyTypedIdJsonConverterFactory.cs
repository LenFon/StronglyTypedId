using System.Text.Json.Serialization;

namespace System.Text.Json;

internal class StronglyTypedIdJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsStronglyTypedId();

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var primitiveIdType = typeToConvert.GetPrimitiveIdType()!;
        var type = typeof(StronglyTypedIdJsonConverter<,>).MakeGenericType(typeToConvert, primitiveIdType);
        return (JsonConverter)Activator.CreateInstance(type)!;
    }
}