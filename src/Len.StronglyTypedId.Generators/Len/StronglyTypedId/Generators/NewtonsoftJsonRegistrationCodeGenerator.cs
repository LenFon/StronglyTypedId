namespace Len.StronglyTypedId.Generators;

/// <summary>
/// 为强类型 Id 生成 Newtonsoft.Json 的全局注册入口。
/// </summary>
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
                /// <summary>Employ strongly typed IDs in Newtonsoft.Json.</summary>
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
