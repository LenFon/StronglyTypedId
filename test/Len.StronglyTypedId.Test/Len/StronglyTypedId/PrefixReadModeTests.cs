using System.ComponentModel;
using System.Globalization;
using FluentAssertions;

namespace Len.StronglyTypedId;

/// <summary>
/// 前缀读入模式（<see cref="PrefixReadMode"/>）在各文本入口上的表现。
/// </summary>
/// <remarks>
/// <para>
/// 前缀只存在于<b>规范文本形</b>里，因此凡是「按文本读入」的入口都必须遵守同一套策略：
/// <see cref="PrefixReadMode.Lenient"/> 接受带前缀与裸值两种写法，<see cref="PrefixReadMode.Strict"/> 只接受带前缀。
/// </para>
/// <para>
/// 现有用例只在 <c>Parse</c> / <c>TryParse</c> 上验证过这条策略。JSON 转换器与 <c>TypeConverter</c> 是另外几条
/// 独立的读入路径（各自生成，各自调用 <c>TryParse</c> / <c>Parse</c>），一旦哪条漏掉策略，
/// 严格模式下「缺前缀的裸值」就会被静默接受 —— 而这正是严格模式存在的理由。
/// </para>
/// <para>
/// 这里的 JSON 类型一律写全限定名：本文件同时用到 System.Text.Json 与 Newtonsoft.Json，
/// 两者的 <c>JsonSerializer</c> / <c>JsonException</c> 同名，引入命名空间会造成 CS0104 歧义。
/// </para>
/// </remarks>
public class PrefixReadModeTests
{
    /// <summary>
    /// 固定取值：断言里要拼出带前缀的文本，随机值会让失败信息难以复现。
    /// </summary>
    private static readonly Guid _value = new("3f2504e0-4f89-11d3-9a0c-0305e82c3301");

    /// <summary>
    /// 规范文本形：前缀 + 基元的规范文本。
    /// </summary>
    private static string PrefixedText => "ord_" + _value.ToString("D", CultureInfo.InvariantCulture);

    /// <summary>
    /// 裸值：只有基元文本，缺前缀。
    /// </summary>
    private static string BareText => _value.ToString("D", CultureInfo.InvariantCulture);

    #region System.Text.Json

    [Fact]
    public void SystemTextJson_Should_ReadPrefixedText_WhenStrict()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(PrefixedText);

        System.Text.Json.JsonSerializer.Deserialize<StrictPrefixedGuidId>(json).Value.Should().Be(_value);
    }

    /// <summary>
    /// 严格模式下裸值必须失败：这是「前缀是契约的一部分」的唯一可观测后果。
    /// </summary>
    [Fact]
    public void SystemTextJson_Should_RejectBareText_WhenStrict()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(BareText);

        var deserialize = () => System.Text.Json.JsonSerializer.Deserialize<StrictPrefixedGuidId>(json);

        deserialize.Should().Throw<System.Text.Json.JsonException>();
    }

    [Fact]
    public void SystemTextJson_Should_ReadBareText_WhenLenient()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(BareText);

        System.Text.Json.JsonSerializer.Deserialize<PrefixedGuidId>(json).Value.Should().Be(_value);
    }

    /// <summary>
    /// 写侧与读侧必须自洽：严格模式下自己写出去的文本一定能读回来。
    /// </summary>
    [Fact]
    public void SystemTextJson_Should_RoundTrip_WhenStrict()
    {
        var id = StrictPrefixedGuidId.Create(_value);
        var json = System.Text.Json.JsonSerializer.Serialize(id);

        json.Should().Be(System.Text.Json.JsonSerializer.Serialize(PrefixedText));
        System.Text.Json.JsonSerializer.Deserialize<StrictPrefixedGuidId>(json).Should().Be(id);
    }

    /// <summary>
    /// 对象形态（<c>{ "value": … }</c>）是另一段生成代码，严格策略同样要生效。
    /// </summary>
    [Fact]
    public void SystemTextJson_Should_RejectBareText_InObjectShape_WhenStrict()
    {
        var json = $$"""{"value":"{{BareText}}"}""";

        var deserialize = () => System.Text.Json.JsonSerializer.Deserialize<StrictPrefixedObjectGuidId>(json);

        deserialize.Should().Throw<System.Text.Json.JsonException>();
    }

    [Fact]
    public void SystemTextJson_Should_ReadPrefixedText_InObjectShape_WhenStrict()
    {
        var json = $$"""{"value":"{{PrefixedText}}"}""";

        System.Text.Json.JsonSerializer.Deserialize<StrictPrefixedObjectGuidId>(json).Value.Should().Be(_value);
    }

    #endregion

    #region Newtonsoft.Json

    [Fact]
    public void NewtonsoftJson_Should_ReadPrefixedText_WhenStrict()
    {
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(PrefixedText);

        Newtonsoft.Json.JsonConvert.DeserializeObject<StrictPrefixedGuidId>(json)!.Value.Should().Be(_value);
    }

    [Fact]
    public void NewtonsoftJson_Should_RejectBareText_WhenStrict()
    {
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(BareText);

        var deserialize = () => Newtonsoft.Json.JsonConvert.DeserializeObject<StrictPrefixedGuidId>(json);

        deserialize.Should().Throw<Newtonsoft.Json.JsonSerializationException>();
    }

    [Fact]
    public void NewtonsoftJson_Should_ReadBareText_WhenLenient()
    {
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(BareText);

        Newtonsoft.Json.JsonConvert.DeserializeObject<PrefixedGuidId>(json)!.Value.Should().Be(_value);
    }

    /// <summary>
    /// 对象形态在 Newtonsoft 侧同样是独立分支（走 <c>JObject</c> 取 <c>value</c> 再 <c>TryParse</c>）。
    /// </summary>
    [Fact]
    public void NewtonsoftJson_Should_RejectBareText_InObjectShape_WhenStrict()
    {
        var json = $$"""{"value":"{{BareText}}"}""";

        var deserialize = () => Newtonsoft.Json.JsonConvert.DeserializeObject<StrictPrefixedObjectGuidId>(json);

        deserialize.Should().Throw<Newtonsoft.Json.JsonSerializationException>();
    }

    #endregion

    #region TypeConverter

    /// <summary>
    /// 生成的转换器内部调用 <c>Parse(text, InvariantCulture)</c>，故严格模式的拒绝表现为
    /// <see cref="FormatException"/>（<c>IParsable</c> 的失败约定），而不是 <see cref="NotSupportedException"/>。
    /// </summary>
    [Fact]
    public void TypeConverter_Should_RejectBareText_WhenStrict()
    {
        var converter = TypeDescriptor.GetConverter(typeof(StrictConvertiblePrefixedGuidId));

        var convert = () => converter.ConvertFromInvariantString(BareText);

        convert.Should().Throw<FormatException>();
    }

    [Fact]
    public void TypeConverter_Should_ReadPrefixedText_WhenStrict()
    {
        var converter = TypeDescriptor.GetConverter(typeof(StrictConvertiblePrefixedGuidId));

        converter.ConvertFromInvariantString(PrefixedText)
            .Should().Be(StrictConvertiblePrefixedGuidId.Create(_value));
    }

    /// <summary>
    /// 写侧恒带前缀（<c>ToString()</c> 的语义），与读侧的严格策略配套。
    /// </summary>
    [Fact]
    public void TypeConverter_Should_WritePrefixedText_WhenStrict()
    {
        var converter = TypeDescriptor.GetConverter(typeof(StrictConvertiblePrefixedGuidId));

        converter.ConvertToInvariantString(StrictConvertiblePrefixedGuidId.Create(_value)).Should().Be(PrefixedText);
    }

    #endregion
}
