using Len.StronglyTypedId;
using Newtonsoft.Json.Serialization;
using System.Collections;

namespace Newtonsoft.Json;

public class JsonSerializerOptionsExtensionTests
{
    [Fact]
    public void AddStronglyTypedId()
    {
        var settings = new JsonSerializerSettings();

        settings.AddStronglyTypedId();
        settings.AddStronglyTypedId();

        settings.ContractResolver = new TestContractResolver();

        settings.AddStronglyTypedId();

        if (settings.ContractResolver is IEnumerable enumerable)
        {
            var i = 0;
            foreach (var item in enumerable)
            {
                i++;
            }

            Assert.Equal(2, i);
        }

        if (settings.ContractResolver is IEnumerable<IContractResolver> enumerable2)
        {
            var i = 0;
            foreach (var item in enumerable2)
            {
                i++;
            }

            Assert.Equal(2, i);
        }
    }

    class TestContractResolver : IContractResolver
    {
        public JsonContract ResolveContract(Type type)
        {
            return default!;
        }
    }

    [Fact]
    public void AddStronglyTypedId_Options_Is_Null()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
        {
            JsonSerializerSettings? settings = null;

            settings!.AddStronglyTypedId();
        });

        Assert.Equal("settings", ex.ParamName);
    }

    [Fact]
    public void Serialize_StronglyTypedId_Guid()
    {
        var settings = new JsonSerializerSettings();
        settings.AddStronglyTypedId();

        var id = new Id(Guid.NewGuid());
        var val = $"\"{id.Value}\"";
        var jsonVal = JsonConvert.SerializeObject(id, settings);

        Assert.Equal(val, jsonVal);
    }

    [Fact]
    public void Serialize_StronglyTypedId_Int32()
    {
        var settings = new JsonSerializerSettings();
        settings.AddStronglyTypedId();

        var id = new Int32Id(1);
        var val = $"{id.Value}";
        var jsonVal = JsonConvert.SerializeObject(id, settings);

        Assert.Equal(val, jsonVal);
    }

    [Fact]
    public void Serialize_StronglyTypedId_UInt32()
    {
        var settings = new JsonSerializerSettings();
        settings.AddStronglyTypedId();

        var id = new UInt32Id(1);
        var val = $"{id.Value}";
        var jsonVal = JsonConvert.SerializeObject(id, settings);

        Assert.Equal(val, jsonVal);
    }

    [Fact]
    public void Serialize_StronglyTypedId_Int64()
    {
        var settings = new JsonSerializerSettings();
        settings.AddStronglyTypedId();

        var id = new Int64Id(1);
        var val = $"{id.Value}";
        var jsonVal = JsonConvert.SerializeObject(id, settings);

        Assert.Equal(val, jsonVal);
    }

    [Fact]
    public void Serialize_StronglyTypedId_UInt64()
    {
        var settings = new JsonSerializerSettings();
        settings.AddStronglyTypedId();

        var id = new UInt64Id(1);
        var val = $"{id.Value}";
        var jsonVal = JsonConvert.SerializeObject(id, settings);

        Assert.Equal(val, jsonVal);
    }

    [Fact]
    public void Serialize_StronglyTypedId_String()
    {
        var settings = new JsonSerializerSettings();
        settings.AddStronglyTypedId();

        var id = new StringId("AA");
        var val = $"\"{id.Value}\"";
        var jsonVal = JsonConvert.SerializeObject(id, settings);

        Assert.Equal(val, jsonVal);
    }

    [Fact]
    public void Serialize_StronglyTypedId_Byte()
    {
        var settings = new JsonSerializerSettings();
        settings.AddStronglyTypedId();

        var id = new ByteId(1);
        var val = $"{id.Value}";

        var jsonVal = JsonConvert.SerializeObject(id, settings);

        Assert.Equal(val, jsonVal);
    }

    [Fact]
    public void Deserialize_StronglyTypedId_Guid()
    {
        var settings = new JsonSerializerSettings();
        settings.AddStronglyTypedId();

        var val = Guid.NewGuid();
        var jsonVal = $"\"{val}\"";
        var id = JsonConvert.DeserializeObject<Id>(jsonVal, settings);

        Assert.Equal(val, id.Value);
    }

    [Fact]
    public void Deserialize_StronglyTypedId_Int32()
    {
        var settings = new JsonSerializerSettings();
        settings.AddStronglyTypedId();

        var val = 1;
        var jsonVal = $"{val}";
        var id = JsonConvert.DeserializeObject<Int32Id>(jsonVal, settings);

        Assert.Equal(val, id.Value);
    }

    [Fact]
    public void Deserialize_StronglyTypedId_UInt32()
    {
        var settings = new JsonSerializerSettings();
        settings.AddStronglyTypedId();

        var val = 1U;
        var jsonVal = $"{val}";
        var id = JsonConvert.DeserializeObject<UInt32Id>(jsonVal, settings);

        Assert.Equal(val, id.Value);
    }

    [Fact]
    public void Deserialize_StronglyTypedId_Int64()
    {
        var settings = new JsonSerializerSettings();
        settings.AddStronglyTypedId();

        var val = 1L;
        var jsonVal = $"{val}";
        var id = JsonConvert.DeserializeObject<Int64Id>(jsonVal, settings);

        Assert.Equal(val, id.Value);
    }

    [Fact]
    public void Deserialize_StronglyTypedId_UInt64()
    {
        var settings = new JsonSerializerSettings();
        settings.AddStronglyTypedId();

        var val = 1UL;
        var jsonVal = $"{val}";
        var id = JsonConvert.DeserializeObject<UInt64Id>(jsonVal, settings);

        Assert.Equal(val, id.Value);
    }

    [Fact]
    public void Deserialize_StronglyTypedId_String()
    {
        var settings = new JsonSerializerSettings();
        settings.AddStronglyTypedId();

        var val = "AA";
        var jsonVal = $"\"{val}\"";
        var id = JsonConvert.DeserializeObject<StringId>(jsonVal, settings);

        Assert.Equal(val, id.Value);
    }

    [Fact]
    public void Deserialize_StronglyTypedId_Byte()
    {
        var settings = new JsonSerializerSettings();
        settings.AddStronglyTypedId();

        byte val = 1;
        var jsonVal = $"{val}";

        var id = JsonConvert.DeserializeObject<ByteId>(jsonVal, settings);

        Assert.Equal(val, id.Value);
    }
}