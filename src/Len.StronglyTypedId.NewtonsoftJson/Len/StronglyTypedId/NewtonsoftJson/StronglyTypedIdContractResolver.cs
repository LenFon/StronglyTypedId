using Newtonsoft.Json.Serialization;

namespace Len.StronglyTypedId.NewtonsoftJson
{
    internal class StronglyTypedIdContractResolver : DefaultContractResolver
    {
        protected override JsonConverter? ResolveContractConverter(Type objectType)
        {
            if (objectType.TryGetPrimitiveIdType(out var primitiveIdType))
            {
                var type = typeof(StronglyTypedIdJsonConverter<,>).MakeGenericType(objectType, primitiveIdType);
                return (JsonConverter)Activator.CreateInstance(type)!;
            }
            return base.ResolveContractConverter(objectType);
        }
    }
}