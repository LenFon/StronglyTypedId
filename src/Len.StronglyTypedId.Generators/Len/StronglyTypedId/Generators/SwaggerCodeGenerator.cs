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

        var swaggerIds = discoveredIds.ExternalFor(IntegrationFlags.Swagger).ToList();

        // 空注册入口没有任何作用：Integrations = None 时必须连这个文件也不产出。
        if (swaggerIds.Count == 0)
        {
            return;
        }

        // MapType 底层是 Add，同一类型注册两次会抛「重复键」，而 ApplyTo 必须幂等（调用方常在多个模块各调一次）。
        // 守卫走 CustomTypeMappings：已存在的类型不覆盖，使用者自己注册的那份保持有效。
        // MapType<T> 是该命名空间下的扩展方法，缺 using 会 CS1061；注册语句经插值洞注入，故每行自带绝对缩进。
        var guardedRegistrations = string.Join("\n\n", swaggerIds.Select(idInfo =>
            _registrationIndent + "if (!schemaMappings.ContainsKey(typeof(" + idInfo.FullyQualifiedName + ")))"
            + "\n"
            + _registrationIndent + "{"
            + "\n"
            + _registrationIndent + "    options.MapType<" + idInfo.FullyQualifiedName + ">(() => " + CreateOpenApiSchema(idInfo, useOpenApiV2) + ");"
            + "\n"
            + _registrationIndent + "}"));

        // 操作过滤器补的是 Minimal API 的参数：框架把实现了 IParsable 的参数按 string 报给 ApiExplorer，
        // Swashbuckle 据此生成 schema，MapType 对这类参数无从下手（详见 StronglyTypedIdOperationFilter 的说明）。
        // 表里存的是工厂而不是现成的 schema：每个参数拿到的必须是自己的实例，共用同一个可变对象，
        // 一旦框架给它补了描述信息，所有参数都会跟着变。
        var schemaEntries = swaggerIds
            .Select(idInfo => $"            [typeof({idInfo.FullyQualifiedName})] = () => {CreateOpenApiSchema(idInfo, useOpenApiV2)},")
            .ToList();

        var schemaTable = string.Join("\n", schemaEntries);

        var code = GeneratedCode.Header("Len.StronglyTypedId", "// Required by SwaggerGenOptions.MapType<T>, an extension method declared in this namespace.\nusing Microsoft.Extensions.DependencyInjection;") + $$"""
            internal static partial class StronglyTypedIds
            {
                /// <summary>
                /// 在 Swagger 中启用强类型 Id。
                /// </summary>
                /// <param name="options"><see cref="global::Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions"/></param>
                {{GeneratedCode.CompilerGeneratedAttribute}}
                {{GeneratedCode.GeneratedCodeAttribute(nameof(SwaggerCodeGenerator), version)}}
                public static void ApplyTo(global::Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions options)
                {
                    var schemaMappings = options.SchemaGeneratorOptions.CustomTypeMappings;

            {{guardedRegistrations}}

                    // Minimal API 的参数另需补一次 schema：MapType 只影响按类型登记的组件 schema，
                    // 而框架已经把 IParsable 参数的 ModelMetadata 换成了 string。
                    var generatorOptions = options.SwaggerGeneratorOptions;

                    var alreadyRegistered = false;

                    foreach (var filter in generatorOptions.OperationFilters)
                    {
                        if (filter is StronglyTypedIdOperationFilter)
                        {
                            alreadyRegistered = true;

                            break;
                        }
                    }

                    if (!alreadyRegistered)
                    {
                        generatorOptions.OperationFilters.Add(new StronglyTypedIdOperationFilter());
                    }
                }

                /// <summary>
                /// 还原作为 Minimal API 参数使用的强类型 Id 的 schema。
                /// </summary>
                /// <remarks>
                /// API explorer 会把实现 <c>IParsable</c> 的参数报告成以基元为模型元数据，Swashbuckle 因此把这类参数
                /// 描述成普通 <c>string</c>，<c>MapType</c> 注册对它不生效。参数描述仍保留声明类型，本过滤器据此查找。
                /// </remarks>
                private sealed class StronglyTypedIdOperationFilter : global::Swashbuckle.AspNetCore.SwaggerGen.IOperationFilter
                {
                    private static readonly global::System.Collections.Generic.Dictionary<global::System.Type, global::System.Func<{{GetOpenApiSchemaTypeName(useOpenApiV2)}}>> _schemas = new()
                    {
            {{schemaTable}}
                    };

                    /// <inheritdoc/>
                    {{GeneratedCode.CompilerGeneratedAttribute}}
                    {{GeneratedCode.GeneratedCodeAttribute(nameof(SwaggerCodeGenerator), version)}}
                    public void Apply({{GetOpenApiOperationTypeName(useOpenApiV2)}} operation, global::Swashbuckle.AspNetCore.SwaggerGen.OperationFilterContext context)
                    {
                        if (operation.Parameters is null || operation.Parameters.Count == 0)
                        {
                            return;
                        }

                        foreach (var parameterDescription in context.ApiDescription.ParameterDescriptions)
                        {
                            if (!_schemas.TryGetValue(parameterDescription.Type, out var factory))
                            {
                                continue;
                            }

                            foreach (var parameter in operation.Parameters)
                            {
                                if (!string.Equals(parameter.Name, parameterDescription.Name, global::System.StringComparison.OrdinalIgnoreCase))
                                {
                                    continue;
                                }

                                ApplySchema(parameter, factory());

                                break;
                            }
                        }
                    }

                    /// <summary>
                    /// 把 schema 写入参数。
                    /// </summary>
                    /// <remarks>
                    /// Microsoft.OpenApi 2.x 通过 <c>IOpenApiParameter</c> 暴露参数，其成员全为只读，schema 只能写在具体的
                    /// <c>OpenApiParameter</c> 上；1.x 直接给出具体类型。
                    /// </remarks>
                    private static void ApplySchema({{GetOpenApiParameterTypeName(useOpenApiV2)}} parameter, {{GetOpenApiSchemaTypeName(useOpenApiV2)}} schema)
                    {
            {{GetApplySchemaBody(useOpenApiV2)}}
                    }
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
    /// <remarks>
    /// <c>MapType</c> 与操作过滤器共用这一份口径，同一个 Id 在组件 schema 与参数 schema 上必须一致。
    /// </remarks>
    private static string CreateOpenApiSchema(StronglyTypedIdInfo idInfo, bool useOpenApiV2)
    {
        var schemaTypeName = useOpenApiV2
            ? "global::Microsoft.OpenApi.OpenApiSchema"
            : "global::Microsoft.OpenApi.Models.OpenApiSchema";

        var (type, format) = PrimitiveIdSchemaMap.Resolve(idInfo);

        // 1.x 的 Type 是字符串字面量；2.x 的 Type 是 Microsoft.OpenApi.JsonSchemaType 枚举。
        var typeExpression = GetTypeExpression(type, useOpenApiV2);

        var primitiveSchema = format is null
            ? $$"""new {{schemaTypeName}} { Type = {{typeExpression}} }"""
            : $$"""new {{schemaTypeName}} { Type = {{typeExpression}}, Format = "{{format}}" }""";

        if (!idInfo.IsObjectJsonShape)
        {
            return primitiveSchema;
        }

        // JsonShape.Object 的线上报文是 {"value": <基元>}，schema 必须照着写，否则生成的客户端按裸基元收发。
        // 2.x 的 Properties 值是 IOpenApiSchema（接口），不能用 Dictionary<string, OpenApiSchema> 顶替，泛型不变。
        var propertyValueType = useOpenApiV2
            ? "global::Microsoft.OpenApi.IOpenApiSchema"
            : schemaTypeName;

        var objectTypeExpression = useOpenApiV2
            ? "global::Microsoft.OpenApi.JsonSchemaType.Object"
            : "\"object\"";

        return $"new {schemaTypeName} {{ Type = {objectTypeExpression}, "
            + $"Properties = new global::System.Collections.Generic.Dictionary<string, {propertyValueType}> {{ [\"value\"] = {primitiveSchema} }}, "
            + "Required = new global::System.Collections.Generic.HashSet<string> { \"value\" } }";
    }

    /// <summary>
    /// 取 <c>OpenApiOperation</c> 的类型名：1.x 在 <c>Microsoft.OpenApi.Models</c> 下，2.x 提到了
    /// <c>Microsoft.OpenApi</c> 根命名空间。
    /// </summary>
    private static string GetOpenApiOperationTypeName(bool useOpenApiV2)
        => useOpenApiV2
            ? "global::Microsoft.OpenApi.OpenApiOperation"
            : "global::Microsoft.OpenApi.Models.OpenApiOperation";

    /// <summary>
    /// 取参数的类型名：1.x 的 <c>OpenApiOperation.Parameters</c> 里装的是具体类，2.x 换成了接口
    /// <c>IOpenApiParameter</c>。
    /// </summary>
    private static string GetOpenApiParameterTypeName(bool useOpenApiV2)
        => useOpenApiV2
            ? "global::Microsoft.OpenApi.IOpenApiParameter"
            : "global::Microsoft.OpenApi.Models.OpenApiParameter";

    /// <summary>
    /// 取「把 schema 写到参数上」的方法体。
    /// </summary>
    /// <remarks>
    /// 2.x 的接口成员全是只读的，只能落到具体类上写；遇到别的实现就跳过，那不是本库能替它决定的。
    /// 方法体嵌在 <c>StronglyTypedIdOperationFilter</c> 里，产物中的缩进是 12 空格，各行自带。
    /// </remarks>
    private static string GetApplySchemaBody(bool useOpenApiV2)
        => useOpenApiV2
            ? "            if (parameter is global::Microsoft.OpenApi.OpenApiParameter concreteParameter)\n"
                + "            {\n"
                + "                concreteParameter.Schema = schema;\n"
                + "            }"
            : "            parameter.Schema = schema;";

    /// <summary>
    /// 取 schema 的类型名：1.x 的 <c>OpenApiParameter.Schema</c> 是具体类，2.x 换成了接口
    /// <c>IOpenApiSchema</c>，工厂的返回类型必须与之一致。
    /// </summary>
    private static string GetOpenApiSchemaTypeName(bool useOpenApiV2)
        => useOpenApiV2
            ? "global::Microsoft.OpenApi.IOpenApiSchema"
            : "global::Microsoft.OpenApi.Models.OpenApiSchema";

    /// <summary>
    /// 取 OpenAPI <c>schema.Type</c> 的表达式。
    /// </summary>
    private static string GetTypeExpression(string type, bool useOpenApiV2)
        => useOpenApiV2
            ? type switch
            {
                PrimitiveIdSchemaMap.String => "global::Microsoft.OpenApi.JsonSchemaType.String",
                PrimitiveIdSchemaMap.Integer => "global::Microsoft.OpenApi.JsonSchemaType.Integer",
                PrimitiveIdSchemaMap.Number => "global::Microsoft.OpenApi.JsonSchemaType.Number",
                _ => "global::Microsoft.OpenApi.JsonSchemaType.String",
            }
            : "\"" + type + "\"";
}
