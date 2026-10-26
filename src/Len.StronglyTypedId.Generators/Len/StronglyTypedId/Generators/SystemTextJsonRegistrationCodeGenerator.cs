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
                /// Employ strongly typed IDs in System.Text.Json.
                /// </summary>
                /// <remarks>Idempotent: a converter that is already registered is left alone, so calling this
                /// from several modules — or twice from the same one — does not grow the converter list.</remarks>
                /// <param name="options"><see cref="global::System.Text.Json.JsonSerializerOptions"/>.</param>
                {{GeneratedCode.CompilerGeneratedAttribute}}
                {{GeneratedCode.GeneratedCodeAttribute(nameof(SystemTextJsonRegistrationCodeGenerator), version)}}
                public static void ApplyTo(global::System.Text.Json.JsonSerializerOptions options)
                {
                    // 先收集已注册的转换器类型再逐个比对：重复调用时列表不增长，且已注册时完全不触碰集合 ——
                    // JsonSerializerOptions 一旦被使用就会冻结，此时再 Add 会抛 InvalidOperationException。
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
