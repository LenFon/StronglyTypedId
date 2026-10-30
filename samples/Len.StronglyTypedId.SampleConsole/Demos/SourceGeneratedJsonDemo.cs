using System.Text.Json;
using System.Text.Json.Serialization;

namespace Len.StronglyTypedId.SampleConsole.Demos;

internal static class SourceGeneratedJsonDemo
{
    // 缓存而非每次新建：CA1869 要求复用 JsonSerializerOptions，且 options 一经使用即冻结。
    private static readonly JsonSerializerOptions _contextOnly = new()
    {
        TypeInfoResolver = SampleJsonContext.Default,
    };

    private static readonly JsonSerializerOptions _withIds = new()
    {
        TypeInfoResolver = SampleJsonContext.Default.WithStronglyTypedIds(),
    };

    public static void Run()
    {
        Demo.Section("源生成 JsonSerializerContext：不挂 resolver 时 Id 被写成对象形态");

        var order = OrderId.Create(Guid.Parse("3f2c1b4e-0000-4000-8000-000000000001"));
        Demo.Line($"context 直出                       -> {JsonSerializer.Serialize(order, _contextOnly)}");

        Demo.Section("源生成 JsonSerializerContext：WithStronglyTypedIds() 对齐成基元形态");

        Demo.Line($"挂上 resolver                      -> {JsonSerializer.Serialize(order, _withIds)}");
        Demo.Line($"与反射式序列化一致                  -> {JsonSerializer.Serialize(order, _withIds) == JsonSerializer.Serialize(order)}");
        Demo.Line($"往返                               -> {JsonSerializer.Deserialize<OrderId>(JsonSerializer.Serialize(order, _withIds), _withIds) == order}");

        Demo.Section("源生成 JsonSerializerContext：Id 以外的类型仍由 context 处理");

        Demo.Line($"Serialize(42)                      -> {JsonSerializer.Serialize(42, _withIds)}");
    }
}

/// <summary>
/// 源生成的 System.Text.Json 元数据上下文，只登记本示例用到的类型。
/// </summary>
/// <remarks>
/// <c>Metadata</c> 模式是 AOT 的推荐配置：它不展开类型上的 <c>[JsonConverter]</c>，
/// 因此 Id 会被按属性写成 <c>{"Value":…}</c>，需要 <c>WithStronglyTypedIds()</c> 对齐。
/// </remarks>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(OrderId))]
[JsonSerializable(typeof(int))]
internal sealed partial class SampleJsonContext : JsonSerializerContext
{
}
