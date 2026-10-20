namespace Len.StronglyTypedId.Generators;

/// <summary>
/// 为强类型 Id 生成 Swashbuckle.AspNetCore 的 <c>MapType</c> 注册。
/// </summary>
internal class SwaggerCodeGenerator : IIntegrationCodeGenerator
{
    private const string OpenApiAssemblyName = "Microsoft.OpenApi.dll";

    /// <summary>
    /// <c>ApplyTo</c> 方法体内语句的缩进（产物中为 8 空格）。
    /// </summary>
    private static readonly string _registrationIndent = new(' ', 8);

    internal static readonly IIntegrationCodeGenerator Instance = new SwaggerCodeGenerator();

    public int Order { get; } = 4;

    public void Generate(
        DiscoveredIds discoveredIds,
        ImmutableArray<ModuleInfo> modules,
        SourceProductionContext context,
        string version)
    {
        var useOpenApiV2 = UsesMicrosoftOpenApiV2(modules);

        var mapTypeCalls = discoveredIds.ExternalFor(IntegrationFlags.Swagger)
            .Select(idInfo => $"options.MapType<{idInfo.FullyQualifiedName}>(() => {CreateOpenApiSchema(idInfo, useOpenApiV2)});")
            .ToList();

        // 空注册入口没有任何作用：Integrations = None 时必须连这个文件也不产出。
        if (mapTypeCalls.Count == 0)
        {
            return;
        }

        // MapType<T> 是该命名空间下的扩展方法，只能用 using 引入。Web SDK 的隐式 using 恰好含它，
        // 类库 / 控制台消费者缺此 using 会 CS1061；该命名空间由 Swashbuckle 的传递依赖提供。
        //
        // 注册语句经插值洞注入，洞本身写在模板的去缩进基准列上，故每行必须自带产物中的绝对缩进。
        var registrations = string.Join("\n", mapTypeCalls.Select(call => _registrationIndent + call));

        var code = GeneratedCode.Header("Len.StronglyTypedId", "// Required by SwaggerGenOptions.MapType<T>, an extension method declared in this namespace.\nusing Microsoft.Extensions.DependencyInjection;") + $$"""
            internal static partial class StronglyTypedIds
            {
                /// <summary>
                /// Employ strongly typed IDs in Swagger.
                /// </summary>
                /// <param name="options"><see cref="global::Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions"/></param>
                {{GeneratedCode.CompilerGeneratedAttribute}}
                {{GeneratedCode.GeneratedCodeAttribute(nameof(SwaggerCodeGenerator), version)}}
                public static void ApplyTo(global::Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions options)
                {
            {{registrations}}
                }
            }
            """;

        context.AddSource("StronglyTypedIds.Swagger.g.cs", GeneratedCode.NormalizeLineEndings(code));
    }

    /// <summary>
    /// 判断当前编译引用的是否为 Microsoft.OpenApi 2.x。
    /// </summary>
    private static bool UsesMicrosoftOpenApiV2(ImmutableArray<ModuleInfo> modules)
        => modules.HasModule(OpenApiAssemblyName, 2);

    /// <summary>
    /// 按 Microsoft.OpenApi 主版本生成 <c>OpenApiSchema</c> 初始化表达式。
    /// </summary>
    private static string CreateOpenApiSchema(StronglyTypedIdInfo idInfo, bool useOpenApiV2)
    {
        var schemaTypeName = useOpenApiV2
            ? "global::Microsoft.OpenApi.OpenApiSchema"
            : "global::Microsoft.OpenApi.Models.OpenApiSchema";

        var (type, format) = PrimitiveIdSchemaMap.Resolve(idInfo);

        // 1.x 的 Type 是字符串字面量；2.x 的 Type 是 Microsoft.OpenApi.JsonSchemaType 枚举。
        var typeExpression = useOpenApiV2
            ? type switch
            {
                PrimitiveIdSchemaMap.String => "global::Microsoft.OpenApi.JsonSchemaType.String",
                PrimitiveIdSchemaMap.Integer => "global::Microsoft.OpenApi.JsonSchemaType.Integer",
                PrimitiveIdSchemaMap.Number => "global::Microsoft.OpenApi.JsonSchemaType.Number",
                _ => "global::Microsoft.OpenApi.JsonSchemaType.String",
            }
            : "\"" + type + "\"";

        return format is null
            ? $$"""new {{schemaTypeName}} { Type = {{typeExpression}} }"""
            : $$"""new {{schemaTypeName}} { Type = {{typeExpression}}, Format = "{{format}}" }""";
    }
}
