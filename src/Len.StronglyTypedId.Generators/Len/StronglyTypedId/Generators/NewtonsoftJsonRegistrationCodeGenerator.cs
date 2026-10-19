namespace Len.StronglyTypedId.Generators;

/// <summary>
/// 为强类型 Id 生成 Newtonsoft.Json 的全局注册入口。
/// </summary>
/// <remarks>
/// <para>
/// 每个 Id 已带 <c>[JsonConverter]</c>，Newtonsoft 默认就能处理；本生成器补的是与 STJ 侧
/// <c>ApplyTo</c> 对称的 <c>settings.AddStronglyTypedId()</c>。注册的是各 Id 内嵌的 <c>public</c> 转换器
/// （跨程序集实例化要求可见）。
/// </para>
/// <para>
/// 按 <see cref="StronglyTypedIdInfo.HasNewtonsoftJsonConverter"/> 过滤：领域库不引 Newtonsoft、宿主引，
/// 是完全正常的分层，此时照常生成 <c>new X.NewtonsoftJsonConverter()</c> 会 CS0426；漏注册无损失，特性仍在。
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
        var stronglyTypedIds = discoveredIds.External
            .Where(static idInfo => idInfo.HasNewtonsoftJsonConverter)
            .ToList();

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
            {{string.Join("\n", registrations)}}
                }
            }
            """;

        context.AddSource("StronglyTypedIds.NewtonsoftJson.g.cs", GeneratedCode.NormalizeLineEndings(code));
    }
}
