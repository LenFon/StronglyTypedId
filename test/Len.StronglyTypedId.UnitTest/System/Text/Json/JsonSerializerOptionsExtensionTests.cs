using Len.StronglyTypedId;

namespace System.Text.Json;

public class JsonSerializerOptionsExtensionTests
{
    [Fact]
    public void AddStronglyTypedId()
    {
        var options = new JsonSerializerOptions();

        options.AddStronglyTypedId();

        Assert.True(options.Converters.Count == 1);
    }

    [Fact]
    public void AddStronglyTypedId_Options_Is_Null()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
        {
            JsonSerializerOptions? options = null;

            options!.AddStronglyTypedId();
        });

        Assert.Equal("options", ex.ParamName);
    }

    [Fact]
    public void Serialize_StronglyTypedId_Guid()
    {
        var options = new JsonSerializerOptions();
        options.AddStronglyTypedId();

        var id = new Id(Guid.NewGuid());
        var val = $"\"{id.Value}\"";
        var jsonVal = JsonSerializer.Serialize(id, options);

        Assert.Equal(val, jsonVal);
    }

    [Fact]
    public void Serialize_StronglyTypedId_Int32()
    {
        var options = new JsonSerializerOptions();
        options.AddStronglyTypedId();

        var id = new Int32Id(1);
        var val = $"{id.Value}";
        var jsonVal = JsonSerializer.Serialize(id, options);

        Assert.Equal(val, jsonVal);
    }

    [Fact]
    public void Serialize_StronglyTypedId_UInt32()
    {
        var options = new JsonSerializerOptions();
        options.AddStronglyTypedId();

        var id = new UInt32Id(1);
        var val = $"{id.Value}";
        var jsonVal = JsonSerializer.Serialize(id, options);

        Assert.Equal(val, jsonVal);
    }

    [Fact]
    public void Serialize_StronglyTypedId_Int64()
    {
        var options = new JsonSerializerOptions();
        options.AddStronglyTypedId();

        var id = new Int64Id(1);
        var val = $"{id.Value}";
        var jsonVal = JsonSerializer.Serialize(id, options);

        Assert.Equal(val, jsonVal);
    }

    [Fact]
    public void Serialize_StronglyTypedId_UInt64()
    {
        var options = new JsonSerializerOptions();
        options.AddStronglyTypedId();

        var id = new UInt64Id(1);
        var val = $"{id.Value}";
        var jsonVal = JsonSerializer.Serialize(id, options);

        Assert.Equal(val, jsonVal);
    }

    [Fact]
    public void Serialize_StronglyTypedId_String()
    {
        var options = new JsonSerializerOptions();
        options.AddStronglyTypedId();

        var id = new StringId("AA");
        var val = $"\"{id.Value}\"";
        var jsonVal = JsonSerializer.Serialize(id, options);

        Assert.Equal(val, jsonVal);
    }

    [Fact]
    public void Serialize_StronglyTypedId_Byte()
    {
        var options = new JsonSerializerOptions();
        options.AddStronglyTypedId();

        var id = new ByteId(1);
        var val = $"{id.Value}";

        Assert.Throws<NotSupportedException>(() => JsonSerializer.Serialize(id, options));
    }

    [Fact]
    public void Deserialize_StronglyTypedId_Guid()
    {
        var options = new JsonSerializerOptions();
        options.AddStronglyTypedId();

        var val = Guid.NewGuid();
        var jsonVal = $"\"{val}\"";
        var id = JsonSerializer.Deserialize<Id>(jsonVal, options);

        Assert.Equal(val, id.Value);
    }

    [Fact]
    public void Deserialize_StronglyTypedId_Int32()
    {
        var options = new JsonSerializerOptions();
        options.AddStronglyTypedId();

        var val = 1;
        var jsonVal = $"{val}";
        var id = JsonSerializer.Deserialize<Int32Id>(jsonVal, options);

        Assert.Equal(val, id.Value);
    }

    [Fact]
    public void Deserialize_StronglyTypedId_UInt32()
    {
        var options = new JsonSerializerOptions();
        options.AddStronglyTypedId();

        var val = 1U;
        var jsonVal = $"{val}";
        var id = JsonSerializer.Deserialize<UInt32Id>(jsonVal, options);

        Assert.Equal(val, id.Value);
    }

    [Fact]
    public void Deserialize_StronglyTypedId_Int64()
    {
        var options = new JsonSerializerOptions();
        options.AddStronglyTypedId();

        var val = 1L;
        var jsonVal = $"{val}";
        var id = JsonSerializer.Deserialize<Int64Id>(jsonVal, options);

        Assert.Equal(val, id.Value);
    }

    [Fact]
    public void Deserialize_StronglyTypedId_UInt64()
    {
        var options = new JsonSerializerOptions();
        options.AddStronglyTypedId();

        var val = 1UL;
        var jsonVal = $"{val}";
        var id = JsonSerializer.Deserialize<UInt64Id>(jsonVal, options);

        Assert.Equal(val, id.Value);
    }

    [Fact]
    public void Deserialize_StronglyTypedId_String()
    {
        var options = new JsonSerializerOptions();
        options.AddStronglyTypedId();

        var val = "AA";
        var jsonVal = $"\"{val}\"";
        var id = JsonSerializer.Deserialize<StringId>(jsonVal, options);

        Assert.Equal(val, id.Value);
    }

    [Fact]
    public void Deserialize_StronglyTypedId_Byte()
    {
        var options = new JsonSerializerOptions();
        options.AddStronglyTypedId();

        byte val = 1;
        var jsonVal = $"{val}";

        Assert.Throws<NotSupportedException>(() => JsonSerializer.Deserialize<ByteId>(jsonVal, options));
    }
}