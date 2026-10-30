namespace Len.StronglyTypedId.Generators;

/// <summary>
/// Swashbuckle（Swagger）与内置 OpenAPI 两个生成器共用的 Microsoft.OpenApi 版本判定与类型名。
/// </summary>
/// <remarks>
/// 两个集成都要在 Microsoft.OpenApi 1.x 与 2.x 之间切换：2.x 把类型提到了根命名空间、把
/// <c>Type</c> 从字符串换成 <c>JsonSchemaType</c> 枚举、把参数与 schema 换成接口。这些差异
/// 只影响生成文本，与具体集成无关，集中在这里避免两处各写一份而漂移。
/// </remarks>
internal static class OpenApiSchemaNames
{
    /// <summary>
    /// Microsoft.OpenApi 的程序集名。
    /// </summary>
    private const string OpenApiAssemblyName = "Microsoft.OpenApi.dll";

    /// <summary>
    /// 判断当前编译引用的是否为 Microsoft.OpenApi 2.x。
    /// </summary>
    public static bool UsesVersion2(ImmutableArray<ModuleInfo> modules)
        => modules.HasModule(OpenApiAssemblyName, 2);

    /// <summary>
    /// 取具体的 <c>OpenApiSchema</c> 类型名（可 new 的那个）。
    /// </summary>
    public static string ConcreteSchemaType(bool useVersion2)
        => useVersion2
            ? "global::Microsoft.OpenApi.OpenApiSchema"
            : "global::Microsoft.OpenApi.Models.OpenApiSchema";

    /// <summary>
    /// 取 schema 的接口类型名：2.x 是 <c>IOpenApiSchema</c>，1.x 没有接口、直接用具体类。
    /// </summary>
    /// <remarks>
    /// 用作字典值类型与工厂返回类型：2.x 的 <c>Properties</c> 值是接口，泛型不变，不能用具体类顶替。
    /// </remarks>
    public static string SchemaInterfaceType(bool useVersion2)
        => useVersion2
            ? "global::Microsoft.OpenApi.IOpenApiSchema"
            : ConcreteSchemaType(useVersion2);

    /// <summary>
    /// 取 <c>OpenApiOperation</c> 的类型名：1.x 在 <c>Microsoft.OpenApi.Models</c> 下，2.x 提到了根命名空间。
    /// </summary>
    public static string OperationType(bool useVersion2)
        => useVersion2
            ? "global::Microsoft.OpenApi.OpenApiOperation"
            : "global::Microsoft.OpenApi.Models.OpenApiOperation";

    /// <summary>
    /// 取参数的类型名：1.x 的 <c>OpenApiOperation.Parameters</c> 里装的是具体类，2.x 换成了接口。
    /// </summary>
    public static string ParameterType(bool useVersion2)
        => useVersion2
            ? "global::Microsoft.OpenApi.IOpenApiParameter"
            : "global::Microsoft.OpenApi.Models.OpenApiParameter";

    /// <summary>
    /// 取 <c>schema.Type</c> 的类型名：1.x 是字符串，2.x 是 <c>JsonSchemaType</c> 枚举。
    /// </summary>
    public static string SchemaTypeValueType(bool useVersion2)
        => useVersion2
            ? "global::Microsoft.OpenApi.JsonSchemaType"
            : "string";

    /// <summary>
    /// 取 OpenAPI <c>schema.Type</c> 的表达式。
    /// </summary>
    /// <param name="type">来自 <see cref="PrimitiveIdSchemaMap"/> 的类型关键字。</param>
    /// <param name="useVersion2">是否面向 Microsoft.OpenApi 2.x。</param>
    public static string TypeLiteral(string type, bool useVersion2)
        => useVersion2
            ? type switch
            {
                PrimitiveIdSchemaMap.String => "global::Microsoft.OpenApi.JsonSchemaType.String",
                PrimitiveIdSchemaMap.Integer => "global::Microsoft.OpenApi.JsonSchemaType.Integer",
                PrimitiveIdSchemaMap.Number => "global::Microsoft.OpenApi.JsonSchemaType.Number",
                _ => "global::Microsoft.OpenApi.JsonSchemaType.String",
            }
            : "\"" + type + "\"";

    /// <summary>
    /// 取 OpenAPI <c>schema.Format</c> 的表达式；无特定格式时为 <c>null</c>。
    /// </summary>
    public static string FormatLiteral(string? format)
        => format is null ? "null" : "\"" + format + "\"";

    /// <summary>
    /// 取对象形态 schema 的 <c>Type</c> 表达式（<c>JsonShape.Object</c> 的线上报文是 <c>{"value": …}</c>）。
    /// </summary>
    public static string ObjectTypeLiteral(bool useVersion2)
        => useVersion2
            ? "global::Microsoft.OpenApi.JsonSchemaType.Object"
            : "\"object\"";
}
