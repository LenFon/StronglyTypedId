namespace Len.StronglyTypedId.Generators;

/// <summary>
/// 为强类型 Id 生成 System.Text.Json 的全局注册入口。
/// </summary>
/// <remarks>
/// <para>
/// 每个 Id 已带 <c>[JsonConverter]</c>，STJ 默认就能处理；本生成器补的是与其它集成对称的
/// <c>StronglyTypedIds.ApplyTo(JsonSerializerOptions)</c>。注册的是各 Id 内嵌的 <c>public</c> 转换器
/// （跨程序集实例化要求可见），字典键支持随之生效。
/// </para>
/// <para>
/// 按 <see cref="StronglyTypedIdInfo.HasSystemTextJsonConverter"/> 过滤：声明程序集未引用 STJ 的 Id 没有内嵌转换器，
/// 照常生成 <c>new X.SystemTextJsonConverter()</c> 会 CS0426；漏注册无损失，特性仍在。
/// </para>
/// </remarks>
internal class SystemTextJsonRegistrationCodeGenerator : IIntegrationCodeGenerator
{
    internal static readonly IIntegrationCodeGenerator Instance = new SystemTextJsonRegistrationCodeGenerator();

    public int Order { get; } = 8;

    public void Generate(
        DiscoveredIds discoveredIds,
        ImmutableArray<ModuleInfo> modules,
        SourceProductionContext context,
        string version)
    {
        var stronglyTypedIds = discoveredIds.External
            .Where(static idInfo => idInfo.HasSystemTextJsonConverter)
            .ToList();

        // 一个 Id 都没有时不产出空壳类：既有行为是「无集成则不生成 StronglyTypedIds」，
        // 空注册入口只会平白多出一个成员。
        if (stronglyTypedIds.Count == 0)
        {
            return;
        }

        var registrations = stronglyTypedIds
            .Select(idInfo => $"            options.Converters.Add(new {idInfo.FullyQualifiedName}.SystemTextJsonConverter());");

        var code = GeneratedCode.Header("Len.StronglyTypedId") + $$"""
            internal static partial class StronglyTypedIds
            {
                /// <summary>
                /// Employ strongly typed IDs in System.Text.Json.
                /// </summary>
                /// <param name="options"><see cref="global::System.Text.Json.JsonSerializerOptions"/>.</param>
                {{GeneratedCode.CompilerGeneratedAttribute}}
                {{GeneratedCode.GeneratedCodeAttribute(nameof(SystemTextJsonRegistrationCodeGenerator), version)}}
                public static void ApplyTo(global::System.Text.Json.JsonSerializerOptions options)
                {
            {{string.Join("\n", registrations)}}
                }
            }
            """;

        context.AddSource("StronglyTypedIds.SystemTextJson.g.cs", GeneratedCode.NormalizeLineEndings(code));
    }
}
