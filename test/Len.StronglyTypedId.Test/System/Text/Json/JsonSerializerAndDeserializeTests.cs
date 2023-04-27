using Len.StronglyTypedId;

namespace System.Text.Json;

public class JsonSerializerAndDeserializeTests
{
    [Fact]
    public void Serialize_StronglyTypedId_Guid()
    {
        var options = new JsonSerializerOptions();

        var id = new GuidId(Guid.NewGuid());
        var val = $"\"{id.Value}\"";
        var jsonVal = JsonSerializer.Serialize(id, options);

        Assert.Equal(val, jsonVal);
    }

    [Fact]
    public void Serialize_StronglyTypedId_Int32()
    {
        var options = new JsonSerializerOptions();

        var id = new Int32Id(1);
        var val = $"{id.Value}";
        var jsonVal = JsonSerializer.Serialize(id, options);

        Assert.Equal(val, jsonVal);
    }

    [Fact]
    public void Serialize_StronglyTypedId_UInt32()
    {
        var options = new JsonSerializerOptions();

        var id = new UInt32Id(1);
        var val = $"{id.Value}";
        var jsonVal = JsonSerializer.Serialize(id, options);

        Assert.Equal(val, jsonVal);
    }

    [Fact]
    public void Serialize_StronglyTypedId_Int64()
    {
        var options = new JsonSerializerOptions();

        var id = new Int64Id(1);
        var val = $"{id.Value}";
        var jsonVal = JsonSerializer.Serialize(id, options);

        Assert.Equal(val, jsonVal);
    }

    [Fact]
    public void Serialize_StronglyTypedId_UInt64()
    {
        var options = new JsonSerializerOptions();

        var id = new UInt64Id(1);
        var val = $"{id.Value}";
        var jsonVal = JsonSerializer.Serialize(id, options);

        Assert.Equal(val, jsonVal);
    }

    [Fact]
    public void Serialize_StronglyTypedId_String()
    {
        var options = new JsonSerializerOptions();

        var id = new StringId("AA");
        var val = $"\"{id.Value}\"";
        var jsonVal = JsonSerializer.Serialize(id, options);

        Assert.Equal(val, jsonVal);
    }

    [Fact]
    public void Deserialize_StronglyTypedId_Guid()
    {
        var options = new JsonSerializerOptions();

        var val = Guid.NewGuid();
        var jsonVal = $"\"{val}\"";
        var id = JsonSerializer.Deserialize<GuidId>(jsonVal, options);

        Assert.Equal(val, id.Value);
    }

    [Fact]
    public void Deserialize_StronglyTypedId_Int32()
    {
        var options = new JsonSerializerOptions();

        var val = 1;
        var jsonVal = $"{val}";
        var id = JsonSerializer.Deserialize<Int32Id>(jsonVal, options);

        Assert.Equal(val, id.Value);
    }

    [Fact]
    public void Deserialize_StronglyTypedId_UInt32()
    {
        var options = new JsonSerializerOptions();

        var val = 1U;
        var jsonVal = $"{val}";
        var id = JsonSerializer.Deserialize<UInt32Id>(jsonVal, options);

        Assert.Equal(val, id.Value);
    }

    [Fact]
    public void Deserialize_StronglyTypedId_Int64()
    {
        var options = new JsonSerializerOptions();

        var val = 1L;
        var jsonVal = $"{val}";
        var id = JsonSerializer.Deserialize<Int64Id>(jsonVal, options);

        Assert.Equal(val, id.Value);
    }

    [Fact]
    public void Deserialize_StronglyTypedId_UInt64()
    {
        var options = new JsonSerializerOptions();

        var val = 1UL;
        var jsonVal = $"{val}";
        var id = JsonSerializer.Deserialize<UInt64Id>(jsonVal, options);

        Assert.Equal(val, id.Value);
    }

    [Fact]
    public void Deserialize_StronglyTypedId_String()
    {
        var options = new JsonSerializerOptions();

        var val = "AA";
        var jsonVal = $"\"{val}\"";
        var id = JsonSerializer.Deserialize<StringId>(jsonVal, options);

        Assert.Equal(val, id.Value);
    }
}