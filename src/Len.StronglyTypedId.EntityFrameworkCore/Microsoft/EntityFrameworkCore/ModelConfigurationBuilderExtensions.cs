using Len.StronglyTypedId;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Reflection;

namespace Microsoft.EntityFrameworkCore
{
    public static class ModelConfigurationBuilderExtensions
    {
        public static void AddStronglyTypedId(this ModelConfigurationBuilder configurationBuilder, IEnumerable<Assembly> assemblies)
        {
            if (assemblies == null || !assemblies.Any()) return;

            configurationBuilder.AddStronglyTypedId(assemblies.SelectMany(s => s.GetTypes()));
        }

        public static void AddStronglyTypedId(this ModelConfigurationBuilder configurationBuilder, params Assembly[] assemblies)
        {
            if (assemblies == null || !assemblies.Any()) return;

            configurationBuilder.AddStronglyTypedId(assemblies.SelectMany(s => s.GetTypes()));
        }

        public static void AddStronglyTypedId(this ModelConfigurationBuilder configurationBuilder, params Type[] stronglyTypedIdTypes)
        {
            if (stronglyTypedIdTypes == null || stronglyTypedIdTypes.Length == 0) return;

            foreach (var stronglyTypedIdType in stronglyTypedIdTypes)
            {
                configurationBuilder.AddStronglyTypedId(stronglyTypedIdType);
            }
        }

        public static void AddStronglyTypedId(this ModelConfigurationBuilder configurationBuilder, IEnumerable<Type> stronglyTypedIdTypes)
        {
            if (stronglyTypedIdTypes == null || !stronglyTypedIdTypes.Any()) return;

            foreach (var stronglyTypedIdType in stronglyTypedIdTypes)
            {
                configurationBuilder.AddStronglyTypedId(stronglyTypedIdType);
            }
        }

        public static void AddStronglyTypedId(this ModelConfigurationBuilder configurationBuilder, Type stronglyTypedIdType)
        {
            if (stronglyTypedIdType.TryGetPrimitiveIdType(out var primitiveIdType))
            {
                configurationBuilder.Properties(stronglyTypedIdType)
                    .HaveConversion(typeof(StronglyTypedIdConverter<,>).MakeGenericType(stronglyTypedIdType, primitiveIdType));
            }
        }

        class StronglyTypedIdConverter<TStronglyTypedId, TPrimitiveId> : ValueConverter<TStronglyTypedId, TPrimitiveId>
             where TStronglyTypedId : IStronglyTypedId<TPrimitiveId>
             where TPrimitiveId : notnull, IComparable, IComparable<TPrimitiveId>, IEquatable<TPrimitiveId>
        {
            public StronglyTypedIdConverter() : base(v => v.Value, val => ConvertToStronglyTypedId(val))
            {
            }

            private static TStronglyTypedId ConvertToStronglyTypedId(TPrimitiveId value)
            {
                return (TStronglyTypedId)TStronglyTypedId.Create(value);
            }
        }

    }
}
