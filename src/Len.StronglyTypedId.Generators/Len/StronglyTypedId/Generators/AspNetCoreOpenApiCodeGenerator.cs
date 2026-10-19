using System.Text;

namespace Len.StronglyTypedId.Generators;

/// <summary>
/// 为强类型 Id 生成 .NET 9+ 内置 OpenAPI（<c>Microsoft.AspNetCore.OpenApi</c>）的 schema 转换器注册入口。
/// </summary>
/// <remarks>
/// 内置 OpenAPI 的 schema 由 STJ 驱动，Id 已是基元形态，但 STJ 不会补 <c>format: uuid</c> 这类语义，
/// 故用 <c>IOpenApiSchemaTransformer</c> 把类型 / format 写进文档，与 Swashbuckle 侧保持一致。
/// </remarks>
internal class AspNetCoreOpenApiCodeGenerator : IIntegrationCodeGenerator
{
    internal static readonly IIntegrationCodeGenerator Instance = new AspNetCoreOpenApiCodeGenerator();

    public int Order { get; } = 6;

    public void Generate(
        DiscoveredIds discoveredIds,
        ImmutableArray<ModuleInfo> modules,
        SourceProductionContext context,
        string version)
    {
        var stronglyTypedIds = discoveredIds.External.ToList();

        var schemaEntries = stronglyTypedIds
            .Select(idInfo => $"            [typeof({idInfo.FullyQualifiedName})] = ({GetTypeLiteral(idInfo)}, {GetFormatLiteral(idInfo)}),")
            .ToArray();

        var code = GeneratedCode.Header("Len.StronglyTypedId") + $$"""
            internal static partial class StronglyTypedIds
            {
                /// <inheritdoc/>
                {{GeneratedCode.CompilerGeneratedAttribute}}
                {{GeneratedCode.GeneratedCodeAttribute(nameof(AspNetCoreOpenApiCodeGenerator), version)}}
                class StronglyTypedIdOpenApiSchemaTransformer : global::Microsoft.AspNetCore.OpenApi.IOpenApiSchemaTransformer
                {
                    private static readonly global::System.Collections.Generic.Dictionary<global::System.Type, (string Type, string? Format)> _schemas = new()
                    {
            {{string.Join("\n", schemaEntries)}}
                    };

                    /// <inheritdoc/>
                    {{GeneratedCode.CompilerGeneratedAttribute}}
                    {{GeneratedCode.GeneratedCodeAttribute(nameof(AspNetCoreOpenApiCodeGenerator), version)}}
                    public global::System.Threading.Tasks.Task TransformAsync(
                        global::Microsoft.OpenApi.Models.OpenApiSchema schema,
                        global::Microsoft.AspNetCore.OpenApi.OpenApiSchemaTransformerContext context,
                        global::System.Threading.CancellationToken cancellationToken)
                    {
                        if (context.JsonTypeInfo?.Type is { } type && _schemas.TryGetValue(type, out var entry))
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
    /// 取 OpenAPI <c>schema.Type</c> 的字面量（字符串形态，对应 Microsoft.OpenApi 1.x 的 <c>string?</c> 字段）。
    /// </summary>
    /// <remarks>有前缀时对外一律是字符串，故类型恒为 <c>string</c>。</remarks>
    private static string GetTypeLiteral(StronglyTypedIdInfo idInfo)
        => idInfo.HasPrefix
            ? "\"string\""
            : StronglyTypedIdInfo.GetPrimitiveIdTypeShortName(idInfo.PrimitiveIdTypeName) switch
            {
                "Guid" or "string" => "\"string\"",
                _ => "\"integer\"",
            };

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
