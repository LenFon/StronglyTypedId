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

        var schemaEntries = discoveredIds.External
            .Select(idInfo => $"            [typeof({idInfo.FullyQualifiedName})] = ({GetTypeLiteral(idInfo, useOpenApiV2)}, {GetFormatLiteral(idInfo)}),")
            .ToArray();

        var schemaTypeName = useOpenApiV2
            ? "global::Microsoft.OpenApi.OpenApiSchema"
            : "global::Microsoft.OpenApi.Models.OpenApiSchema";

        var schemaTypeValueType = useOpenApiV2
            ? "global::Microsoft.OpenApi.JsonSchemaType"
            : "string";

        var code = GeneratedCode.Header("Len.StronglyTypedId") + $$"""
            internal static partial class StronglyTypedIds
            {
                /// <inheritdoc/>
                {{GeneratedCode.CompilerGeneratedAttribute}}
                {{GeneratedCode.GeneratedCodeAttribute(nameof(AspNetCoreOpenApiCodeGenerator), version)}}
                class StronglyTypedIdOpenApiSchemaTransformer : global::Microsoft.AspNetCore.OpenApi.IOpenApiSchemaTransformer
                {
                    private static readonly global::System.Collections.Generic.Dictionary<global::System.Type, ({{schemaTypeValueType}} Type, string? Format)> _schemas = new()
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
                            schema.Type = entry.Type;
                            schema.Format = entry.Format;
                        }

                        return global::System.Threading.Tasks.Task.CompletedTask;
                    }
                }

                /// <summary>Employ strongly typed IDs in the built-in OpenAPI.</summary>
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
    private static string GetTypeLiteral(StronglyTypedIdInfo idInfo, bool useOpenApiV2)
    {
        var isString = idInfo.HasPrefix
            || StronglyTypedIdInfo.GetPrimitiveIdTypeShortName(idInfo.PrimitiveIdTypeName) is "Guid" or "string" or "char";

        return useOpenApiV2
            ? (isString ? "global::Microsoft.OpenApi.JsonSchemaType.String" : "global::Microsoft.OpenApi.JsonSchemaType.Integer")
            : (isString ? "\"string\"" : "\"integer\"");
    }

    /// <summary>
    /// 取 OpenAPI <c>schema.Format</c> 的字面量；无特定格式时为 <see langword="null"/>。
    /// </summary>
    private static string? GetFormatLiteral(StronglyTypedIdInfo idInfo)
        => idInfo.HasPrefix
            ? "null"
            : StronglyTypedIdInfo.GetPrimitiveIdTypeShortName(idInfo.PrimitiveIdTypeName) switch
            {
                "Guid" => "\"uuid\"",
                "int" => "\"int32\"",
                "long" => "\"int64\"",
                "uint" => "\"uint32\"",
                "ulong" => "\"uint64\"",
                "byte" => "\"byte\"",
                "sbyte" => "\"sbyte\"",
                "short" => "\"int16\"",
                "ushort" => "\"uint16\"",
                _ => "null",
            };
}
