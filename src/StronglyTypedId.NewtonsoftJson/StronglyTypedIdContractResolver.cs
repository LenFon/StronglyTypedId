using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace StronglyTypedId.NewtonsoftJson
{
    internal class StronglyTypedIdContractResolver : DefaultContractResolver
    {
        protected override JsonConverter? ResolveContractConverter(Type objectType)
        {
            if (objectType.TryGetPrimitiveIdType(out var primitiveIdType))
            {
                var type = typeof(StronglyTypedIdConverter<,>).MakeGenericType(objectType, primitiveIdType);
                return (JsonConverter)Activator.CreateInstance(type)!;
            }
            return base.ResolveContractConverter(objectType);
        }

        class StronglyTypedIdConverter<TStronglyTypedId, TPrimitiveId> : JsonConverter<TStronglyTypedId>
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
    }
}
