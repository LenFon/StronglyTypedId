namespace Len.StronglyTypedId.NewtonsoftJson;

internal class StronglyTypedIdJsonConverter<TStronglyTypedId, TPrimitiveId> : JsonConverter<TStronglyTypedId>
    where TStronglyTypedId : IStronglyTypedId<TPrimitiveId>
    where TPrimitiveId : struct, IComparable, IComparable<TPrimitiveId>, IEquatable<TPrimitiveId>, ISpanParsable<TPrimitiveId>
{
    public override TStronglyTypedId? ReadJson(JsonReader reader, Type objectType, TStronglyTypedId? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
        {
            return default;
        }

        var val = serializer.Deserialize<TPrimitiveId>(reader);

        return (TStronglyTypedId)TStronglyTypedId.Create(val);
    }

    public override void WriteJson(JsonWriter writer, TStronglyTypedId? value, JsonSerializer serializer)
    {
        if (value is null)
        {
            writer.WriteNull();
        }
        else
        {
            writer.WriteValue(value.Value);
        }
    }
}