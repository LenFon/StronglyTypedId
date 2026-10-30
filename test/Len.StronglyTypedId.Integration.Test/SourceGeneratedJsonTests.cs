using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;

namespace Len.StronglyTypedId;

/// <summary>
/// 源生成 <c>JsonSerializerContext</c> 场景下的 Id 形态一致性。
/// </summary>
/// <remarks>
/// 源生成器对<b>本次编译声明</b>的类型走 fast-path，不读取生成器补在 partial 声明上的
/// <c>[JsonConverter]</c>，于是 Id 被按属性写成 <c>{"Value":…}</c>，而反射式序列化写的是字符串。
/// <c>WithStronglyTypedIds()</c> 把 Id 的 <c>JsonTypeInfo</c> 插到 context 之前，两者因此对齐。
/// <para>
/// 两条 TFM 腿都跑：net8.0 的引用包里同样带 <c>System.Text.Json.SourceGeneration.dll</c>
/// （实测 8.0.22 与 10.0.12 都有），源生成 context 在 net8.0 上可用，缺陷与修复也都在两条腿上成立。
/// </para>
/// </remarks>
public class SourceGeneratedJsonTests
{
    private static readonly Guid _value = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");

    // 缓存而非每次新建：CA1869 要求复用 JsonSerializerOptions，且 options 一旦被使用就会冻结，
    // 复用也顺带证明 resolver 组合在冻结后仍然可用。
    private static readonly JsonSerializerOptions _options = new()
    {
        TypeInfoResolver = SourceGeneratedJsonContext.Default.WithStronglyTypedIds(),
    };

    // 对照组：不挂 resolver，用来证明缺陷本身存在（否则「修好了」无法与「本来就没问题」区分）。
    private static readonly JsonSerializerOptions _contextOnlyOptions = new()
    {
        TypeInfoResolver = SourceGeneratedJsonContext.Default,
    };

    [Fact]
    public void SourceGeneratedContext_Should_WriteObjectShape_WithoutResolver()
    {
        // 这就是缺陷本身：与反射式序列化不一致。
        JsonSerializer.Serialize(new IntegrationGuidId(_value), _contextOnlyOptions)
            .Should().Be("{\"Value\":\"3fa85f64-5717-4562-b3fc-2c963f66afa6\"}");
    }

    [Fact]
    public void SourceGeneratedContext_Should_WritePrimitiveShape_WithResolver()
    {
        JsonSerializer.Serialize(new IntegrationGuidId(_value), _options)
            .Should().Be("\"3fa85f64-5717-4562-b3fc-2c963f66afa6\"");
    }

    [Fact]
    public void SourceGeneratedContext_Should_MatchReflectionSerialization()
    {
        var id = new IntegrationGuidId(_value);

        var reflection = JsonSerializer.Serialize(id);
        var sourceGenerated = JsonSerializer.Serialize(id, _options);

        sourceGenerated.Should().Be(reflection, "同一个 Id 在两条序列化路径下必须是同一种形态");
    }

    [Fact]
    public void SourceGeneratedContext_Should_RoundTrip()
    {
        var id = new IntegrationGuidId(_value);

        var json = JsonSerializer.Serialize(id, _options);

        JsonSerializer.Deserialize<IntegrationGuidId>(json, _options).Should().Be(id);
    }

    [Fact]
    public void SourceGeneratedContext_Should_HonourObjectJsonShape()
    {
        var id = new ObjectShapeIntegrationGuidId(_value);

        JsonSerializer.Serialize(id, _options).Should().Be("{\"value\":\"3fa85f64-5717-4562-b3fc-2c963f66afa6\"}");
        JsonSerializer.Deserialize<ObjectShapeIntegrationGuidId>("{\"value\":\"3fa85f64-5717-4562-b3fc-2c963f66afa6\"}", _options)
            .Should().Be(id);
    }

    [Fact]
    public void SourceGeneratedContext_Should_HonourPrefix()
    {
        var id = new PrefixedIntegrationGuidId(_value);

        JsonSerializer.Serialize(id, _options).Should().Be("\"ord_3fa85f64-5717-4562-b3fc-2c963f66afa6\"");
        JsonSerializer.Deserialize<PrefixedIntegrationGuidId>("\"ord_3fa85f64-5717-4562-b3fc-2c963f66afa6\"", _options)
            .Should().Be(id);
    }

    [Fact]
    public void SourceGeneratedContext_Should_StillServeNonIdTypes()
        // resolver 只认 Id，其余类型必须回落到 context。
        => JsonSerializer.Serialize(42, _options).Should().Be("42");

    [Fact]
    public void SourceGeneratedContext_Should_UseIdAsDictionaryKey()
    {
        var id = new IntegrationGuidId(_value);
        var dictionary = new Dictionary<IntegrationGuidId, int> { [id] = 1 };

        var json = JsonSerializer.Serialize(dictionary, _options);

        json.Should().Be("{\"3fa85f64-5717-4562-b3fc-2c963f66afa6\":1}");
    }
}

/// <summary>
/// 源生成的 System.Text.Json 元数据上下文，只登记本测试用到的类型。
/// </summary>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(IntegrationGuidId))]
[JsonSerializable(typeof(ObjectShapeIntegrationGuidId))]
[JsonSerializable(typeof(PrefixedIntegrationGuidId))]
[JsonSerializable(typeof(Dictionary<IntegrationGuidId, int>))]
[JsonSerializable(typeof(int))]
internal sealed partial class SourceGeneratedJsonContext : JsonSerializerContext
{
}
