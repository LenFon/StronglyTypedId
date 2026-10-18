using System.Text.Json.Serialization;

namespace Len.StronglyTypedId.Aot.SmokeTest;

/// <summary>
/// 源生成的 System.Text.Json 元数据上下文。
/// </summary>
/// <remarks>
/// <para>
/// AOT / 裁剪下 STJ 的反射序列化被禁用，任何 <c>JsonSerializer.Serialize</c> / <c>Deserialize</c>
/// 都必须能从某个 <see cref="JsonSerializerOptions.TypeInfoResolver"/> 取到 <c>JsonTypeInfo</c>。
/// </para>
/// <para>
/// 生成的强类型 Id 转换器内部正是用 <c>JsonSerializer</c> 处理基元值（这样才能尊重宿主配置的
/// 转换器，例如 <c>JsonStringEnumConverter</c>），因此 AOT 宿主必须提供上下文 —— 这与「AOT 下用 STJ
/// 必须配 source-gen」的既有规则一致，不是本库额外的要求。
/// </para>
/// </remarks>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(Guid))]
[JsonSerializable(typeof(string))]
internal sealed partial class SmokeTestJsonContext : JsonSerializerContext
{
}
