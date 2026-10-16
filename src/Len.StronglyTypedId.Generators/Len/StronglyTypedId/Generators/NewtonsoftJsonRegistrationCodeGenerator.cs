namespace Len.StronglyTypedId.Generators;

/// <summary>
/// 为强类型 Id 生成 Newtonsoft.Json 的全局注册入口。
/// </summary>
/// <remarks>
/// <para>
/// 每个强类型 Id 自身已带 <c>[JsonConverter]</c> 特性，Newtonsoft 默认就能正确处理它；本生成器补的是
/// <c>settings.AddStronglyTypedId()</c> 扩展方法（<c>sample1</c> 里曾被注释掉的那行 API），与 STJ 侧的
/// <c>ApplyTo(JsonSerializerOptions)</c> 对称。显式注册在「特性不被采纳」或「希望统一挂载」的场景下才有实际作用。
/// </para>
/// <para>
/// 注册的是各 Id 内嵌的 <c>NewtonsoftJsonConverter</c>（<c>public</c>，因为注册入口按 <c>External</c> 取全集，
/// 含来自被引用程序集的 Id，跨程序集实例化要求它对外可见）。
/// </para>
/// <para>
/// 生成文件只在引用了 <c>Newtonsoft.Json.dll</c>（主版本 ≥ 13）时才产出，与核心 Newtonsoft 生成器同一门控。
/// </para>
/// </remarks>
internal class NewtonsoftJsonRegistrationCodeGenerator : IIntegrationCodeGenerator
{
    internal static readonly IIntegrationCodeGenerator Instance = new NewtonsoftJsonRegistrationCodeGenerator();

    public int Order { get; } = 9;

    public void Generate(
        DiscoveredIds discoveredIds,
        ImmutableArray<ModuleInfo> modules,
        SourceProductionContext context,
        string version)
    {
        var stronglyTypedIds = discoveredIds.External.ToList();

        if (stronglyTypedIds.Count == 0)
        {
            return;
        }

        var registrations = stronglyTypedIds
            .Select(idInfo => $"            settings.Converters.Add(new {idInfo.FullyQualifiedName}.NewtonsoftJsonConverter());");

        var code = GeneratedCode.Header("Len.StronglyTypedId") + $$"""
            internal static partial class StronglyTypedIds
            {
                /// <summary>
                /// Employ strongly typed IDs in Newtonsoft.Json.
                /// </summary>
                /// <param name="settings"><see cref="global::Newtonsoft.Json.JsonSerializerSettings"/>.</param>
                {{GeneratedCode.CompilerGeneratedAttribute}}
                {{GeneratedCode.GeneratedCodeAttribute(nameof(NewtonsoftJsonRegistrationCodeGenerator), version)}}
                public static void AddStronglyTypedId(this global::Newtonsoft.Json.JsonSerializerSettings settings)
                {
            {{string.Join("\r\n", registrations)}}
                }
            }
            """;

        context.AddSource("StronglyTypedIds.NewtonsoftJson.g.cs", code);
    }
}
