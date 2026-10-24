using FluentAssertions;

namespace Len.StronglyTypedId;

public class SerializationAndDeserializationTests
{
    // JsonSerializerOptions 首次参与序列化后会冻结并缓存元数据，内联 new 等于每次重建这份缓存（CA1869）。
    private static readonly System.Text.Json.JsonSerializerOptions _caseInsensitiveOptions = new() { PropertyNameCaseInsensitive = true };

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
    // char 在 JSON 里是单字符串，与 string 同形；这条同时守住「字典键写入不经 IFormattable」的分支。
    [InlineData('L', typeof(CharId), "\"L\"")]
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
    [InlineData("\"L\"", typeof(CharId), 'L')]
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
    /// C4：<c>JsonShape = JsonShape.Object</c> 时，值位置序列化为 <c>{ "value": …
    /// }</c>，供「已对外承诺对象形态」的既有契约在不改动客户端的前提下迁移；两种序列化器行为一致。
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
    /// 对象形态的属性名匹配要跟着 <c>PropertyNameCaseInsensitive</c> 走：默认只认小写 <c>value</c>，
    /// 开了选项后 <c>{"Value": …}</c> 也必须能读 —— 否则与 JsonSerializer 对普通对象的处理不一致。
    /// </summary>
    [Fact]
    public void ObjectJsonShape_Should_HonorPropertyNameCaseInsensitive_When()
    {
        var guid = Guid.Parse("d8ac85d4-ed76-4974-b055-8ef3508743f3");
        const string pascalCaseJson = """{"Value":"d8ac85d4-ed76-4974-b055-8ef3508743f3"}""";

        System.Text.Json.JsonSerializer.Deserialize<ObjectShapeGuidId>(pascalCaseJson, _caseInsensitiveOptions).Value.Should().Be(guid);

        // 未开启该选项时保持原样：属性名大小写不符 → 等同于缺少 value 属性。
        var deserialize = () => System.Text.Json.JsonSerializer.Deserialize<ObjectShapeGuidId>(pascalCaseJson);

        deserialize.Should().Throw<System.Text.Json.JsonException>();
    }

    /// <summary>
    /// C4：对象形态只作用于「值」的位置——字典键本就是字符串，读写键的路径不受影响。
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

    /// <summary>
    /// C1：char 基元的字典键走 <c>WriteAsPropertyName</c> / <c>ReadAsPropertyName</c>。
    /// </summary>
    [Fact]
    public void CharId_Should_RoundTripAsDictionaryKey_When()
    {
        var dictionary = new Dictionary<CharId, int> { [CharId.Create('k')] = 1 };

        const string expectedJson = """{"k":1}""";

        System.Text.Json.JsonSerializer.Serialize(dictionary).Should().Be(expectedJson);

        var roundTripped = System.Text.Json.JsonSerializer.Deserialize<Dictionary<CharId, int>>(expectedJson)!;

        roundTripped.Should().ContainKey(CharId.Create('k'));
    }

    /// <summary>
    /// 枚举基元的字典键写成成员名、值写成数字。这不是本项目的独有偏差：<c>System.Text.Json</c> 对原生
    /// <c>Dictionary&lt;MyEnum, MyEnum&gt;</c> 也是同样的「键用名字、值用数字」（JSON 对象的键只能是字符串）。
    /// 用例把这个形态钉住，避免日后被当成 bug「修」成另一种表示而破坏线上的 JSON。
    /// </summary>
    [Fact]
    public void EnumId_Should_WriteMemberNameAsDictionaryKey_AndNumberAsValue_When()
    {
        var dictionary = new Dictionary<SampleStatusId, SampleStatusId>
        {
            [SampleStatusId.Create(SampleStatus.Active)] = SampleStatusId.Create(SampleStatus.Suspended),
        };

        const string expectedJson = """{"Active":1}""";

        System.Text.Json.JsonSerializer.Serialize(dictionary).Should().Be(expectedJson);

        var roundTripped = System.Text.Json.JsonSerializer
            .Deserialize<Dictionary<SampleStatusId, SampleStatusId>>(expectedJson)!;

        roundTripped.Should().ContainKey(SampleStatusId.Create(SampleStatus.Active));
    }

    /// <summary>
    /// 枚举 Id 作字典键时应用 <c>DictionaryKeyPolicy</c>，与 <c>System.Text.Json</c> 对原生枚举键的行为一致
    /// （原生 <c>Dictionary&lt;Status,int&gt;</c> 在 CamelCase 下写出 <c>{"active":1}</c>）。
    /// </summary>
    [Fact]
    public void EnumId_Should_ApplyDictionaryKeyPolicy_When()
    {
        var options = new System.Text.Json.JsonSerializerOptions
        {
            DictionaryKeyPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        };

        var dictionary = new Dictionary<SamplePriorityId, int>
        {
            [SamplePriorityId.Create(SamplePriority.VeryHigh)] = 1,
            [SamplePriorityId.Create(SamplePriority.LowPriority)] = 2,
        };

        const string expectedJson = """{"veryHigh":1,"lowPriority":2}""";

        System.Text.Json.JsonSerializer.Serialize(dictionary, options).Should().Be(expectedJson);

        var roundTripped = System.Text.Json.JsonSerializer
            .Deserialize<Dictionary<SamplePriorityId, int>>(expectedJson, options)!;

        roundTripped.Should().ContainKey(SamplePriorityId.Create(SamplePriority.VeryHigh));
        roundTripped.Should().ContainKey(SamplePriorityId.Create(SamplePriority.LowPriority));
    }

    /// <summary>
    /// 键文本被 policy 改写后仍能读回：解析必须忽略大小写。这也是 <c>System.Text.Json</c> 对原生枚举键的
    /// 行为（<c>{"ACTIVE":1}</c> 同样能读回 <c>Active</c>）。
    /// </summary>
    [Fact]
    public void EnumId_Should_ReadDictionaryKeyIgnoringCase_When()
    {
        const string json = """{"VERYHIGH":1,"lowpriority":2}""";

        var roundTripped = System.Text.Json.JsonSerializer
            .Deserialize<Dictionary<SamplePriorityId, int>>(json)!;

        roundTripped.Should().ContainKey(SamplePriorityId.Create(SamplePriority.VeryHigh));
        roundTripped.Should().ContainKey(SamplePriorityId.Create(SamplePriority.LowPriority));
    }

    /// <summary>
    /// <c>[Flags]</c> 枚举作字典键：位组合必须能往返（键文本是 <c>"Read, Write"</c>），
    /// 而含未定义位的键必须被拒 —— 字典键走的是忽略大小写的独立解析路径，位校验在那里同样要生效。
    /// </summary>
    [Fact]
    public void FlagsEnumId_Should_RoundTripCombinationAsDictionaryKey_AndRejectUndefinedBits()
    {
        var dictionary = new Dictionary<SampleAccessId, int>
        {
            [SampleAccessId.Create(SampleAccess.Read | SampleAccess.Write)] = 1,
        };

        const string expectedJson = """{"Read, Write":1}""";

        System.Text.Json.JsonSerializer.Serialize(dictionary).Should().Be(expectedJson);

        var roundTripped = System.Text.Json.JsonSerializer
            .Deserialize<Dictionary<SampleAccessId, int>>(expectedJson)!;

        roundTripped.Should().ContainKey(SampleAccessId.Create(SampleAccess.Read | SampleAccess.Write));

        Action act = () => System.Text.Json.JsonSerializer.Deserialize<Dictionary<SampleAccessId, int>>("""{"999":1}""");

        act.Should().Throw<System.Text.Json.JsonException>();
    }

    /// <summary>
    /// 缺陷回归：枚举 Id 的 JSON <b>值</b>路径曾绕过枚举已定义性校验。两个转换器的读入都走
    /// <c>TryCreate</c>，而它只跑用户配的 Validator —— <c>999</c> 会原样造出一个不对应任何成员的 Id，
    /// 后续 switch 落空、序列化写出野值。TryParse 与 Dapper 三条路径早已收紧（见
    /// <see cref="EnumPrimitiveTests"/> 与集成矩阵），这里把 JSON 入口对齐。
    /// </summary>
    /// <remarks>
    /// 序列化形钉住数字：枚举在两种序列化器下都按数值写出，这是与 STJ 对原生枚举一致的既有行为。
    /// </remarks>
    [Fact]
    public void EnumId_Should_RejectUndefinedValue_OnJsonValuePath()
    {
        System.Text.Json.JsonSerializer.Serialize(SampleStatusId.Create(SampleStatus.Suspended)).Should().Be("1");
        Newtonsoft.Json.JsonConvert.SerializeObject(SampleStatusId.Create(SampleStatus.Suspended)).Should().Be("1");

        // 已定义取值照常往返。
        System.Text.Json.JsonSerializer.Deserialize<SampleStatusId>("1").Should().Be(SampleStatusId.Create(SampleStatus.Suspended));
        Newtonsoft.Json.JsonConvert.DeserializeObject<SampleStatusId>("1").Should().Be(SampleStatusId.Create(SampleStatus.Suspended));

        // 未定义数值必须抛各自的框架异常（转换器契约：STJ 抛 JsonException，Newtonsoft 抛 JsonSerializationException），
        // 而不是静默产出一个 (SampleStatus)999。
        Action stj = () => System.Text.Json.JsonSerializer.Deserialize<SampleStatusId>("999");
        Action newtonsoft = () => Newtonsoft.Json.JsonConvert.DeserializeObject<SampleStatusId>("999");

        stj.Should().Throw<System.Text.Json.JsonException>();
        newtonsoft.Should().Throw<Newtonsoft.Json.JsonSerializationException>();
    }

    /// <summary>
    /// <c>[Flags]</c> 枚举的值路径同理：位组合（<c>3</c> = Read | Write）放行，含未定义位的 <c>999</c> 拒绝。
    /// </summary>
    [Fact]
    public void FlagsEnumId_Should_RejectUndefinedBits_OnJsonValuePath()
    {
        System.Text.Json.JsonSerializer.Deserialize<SampleAccessId>("3")
            .Should().Be(SampleAccessId.Create(SampleAccess.Read | SampleAccess.Write));
        Newtonsoft.Json.JsonConvert.DeserializeObject<SampleAccessId>("3")
            .Should().Be(SampleAccessId.Create(SampleAccess.Read | SampleAccess.Write));

        Action stj = () => System.Text.Json.JsonSerializer.Deserialize<SampleAccessId>("999");
        Action newtonsoft = () => Newtonsoft.Json.JsonConvert.DeserializeObject<SampleAccessId>("999");

        stj.Should().Throw<System.Text.Json.JsonException>();
        newtonsoft.Should().Throw<Newtonsoft.Json.JsonSerializationException>();
    }

    /// <summary>
    /// 带前缀的枚举 Id 不应用 <c>DictionaryKeyPolicy</c>：键文本含用户定义的前缀，policy 会把前缀一起改写，
    /// 读回时前缀匹配必然失败。用 SnakeCaseLower 才能看出差异（CamelCase 对 <c>pri_VeryHigh</c> 无变化）。
    /// </summary>
    [Fact]
    public void PrefixedEnumId_Should_NotApplyDictionaryKeyPolicy_When()
    {
        var options = new System.Text.Json.JsonSerializerOptions
        {
            DictionaryKeyPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower,
        };

        var dictionary = new Dictionary<PrefixedPriorityId, int>
        {
            [PrefixedPriorityId.Create(SamplePriority.VeryHigh)] = 1,
        };

        const string expectedJson = """{"pri_VeryHigh":1}""";

        System.Text.Json.JsonSerializer.Serialize(dictionary, options).Should().Be(expectedJson);

        var roundTripped = System.Text.Json.JsonSerializer
            .Deserialize<Dictionary<PrefixedPriorityId, int>>(expectedJson, options)!;

        roundTripped.Should().ContainKey(PrefixedPriorityId.Create(SamplePriority.VeryHigh));
    }

    /// <summary>
    /// string 基元的键值是数据本身而非名字，不应用 <c>DictionaryKeyPolicy</c>：改写它等于改写键值，
    /// 写出去就读不回来（<c>StringId.Create("UserName")</c> 会变成 <c>StringId.Create("userName")</c>）。
    /// </summary>
    [Fact]
    public void StringId_Should_NotApplyDictionaryKeyPolicy_When()
    {
        var options = new System.Text.Json.JsonSerializerOptions
        {
            DictionaryKeyPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        };

        var dictionary = new Dictionary<StringId, int> { [StringId.Create("UserName")] = 1 };

        const string expectedJson = """{"UserName":1}""";

        System.Text.Json.JsonSerializer.Serialize(dictionary, options).Should().Be(expectedJson);

        var roundTripped = System.Text.Json.JsonSerializer
            .Deserialize<Dictionary<StringId, int>>(expectedJson, options)!;

        roundTripped.Should().ContainKey(StringId.Create("UserName"));
    }

    /// <summary>
    /// D3：string 基元的字典键被验证器拒绝时，必须抛 <c>JsonException</c> 而不是 <c>ArgumentException</c> ——
    /// STJ 的转换器契约要求失败一律抛 <c>JsonException</c>，同一生成器的带前缀 / 非 string 分支都是这么做的。
    /// </summary>
    [Fact]
    public void StringId_Should_ThrowJsonException_When_DictionaryKeyRejected()
    {
        // ShortStringId 的验证器限制长度 ≤ 8，"far-too-long" 会被拒。
        const string json = """{"far-too-long":1}""";

        var act = () => System.Text.Json.JsonSerializer.Deserialize<Dictionary<ShortStringId, int>>(json);

        act.Should().Throw<System.Text.Json.JsonException>();
    }

    #region C10 文本前缀：JSON 走规范文本形

    /// <summary>
    /// C10：有前缀时，值位置的 JSON 变为字符串（数字 / 枚举 / 日期基元由 number 变 string），内容为规范文本形（含前缀）；两种序列化器行为一致。
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
    /// C10：前缀 + 对象形态—— <c>{ "value": "ord_x" }</c>。
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

    #region 验证失败的异常契约

    /// <summary>
    /// 对象形态的读入分支与基元形态是两段生成代码：验证器拒绝时同样必须抛框架异常，
    /// 不能让 <c>Create</c> 的 <see cref="ArgumentException"/> 穿透转换器。
    /// </summary>
    [Fact]
    public void ObjectShape_Should_ThrowFrameworkExceptions_WhenValidatorRejects()
    {
        var json = $$"""{"value":"{{Guid.Empty}}"}""";

        var systemTextJson = () => System.Text.Json.JsonSerializer.Deserialize<ObjectShapeValidatedGuidId>(json);
        var newtonsoft = () => Newtonsoft.Json.JsonConvert.DeserializeObject<ObjectShapeValidatedGuidId>(json);

        systemTextJson.Should().Throw<System.Text.Json.JsonException>();
        newtonsoft.Should().Throw<Newtonsoft.Json.JsonSerializationException>();
    }

    /// <summary>
    /// <c>BigInteger</c> 的读入路径先按文本解析再交给 <c>TryCreate</c>：文本不是合法数字时
    /// <c>BigInteger.Parse</c> 的 <see cref="FormatException"/> 同样会穿透转换器，故必须一并收敛到 <c>JsonException</c>。
    /// </summary>
    [Fact]
    public void BigInteger_Should_ThrowJsonException_WhenTextIsNotANumber()
    {
        var act = () => System.Text.Json.JsonSerializer.Deserialize<BigIntegerId>("\"not-a-number\"");

        act.Should().Throw<System.Text.Json.JsonException>();
    }

    #endregion
}