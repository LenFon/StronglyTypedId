using System.Text.Json.Serialization;

namespace Len.StronglyTypedId.Aot.SmokeTest;

/// <summary>
/// 源生成的 System.Text.Json 元数据上下文。
/// </summary>
/// <remarks>
/// 登记 Id 本身是为了让 <c>JsonSerializer.Serialize(id, options)</c> 有根类型可解析；
/// 形态由 <c>WithStronglyTypedIds()</c> 补上的 resolver 决定，见 <see cref="Program.JsonViaSourceGeneratedContext"/>。
/// </remarks>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(Guid))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(AotProductId))]
[JsonSerializable(typeof(AotOrderId))]
[JsonSerializable(typeof(AotPrefixedId))]
internal sealed partial class SmokeTestJsonContext : JsonSerializerContext
{
}
