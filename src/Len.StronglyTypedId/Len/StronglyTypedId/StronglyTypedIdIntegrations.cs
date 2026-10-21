namespace Len.StronglyTypedId;

/// <summary>
/// 指定为强类型 Id 生成哪些集成代码。
/// </summary>
/// <remarks>
/// 这是一道<b>过滤器</b>，不是开关：只有在当前编译确实引用了对应程序集时才会生成代码，
/// 本枚举只能在此基础上进一步收窄，无法凭空造出依赖。
/// <para>
/// 未指定该参数时按「引用了就生成」处理（即 <see cref="All"/> 的行为），与引入本枚举之前一致。
/// </para>
/// </remarks>
[Flags]
public enum StronglyTypedIdIntegrations
{
    /// <summary>
    /// 未指定。表示「沿用程序集级默认值；若也没有，则按引用生成」。这是默认值。
    /// </summary>
    /// <remarks>
    /// 该值只用于区分「没写这个参数」与「写了但什么都没选」，因此 <see cref="None"/> 必须占用一个独立的位。
    /// </remarks>
    Default = 0,

    /// <summary>
    /// 显式关闭全部集成。核心实现（工厂、解析、比较、格式化）不受影响，
    /// 显式打开的 <see cref="StronglyTypedIdAttribute.TypeConverter"/> 也不受影响。
    /// </summary>
    /// <remarks>
    /// 该位只作「已显式指定」的标记，与其他标志组合时不产生额外效果：
    /// <c>None | Dapper</c> 等价于 <c>Dapper</c>。
    /// </remarks>
    None = 1 << 0,

    /// <summary>
    /// Entity Framework Core 的值转换器与 <c>ApplyTo(ModelConfigurationBuilder)</c> /
    /// <c>ApplyTo(ModelBuilder)</c>。需要引用 <c>Microsoft.EntityFrameworkCore</c> 7.0.0 及以上，
    /// 且存在重写了 <c>ConfigureConventions</c> 的 <c>DbContext</c>。
    /// </summary>
    EntityFrameworkCore = 1 << 1,

    /// <summary>
    /// Dapper 的 <c>SqlMapper.TypeHandler&lt;TId&gt;</c> 与 <c>ApplyTo()</c>。注册是进程级全局的，
    /// 调用一次即可。需要引用 <c>Dapper</c> 2.0.0 及以上。
    /// </summary>
    Dapper = 1 << 2,

    /// <summary>
    /// System.Text.Json 的转换器、<c>[JsonConverter]</c> 特性与
    /// <c>ApplyTo(JsonSerializerOptions)</c>。需要引用 <c>System.Text.Json</c>。
    /// </summary>
    SystemTextJson = 1 << 3,

    /// <summary>
    /// Newtonsoft.Json 的转换器、<c>[JsonConverter]</c> 特性与
    /// <c>AddStronglyTypedId(JsonSerializerSettings)</c>。需要引用 <c>Newtonsoft.Json</c> 13.0.0 及以上。
    /// </summary>
    NewtonsoftJson = 1 << 4,

    /// <summary>
    /// Swashbuckle 的 <c>ApplyTo(SwaggerGenOptions)</c>。需要引用
    /// <c>Swashbuckle.AspNetCore.SwaggerGen</c> 6.0.0 及以上。
    /// </summary>
    Swagger = 1 << 5,

    /// <summary>
    /// ASP.NET Core OpenAPI 的 <c>ApplyTo(OpenApiOptions)</c>。需要引用
    /// <c>Microsoft.AspNetCore.OpenApi</c> 9.0.0 及以上。
    /// </summary>
    AspNetCoreOpenApi = 1 << 6,

    /// <summary>
    /// ASP.NET Core MVC 的模型绑定与路由约束（<c>ApplyTo(MvcOptions)</c> /
    /// <c>ApplyTo(RouteOptions)</c>，同时支撑 Minimal API 的 <c>{id:OrderId}</c> 约束）。
    /// 需要引用 <c>Microsoft.AspNetCore.Mvc</c> 2.0.0 及以上（或其 <c>.Core</c> / <c>.Abstractions</c> 门面）。
    /// </summary>
    AspNetCoreMvc = 1 << 7,

    /// <summary>
    /// MessagePack 的 <c>IMessagePackFormatter&lt;TId&gt;</c>、<c>[MessagePackFormatter]</c> 特性与
    /// <c>ApplyTo(MessagePackSerializerOptions)</c>。需要引用 <c>MessagePack</c> 2.0.0 及以上。
    /// </summary>
    /// <remarks>
    /// 二进制格式存放的是基元值本身，文本前缀只作用于文本形（<c>ToString()</c> / JSON），不进入二进制载荷。
    /// </remarks>
    MessagePack = 1 << 8,

    /// <summary>
    /// MongoDB BSON 的 <c>IBsonSerializer&lt;TId&gt;</c>、<c>[BsonSerializer]</c> 特性与
    /// <c>ApplyTo()</c>。需要引用 <c>MongoDB.Bson</c> 3.0.0 及以上。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="MessagePack"/> 同理：二进制格式存放基元值本身，文本前缀不进入载荷。
    /// </remarks>
    MongoDb = 1 << 9,

    /// <summary>
    /// 全部集成。仍受「是否引用了对应程序集」这一道门控约束。
    /// </summary>
    All = EntityFrameworkCore | Dapper | SystemTextJson | NewtonsoftJson | Swagger | AspNetCoreOpenApi | AspNetCoreMvc | MessagePack | MongoDb,
}
