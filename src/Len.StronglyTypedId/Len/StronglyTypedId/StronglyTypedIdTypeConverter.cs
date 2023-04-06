using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Len.StronglyTypedId;

internal class StronglyTypedIdTypeConverter<TStronglyTypedId, TPrimitiveId> : TypeConverter
         where TStronglyTypedId : IStronglyTypedId<TPrimitiveId>
         where TPrimitiveId : struct, IComparable, IComparable<TPrimitiveId>, IEquatable<TPrimitiveId>, ISpanParsable<TPrimitiveId>
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
        sourceType == typeof(string) ||
        sourceType == typeof(TPrimitiveId) ||
        base.CanConvertFrom(context, sourceType);

    public override bool CanConvertTo(ITypeDescriptorContext? context, [NotNullWhen(true)] Type? destinationType) =>
        destinationType == typeof(string) ||
        destinationType == typeof(TPrimitiveId) ||
        base.CanConvertTo(context, destinationType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        return value switch
        {
            TPrimitiveId val => TStronglyTypedId.Create(val),
            string val when !string.IsNullOrEmpty(val) && TPrimitiveId.TryParse(val, null, out var result) =>
                TStronglyTypedId.Create(result),
            _ => base.ConvertFrom(context, culture, value),
        };
    }

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (value is IStronglyTypedId<TPrimitiveId> id)
        {
            if (destinationType == typeof(TPrimitiveId))
            {
                return id.Value;
            }

            if (destinationType == typeof(string))
            {
                return id.Value.ToString();
            }
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }
}