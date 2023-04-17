using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Len.StronglyTypedId;

public class StronglyTypedIdTypeConverterTests
{
    [Fact]
    public void ConvertTo()
    {
        var id = new GuidId(Guid.NewGuid());
        var converter = new StronglyTypedIdTypeConverter<GuidId, Guid>();

        var val = converter.ConvertTo(id, typeof(Guid));

        Assert.NotNull(val);
        Assert.IsType<Guid>(val);
        Assert.Equal(id.Value, val);

        var val2 = converter.ConvertTo(id, typeof(string));

        Assert.NotNull(val2);
        Assert.IsType<string>(val2);
        Assert.Equal(id.Value.ToString(), val2);
    }

    [Fact]
    public void ConvertTo_Not_StronglyTypedId()
    {
        var id = Guid.NewGuid();
        var converter = new StronglyTypedIdTypeConverter<GuidId, Guid>();

        Assert.Throws<NotSupportedException>(() => converter.ConvertTo(id, typeof(Guid)));
    }

    [Fact]
    public void ConvertFrom()
    {
        var id = Guid.NewGuid();
        var converter = new StronglyTypedIdTypeConverter<GuidId, Guid>();

        var val = converter.ConvertFrom(id);

        Assert.NotNull(val);
        Assert.IsType<GuidId>(val);
        Assert.Equal(id, ((GuidId)val).Value);

        var id2 = Guid.NewGuid().ToString();

        var val2 = converter.ConvertFrom(id2);

        Assert.NotNull(val2);
        Assert.IsType<GuidId>(val2);
        Assert.Equal(id2, ((GuidId)val2).Value.ToString());
    }

    [Fact]
    public void ConvertFrom_Int()
    {
        var id = 10;
        var converter = new StronglyTypedIdTypeConverter<GuidId, Guid>();

        Assert.Throws<NotSupportedException>(() => converter.ConvertFrom(id));
    }

    [Fact]
    public void CanConvertFrom()
    {
        var converter = new StronglyTypedIdTypeConverter<GuidId, Guid>();

        var result1 = converter.CanConvertFrom(typeof(string));

        Assert.True(result1);

        var result2 = converter.CanConvertFrom(typeof(Guid));

        Assert.True(result2);

        var result3 = converter.CanConvertFrom(typeof(int));

        Assert.False(result3);
    }

    [Fact]
    public void CanConvertTo()
    {
        var converter = new StronglyTypedIdTypeConverter<GuidId, Guid>();

        var result1 = converter.CanConvertTo(typeof(string));

        Assert.True(result1);

        var result2 = converter.CanConvertTo(typeof(Guid));

        Assert.True(result2);

        var result3 = converter.CanConvertTo(typeof(int));

        Assert.False(result3);
    }
}