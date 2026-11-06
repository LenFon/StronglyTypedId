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
    /// C4：<c>JsonShape = JsonShape.Object</c> 时，值位置序列化为
    /// <c>{ "value": … }</c>，供「已对外承诺对象形态」的既有契约在不改动客户端的前提下迁移；两种序列化器行为一致。
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
    /// 对象形态的反序列化往返。
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
    /// 对象形态 + 引用类型基元为 <see langword="null"/> 时必须写读对称：写出的
    /// <c>{"value":null}</c> 要能读回「基元为 null 的 Id」，而不是抛异常。
    /// </summary>
    /// <remarks>
    /// 值类型基元没有 null 可写，故只覆盖 <c>string</c> 基元（引用类型）。
    /// </remarks>
    [Fact]
    public void ObjectJsonShape_Should_RoundTripNullPrimitive_When()
    {
        var id = default(ObjectShapeStringId);

        var stjJson = System.Text.Json.JsonSerializer.Serialize(id);
        stjJson.Should().Be("""{"value":null}""");
        System.Text.Json.JsonSerializer.Deserialize<ObjectShapeStringId>(stjJson).Should().Be(id);

        var newtonsoftJson = Newtonsoft.Json.JsonConvert.SerializeObject(id);
        newtonsoftJson.Should().Be("""{"value":null}""");
        Newtonsoft.Json.JsonConvert.DeserializeObject<ObjectShapeStringId>(newtonsoftJson).Should().Be(id);
    }

    /// <summary>
    /// 对象形态下 <c>value</c> 为 <c>null</c> 且对象里还有后续属性时，读入必须成功。
    /// </summary>
    /// <remarks>
    /// 修复前 STJ 转换器读到 null 令牌就 <c>return</c>，没有把对象读到 <c>EndObject</c>，
    /// 于是 <c>{"value":null,"extra":1}</c> 抛「read too much or not enough」。转换器必须消费整个对象，
    /// 否则客户端多带一个字段就会让整条载荷读不进来。
    /// </remarks>
    [Fact]
    public void ObjectJsonShape_Should_ReadNullPrimitive_WhenMorePropertiesFollow()
    {
        var id = default(ObjectShapeStringId);

        System.Text.Json.JsonSerializer
            .Deserialize<ObjectShapeStringId>("""{"value":null,"extra":1}""")
            .Should().Be(id);

        System.Text.Json.JsonSerializer
            .Deserialize<ObjectShapeStringId>("""{"extra":1,"value":null}""")
            .Should().Be(id);

        // 同一对象里既有 null 又有真实值时（重复键），保留真实值而不是丢掉它。
        System.Text.Json.JsonSerializer
            .Deserialize<ObjectShapeStringId>("""{"value":null,"value":"Len"}""")
            .Should().Be(ObjectShapeStringId.Create("Len"));

        // 非 null 值后面跟属性是修复前就正确的路径，一并钉住以免回归。
        System.Text.Json.JsonSerializer
            .Deserialize<ObjectShapeStringId>("""{"value":"Len","extra":1}""")
            .Should().Be(ObjectShapeStringId.Create("Len"));
    }

    /// <summary>
    /// 对象形态读到 JSON <c>null</c> 时，值类型 Id 必须抛框架自己的序列化异常，而不是把 <c>null</c>
    /// 交给调用方（拆箱时变成 <see cref="NullReferenceException"/>）。
    /// </summary>
    /// <remarks>
    /// 与基元形态的 null 分支同一口径：值类型 Id 无法表示 null，静默落成 <c>default</c> 会把「数据缺失」
    /// 伪装成合法值。引用类型 Id 仍读回 <c>null</c>。
    /// </remarks>
    [Fact]
    public void ObjectJsonShape_Should_RejectNullToken_ForValueTypeId()
    {
        var stjDeserialize = () => System.Text.Json.JsonSerializer.Deserialize<ObjectShapeGuidId>("null");

        stjDeserialize.Should().Throw<System.Text.Json.JsonException>();

        var newtonsoftDeserialize = () => Newtonsoft.Json.JsonConvert.DeserializeObject<ObjectShapeGuidId>("null");

        newtonsoftDeserialize.Should().Throw<Newtonsoft.Json.JsonSerializationException>();
    }

    /// <summary>
    /// 对象形态的属性名匹配要跟着 <c>PropertyNameCaseInsensitive</c> 走：默认只认小写 <c>value</c>，
    /// 开了选项后 <c>{"Value": …}</c> 也必须能读，否则与 JsonSerializer 对普通对象的处理不一致。
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
    /// C4：对象形态只作用于「值」的位置，字典键本就是字符串，读写键的路径不受影响。
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
    /// 时间基元作字典键时不得塌缩：规范文本形必须保留亚秒，否则两个不同的 Id 会写出同一个键。
    /// </summary>
    /// <remarks>
    /// 默认格式串（<c>DateTime</c> 的 "G"、<c>TimeOnly</c> 的 "t"）会丢掉亚秒甚至秒，
    /// 于是 <c>{"01/02/2020 03:04:05":1,"01/02/2020 03:04:05":2}</c> 这样的 JSON 读回来只剩一个键。
    /// 生成器固定用 <c>"O"</c> 之后，两个键必须保持可区分。
    /// </remarks>
    [Fact]
    public void TemporalIds_Should_KeepDistinctDictionaryKeys()
    {
        var first = DateTimeId.Create(new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc).AddTicks(1000000));
        var second = DateTimeId.Create(new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc).AddTicks(2000000));

        first.ToString().Should().NotBe(second.ToString(), "规范文本形必须保留亚秒，否则字典键会塌缩");

        var dictionary = new Dictionary<DateTimeId, int> { [first] = 1, [second] = 2 };
        var json = System.Text.Json.JsonSerializer.Serialize(dictionary);

        var roundTripped = System.Text.Json.JsonSerializer.Deserialize<Dictionary<DateTimeId, int>>(json)!;

        roundTripped.Should().HaveCount(2, "两个不同的 Id 必须读回两个键（{0}）", json);
        roundTripped.Should().ContainKey(first).And.ContainKey(second);

        var timeFirst = TimeOnlyId.Create(new TimeOnly(13, 45, 30, 100));
        var timeSecond = TimeOnlyId.Create(new TimeOnly(13, 45, 31, 900));

        timeFirst.ToString().Should().NotBe(timeSecond.ToString(), "TimeOnly 的规范文本形必须保留秒与亚秒");

        var timeJson = System.Text.Json.JsonSerializer.Serialize(
            new Dictionary<TimeOnlyId, int> { [timeFirst] = 1, [timeSecond] = 2 });

        System.Text.Json.JsonSerializer.Deserialize<Dictionary<TimeOnlyId, int>>(timeJson)!
            .Should().HaveCount(2, "两个不同的 TimeOnly Id 必须读回两个键（{0}）", timeJson);
    }

    /// <summary>
    /// <c>DateTime</c> 的文本解析必须保留 <see cref="DateTimeKind"/>：带 <c>"Z"</c> 的文本按 UTC 解释，
    /// 不能按本地时区平移。
    /// </summary>
    /// <remarks>
    /// 默认的 <c>DateTime.TryParse</c> 样式会把 <c>"…Z"</c> 折成本地时间（Kind 变 Local、ticks 整体平移），
    /// 于是 <c>Parse(jsonText)</c> 与 JSON 转换器读出的值不一致。生成代码固定带
    /// <c>DateTimeStyles.RoundtripKind</c>。
    /// </remarks>
    [Fact]
    public void DateTimeId_Should_PreserveKind_WhenParsingRoundTripText()
    {
        var utc = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc).AddTicks(1234567);
        var id = DateTimeId.Create(utc);

        var parsed = DateTimeId.Parse("2020-01-02T03:04:05.1234567Z", System.Globalization.CultureInfo.InvariantCulture);

        parsed.Value.Kind.Should().Be(DateTimeKind.Utc, "带 Z 的文本必须按 UTC 解释");
        parsed.Value.Ticks.Should().Be(utc.Ticks, "Kind 与 ticks 都不能被本地时区平移");
        parsed.Should().Be(id);
    }

    /// <summary>
    /// <c>DateOnly</c> 的规范文本形必须是 ISO 形：与 System.Text.Json 写出的一致，
    /// 且 Newtonsoft 写出的文本 STJ 也能读回。
    /// </summary>
    /// <remarks>
    /// 默认短日期形（不变文化下是 <c>01/02/2020</c>）与 STJ 的 <c>2020-01-02</c> 不同，
    /// 会让同一个 Id 的值与字典键、两个 JSON 库之间写出两段不同的文本。
    /// </remarks>
    [Fact]
    public void DateOnlyId_Should_UseIsoTextForm_AcrossJsonLibraries()
    {
        var id = DateOnlyId.Create(new DateOnly(2020, 1, 2));

        id.ToString().Should().Be("2020-01-02");
        System.Text.Json.JsonSerializer.Serialize(id).Should().Be("\"2020-01-02\"");
        Newtonsoft.Json.JsonConvert.SerializeObject(id).Should().Be("\"2020-01-02\"");

        // 字典键与值同形，且与原生 DateOnly 的键文本一致。
        System.Text.Json.JsonSerializer.Serialize(new Dictionary<DateOnlyId, int> { [id] = 1 })
            .Should().Be("""{"2020-01-02":1}""");

        // Newtonsoft 写出的文本 STJ 必须读得回来。
        var newtonsoftText = Newtonsoft.Json.JsonConvert.SerializeObject(id);
        System.Text.Json.JsonSerializer.Deserialize<DateOnlyId>(newtonsoftText).Should().Be(id);
    }

    /// <summary>
    /// <c>decimal</c> 基元在 Newtonsoft 下必须按规范文本写出，否则超过约 15 位有效数字的值会被静默舍入，
    /// <c>decimal.MaxValue</c> / <c>MinValue</c> 直接抛 <c>JsonSerializationException</c>。
    /// </summary>
    /// <remarks>
    /// Newtonsoft 在调用转换器之前就把数字 token 解析成了 <c>double</c>（<c>ReadForType</c> 见到转换器时
    /// 只做 <c>reader.Read()</c>），转换器拿不到原始文本，任何读侧写法都救不回来。故与
    /// <c>Int128</c> / <c>UInt128</c> / <c>Half</c> 同走文本形 —— 代价是 Newtonsoft 写出的是 JSON 字符串，
    /// 而 System.Text.Json 仍写数字，这与那几个基元既有的跨库差异一致。
    /// </remarks>
    [Fact]
    public void DecimalId_Should_RoundTripPrecision_ThroughNewtonsoft()
    {
        decimal[] values = [decimal.MaxValue, decimal.MinValue, 123456789012345.67m, 123456789012345678.9m, 1.5m, 1.0m];

        foreach (var value in values)
        {
            var id = DecimalId.Create(value);
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(id);

            // 文本形：写出的是 JSON 字符串，且与规范文本一致。
            json.Should().Be($"\"{value.ToString(null, System.Globalization.CultureInfo.InvariantCulture)}\"", $"{value} 必须按规范文本写出");

            Newtonsoft.Json.JsonConvert.DeserializeObject<DecimalId>(json).Should().Be(id, $"{value} 必须无损读回");
        }
    }

    /// <summary>
    /// 升级前 Newtonsoft 把 <c>decimal</c> 写成 JSON number，那些既有载荷必须仍能读回 ——
    /// 否则改一次线上格式就让历史数据全部失效。
    /// </summary>
    /// <remarks>
    /// 数字 token 的精度仍受 Newtonsoft 的 <c>double</c> 解析限制（与修复前一致），故这里只断言
    /// 「能读出来且值正确」的普通取值，不重复覆盖 <c>MaxValue</c> 那类本来就读不回来的边界。
    /// </remarks>
    [Fact]
    public void DecimalId_Should_StillReadLegacyNumericPayload_ThroughNewtonsoft()
    {
        // 旧版 WriteValue(decimal) 的产物：普通值、指数形（Newtonsoft 对 1E-05 这类值会写指数）、整数形。
        // 只用 double 能精确表示的取值 —— 旧路径本来就有精度上限，这里验证的是「不会因为改格式而读不出来」。
        (string Json, decimal Expected)[] legacyPayloads =
        [
            ("1.5", 1.5m),
            ("0.0", 0m),
            ("1.0", 1m),
            ("0.00001", 0.00001m),
            ("1E-05", 0.00001m),
            ("1000000000000000.0", 1000000000000000m),
        ];

        foreach (var (json, expected) in legacyPayloads)
        {
            Newtonsoft.Json.JsonConvert.DeserializeObject<DecimalId>(json).Value
                .Should().Be(expected, $"旧载荷 {json} 必须仍能读回");
        }
    }

    /// <summary>
    /// Newtonsoft 在调用转换器之前就把「像日期」的字符串解析成了本地时区的 <see cref="DateTime"/>，
    /// 故 <c>DateTimeOffset</c> 基元的 Id 读回来带的是机器本地 offset。时刻不变，offset 变了。
    /// </summary>
    /// <remarks>
    /// 转换器内部无解：它运行时原文已经没了。把序列化器设成
    /// <c>DateParseHandling.None</c> 才能保留原始字符串并让 offset 往返 —— 本用例把这两点都钉住，
    /// 避免日后有人「顺手」把 <c>DateTimeOffset</c> 加进文本路径（那救不回来，只会改掉线上形态）。
    /// </remarks>
    [Fact]
    public void DateTimeOffsetId_Should_LoseOffset_UnderNewtonsoft_UnlessDateParsingDisabled()
    {
        var value = new DateTimeOffset(2020, 1, 2, 3, 4, 5, TimeSpan.FromHours(-5));
        var id = DateTimeOffsetId.Create(value);

        // 默认设置：时刻保留，offset 变成机器本地。
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(id);
        var back = Newtonsoft.Json.JsonConvert.DeserializeObject<DateTimeOffsetId>(json);

        back.Value.UtcTicks.Should().Be(value.UtcTicks, "时刻本身必须保留");
        back.Value.Offset.Should().Be(TimeZoneInfo.Local.GetUtcOffset(value.UtcDateTime), "offset 会被归一成本地时区");

        // 关掉日期解析：原文保留，offset 往返。
        var settings = new Newtonsoft.Json.JsonSerializerSettings
        {
            DateParseHandling = Newtonsoft.Json.DateParseHandling.None,
        };

        var roundTripped = Newtonsoft.Json.JsonConvert.DeserializeObject<DateTimeOffsetId>(
            Newtonsoft.Json.JsonConvert.SerializeObject(id, settings), settings);

        roundTripped.Value.Should().Be(value, "DateParseHandling.None 下 offset 必须往返");

        // System.Text.Json 不受影响。
        var stj = System.Text.Json.JsonSerializer.Deserialize<DateTimeOffsetId>(
            System.Text.Json.JsonSerializer.Serialize(id));

        stj.Value.Should().Be(value);
    }

    /// <summary>
    /// 同一机制还会毁掉 <c>string</c> 基元的 Id：值恰好长得像 ISO 日期时间时，Newtonsoft 把它解析成
    /// <see cref="DateTime"/>，转换器再取字符串就只剩本地渲染。这比 offset 丢失更严重 —— 值本身变了。
    /// </summary>
    /// <remarks>
    /// 手写的 <c>JsonConverter&lt;string&gt;</c> 同样如此，故这是 Newtonsoft 的限制而非本库引入的。
    /// 关掉 <c>DateParseHandling</c> 后原文保留，值可往返。
    /// </remarks>
    [Fact]
    public void StringId_Should_LoseDateLikeValue_UnderNewtonsoft_UnlessDateParsingDisabled()
    {
        const string value = "2020-01-02T03:04:05Z";
        var id = StringId.Create(value);

        // 默认设置：值被替换成本地渲染的日期文本（具体文本随当前区域变化，故只断言「不再是原字符串」）。
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(id);
        var back = Newtonsoft.Json.JsonConvert.DeserializeObject<StringId>(json);

        back.Value.Should().NotBe(value, "Newtonsoft 会先把像日期的字符串解析成 DateTime，转换器只能拿到本地渲染文本");
        back.Value.Should().NotContain("T03:04:05", "原字符串的 ISO 形已经不存在了");

        // 关掉日期解析：原文保留。
        var settings = new Newtonsoft.Json.JsonSerializerSettings
        {
            DateParseHandling = Newtonsoft.Json.DateParseHandling.None,
        };

        var roundTripped = Newtonsoft.Json.JsonConvert.DeserializeObject<StringId>(
            Newtonsoft.Json.JsonConvert.SerializeObject(id, settings), settings);

        roundTripped.Value.Should().Be(value, "DateParseHandling.None 下原字符串必须保留");

        // System.Text.Json 不受影响。
        System.Text.Json.JsonSerializer.Deserialize<StringId>(
            System.Text.Json.JsonSerializer.Serialize(id)).Value.Should().Be(value);
    }

    /// <summary>
    /// 对象形态读到「类型不符」或「文本不合法」的值时，Newtonsoft 转换器同样必须抛
    /// <see cref="Newtonsoft.Json.JsonSerializationException"/>，而不是把 <see cref="FormatException"/> /
    /// <see cref="ArgumentException"/> 漏给调用方。
    /// </summary>
    /// <remarks>
    /// <c>JToken.ToObject&lt;T&gt;()</c> 不传 <c>serializer</c> 时不会包装异常，会直接把基元的
    /// <c>FormatException</c>（文本不合法）或 <c>ArgumentException</c>（token 类型不符）抛出去。
    /// 基元形态走的是 <c>serializer.Deserialize&lt;T?&gt;</c>，本来就包装好了，故只有对象形态受影响。
    /// </remarks>
    [Fact]
    public void ObjectJsonShape_Should_ThrowJsonSerializationException_WhenValueIsMalformed()
    {
        string[] malformedPayloads =
        [
            """{"value":"abc"}""",   // 文本不合法
            """{"value":[]}""",      // token 类型不符
            """{"value":1.5}""",     // 数字不是 Guid
        ];

        foreach (var json in malformedPayloads)
        {
            var newtonsoft = () => Newtonsoft.Json.JsonConvert.DeserializeObject<ObjectShapeGuidId>(json);

            newtonsoft.Should().Throw<Newtonsoft.Json.JsonSerializationException>($"{json} 必须抛 JsonSerializationException");

            // System.Text.Json 侧本来就收敛到 JsonException。
            var systemTextJson = () => System.Text.Json.JsonSerializer.Deserialize<ObjectShapeGuidId>(json);

            systemTextJson.Should().Throw<System.Text.Json.JsonException>();
        }
    }

    /// <summary>
    /// 对象形态的文本路径（<c>string</c> 基元）读到非字符串 token 时同样必须抛 Newtonsoft 自己的
    /// 序列化异常，而不是把 <see cref="ArgumentException"/> 漏给调用方。
    /// </summary>
    /// <remarks>
    /// 这里抛的是 <c>JsonReaderException</c>（<c>JsonException</c> 的子类），与基元形态一致：
    /// 调用方按文档 <c>catch (JsonException)</c> 就能接住。
    /// </remarks>
    [Fact]
    public void ObjectJsonShape_Should_ThrowJsonException_WhenStringValueIsNotAString()
    {
        var newtonsoft = () => Newtonsoft.Json.JsonConvert.DeserializeObject<ObjectShapeStringId>("""{"value":[]}""");

        newtonsoft.Should().Throw<Newtonsoft.Json.JsonException>();
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
    /// 而含未定义位的键必须被拒，字典键走的是忽略大小写的独立解析路径，位校验在那里同样要生效。
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
    /// 缺陷回归：枚举 Id 的 JSON 值路径曾绕过枚举已定义性校验。两个转换器的读入都走 <c>TryCreate</c>，
    /// 而它只跑用户配的 Validator，<c>999</c> 会原样造出一个不对应任何成员的 Id。这里把 JSON 入口对齐到
    /// <c>TryParse</c> 与 Dapper 三条路径的收紧规则。
    /// </summary>
    /// <remarks>
    /// 序列化形钉住数字：枚举在两种序列化器下都按数值写出，与 STJ 对原生枚举的既有行为一致。
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
    /// D3：string 基元的字典键被验证器拒绝时，必须抛 <c>JsonException</c> 而不是 <c>ArgumentException</c>
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

    /// <summary>
    /// 引用类型基元为 <see langword="null"/> 的 Id 不能作 JSON 字典键：JSON 属性名不允许 null。
    /// 必须给出明确的 <see cref="NotSupportedException"/>，而不是在 <c>WritePropertyName</c> 里抛 NRE。
    /// </summary>
    /// <remarks>
    /// 与「null Id 作键」同一处理口径。带前缀时键文本是「前缀 + 空串」，永远非 null，故不受影响。
    /// </remarks>
    [Theory]
    [InlineData("ReferenceCodeId")]
    [InlineData("StringId")]
    [InlineData("PrefixedStringId")]
    public void NullPrimitiveId_Should_ThrowNotSupported_When_UsedAsDictionaryKey(string idType)
    {
        var dictionary = idType switch
        {
            "ReferenceCodeId" => (object)new Dictionary<ReferenceCodeId, int> { [default] = 1 },
            "PrefixedStringId" => new Dictionary<PrefixedStringId, int> { [default] = 1 },
            _ => new Dictionary<StringId, int> { [default] = 1 },
        };

        var act = () => System.Text.Json.JsonSerializer.Serialize(dictionary);

        act.Should().Throw<NotSupportedException>()
            .WithMessage($"A null {idType} cannot be written as a JSON property name.*");
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
    /// 带前缀 + 引用类型基元为 <see langword="null"/> 时必须写读对称：写出 JSON <c>null</c>，
    /// 读回得到「基元为 null 的 Id」。
    /// </summary>
    /// <remarks>
    /// 裸前缀（<c>"ord_"</c>）剥离后是空文本，读回必然失败；写 <c>null</c> 才能往返，
    /// 也与无前缀路径的 null 写出保持一致。
    /// </remarks>
    [Fact]
    public void Prefix_Should_RoundTripNullPrimitive_When()
    {
        var id = default(PrefixedStringId);

        var stjJson = System.Text.Json.JsonSerializer.Serialize(id);
        stjJson.Should().Be("null");
        System.Text.Json.JsonSerializer.Deserialize<PrefixedStringId>(stjJson).Should().Be(id);

        var newtonsoftJson = Newtonsoft.Json.JsonConvert.SerializeObject(id);
        newtonsoftJson.Should().Be("null");
        Newtonsoft.Json.JsonConvert.DeserializeObject<PrefixedStringId>(newtonsoftJson).Should().Be(id);
    }

    /// <summary>
    /// C10：前缀 + 对象形态，<c>{ "value": "ord_x" }</c>。
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