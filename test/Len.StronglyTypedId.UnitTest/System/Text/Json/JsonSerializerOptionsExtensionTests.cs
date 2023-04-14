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
    public void Serialize_StronglyTypedId()
    {
        var options = new JsonSerializerOptions();
        options.AddStronglyTypedId();

        var id = new Id(Guid.NewGuid());
        var val = $"\"{id.Value}\"";
        var jsonVal = JsonSerializer.Serialize(id, options);

        Assert.Equal(val, jsonVal);
    }

    [Fact]
    public void Deserialize_StronglyTypedId()
    {
        var options = new JsonSerializerOptions();
        options.AddStronglyTypedId();

        var val = Guid.NewGuid();
        var jsonVal = $"\"{val}\"";
        var id = JsonSerializer.Deserialize<Id>(jsonVal, options);

        Assert.Equal(val, id.Value);
    }
}