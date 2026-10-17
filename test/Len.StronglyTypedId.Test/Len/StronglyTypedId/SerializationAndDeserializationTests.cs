using FluentAssertions;

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
    [InlineData((sbyte)12, typeof(SByteId), "12")]
    [InlineData((short)1234, typeof(Int16Id), "1234")]
    [InlineData((ushort)1234, typeof(UInt16Id), "1234")]
    [InlineData((sbyte)12, typeof(SByteIdV2), "12")]
    [InlineData((short)1234, typeof(Int16IdV2), "1234")]
    [InlineData((ushort)1234, typeof(UInt16IdV2), "1234")]
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
    [InlineData("12", typeof(SByteId), (sbyte)12)]
    [InlineData("1234", typeof(Int16Id), (short)1234)]
    [InlineData("1234", typeof(UInt16Id), (ushort)1234)]
    [InlineData("12", typeof(SByteIdV2), (sbyte)12)]
    [InlineData("1234", typeof(Int16IdV2), (short)1234)]
    [InlineData("1234", typeof(UInt16IdV2), (ushort)1234)]
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

    /// <summary>
    /// C4：<c>JsonShape = JsonShape.Object</c> 时，值位置序列化为 <c>{ "value": … }</c>，
    /// 供「已对外承诺对象形态」的既有契约在不改动客户端的前提下迁移；两种序列化器行为一致。
    /// </summary>
    [Fact]
    public void ObjectJsonShape_Should_SerializeAsObject_When()
    {
        var guid = Guid.Parse("d8ac85d4-ed76-4974-b055-8ef3508743f3");
        const string guidJson = """{"value":"d8ac85d4-ed76-4974-b055-8ef3508743f3"}""";

        System.Text.Json.JsonSerializer.Serialize(ObjectShapeGuidId.Create(guid)).Should().Be(guidJson);
        Newtonsoft.Json.JsonConvert.SerializeObject(ObjectShapeGuidId.Create(guid)).Should().Be(guidJson);

        const string stringJson = """{"value":"Len"}""";

        System.Text.Json.JsonSerializer.Serialize(ObjectShapeStringId.Create("Len")).Should().Be(stringJson);
        Newtonsoft.Json.JsonConvert.SerializeObject(ObjectShapeStringId.Create("Len")).Should().Be(stringJson);
    }

    /// <summary>
    /// C4：对象形态的反序列化往返。
    /// </summary>
    [Fact]
    public void ObjectJsonShape_Should_DeserializeFromObject_When()
    {
        var guid = Guid.Parse("d8ac85d4-ed76-4974-b055-8ef3508743f3");
        const string guidJson = """{"value":"d8ac85d4-ed76-4974-b055-8ef3508743f3"}""";

        System.Text.Json.JsonSerializer.Deserialize<ObjectShapeGuidId>(guidJson).Value.Should().Be(guid);
        Newtonsoft.Json.JsonConvert.DeserializeObject<ObjectShapeGuidId>(guidJson).Value.Should().Be(guid);

        const string stringJson = """{"value":"Len"}""";

        System.Text.Json.JsonSerializer.Deserialize<ObjectShapeStringId>(stringJson).Value.Should().Be("Len");
        Newtonsoft.Json.JsonConvert.DeserializeObject<ObjectShapeStringId>(stringJson).Value.Should().Be("Len");
    }

    /// <summary>
    /// C4：对象形态只作用于「值」的位置 —— 字典键本就是字符串，读写键的路径不受影响。
    /// </summary>
    [Fact]
    public void ObjectJsonShape_Should_KeepPrimitiveDictionaryKey_When()
    {
        var guid = Guid.Parse("d8ac85d4-ed76-4974-b055-8ef3508743f3");
        var dictionary = new Dictionary<ObjectShapeGuidId, int> { [ObjectShapeGuidId.Create(guid)] = 1 };

        const string expectedJson = """{"d8ac85d4-ed76-4974-b055-8ef3508743f3":1}""";

        System.Text.Json.JsonSerializer.Serialize(dictionary).Should().Be(expectedJson);

        var roundTripped = System.Text.Json.JsonSerializer.Deserialize<Dictionary<ObjectShapeGuidId, int>>(expectedJson)!;

        roundTripped.Should().ContainKey(ObjectShapeGuidId.Create(guid));
    }

    #region C10 文本前缀：JSON 走规范文本形

    /// <summary>
    /// C10：有前缀时，值位置的 JSON 变为字符串（数字 / 枚举 / 日期基元由 number 变 string），
    /// 内容为规范文本形（含前缀）；两种序列化器行为一致。
    /// </summary>
    [Fact]
    public void Prefix_Should_SerializeAsPrefixedString_When()
    {
        var guid = Guid.Parse("d8ac85d4-ed76-4974-b055-8ef3508743f3");

        const string guidJson = "\"ord_d8ac85d4-ed76-4974-b055-8ef3508743f3\"";

        System.Text.Json.JsonSerializer.Serialize(PrefixedGuidId.Create(guid)).Should().Be(guidJson);
        Newtonsoft.Json.JsonConvert.SerializeObject(PrefixedGuidId.Create(guid)).Should().Be(guidJson);

        const string stringJson = "\"ord_x\"";

        System.Text.Json.JsonSerializer.Serialize(PrefixedStringId.Create("x")).Should().Be(stringJson);
        Newtonsoft.Json.JsonConvert.SerializeObject(PrefixedStringId.Create("x")).Should().Be(stringJson);
    }

    /// <summary>
    /// C10：有前缀时的反序列化往返（宽松读入，前缀可选）。
    /// </summary>
    [Fact]
    public void Prefix_Should_DeserializeFromPrefixedString_When()
    {
        var guid = Guid.Parse("d8ac85d4-ed76-4974-b055-8ef3508743f3");
        const string guidJson = "\"ord_d8ac85d4-ed76-4974-b055-8ef3508743f3\"";

        System.Text.Json.JsonSerializer.Deserialize<PrefixedGuidId>(guidJson).Value.Should().Be(guid);
        Newtonsoft.Json.JsonConvert.DeserializeObject<PrefixedGuidId>(guidJson).Value.Should().Be(guid);

        const string stringJson = "\"ord_x\"";

        System.Text.Json.JsonSerializer.Deserialize<PrefixedStringId>(stringJson).Value.Should().Be("x");
        Newtonsoft.Json.JsonConvert.DeserializeObject<PrefixedStringId>(stringJson).Value.Should().Be("x");
    }

    /// <summary>
    /// C10：前缀 + 对象形态 —— <c>{ "value": "ord_x" }</c>。
    /// </summary>
    [Fact]
    public void Prefix_WithObjectShape_Should_SerializeAsObject_When()
    {
        var guid = Guid.Parse("d8ac85d4-ed76-4974-b055-8ef3508743f3");
        const string guidJson = """{"value":"ord_d8ac85d4-ed76-4974-b055-8ef3508743f3"}""";

        System.Text.Json.JsonSerializer.Serialize(PrefixedObjectGuidId.Create(guid)).Should().Be(guidJson);
        Newtonsoft.Json.JsonConvert.SerializeObject(PrefixedObjectGuidId.Create(guid)).Should().Be(guidJson);

        System.Text.Json.JsonSerializer.Deserialize<PrefixedObjectGuidId>(guidJson).Value.Should().Be(guid);
        Newtonsoft.Json.JsonConvert.DeserializeObject<PrefixedObjectGuidId>(guidJson).Value.Should().Be(guid);
    }

    /// <summary>
    /// C10：字典键走规范文本形（含前缀），读写一致。
    /// </summary>
    [Fact]
    public void Prefix_Should_UsePrefixedDictionaryKey_When()
    {
        var guid = Guid.Parse("d8ac85d4-ed76-4974-b055-8ef3508743f3");
        var dictionary = new Dictionary<PrefixedGuidId, int> { [PrefixedGuidId.Create(guid)] = 1 };

        const string expectedJson = """{"ord_d8ac85d4-ed76-4974-b055-8ef3508743f3":1}""";

        System.Text.Json.JsonSerializer.Serialize(dictionary).Should().Be(expectedJson);

        var roundTripped = System.Text.Json.JsonSerializer.Deserialize<Dictionary<PrefixedGuidId, int>>(expectedJson)!;

        roundTripped.Should().ContainKey(PrefixedGuidId.Create(guid));
    }

    #endregion
}