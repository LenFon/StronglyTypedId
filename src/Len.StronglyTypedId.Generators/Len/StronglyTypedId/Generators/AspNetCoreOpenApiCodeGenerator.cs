using System.Text;

namespace Len.StronglyTypedId.Generators;

/// <summary>
/// 为强类型 Id 生成 .NET 9+ 内置 OpenAPI（<c>Microsoft.AspNetCore.OpenApi</c>）的 schema 转换器注册入口。
/// </summary>
internal class AspNetCoreOpenApiCodeGenerator : IIntegrationCodeGenerator
{
    private const string OpenApiAssemblyName = "Microsoft.OpenApi.dll";

    internal static readonly IIntegrationCodeGenerator Instance = new AspNetCoreOpenApiCodeGenerator();

    public int Order { get; } = 6;

    public void Generate(
        DiscoveredIds discoveredIds,
        ImmutableArray<ModuleInfo> modules,
        SourceProductionContext context,
        string version)
    {
        var useOpenApiV2 = UsesMicrosoftOpenApiV2(modules);

        var schemaEntries = discoveredIds.ExternalFor(IntegrationFlags.AspNetCoreOpenApi)
            .Select(idInfo =>
            {
                var (type, format) = PrimitiveIdSchemaMap.Resolve(idInfo);

                return $"            [typeof({idInfo.FullyQualifiedName})] = ({GetTypeLiteral(type, useOpenApiV2)}, {GetFormatLiteral(format)}, {(idInfo.IsObjectJsonShape ? "true" : "false")}),";
            })
            .ToArray();

        // 空注册入口没有任何作用：Integrations = None 时必须连这个文件也不产出。
        if (schemaEntries.Length == 0)
        {
            return;
        }

        var schemaTypeName = useOpenApiV2
            ? "global::Microsoft.OpenApi.OpenApiSchema"
            : "global::Microsoft.OpenApi.Models.OpenApiSchema";

        var schemaTypeValueType = useOpenApiV2
            ? "global::Microsoft.OpenApi.JsonSchemaType"
            : "string";

        // JsonShape.Object 的线上报文是 {"value": <基元>}。框架按 JsonTypeInfo 构建的 schema 认不出自定义转换器，
        // 只会留下空 schema，故这里显式写入对象形态；2.x 的 Properties 值是 IOpenApiSchema 接口（泛型不变）。
        var objectTypeLiteral = useOpenApiV2
            ? "global::Microsoft.OpenApi.JsonSchemaType.Object"
            : "\"object\"";

        var propertyValueType = useOpenApiV2
            ? "global::Microsoft.OpenApi.IOpenApiSchema"
            : schemaTypeName;

        var code = GeneratedCode.Header("Len.StronglyTypedId") + $$"""
            internal static partial class StronglyTypedIds
            {
                /// <inheritdoc/>
                {{GeneratedCode.CompilerGeneratedAttribute}}
                {{GeneratedCode.GeneratedCodeAttribute(nameof(AspNetCoreOpenApiCodeGenerator), version)}}
                class StronglyTypedIdOpenApiSchemaTransformer : global::Microsoft.AspNetCore.OpenApi.IOpenApiSchemaTransformer
                {
                    private static readonly global::System.Collections.Generic.Dictionary<global::System.Type, ({{schemaTypeValueType}} Type, string? Format, bool IsObject)> _schemas = new()
                    {
            {{string.Join("\n", schemaEntries)}}
                    };

                    /// <inheritdoc/>
                    {{GeneratedCode.CompilerGeneratedAttribute}}
                    {{GeneratedCode.GeneratedCodeAttribute(nameof(AspNetCoreOpenApiCodeGenerator), version)}}
                    public global::System.Threading.Tasks.Task TransformAsync(
                        {{schemaTypeName}} schema,
                        global::Microsoft.AspNetCore.OpenApi.OpenApiSchemaTransformerContext context,
                        global::System.Threading.CancellationToken cancellationToken)
                    {
                        // 参数 schema 由基元的 JsonTypeInfo 驱动（Id 在 JSON 里本就是基元），
                        // 真正的 Id 类型只在 ParameterDescription 上；请求 / 响应体则相反。
                        var parameterType = context.ParameterDescription?.Type;
                        var jsonType = context.JsonTypeInfo?.Type;

                        if ((parameterType is not null && _schemas.TryGetValue(parameterType, out var entry))
                            || (jsonType is not null && _schemas.TryGetValue(jsonType, out entry)))
                        {
                            if (entry.IsObject)
                            {
                                // 对象形态：{"value": <基元>}，value 必填。
                                schema.Type = {{objectTypeLiteral}};
                                schema.Format = null;
                                schema.Properties = new global::System.Collections.Generic.Dictionary<string, {{propertyValueType}}>
                                {
                                    ["value"] = new {{schemaTypeName}} { Type = entry.Type, Format = entry.Format },
                                };
                                schema.Required = new global::System.Collections.Generic.HashSet<string> { "value" };
                            }
                            else
                            {
                                schema.Type = entry.Type;
                                schema.Format = entry.Format;
                            }
                        }

                        return global::System.Threading.Tasks.Task.CompletedTask;
                    }
                }

                /// <summary>
                /// 在内置 OpenAPI 中启用强类型 Id。
                /// </summary>
                /// <param name="options"><see cref="global::Microsoft.AspNetCore.OpenApi.OpenApiOptions"/>.</param>
                {{GeneratedCode.CompilerGeneratedAttribute}}
                {{GeneratedCode.GeneratedCodeAttribute(nameof(AspNetCoreOpenApiCodeGenerator), version)}}
                public static void ApplyTo(global::Microsoft.AspNetCore.OpenApi.OpenApiOptions options) =>
                    options.AddSchemaTransformer(new StronglyTypedIdOpenApiSchemaTransformer());
            }
            """;

        context.AddSource("StronglyTypedIds.AspNetCoreOpenApi.g.cs", GeneratedCode.NormalizeLineEndings(code));
    }

    /// <summary>
    /// 判断当前编译引用的是否为 Microsoft.OpenApi 2.x。
    /// </summary>
    private static bool UsesMicrosoftOpenApiV2(ImmutableArray<ModuleInfo> modules)
        => modules.HasModule(OpenApiAssemblyName, 2);

    /// <summary>
    /// 取 OpenAPI <c>schema.Type</c> 的字面量。
    /// </summary>
    private static string GetTypeLiteral(string type, bool useOpenApiV2)
        => useOpenApiV2
            ? type switch
            {
                PrimitiveIdSchemaMap.String => "global::Microsoft.OpenApi.JsonSchemaType.String",
                PrimitiveIdSchemaMap.Integer => "global::Microsoft.OpenApi.JsonSchemaType.Integer",
                PrimitiveIdSchemaMap.Number => "global::Microsoft.OpenApi.JsonSchemaType.Number",
                _ => "global::Microsoft.OpenApi.JsonSchemaType.String",
            }
            : "\"" + type + "\"";

    /// <summary>
    /// 取 OpenAPI <c>schema.Format</c> 的字面量；无特定格式时为 <c>null</c>。
    /// </summary>
    private static string GetFormatLiteral(string? format)
        => format is null ? "null" : "\"" + format + "\"";
}
