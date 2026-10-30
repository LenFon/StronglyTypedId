namespace Len.StronglyTypedId;

/// <summary>
/// 运行时 <c>StronglyTypedIdIntegrations</c> 的镜像值。
/// </summary>
/// <remarks>
/// 生成器与分析器不引用运行时程序集，只能按底层整数匹配；取值必须与 <c>StronglyTypedIdIntegrations</c> 逐位一致。
/// </remarks>
internal static class IntegrationFlags
{
    /// <summary>
    /// 未指定。只用于区分「没写这个参数」与「写了但什么都没选」，不参与掩码运算。
    /// </summary>
    internal const int Default = 0;

    /// <summary>
    /// 显式关闭全部集成；只作「已指定」的标记，参与掩码时会被剔除。
    /// </summary>
    internal const int None = 1 << 0;

    /// <summary>
    /// Entity Framework Core 集成。
    /// </summary>
    internal const int EntityFrameworkCore = 1 << 1;

    /// <summary>
    /// Dapper 集成。
    /// </summary>
    internal const int Dapper = 1 << 2;

    /// <summary>
    /// System.Text.Json 集成。
    /// </summary>
    internal const int SystemTextJson = 1 << 3;

    /// <summary>
    /// Newtonsoft.Json 集成。
    /// </summary>
    internal const int NewtonsoftJson = 1 << 4;

    /// <summary>
    /// Swashbuckle 集成。
    /// </summary>
    internal const int Swagger = 1 << 5;

    /// <summary>
    /// ASP.NET Core OpenAPI 集成。
    /// </summary>
    internal const int AspNetCoreOpenApi = 1 << 6;

    /// <summary>
    /// ASP.NET Core MVC 集成（含 Minimal API 路由约束）。
    /// </summary>
    internal const int AspNetCoreMvc = 1 << 7;

    /// <summary>
    /// MessagePack 集成。
    /// </summary>
    internal const int MessagePack = 1 << 8;

    /// <summary>
    /// MongoDB BSON 集成。
    /// </summary>
    internal const int MongoDb = 1 << 9;

    /// <summary>
    /// HotChocolate（GraphQL）集成。
    /// </summary>
    internal const int HotChocolate = 1 << 10;

    /// <summary>
    /// protobuf-net 集成。
    /// </summary>
    internal const int Protobuf = 1 << 11;

    /// <summary>
    /// 全部集成，等价于「引用了就生成」。
    /// </summary>
    internal const int All = EntityFrameworkCore | Dapper | SystemTextJson | NewtonsoftJson | Swagger | AspNetCoreOpenApi | AspNetCoreMvc | MessagePack | MongoDb | HotChocolate | Protobuf;

    /// <summary>
    /// 把特性上的原始值规范成掩码：未指定返回 <see langword="null"/>，显式 <c>Default</c> 归一为
    /// <see cref="All"/>，其余剔除 <see cref="None"/> 标记位。
    /// </summary>
    internal static int? Normalize(int? raw)
        => raw switch
        {
            null => null,
            0 => All,
            var value => value & ~None,
        };
}
