using FluentAssertions;

namespace Len.StronglyTypedId;

/// <summary>
/// 生成的 JSON 转换器在「null 与畸形载荷」下的边界行为。
/// </summary>
/// <remarks>
/// <para>
/// 既有 <see cref="SerializationAndDeserializationTests"/> 只覆盖合法值的往返，本类补的是
/// 生成代码里那条「取不到值」的兜底路径（<c>_ =&gt; throw …</c>）与写侧的空值路径
/// （<c>writer.WriteNullValue()</c> / <c>writer.WriteNull()</c>），二者此前从未被执行过。
/// </para>
/// <para>
/// 断言按「实测事实」而非「推测」书写，且只锁定异常**类型**不锁定消息文本：消息里的
/// 路径 / 行列号由 JSON 库在不同主版本间调整。
/// </para>
/// <para>
/// 值类型（record struct）与引用类型（record）在 null 上的契约不同，因此两者都测：
/// Newtonsoft 的生成代码显式用 <c>null when (objectType.IsClass || …)</c> 分流，System.Text.Json 则由框架
/// 依据 <see cref="System.Text.Json.Serialization.JsonConverter{T}"/> 的空值处理约定分流。
/// </para>
/// </remarks>
public class JsonConverterBoundaryTests
{
    #region System.Text.Json：null 令牌

    /// <summary>
    /// 值类型 Id 遇到 JSON <c>null</c> 时，框架仍会把令牌交给生成的转换器，
    /// 转换器取不到值 → 抛 <see cref="InvalidOperationException"/>。
    /// </summary>
    [Fact]
    public void SystemTextJson_Should_Throw_WhenValueTypeIdReceivesNullToken()
    {
        var act = () => System.Text.Json.JsonSerializer.Deserialize<GuidId>("null");

        act.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// 引用类型 Id 遇到 JSON <c>null</c> 时由框架直接返回 <see langword="null"/>，
    /// 不进入转换器（因此不会抛出"取不到值"异常）。
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
    public void SystemTextJson_Should_WriteNull_WhenReferenceTypeIdIsNull()
    {
        System.Text.Json.JsonSerializer.Serialize(default(GuidIdV2)).Should().Be("null");
    }

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
    /// 的兜底分支：值类型 Id 两个条件都不成立，落到 <c>_ =&gt;</c> 抛出。
    /// </summary>
    [Fact]
    public void NewtonsoftJson_Should_Throw_WhenValueTypeIdReceivesNullToken()
    {
        var act = () => Newtonsoft.Json.JsonConvert.DeserializeObject<GuidId>("null");

        act.Should().Throw<InvalidOperationException>();
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
    public void NewtonsoftJson_Should_WriteNull_WhenReferenceTypeIdIsNull()
    {
        Newtonsoft.Json.JsonConvert.SerializeObject(default(GuidIdV2)).Should().Be("null");
    }

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
        act.Should().Throw<InvalidOperationException>();

        System.Text.Json.JsonSerializer.Deserialize<Int32IdV2>("null").Should().BeNull();
    }

    [Fact]
    public void NullTokenContract_Should_BePrimitiveAgnostic_ForNewtonsoftJson()
    {
        var act = () => Newtonsoft.Json.JsonConvert.DeserializeObject<Int32Id>("null");
        act.Should().Throw<InvalidOperationException>();

        Newtonsoft.Json.JsonConvert.DeserializeObject<Int32IdV2>("null").Should().BeNull();
    }

    #endregion
}
