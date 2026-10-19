namespace Len.StronglyTypedId.Generators;

/// <summary>
/// 为强类型 Id 生成 Swashbuckle.AspNetCore 的 <c>MapType</c> 注册。
/// </summary>
internal class SwaggerCodeGenerator : IIntegrationCodeGenerator
{
    private const string OpenApiAssemblyName = "Microsoft.OpenApi.dll";

    internal static readonly IIntegrationCodeGenerator Instance = new SwaggerCodeGenerator();

    public int Order { get; } = 4;

    public void Generate(
        DiscoveredIds discoveredIds,
        ImmutableArray<ModuleInfo> modules,
        SourceProductionContext context,
        string version)
    {
        var useOpenApiV2 = UsesMicrosoftOpenApiV2(modules);

        var mapTypeCalls = discoveredIds.External
            .Select(idInfo => $"options.MapType<{idInfo.FullyQualifiedName}>(() => {CreateOpenApiSchema(idInfo, useOpenApiV2)});");

        // MapType<T> 是该命名空间下的扩展方法，只能用 using 引入。Web SDK 的隐式 using 恰好含它，
        // 类库 / 控制台消费者缺此 using 会 CS1061；该命名空间由 Swashbuckle 的传递依赖提供。
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
                { {{$"\n\t\t{string.Join("\n\t\t", mapTypeCalls)}"}}
                }
            }
            """;

        context.AddSource("StronglyTypedIds.Swagger.g.cs", GeneratedCode.NormalizeLineEndings(code));
    }

    /// <summary>
    /// 判断当前编译引用的是否为 Microsoft.OpenApi 2.x。
    /// </summary>
    /// <remarks>
    /// 只以 <c>Microsoft.OpenApi.dll</c> 的主版本为判据。不能改用 Swashbuckle 版本作代理：7/8/9 都依赖
    /// Microsoft.OpenApi 1.6.x，直到 10.0.0 才升到 2.3.0，以 7 为界会生成 1.6.x 下不存在的类型。
    /// </remarks>
    private static bool UsesMicrosoftOpenApiV2(ImmutableArray<ModuleInfo> modules)
        => modules.HasModule(OpenApiAssemblyName, 2);

    /// <summary>
    /// 按 Microsoft.OpenApi 主版本生成 <c>OpenApiSchema</c> 初始化表达式。
    /// </summary>
    /// <remarks>
    /// 2.x（随 Swashbuckle 10+）把 OpenApiSchema 的命名空间迁到 <c>Microsoft.OpenApi</c>，并把 <c>Type</c>
    /// 由 <c>string</c> 改为 <c>JsonSchemaType?</c> 枚举，故按主版本分支生成。
    /// </remarks>
    private static string CreateOpenApiSchema(StronglyTypedIdInfo idInfo, bool useOpenApiV2)
    {
        var schemaTypeName = useOpenApiV2
            ? "global::Microsoft.OpenApi.OpenApiSchema"
            : "global::Microsoft.OpenApi.Models.OpenApiSchema";

        // 1.x 的 Type 是字符串字面量；2.x 的 Type 是 Microsoft.OpenApi.JsonSchemaType 枚举。
        string TypeExpression(string keyword) => useOpenApiV2
            ? keyword == "string"
                ? "global::Microsoft.OpenApi.JsonSchemaType.String"
                : "global::Microsoft.OpenApi.JsonSchemaType.Integer"
            : "\"" + keyword + "\"";

        string Schema(string typeExpression, string? format) =>
            format is null
                ? $$"""new {{schemaTypeName}} { Type = {{typeExpression}} }"""
                : $$"""new {{schemaTypeName}} { Type = {{typeExpression}}, Format = "{{format}}" }""";

        // 有前缀时规范文本形是字符串，故 schema 一律为 string 且不带 format ——
        // 数字 / 枚举 / 日期基元在有前缀时对外也是字符串。
        return idInfo.HasPrefix
            ? Schema(TypeExpression("string"), null)
            : StronglyTypedIdInfo.GetPrimitiveIdTypeShortName(idInfo.PrimitiveIdTypeName) switch
            {
                "Guid" => Schema(TypeExpression("string"), "uuid"),
                "int" => Schema(TypeExpression("integer"), "int32"),
                "long" => Schema(TypeExpression("integer"), "int64"),
                "uint" => Schema(TypeExpression("integer"), "uint32"),
                "ulong" => Schema(TypeExpression("integer"), "uint64"),
                "byte" => Schema(TypeExpression("integer"), "byte"),
                "sbyte" => Schema(TypeExpression("integer"), "sbyte"),
                "short" => Schema(TypeExpression("integer"), "int16"),
                "ushort" => Schema(TypeExpression("integer"), "uint16"),
                _ => Schema(TypeExpression("string"), null),
            };
    }
}
