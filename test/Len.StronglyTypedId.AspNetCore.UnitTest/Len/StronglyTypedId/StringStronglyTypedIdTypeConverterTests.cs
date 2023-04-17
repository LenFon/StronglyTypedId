namespace Len.StronglyTypedId;

public class StringStronglyTypedIdTypeConverterTests
{
    [Fact]
    public void ConvertTo()
    {
        var id = new StringId("AA");
        var converter = new StronglyTypedIdTypeConverter<StringId>();

        var val = converter.ConvertTo(id, typeof(string));

        Assert.NotNull(val);
        Assert.IsType<string>(val);
        Assert.Equal(id.Value, val);

        var id2 = "AA";
        Assert.Throws<NotSupportedException>(() => converter.ConvertTo(id2, typeof(Guid)));
    }

    [Fact]
    public void ConvertFrom()
    {
        var id = "AA";
        var converter = new StronglyTypedIdTypeConverter<StringId>();

        var val = converter.ConvertFrom(id);

        Assert.NotNull(val);
        Assert.IsType<StringId>(val);
        Assert.Equal(id, ((StringId)val).Value);

        var id2 = 10;
        Assert.Throws<NotSupportedException>(() => converter.ConvertFrom(id2));
    }

    [Fact]
    public void CanConvertFrom()
    {
        var converter = new StronglyTypedIdTypeConverter<StringId>();

        var result1 = converter.CanConvertFrom(typeof(string));

        Assert.True(result1);

        var result2 = converter.CanConvertFrom(typeof(Guid));

        Assert.False(result2);
    }

    [Fact]
    public void CanConvertTo()
    {
        var converter = new StronglyTypedIdTypeConverter<StringId>();

        var result1 = converter.CanConvertTo(typeof(string));

        Assert.True(result1);

        var result2 = converter.CanConvertTo(typeof(Guid));

        Assert.False(result2);
    }
}
