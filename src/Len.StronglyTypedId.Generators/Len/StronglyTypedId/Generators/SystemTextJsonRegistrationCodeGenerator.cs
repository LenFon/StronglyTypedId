namespace Len.StronglyTypedId.Generators;

/// <summary>
/// 为强类型 Id 生成 System.Text.Json 的全局注册入口。
/// </summary>
/// <remarks>
/// <para>
/// 每个强类型 Id 自身已带 <c>[JsonConverter]</c> 特性，STJ 默认就能正确处理它；本生成器补的是
/// <c>StronglyTypedIds.ApplyTo(JsonSerializerOptions)</c> —— EF / Swagger / Dapper / MVC / 内置 OpenAPI
/// 都有 <c>ApplyTo</c>，唯独 STJ 缺一个，接口不对称。显式注册在「特性不被采纳」的场景下才有实际作用
/// （例如使用者显式传入自己的 <c>JsonSerializerOptions</c> 并希望把本库的转换器一并挂上）。
/// </para>
/// <para>
/// 注册的是各 Id 内嵌的 <c>SystemTextJsonConverter</c>（<c>public</c>，因为注册入口按 <c>External</c> 取全集，
/// 含来自被引用程序集的 Id，跨程序集实例化要求它对外可见），因此字典键支持
/// （<c>ReadAsPropertyName</c> / <c>WriteAsPropertyName</c>）随之一并生效。
/// </para>
/// <para>
/// 生成文件只在引用了 <c>System.Text.Json.dll</c> 时才产出，与核心 STJ 生成器同一门控。
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
        var stronglyTypedIds = discoveredIds.External.ToList();

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
            {{string.Join("\r\n", registrations)}}
                }
            }
            """;

        context.AddSource("StronglyTypedIds.SystemTextJson.g.cs", code);
    }
}
