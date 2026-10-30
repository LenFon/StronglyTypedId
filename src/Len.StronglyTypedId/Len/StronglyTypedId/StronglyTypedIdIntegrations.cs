namespace Len.StronglyTypedId;

/// <summary>
/// 指定为强类型 Id 生成哪些集成代码。
/// </summary>
/// <remarks>
/// 这是<b>过滤器</b>而非开关：只有当前编译确实引用了对应程序集时才会生成代码，本枚举只能在此基础上进一步收窄。
/// 未指定该参数时按 <see cref="All"/> 的行为处理。
/// </remarks>
[Flags]
public enum StronglyTypedIdIntegrations
{
    /// <summary>
    /// 未指定。表示「跟随程序集级默认值；若也没有，则生成编译所引用的一切」。这是默认值。
    /// </summary>
    /// <remarks>
    /// 它只用于区分「参数没写」与「写了但什么都没选」，因此 <see cref="None"/> 必须独占一个位。
    /// </remarks>
    Default = 0,

    /// <summary>
    /// 显式关闭全部集成。核心实现（工厂、解析、比较、格式化）不受影响，显式开启的
    /// <see cref="StronglyTypedIdAttribute.TypeConverter"/> 也不受影响。
    /// </summary>
    /// <remarks>
    /// 仅作为「显式指定」的标记；与其他标志组合时不产生额外效果：<c>None | Dapper</c> 等价于 <c>Dapper</c>。
    /// </remarks>
    None = 1 << 0,

    /// <summary>
    /// Entity Framework Core 值转换器，以及 <c>ModelConfigurationBuilder</c> / <c>ModelBuilder</c> 上的
    /// <c>AddStronglyTypedIds()</c>。需要引用 <c>Microsoft.EntityFrameworkCore</c> 7.0.0 或更高版本。
    /// </summary>
    EntityFrameworkCore = 1 << 1,

    /// <summary>
    /// Dapper 的 <c>SqlMapper.TypeHandler&lt;TId&gt;</c> 以及 <c>AddStronglyTypedIds()</c>。注册是进程级全局的，
    /// 调用一次即可。需要引用 <c>Dapper</c> 2.0.0 或更高版本。
    /// </summary>
    Dapper = 1 << 2,

    /// <summary>
    /// System.Text.Json 转换器、<c>[JsonConverter]</c> 特性以及 <c>JsonSerializerOptions</c> 上的
    /// <c>AddStronglyTypedIds()</c>。需要引用 <c>System.Text.Json</c>。
    /// </summary>
    SystemTextJson = 1 << 3,

    /// <summary>
    /// Newtonsoft.Json 转换器、<c>[JsonConverter]</c> 特性以及 <c>AddStronglyTypedIds(JsonSerializerSettings)</c>。
    /// 需要引用 <c>Newtonsoft.Json</c> 13.0.0 或更高版本。
    /// </summary>
    NewtonsoftJson = 1 << 4,

    /// <summary>
    /// Swashbuckle 的 <c>SwaggerGenOptions</c> 上的 <c>AddStronglyTypedIds()</c>。需要引用
    /// <c>Swashbuckle.AspNetCore.SwaggerGen</c> 6.0.0 或更高版本。
    /// </summary>
    Swagger = 1 << 5,

    /// <summary>
    /// ASP.NET Core OpenAPI 的 <c>OpenApiOptions</c> 上的 <c>AddStronglyTypedIds()</c>。需要引用
    /// <c>Microsoft.AspNetCore.OpenApi</c> 9.0.0 或更高版本。
    /// </summary>
    /// <remarks>
    /// 8.0.x 只有 <c>WithOpenApi()</c>，schema 转换器 API 到 9.0.0 才出现，因此 <c>net8.0</c> 上无法生成，
    /// 请改用 <see cref="Swagger"/>。
    /// </remarks>
    AspNetCoreOpenApi = 1 << 6,

    /// <summary>
    /// ASP.NET Core MVC 模型绑定与路由约束（<c>MvcOptions</c> / <c>RouteOptions</c> 上的
    /// <c>AddStronglyTypedIds()</c>，后者也支撑 Minimal API 的 <c>{id:MyId}</c> 约束）。需要引用
    /// <c>Microsoft.AspNetCore.Mvc</c> 2.0.0 或更高版本（或其 <c>.Core</c> / <c>.Abstractions</c> 外观包）。
    /// </summary>
    AspNetCoreMvc = 1 << 7,

    /// <summary>
    /// MessagePack 的 <c>IMessagePackFormatter&lt;TId&gt;</c> 以及
    /// <c>MessagePackSerializerOptions</c> 上的 <c>AddStronglyTypedIds()</c>。需要引用 <c>MessagePack</c>
    /// 2.0.0 或更高版本。
    /// </summary>
    /// <remarks>
    /// 格式化器放在生成的 <c>StronglyTypedIds</c> 类中，通过入口方法接线。载荷存基元值本身，文本前缀只影响
    /// <c>ToString()</c> 与 JSON；<c>BigInteger</c>、<c>Int128</c>、<c>UInt128</c> 例外，以带前缀的文本写出，
    /// 仅为兼容旧版本载荷。
    /// </remarks>
    MessagePack = 1 << 8,

    /// <summary>
    /// MongoDB BSON 的 <c>IBsonSerializer&lt;TId&gt;</c> 以及 <c>RegisterBsonSerializers()</c>。
    /// 需要引用 <c>MongoDB.Bson</c> 3.0.0 或更高版本。
    /// </summary>
    /// <remarks>
    /// 序列化器放在生成的 <c>StronglyTypedIds</c> 类中，通过入口方法注册。与 <see cref="MessagePack"/> 一样，
    /// 载荷存基元值本身；<c>BigInteger</c>、<c>Int128</c>、<c>UInt128</c> 因 BSON 无对应序列化器而以文本写出。
    /// </remarks>
    MongoDb = 1 << 9,

    /// <summary>
    /// HotChocolate（GraphQL）的 <c>ScalarType&lt;TId&gt;</c> 以及
    /// <c>AddStronglyTypedIds()</c>（<c>IRequestExecutorBuilder</c> 上的扩展方法）。
    /// 需要引用 <c>HotChocolate</c> 13.0.0 或更高版本。
    /// </summary>
    /// <remarks>
    /// scalar 放在生成的 <c>StronglyTypedIds</c> 类中，通过入口方法注册。表示口径与 JSON 一致：
    /// 文本基元（含带前缀的 Id）是 GraphQL <c>String</c>，整数与枚举是 <c>Int</c>，浮点是 <c>Float</c>。
    /// 枚举按底层整数而非成员名，因此同一个 Id 在 REST 与 GraphQL 两侧表示相同。
    /// </remarks>
    HotChocolate = 1 << 10,

    /// <summary>
    /// protobuf-net 的 surrogate 契约（<c>[ProtoContract(Surrogate = ...)]</c> + 转换运算符）。
    /// 需要引用 <c>protobuf-net</c> 3.0.0 或更高版本。
    /// </summary>
    /// <remarks>
    /// 与其余集成不同，surrogate 只能通过<b>特性</b>声明，而特性只能加在本次编译声明的类型上 ——
    /// 因此引用程序集里的 Id 拿不到 protobuf 支持。载荷存基元值本身；<c>Int128</c> / <c>UInt128</c> /
    /// <c>Half</c> / <c>DateTimeOffset</c> / <c>BigInteger</c> 与自定义基元没有原生表示，改存规范文本。
    /// </remarks>
    Protobuf = 1 << 11,

    /// <summary>
    /// 全部集成。仍受「对应程序集是否被引用」这一前置检查约束。
    /// </summary>
    All = EntityFrameworkCore | Dapper | SystemTextJson | NewtonsoftJson | Swagger | AspNetCoreOpenApi | AspNetCoreMvc | MessagePack | MongoDb | HotChocolate | Protobuf,
}
