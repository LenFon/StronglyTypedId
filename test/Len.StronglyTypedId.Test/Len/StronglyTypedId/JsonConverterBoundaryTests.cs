using FluentAssertions;

namespace Len.StronglyTypedId;

/// <summary>
/// 生成的 JSON 转换器在「null 与畸形载荷」下的边界行为。
/// </summary>
public class JsonConverterBoundaryTests
{
    #region System.Text.Json：null 令牌

    /// <summary>
    /// 值类型 Id 遇到 JSON <c>null</c> 时，框架仍会把令牌交给生成的转换器，转换器取不到值 → 抛
    /// <see cref="System.Text.Json.JsonException"/>（STJ 的转换器契约：失败一律抛 JsonException，
    /// 只有它才会被补上 Path / LineNumber / BytePositionInLine）。
    /// </summary>
    [Fact]
    public void SystemTextJson_Should_ThrowJsonException_WhenValueTypeIdReceivesNullToken()
    {
        var act = () => System.Text.Json.JsonSerializer.Deserialize<GuidId>("null");

        act.Should().Throw<System.Text.Json.JsonException>();
    }

    /// <summary>
    /// 嵌套位置的 null 令牌：异常必须带上 JSON path，否则调用方无法定位是哪一层出的问题。
    /// </summary>
    [Fact]
    public void SystemTextJson_Should_AttachJsonPath_WhenNestedValueTypeIdReceivesNullToken()
    {
        var act = () => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, GuidId>>("""{"k":null}""");

        act.Should().Throw<System.Text.Json.JsonException>()
            .Which.Path.Should().Be("$.k");
    }

    /// <summary>
    /// 引用类型 Id 遇到 JSON <c>null</c> 时由框架直接返回 <see langword="null"/>，不进入转换器（因此不会抛出"取不到值"异常）。
    /// </summary>
    [Fact]
    public void SystemTextJson_Should_ReturnNull_WhenReferenceTypeIdReceivesNullToken()
    {
        var id = System.Text.Json.JsonSerializer.Deserialize<GuidIdV2>("null");

        id.Should().BeNull();
    }

    #endregion

    #region System.Text.Json：畸形令牌

    [Theory]
    [InlineData(typeof(GuidId), "\"abc\"")]
    [InlineData(typeof(GuidIdV2), "\"abc\"")]
    [InlineData(typeof(GuidId), "{}")]
    [InlineData(typeof(GuidIdV2), "{}")]
    public void SystemTextJson_Should_ThrowJsonException_WhenTokenCannotBecomePrimitive(Type idType, string json)
    {
        var act = () => System.Text.Json.JsonSerializer.Deserialize(json, idType);

        act.Should().Throw<System.Text.Json.JsonException>();
    }

    #endregion

    #region System.Text.Json：写侧空值

    [Fact]
    public void SystemTextJson_Should_WriteNull_WhenReferenceTypeIdIsNull() => System.Text.Json.JsonSerializer.Serialize(default(GuidIdV2)).Should().Be("null");

    [Fact]
    public void SystemTextJson_Should_WritePrimitiveValue_WhenValueIsDefaultStruct()
    {
        System.Text.Json.JsonSerializer.Serialize(GuidId.Create(Guid.Empty))
            .Should().Be("\"00000000-0000-0000-0000-000000000000\"");
    }

    #endregion

    #region Newtonsoft.Json：null 令牌

    /// <summary>
    /// 生成的 <c>ReadJson</c> 里 <c>null when (objectType.IsClass || Nullable.GetUnderlyingType(...))</c>
    /// 的兜底分支：值类型 Id 两个条件都不成立，落到 <c>_ =&gt;</c> 抛
    /// <see cref="Newtonsoft.Json.JsonSerializationException"/>（与同文件其它结构性错误一致）。
    /// </summary>
    [Fact]
    public void NewtonsoftJson_Should_ThrowJsonSerializationException_WhenValueTypeIdReceivesNullToken()
    {
        var act = () => Newtonsoft.Json.JsonConvert.DeserializeObject<GuidId>("null");

        act.Should().Throw<Newtonsoft.Json.JsonSerializationException>();
    }

    /// <summary>
    /// 同一个兜底分支的另一侧：引用类型 Id 满足 <c>objectType.IsClass</c>，返回 <see langword="null"/>。
    /// </summary>
    [Fact]
    public void NewtonsoftJson_Should_ReturnNull_WhenReferenceTypeIdReceivesNullToken()
    {
        var id = Newtonsoft.Json.JsonConvert.DeserializeObject<GuidIdV2>("null");

        id.Should().BeNull();
    }

    #endregion

    #region Newtonsoft.Json：畸形令牌

    [Theory]
    [InlineData(typeof(GuidId), "\"abc\"")]
    [InlineData(typeof(GuidIdV2), "\"abc\"")]
    [InlineData(typeof(GuidId), "{}")]
    [InlineData(typeof(GuidIdV2), "{}")]
    public void NewtonsoftJson_Should_ThrowJsonSerializationException_WhenTokenCannotBecomePrimitive(Type idType, string json)
    {
        var act = () => Newtonsoft.Json.JsonConvert.DeserializeObject(json, idType);

        act.Should().Throw<Newtonsoft.Json.JsonSerializationException>();
    }

    #endregion

    #region Newtonsoft.Json：写侧空值

    [Fact]
    public void NewtonsoftJson_Should_WriteNull_WhenReferenceTypeIdIsNull() => Newtonsoft.Json.JsonConvert.SerializeObject(default(GuidIdV2)).Should().Be("null");

    [Fact]
    public void NewtonsoftJson_Should_WritePrimitiveValue_WhenValueIsDefaultStruct()
    {
        Newtonsoft.Json.JsonConvert.SerializeObject(GuidId.Create(Guid.Empty))
            .Should().Be("\"00000000-0000-0000-0000-000000000000\"");
    }

    #endregion

    #region 非 Guid 基元：null 语义与 Guid 一致

    [Fact]
    public void NullTokenContract_Should_BePrimitiveAgnostic_ForSystemTextJson()
    {
        var act = () => System.Text.Json.JsonSerializer.Deserialize<Int32Id>("null");
        act.Should().Throw<System.Text.Json.JsonException>();

        System.Text.Json.JsonSerializer.Deserialize<Int32IdV2>("null").Should().BeNull();
    }

    [Fact]
    public void NullTokenContract_Should_BePrimitiveAgnostic_ForNewtonsoftJson()
    {
        var act = () => Newtonsoft.Json.JsonConvert.DeserializeObject<Int32Id>("null");
        act.Should().Throw<Newtonsoft.Json.JsonSerializationException>();

        Newtonsoft.Json.JsonConvert.DeserializeObject<Int32IdV2>("null").Should().BeNull();
    }

    #endregion
}
