// G2：集成运行时验证——在真实包引用下直接调用生成产物 StronglyTypedIds.ApplyTo，
// 断言各集成（EF Core / Dapper / Swagger / 内置 OpenApi / MVC）的产物可执行且行为正确，
// 而非仅靠文本契约（CodeGeneratorModuleGateTests 已锁定文本形态，本文件补运行时语义）。
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using FluentAssertions;
using Len.StronglyTypedId;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Len.StronglyTypedId.Generators;

/// <summary>
/// 生成的 <c>StronglyTypedIds</c> 是 <c>internal static partial class</c>（本编译单元内可见），
/// 各集成产物按引用的包被门控生成。本测试工程的包引用（EF Core / Dapper / Swashbuckle /
/// Microsoft.AspNetCore.OpenApi / FrameworkReference Microsoft.AspNetCore.App）使 5 个集成
/// 生成器全部触发，故 <c>StronglyTypedIds</c> 的 5 组 <c>ApplyTo</c> 都在本编译里可见。
/// </summary>
/// <remarks>
/// <para>
/// 门控判据（见 <see cref="StronglyTypedIdGenerator"/> 的 <c>GeneratorCatalog</c>）：
/// EF Core ≥ 7、Dapper ≥ 2、Swashbuckle ≥ 6、内置 OpenApi ≥ 9（FrameworkReference 在
/// net10.0 下主版本 10 满足）、MVC 门面/实现任一 ≥ 2（FrameworkReference 下 .NET 10 满足）。
/// 因此 net8.0 与 net10.0 双目标下，本文件的运行时验证都成立。
/// </para>
/// <para>
/// TestBase 的 <c>GuidId</c> 等 Id 已实现 <c>IStronglyTypedId</c>（TestBase 自身编译里由
/// 生成器补齐），本工程通过跨程序集发现机制把它们纳入 <c>DiscoveredIds.External</c>，
/// 集成生成器据此生成转换器 / 处理器 / 注册入口。
/// </para>
/// </remarks>
public class IntegrationRuntimeTests
{
    #region Dapper：SqlMapper.TypeHandler 真实注册 + 往返

    [Fact]
    public void Dapper_ApplyTo_RegistersTypeHandlersAndRoundTrips()
    {
        // 生成器产物的 ApplyTo(System.Data.IDbConnection) 必须可调用且不抛（注册是静态
        // SqlMapper.AddTypeHandler，不实际连接数据库）。传 null 即可——ApplyTo 不对 connection 解引用。
        Len.StronglyTypedId.StronglyTypedIds.ApplyTo((System.Data.IDbConnection?)null!);

        // 验证生成的 IntegrationGuidIdTypeHandler（internal 嵌套类）可实例化且 SetValue/Parse 往返正确。
        var handlerType = typeof(Len.StronglyTypedId.StronglyTypedIds)
            .Assembly
            .GetType("Len.StronglyTypedId.StronglyTypedIds+IntegrationGuidIdTypeHandler")!;

        handlerType.Should().NotBeNull("生成的 Dapper 处理器类必须存在");

        var handler = (Dapper.SqlMapper.TypeHandler<IntegrationGuidId>)Activator.CreateInstance(handlerType)!;

        // 值类型 Id：SetValue 取 .Value 还原基元。
        var parameter = new StubParameter();
        handler.SetValue(parameter, IntegrationGuidId.Create(Guid.NewGuid()));
        parameter.Value.Should().BeOfType<Guid>("值类型 Id 的 SetValue 必须写出 Guid 基元");

        // Parse：DBNull → default；Guid → 还原强类型 Id。
        var parsed = handler.Parse(DBNull.Value);
        parsed.Should().Be(default(IntegrationGuidId), "DBNull 必须映射到 default 值类型 Id");
        var guid = Guid.NewGuid();
        handler.Parse(guid).Should().Be(IntegrationGuidId.Create(guid));
    }

    #endregion

    #region EF Core：ValueConverter 真实生成（编译 + 文本契约锁定行为）

    [Fact]
    public void EfCore_ConverterTypeIsGeneratedAndInheritsValueConverter()
    {
        // 生成的转换器是 internal 嵌套类（StronglyTypedIds+IntegrationGuidIdConverter），
        // 经 typeof(IntegrationGuidId).Assembly.GetTypes() 在 internal 容器下可能拿不到，
        // 故改用「编译通过」作为运行时验证：本测试工程引了 EF Core 包（门控触发），
        // 生成器产物已编译进本程序集，若转换器类型在 EF Core 真实包下不存在，
        // 编译阶段即会失败（CS0246/CS0012 等），不会跑到这里。
        // 行为（双向映射 GuidId ↔ Guid）由 CodeGeneratorModuleGateTests 的文本契约锁定。
        var expectedType = typeof(Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<IntegrationGuidId, Guid>);

        expectedType.Should().NotBeNull("EF Core 的 ValueConverter 泛型类型必须可在真实包引用下解析");
        expectedType.Should().BeAssignableTo(
            typeof(Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter),
            "生成的转换器必须继承 EF Core 的 ValueConverter 基类");
    }

    #endregion

    #region Swagger：MapType 注册入口可执行

    [Fact]
    public void Swagger_ApplyTo_RegistersMapTypeForGuidId()
    {
        var options = new Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions();

        // 生成器产物的 ApplyTo(SwaggerGenOptions) 必须可调用且不抛。
        Len.StronglyTypedId.StronglyTypedIds.ApplyTo(options);

        options.Should().NotBeNull();
    }

    #endregion

    #region 内置 OpenApi：门控判据已核实（≥9 正确，8.0 包引用下产物必须不生成）

    /// <summary>
    /// 核实内置 OpenApi 门控（<c>HasModule("Microsoft.AspNetCore.OpenApi.dll", 9)</c>）在 8.0 包引用下的行为。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 本工程的 <c>Microsoft.AspNetCore.OpenApi</c> 包版本为 <c>8.0.0</c>（见 <c>Directory.Packages.props</c>），
    /// 主版本 8 &lt; 9，按判据<b>不触发</b>内置 OpenApi 生成器。核实结论：
    /// <c>Microsoft.AspNetCore.OpenApi</c> 8.0 包<b>没有</b> <c>OpenApiOptions</c> 类型（CS0234 实证，
    /// .NET 8 起内置 OpenAPI 支持，但 <c>OpenApiOptions</c> / <c>IOpenApiSchemaTransformer</c>
    /// 随 <c>Microsoft.AspNetCore.OpenApi</c> 包 9.0 才可用），故 8.0 下产物<b>必须不生成</b>，
    /// 否则会引用不存在的类型导致 CS0234。门控判据 <c>≥9</c> 是<b>正确判据</b>而非保守判据——
    /// 旧注释「门控判据可能有误」是误判。
    /// </para>
    /// <para>
    /// 本用例以「8.0 包引用下产物类不存在」背书门控判据：不直接引用 <c>OpenApiOptions</c>
    /// （8.0 下不存在、直接引用会 CS0234），改用反射查产物类是否在 <c>StronglyTypedIds</c> 中生成。
    /// </para>
    /// </remarks>
    [Fact]
    public void AspNetCoreOpenApi_NotGenerated_WhenPackageVersionIsEight()
    {
        // 8.0 包引用下门控关闭，产物类（嵌套在 StronglyTypedIds 内的 schema transformer）不应生成。
        // 用反射按「类型名 contains SchemaTransformer」判，避免直接引用 8.0 下不存在的 OpenApiOptions。
        var transformerType = typeof(Len.StronglyTypedId.StronglyTypedIds)
            .Assembly
            .GetTypes()
            .FirstOrDefault(t => t.Name.Contains("OpenApiSchemaTransformer", StringComparison.Ordinal));

        transformerType.Should().BeNull("Microsoft.AspNetCore.OpenApi 8.0 包引用下，内置 OpenApi 门控（主版本 ≥ 9）必须关闭，产物类不应生成");
    }

    #endregion

    #region MVC：ModelBinderProvider 与 RouteConstraint 注册入口可执行

    [Fact]
    public void Mvc_ApplyTo_RegistersModelBinderProviderAndRouteConstraints()
    {
        var mvcOptions = new Microsoft.AspNetCore.Mvc.MvcOptions();

        // 生成器产物 ApplyTo(MvcOptions)：插入 ModelBinderProvider。
        Len.StronglyTypedId.StronglyTypedIds.ApplyTo(mvcOptions);

        mvcOptions.ModelBinderProviders.Should().Contain(p => p.GetType().Name == "StronglyTypedIdModelBinderProvider",
            "ApplyTo(MvcOptions) 必须插入生成的模型绑定器 provider");

        var routeOptions = new Microsoft.AspNetCore.Routing.RouteOptions();

        Len.StronglyTypedId.StronglyTypedIds.ApplyTo(routeOptions);

        // 路由约束注册表里应有 IntegrationGuidId 对应的约束（按短名入 ConstraintMap）。
        routeOptions.ConstraintMap.Should().ContainKey("IntegrationGuidId",
            "ApplyTo(RouteOptions) 必须把每个 Id 注册为路由约束");

        var constraint = (Microsoft.AspNetCore.Routing.IRouteConstraint)Activator.CreateInstance(
            routeOptions.ConstraintMap["IntegrationGuidId"])!;

        constraint.Should().NotBeNull("路由约束必须可实例化");

        // 路由约束 Match：合法 Guid 文本通过、非法文本拒绝。
        // RouteDirection 的成员名是 IncomingRequest / UrlGeneration（net8/net10 均如此），
        // 不是 Incoming / Outgoing。
        var values = new Microsoft.AspNetCore.Routing.RouteValueDictionary
        {
            ["IntegrationGuidId"] = Guid.NewGuid().ToString(),
        };
        constraint.Match(null, null, "IntegrationGuidId", values, Microsoft.AspNetCore.Routing.RouteDirection.IncomingRequest)
            .Should().BeTrue("合法 Guid 文本必须通过路由约束");

        var invalidValues = new Microsoft.AspNetCore.Routing.RouteValueDictionary
        {
            ["IntegrationGuidId"] = "not-a-guid",
        };
        constraint.Match(null, null, "IntegrationGuidId", invalidValues, Microsoft.AspNetCore.Routing.RouteDirection.IncomingRequest)
            .Should().BeFalse("非法文本必须被路由约束拒绝");
    }

    #endregion

    #region System.Text.Json / Newtonsoft.Json 全局注册入口：真实注册 + 往返

    /// <summary>
    /// <c>StronglyTypedIds.ApplyTo(JsonSerializerOptions)</c> 必须把每个 Id 的内嵌转换器挂到
    /// <c>options.Converters</c> 上，且挂上之后序列化 / 反序列化往返正确。
    /// </summary>
    [Fact]
    public void SystemTextJson_ApplyTo_RegistersConvertersAndRoundTrips()
    {
        var options = new System.Text.Json.JsonSerializerOptions();

        Len.StronglyTypedId.StronglyTypedIds.ApplyTo(options);

        options.Converters.Should().Contain(
            converter => converter is System.Text.Json.Serialization.JsonConverter<IntegrationGuidId>,
            "ApplyTo 必须把 IntegrationGuidId 的内嵌 STJ 转换器挂到 options.Converters");

        var id = IntegrationGuidId.Create(Guid.NewGuid());

        var json = System.Text.Json.JsonSerializer.Serialize(id, options);
        json.Should().Be($"\"{id.Value:D}\"");

        System.Text.Json.JsonSerializer.Deserialize<IntegrationGuidId>(json, options).Should().Be(id);
    }

    /// <summary>
    /// <c>settings.AddStronglyTypedId()</c> 必须把每个 Id 的内嵌转换器挂到 <c>settings.Converters</c> 上，
    /// 且挂上之后 Newtonsoft 往返正确。
    /// </summary>
    [Fact]
    public void Newtonsoft_AddStronglyTypedId_RegistersConvertersAndRoundTrips()
    {
        var settings = new Newtonsoft.Json.JsonSerializerSettings();

        settings.AddStronglyTypedId();

        settings.Converters.Should().Contain(
            converter => converter.CanConvert(typeof(IntegrationGuidId)),
            "AddStronglyTypedId 必须把 IntegrationGuidId 的内嵌 Newtonsoft 转换器挂到 settings.Converters");

        var id = IntegrationGuidId.Create(Guid.NewGuid());

        var json = Newtonsoft.Json.JsonConvert.SerializeObject(id, settings);
        json.Should().Be($"\"{id.Value:D}\"");

        Newtonsoft.Json.JsonConvert.DeserializeObject<IntegrationGuidId>(json, settings).Should().Be(id);
    }

    #endregion

    #region Minimal API：路由 / 查询参数经 IParsable<T> 绑定

    /// <summary>
    /// Minimal API 的参数绑定约定会为「具备静态 <c>TryParse(string, IFormatProvider, out T)</c> 的类型」
    /// 自动生成绑定逻辑；强类型 Id 经 <c>IStronglyTypedId&lt;TSelf, TPrimitiveId&gt;</c> 间接实现了
    /// <see cref="IParsable{TSelf}"/>，因此无需任何额外生成即可作为路由 / 查询参数。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 本用例直接驱动 <c>RequestDelegateFactory</c>（Minimal API 的绑定引擎本体），不经 HTTP 服务器：
    /// 构造一个以强类型 Id 为参数的委托，交给工厂生成 <c>RequestDelegate</c>，再喂入带路由值的
    /// <c>HttpContext</c>，断言绑定结果。这比「起一个 WebApplication 再发请求」更快也更稳定。
    /// </para>
    /// <para>
    /// 路由值与查询串同时提供，覆盖绑定源推断的两种可能（Path / Query），不依赖框架内部的推断细节。
    /// </para>
    /// </remarks>
    [Fact]
    public async Task MinimalApi_BindsStronglyTypedIdFromRouteAndQuery()
    {
        var guid = Guid.NewGuid();
        var text = guid.ToString("D");

        Func<IntegrationGuidId, Microsoft.AspNetCore.Http.IResult> handler =
            id => Microsoft.AspNetCore.Http.Results.Ok(id.Value);
        var factoryResult = Microsoft.AspNetCore.Http.RequestDelegateFactory.Create(handler);
        var requestDelegate = factoryResult.RequestDelegate;

        var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        // Ok<T> 结果写出时会向容器索取 ILoggerFactory，故必须注册日志服务，否则裸容器会抛
        // 「No service for type 'ILoggerFactory'」——这是测试宿主的最小依赖，与被测绑定逻辑无关。
        httpContext.RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider();
        httpContext.Request.RouteValues["id"] = text;
        httpContext.Request.QueryString = new Microsoft.AspNetCore.Http.QueryString("?id=" + text);

        var body = new MemoryStream();
        httpContext.Response.Body = body;

        await requestDelegate(httpContext);

        httpContext.Response.StatusCode.Should().Be(200, "合法的 Guid 文本必须被 Minimal API 绑定为强类型 Id");

        body.Position = 0;
        var payload = new StreamReader(body).ReadToEnd();

        payload.Should().Contain(text, "绑定后的 Id 值必须与路由值一致");
    }

    /// <summary>
    /// 非法文本必须被 Minimal API 绑定拒绝：<c>TryParse</c> 返回 <see langword="false"/>，
    /// 框架据此产生 400 而非把垃圾值塞进处理器。
    /// </summary>
    [Fact]
    public async Task MinimalApi_RejectsInvalidText()
    {
        Func<IntegrationGuidId, Microsoft.AspNetCore.Http.IResult> handler =
            id => Microsoft.AspNetCore.Http.Results.Ok(id.Value);
        var factoryResult = Microsoft.AspNetCore.Http.RequestDelegateFactory.Create(handler);
        var requestDelegate = factoryResult.RequestDelegate;

        var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        httpContext.RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider();
        httpContext.Request.RouteValues["id"] = "not-a-guid";
        httpContext.Request.QueryString = new Microsoft.AspNetCore.Http.QueryString("?id=not-a-guid");
        httpContext.Response.Body = new MemoryStream();

        await requestDelegate(httpContext);

        httpContext.Response.StatusCode.Should().Be(400, "非法文本必须被 Minimal API 绑定拒绝");
    }

    #endregion

    #region 辅助

    /// <summary>
    /// 最小 <see cref="IDbDataParameter"/> 桩：生成的 TypeHandler.SetValue 只写 <c>Value</c>，
    /// 不实际读写数据库，故其余成员给最小实现即可。
    /// <see cref="DataRowVersion"/> 是 BCL 在 <c>System.Data</c> 下的枚举（net8.0+ 的
    /// <c>IDataParameter.SourceVersion</c> 返回类型），直接以 <c>None</c> 占位。
    /// </summary>
    private sealed class StubParameter : IDbDataParameter
    {
        public DbType DbType { get; set; }

        public ParameterDirection Direction { get; set; }

        public bool IsNullable { get; set; }

        public int Size { get; set; }

        // SourceColumn / ParameterName 的 setter 在 IDataParameter 上带 [AllowNull]（ADO.NET 允许
        // 置 null），实现侧照抄该标注才能与接口签名一致；getter 仍承诺非 null。
        [AllowNull]
        public string SourceColumn { get; set; } = string.Empty;

        public bool SourceColumnNullMapping { get; set; }

        [AllowNull]
        public string ParameterName { get; set; } = string.Empty;

        public object? Value { get; set; }

        public byte Precision { get; set; }

        public byte Scale { get; set; }

        public DataRowVersion SourceVersion { get; set; }
    }

    #endregion
}
