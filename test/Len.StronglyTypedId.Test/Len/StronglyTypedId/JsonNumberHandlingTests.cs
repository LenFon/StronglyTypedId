using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;

namespace Len.StronglyTypedId;

/// <summary>
/// 生成的 System.Text.Json 转换器对 <see cref="JsonNumberHandling"/> 的遵守程度。
/// </summary>
/// <remarks>
/// 判据是「Id 与它包装的裸基元对同一份载荷给出相同结果」：<c>JsonNumberHandling</c> 只作用于框架内置的
/// 数值转换器，自定义 <c>JsonConverter&lt;T&gt;</c> 默认完全看不到它，于是同一份契约里 <c>int</c> 字段
/// 与 <c>Int32Id</c> 字段会对 <c>"42"</c> 给出不同答案。这些用例把两侧并排比对，而不是只断言 Id 一侧。
/// </remarks>
public class JsonNumberHandlingTests
{
    private static readonly JsonSerializerOptions _allowReadingFromString = new()
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    private static readonly JsonSerializerOptions _writeAsString = new()
    {
        NumberHandling = JsonNumberHandling.WriteAsString,
    };

    private static readonly JsonSerializerOptions _defaultOptions = new();

    #region 读：AllowReadingFromString

    [Fact]
    public void Read_Should_AcceptStringToken_WhenAllowReadingFromStringIsSet()
    {
        var id = JsonSerializer.Deserialize<Int32Id>("\"42\"", _allowReadingFromString);

        id.Value.Should().Be(42);
    }

    [Fact]
    public void Read_Should_StillAcceptNumberToken_WhenAllowReadingFromStringIsSet()
    {
        var id = JsonSerializer.Deserialize<Int32Id>("42", _allowReadingFromString);

        id.Value.Should().Be(42);
    }

    [Fact]
    public void Read_Should_RejectStringToken_WhenAllowReadingFromStringIsNotSet()
    {
        var act = () => JsonSerializer.Deserialize<Int32Id>("\"42\"", _defaultOptions);

        act.Should().Throw<JsonException>();
    }

    [Theory]
    [InlineData("42", 42)]
    [InlineData("+42", 42)]
    [InlineData("-42", -42)]
    [InlineData("0042", 42)]
    public void Read_Should_MatchBarePrimitive_ForSignedIntegerStrings(string text, int expected)
    {
        JsonSerializer.Deserialize<Int32Id>($"\"{text}\"", _allowReadingFromString).Value.Should().Be(expected);
        JsonSerializer.Deserialize<int>($"\"{text}\"", _allowReadingFromString).Should().Be(expected);
    }

    [Theory]
    [InlineData(" 42")]
    [InlineData("42 ")]
    [InlineData("4.2")]
    [InlineData("4e1")]
    [InlineData("")]
    [InlineData("0x2A")]
    [InlineData("1,000")]
    public void Read_Should_RejectSameStrings_AsBarePrimitive_ForSignedInteger(string text)
    {
        var bare = () => JsonSerializer.Deserialize<int>($"\"{text}\"", _allowReadingFromString);
        var id = () => JsonSerializer.Deserialize<Int32Id>($"\"{text}\"", _allowReadingFromString);

        bare.Should().Throw<JsonException>();
        id.Should().Throw<JsonException>();
    }

    [Theory]
    [InlineData("255", 255)]
    [InlineData("05", 5)]
    public void Read_Should_MatchBarePrimitive_ForUnsignedIntegerStrings(string text, byte expected)
    {
        JsonSerializer.Deserialize<ByteId>($"\"{text}\"", _allowReadingFromString).Value.Should().Be(expected);
        JsonSerializer.Deserialize<byte>($"\"{text}\"", _allowReadingFromString).Should().Be(expected);
    }

    /// <summary>
    /// 无符号类型连前导 <c>+</c> 都拒绝，与 STJ 的内置转换器一致；<c>NumberStyles.Integer</c> 会接受它。
    /// </summary>
    [Theory]
    [InlineData("+5")]
    [InlineData("-1")]
    [InlineData("256")]
    public void Read_Should_RejectSameStrings_AsBarePrimitive_ForUnsignedInteger(string text)
    {
        var bare = () => JsonSerializer.Deserialize<byte>($"\"{text}\"", _allowReadingFromString);
        var id = () => JsonSerializer.Deserialize<ByteId>($"\"{text}\"", _allowReadingFromString);

        bare.Should().Throw<JsonException>();
        id.Should().Throw<JsonException>();
    }

    [Theory]
    [InlineData("1.5", 1.5)]
    [InlineData("+1.5", 1.5)]
    [InlineData("-1.5", -1.5)]
    [InlineData("1e2", 100)]
    [InlineData("1E2", 100)]
    [InlineData("1.5e-3", 0.0015)]
    [InlineData(".5", 0.5)]
    [InlineData("5.", 5)]
    public void Read_Should_MatchBarePrimitive_ForDoubleStrings(string text, double expected)
    {
        JsonSerializer.Deserialize<DoubleId>($"\"{text}\"", _allowReadingFromString).Value.Should().Be(expected);
        JsonSerializer.Deserialize<double>($"\"{text}\"", _allowReadingFromString).Should().Be(expected);
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    public void Read_Should_AcceptSpecialFloats_WhenAllowReadingFromStringIsSet(string text)
    {
        var id = JsonSerializer.Deserialize<DoubleId>($"\"{text}\"", _allowReadingFromString);

        id.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
            .Should().Be(JsonSerializer.Deserialize<double>($"\"{text}\"", _allowReadingFromString)
                .ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// <c>double.Parse</c> 认小写 <c>nan</c>，也会把溢出折成无穷；STJ 两者都拒绝。
    /// 生成代码补了一道后置校验，这些用例守住它。
    /// </summary>
    [Theory]
    [InlineData("nan")]
    [InlineData("NAN")]
    [InlineData("infinity")]
    [InlineData("INFINITY")]
    [InlineData("-infinity")]
    [InlineData("1e400")]
    [InlineData("-1e400")]
    [InlineData(" 1.5")]
    [InlineData("1.5 ")]
    [InlineData("1,5")]
    public void Read_Should_RejectSameStrings_AsBarePrimitive_ForDouble(string text)
    {
        var bare = () => JsonSerializer.Deserialize<double>($"\"{text}\"", _allowReadingFromString);
        var id = () => JsonSerializer.Deserialize<DoubleId>($"\"{text}\"", _allowReadingFromString);

        bare.Should().Throw<JsonException>();
        id.Should().Throw<JsonException>();
    }

    [Fact]
    public void Read_Should_PreserveDecimalPrecision_WhenReadFromString()
    {
        const string Text = "123456789012345.67";

        var id = JsonSerializer.Deserialize<DecimalId>($"\"{Text}\"", _allowReadingFromString);

        id.Value.Should().Be(JsonSerializer.Deserialize<decimal>($"\"{Text}\"", _allowReadingFromString));
        id.Value.ToString(System.Globalization.CultureInfo.InvariantCulture).Should().Be(Text);
    }

    [Fact]
    public void Read_Should_PreserveDecimalScale_WhenReadFromString()
    {
        var id = JsonSerializer.Deserialize<DecimalId>("\"1.50\"", _allowReadingFromString);

        id.Value.ToString(System.Globalization.CultureInfo.InvariantCulture).Should().Be("1.50");
    }

    /// <summary>
    /// 枚举基元不受 <c>NumberHandling</c> 影响：STJ 的内置枚举转换器完全不看它，
    /// 让 Id 遵守反而会与裸枚举不一致。
    /// </summary>
    [Fact]
    public void Read_Should_RejectEnumString_EvenWhenAllowReadingFromStringIsSet()
    {
        var bare = () => JsonSerializer.Deserialize<SampleStatus>("1", _allowReadingFromString);
        var id = () => JsonSerializer.Deserialize<SampleStatusId>("\"1\"", _allowReadingFromString);

        bare.Should().NotThrow();
        id.Should().Throw<JsonException>();
    }

    #endregion

    #region 写：WriteAsString

    [Theory]
    [InlineData(42, "\"42\"")]
    [InlineData(-42, "\"-42\"")]
    [InlineData(int.MinValue, "\"-2147483648\"")]
    public void Write_Should_WriteString_WhenWriteAsStringIsSet(int value, string expected)
        => JsonSerializer.Serialize(new Int32Id(value), _writeAsString).Should().Be(expected);

    [Fact]
    public void Write_Should_WriteNumber_WhenWriteAsStringIsNotSet()
        => JsonSerializer.Serialize(new Int32Id(42), _defaultOptions).Should().Be("42");

    [Theory]
    [InlineData(1.5, "\"1.5\"")]
    [InlineData(1e300, "\"1E+300\"")]
    [InlineData(1e-300, "\"1E-300\"")]
    public void Write_Should_MatchBarePrimitive_ForDouble(double value, string expected)
    {
        JsonSerializer.Serialize(new DoubleId(value), _writeAsString).Should().Be(expected);
        JsonSerializer.Serialize(value, _writeAsString).Should().Be(expected);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-0.0)]
    public void Write_Should_MatchBarePrimitive_ForSpecialDoubles(double value)
    {
        JsonSerializer.Serialize(new DoubleId(value), _writeAsString)
            .Should().Be(JsonSerializer.Serialize(value, _writeAsString));
    }

    [Fact]
    public void Write_Should_MatchBarePrimitive_ForDecimal()
    {
        const decimal Value = 123456789012345.67m;

        JsonSerializer.Serialize(new DecimalId(Value), _writeAsString)
            .Should().Be(JsonSerializer.Serialize(Value, _writeAsString));
    }

    [Fact]
    public void Write_Should_MatchBarePrimitive_ForUnsignedByte()
    {
        JsonSerializer.Serialize(new ByteId(5), _writeAsString).Should().Be("\"5\"");
        JsonSerializer.Serialize((byte)5, _writeAsString).Should().Be("\"5\"");
    }

    /// <summary>
    /// 枚举基元不受 <c>NumberHandling</c> 影响，与裸枚举一致地仍写数字。
    /// </summary>
    [Fact]
    public void Write_Should_KeepWritingNumber_ForEnumPrimitive()
    {
        JsonSerializer.Serialize(new SampleStatusId(SampleStatus.Active), _writeAsString)
            .Should().Be(JsonSerializer.Serialize(SampleStatus.Active, _writeAsString));
    }

    #endregion

    #region 往返

    [Theory]
    [InlineData(0)]
    [InlineData(42)]
    [InlineData(-7)]
    public void RoundTrip_Should_PreserveValue_WhenWriteAsStringAndAllowReadingFromStringAreBothSet(int value)
    {
        var options = new JsonSerializerOptions
        {
            NumberHandling = JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString,
        };

        var json = JsonSerializer.Serialize(new Int32Id(value), options);

        json.Should().StartWith("\"");
        JsonSerializer.Deserialize<Int32Id>(json, options).Value.Should().Be(value);
    }

    [Fact]
    public void RoundTrip_Should_PreserveSpecialDouble_WhenWriteAsStringAndAllowReadingFromStringAreBothSet()
    {
        var options = new JsonSerializerOptions
        {
            NumberHandling = JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString,
        };

        var json = JsonSerializer.Serialize(new DoubleId(double.NaN), options);

        json.Should().Be("\"NaN\"");
        double.IsNaN(JsonSerializer.Deserialize<DoubleId>(json, options).Value).Should().BeTrue();
    }

    #endregion

    #region JsonShape.Object

    /// <summary>
    /// <c>JsonShape.Object</c> 的 <c>value</c> 属性内部同样遵守 <c>NumberHandling</c>。
    /// </summary>
    [Fact]
    public void Write_Should_WriteStringInsideObjectShape_WhenWriteAsStringIsSet()
    {
        JsonSerializer.Serialize(new ObjectShapeInt32Id(42), _writeAsString)
            .Should().Be("""{"value":"42"}""");
    }

    [Fact]
    public void Read_Should_AcceptStringInsideObjectShape_WhenAllowReadingFromStringIsSet()
    {
        var id = JsonSerializer.Deserialize<ObjectShapeInt32Id>("""{"value":"42"}""", _allowReadingFromString);

        id.Value.Should().Be(42);
    }

    #endregion

    #region 不受影响的基元

    [Fact]
    public void Write_Should_NotChangeGuidOutput_WhenWriteAsStringIsSet()
    {
        var id = GuidId.Create(Guid.Parse("d8ac85d4-ed76-4974-b055-8ef3508743f3"));

        JsonSerializer.Serialize(id, _writeAsString)
            .Should().Be(JsonSerializer.Serialize(id, _defaultOptions));
    }

    [Fact]
    public void Write_Should_NotChangeStringOutput_WhenWriteAsStringIsSet()
    {
        var id = StringId.Create("Len");

        JsonSerializer.Serialize(id, _writeAsString)
            .Should().Be(JsonSerializer.Serialize(id, _defaultOptions));
    }

    [Fact]
    public void Write_Should_NotChangeDateTimeOutput_WhenWriteAsStringIsSet()
    {
        var id = DateTimeId.Create(new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc));

        JsonSerializer.Serialize(id, _writeAsString)
            .Should().Be(JsonSerializer.Serialize(id, _defaultOptions));
    }

    #endregion
}
