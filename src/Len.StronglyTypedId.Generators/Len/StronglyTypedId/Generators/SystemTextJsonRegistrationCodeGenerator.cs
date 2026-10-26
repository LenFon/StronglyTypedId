namespace Len.StronglyTypedId.Generators;

/// <summary>
/// 为强类型 Id 生成 System.Text.Json 的全局注册入口。
/// </summary>
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
        var stronglyTypedIds = discoveredIds.ExternalFor(IntegrationFlags.SystemTextJson)
            .Where(static idInfo => idInfo.HasSystemTextJsonConverter)
            .ToList();

        // 一个 Id 都没有时不产出空壳类：既有行为是「无集成则不生成 StronglyTypedIds」，
        // 空注册入口只会平白多出一个成员。
        if (stronglyTypedIds.Count == 0)
        {
            return;
        }

        var registrations = stronglyTypedIds
            .Select(idInfo => $$"""
                        if (registered.Add(typeof({{idInfo.FullyQualifiedName}}.SystemTextJsonConverter)))
                        {
                            options.Converters.Add(new {{idInfo.FullyQualifiedName}}.SystemTextJsonConverter());
                        }
                """);

        var code = GeneratedCode.Header("Len.StronglyTypedId") + $$"""
            internal static partial class StronglyTypedIds
            {
                /// <summary>
                /// 在 System.Text.Json 中启用强类型 Id。
                /// </summary>
                /// <remarks>幂等：已注册的转换器保持原样，因此从多个模块调用，或从同一模块调用两次，都不会让转换器列表增长。</remarks>
                /// <param name="options"><see cref="global::System.Text.Json.JsonSerializerOptions"/>.</param>
                {{GeneratedCode.CompilerGeneratedAttribute}}
                {{GeneratedCode.GeneratedCodeAttribute(nameof(SystemTextJsonRegistrationCodeGenerator), version)}}
                public static void ApplyTo(global::System.Text.Json.JsonSerializerOptions options)
                {
                    // 幂等：已注册时完全不触碰集合，因为 JsonSerializerOptions 一旦被使用就会冻结，再 Add 会抛异常。
                    var registered = new global::System.Collections.Generic.HashSet<global::System.Type>();

                    foreach (var converter in options.Converters)
                    {
                        registered.Add(converter.GetType());
                    }

            {{string.Join("\n\n", registrations)}}
                }
            }
            """;

        context.AddSource("StronglyTypedIds.SystemTextJson.g.cs", GeneratedCode.NormalizeLineEndings(code));
    }
}
