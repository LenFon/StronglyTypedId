using FluentAssertions;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Len.StronglyTypedId;

public class SerializationAndDeserializationTests
{
    [Theory]
    [InlineData(12, typeof(Int32Id), "12")]
    [InlineData(13L, typeof(Int64Id), "13")]
    [InlineData(14U, typeof(UInt32Id), "14")]
    [InlineData(15UL, typeof(UInt64Id), "15")]
    [InlineData((byte)16, typeof(ByteId), "16")]
    [InlineData(12, typeof(Int32IdV2), "12")]
    [InlineData(13L, typeof(Int64IdV2), "13")]
    [InlineData(14U, typeof(UInt32IdV2), "14")]
    [InlineData(15UL, typeof(UInt64IdV2), "15")]
    [InlineData((byte)16, typeof(ByteIdV2), "16")]
    [InlineData("Len", typeof(StringId), "\"Len\"")]
    [InlineData("Len", typeof(StringIdV2), "\"Len\"")]
    [InlineData("Len", typeof(StringIdV3), "\"Len\"")]
    [InlineData("D8AC85D4-ED76-4974-B055-8EF3508743F3", typeof(GuidId), "\"d8ac85d4-ed76-4974-b055-8ef3508743f3\"")]
    [InlineData("D8AC85D4-ED76-4974-B055-8EF3508743F3", typeof(GuidIdV2), "\"d8ac85d4-ed76-4974-b055-8ef3508743f3\"")]
    [InlineData("d8ac85d4-ed76-4974-b055-8ef3508743f3", typeof(GuidId), "\"d8ac85d4-ed76-4974-b055-8ef3508743f3\"")]
    [InlineData("d8ac85d4-ed76-4974-b055-8ef3508743f3", typeof(GuidIdV2), "\"d8ac85d4-ed76-4974-b055-8ef3508743f3\"")]
    public void Serialize_Should_ReturnJson_When(object data, Type type, string expectedJson)
    {
        var id = Activator.CreateInstance(type, type == typeof(GuidId) || type == typeof(GuidIdV2) ? Guid.Parse(data.ToString()!) : data);
        System.Text.Json.JsonSerializer.Serialize(id).Should().Be(expectedJson);
        Newtonsoft.Json.JsonConvert.SerializeObject(id).Should().Be(expectedJson);
    }

    [Theory]
    [InlineData("12", typeof(Int32Id), 12)]
    [InlineData("13", typeof(Int64Id), 13L)]
    [InlineData("14", typeof(UInt32Id), 14U)]
    [InlineData("15", typeof(UInt64Id), 15UL)]
    [InlineData("16", typeof(ByteId), (byte)16)]
    [InlineData("12", typeof(Int32IdV2), 12)]
    [InlineData("13", typeof(Int64IdV2), 13L)]
    [InlineData("14", typeof(UInt32IdV2), 14U)]
    [InlineData("15", typeof(UInt64IdV2), 15UL)]
    [InlineData("16", typeof(ByteIdV2), (byte)16)]
    [InlineData("\"Len\"", typeof(StringId), "Len")]
    [InlineData("\"Len\"", typeof(StringIdV2), "Len")]
    [InlineData("\"Len\"", typeof(StringIdV3), "Len")]
    [InlineData("\"d8ac85d4-ed76-4974-b055-8ef3508743f3\"", typeof(GuidId), "D8AC85D4-ED76-4974-B055-8EF3508743F3")]
    [InlineData("\"d8ac85d4-ed76-4974-b055-8ef3508743f3\"", typeof(GuidIdV2), "D8AC85D4-ED76-4974-B055-8EF3508743F3")]
    public void Deserialize_Should_ReturnObject_When(string json, Type type, object expectedValue)
    {
        var value = type == typeof(GuidId) || type == typeof(GuidIdV2) ? Guid.Parse(expectedValue.ToString()!) : expectedValue;

        var id = System.Text.Json.JsonSerializer.Deserialize(json, type);

        id.Should().NotBeNull();
        Assert.True(((dynamic)id!).Value.Equals(value));

        var id2 = Newtonsoft.Json.JsonConvert.DeserializeObject(json, type);

        id2.Should().NotBeNull();
        Assert.True(((dynamic)id2!).Value.Equals(value));
    }
}