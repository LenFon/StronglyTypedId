using System.Text;

namespace Len.StronglyTypedId.Generators;

/// <summary>
/// 为强类型 Id 生成 .NET 9+ 内置 OpenAPI（<c>Microsoft.AspNetCore.OpenApi</c>）的 schema 转换器注册入口。
/// </summary>
/// <remarks>
/// 内置 OpenAPI 的 schema 由 STJ 驱动，Id 已是基元形态，但 STJ 不会补 <c>format: uuid</c> 这类语义，
/// 故用 <c>IOpenApiSchemaTransformer</c> 把类型 / format 写进文档，与 Swashbuckle 侧保持一致。
/// 路由 / 查询参数的 schema 不带 <c>JsonTypeInfo</c>，需回退到 <c>ParameterDescription.Type</c>；
/// 该属性自 <c>Microsoft.AspNetCore.OpenApi</c> 9.0 起可用，与本生成器的闸门一致。
/// </remarks>
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

                /// <summary>
                /// Employ strongly typed IDs in the built-in OpenAPI.
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
    /// <remarks>
    /// 只以 <c>Microsoft.OpenApi.dll</c> 的主版本为判据，与 <see cref="SwaggerCodeGenerator"/> 保持一致：
    /// <c>Microsoft.AspNetCore.OpenApi</c> 9.x 依赖 1.6.x，10.x 才升到 2.x，用它的版本作代理会随补丁版本漂移。
    /// </remarks>
    private static bool UsesMicrosoftOpenApiV2(ImmutableArray<ModuleInfo> modules)
        => modules.HasModule(OpenApiAssemblyName, 2);

    /// <summary>
    /// 取 OpenAPI <c>schema.Type</c> 的字面量。
    /// </summary>
    /// <remarks>
    /// 有前缀时对外一律是字符串，故类型恒为 string。1.x 的 <c>Type</c> 是 <c>string?</c>，
    /// 2.x 改为 <c>JsonSchemaType?</c> 枚举，故按主版本分支生成。
    /// </remarks>
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
    /// <remarks>有前缀时类型已是 <c>string</c>，不再带 <c>uuid</c> / <c>int32</c> 这类 format。</remarks>
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
