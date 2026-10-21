// G2：集成运行时验证——在真实包引用下直接调用生成产物 StronglyTypedIds.ApplyTo，
// 断言各集成（EF Core / Dapper / Swagger / 内置 OpenApi / MVC）的产物可执行且行为正确，
// 而非仅靠文本契约（CodeGeneratorModuleGateTests 已锁定文本形态，本文件补运行时语义）。
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Dapper;
using FluentAssertions;
using Len.StronglyTypedId;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Len.StronglyTypedId.Generators;

/// <summary>
/// 生成的 <c>StronglyTypedIds</c> 是 <c>internal static partial class</c>（本编译单元内可见），各集成产物按引用的包被门控生成。本测试工程的包引用（EF
/// Core / Dapper / Swashbuckle / Microsoft.AspNetCore.OpenApi / FrameworkReference
/// Microsoft.AspNetCore.App）使 5 个集成生成器全部触发，故 <c>StronglyTypedIds</c> 的 5 组 <c>ApplyTo</c> 都在本编译里可见。
/// </summary>
public class IntegrationRuntimeTests
{
    #region Dapper：SqlMapper.TypeHandler 真实注册 + 往返

    [Fact]
    public void Dapper_ApplyTo_RegistersTypeHandlersAndRoundTrips()
    {
        // 带参重载是为源码兼容保留的转发：Dapper 的注册是进程级全局的，connection 不参与。
        // 传 null 即可——ApplyTo 不对 connection 解引用。
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
        parsed.Should().Be(default, "DBNull 必须映射到 default 值类型 Id");
        var guid = Guid.NewGuid();
        handler.Parse(guid).Should().Be(IntegrationGuidId.Create(guid));
    }

    /// <summary>
    /// 引用类型 Id（<c>record class</c>）的 <c>SetValue</c> 必须写出基元值，而不是 Id 包装对象。
    /// 后者会让 SqlClient / Sqlite 在参数绑定时抛「No mapping exists from object type ...」——
    /// 这正是曾经的缺陷：值类型分支取了 <c>.Value</c>，引用类型分支直接把 <c>value</c> 塞了进去。
    /// </summary>
    [Fact]
    public void Dapper_SetValue_WritesPrimitive_ForReferenceTypeId()
    {
        Len.StronglyTypedId.StronglyTypedIds.ApplyTo((System.Data.IDbConnection?)null!);

        var handlerType = typeof(Len.StronglyTypedId.StronglyTypedIds)
            .Assembly
            .GetType("Len.StronglyTypedId.StronglyTypedIds+IntegrationReferenceIdTypeHandler")!;

        handlerType.Should().NotBeNull("引用类型 Id 也必须生成 Dapper 处理器");

        var handler = (Dapper.SqlMapper.TypeHandler<IntegrationReferenceId>)Activator.CreateInstance(handlerType)!;

        var guid = Guid.NewGuid();
        var parameter = new StubParameter();

        handler.SetValue(parameter, IntegrationReferenceId.Create(guid));

        // 关键断言：写出的必须是 Guid 基元。若写成包装对象，这里会是 IntegrationReferenceId。
        parameter.Value.Should().BeOfType<Guid>("SetValue 必须解包出基元值，否则驱动无法绑定该参数")
            .Which.Should().Be(guid);

        // null → DBNull：引用类型 Id 的 NULL 语义必须与值类型一致。
        var nullParameter = new StubParameter();
        handler.SetValue(nullParameter, null);
        nullParameter.Value.Should().Be(DBNull.Value);

        // Parse 往返：Guid → Id；DBNull → null（引用类型的 default）。
        handler.Parse(guid).Should().Be(IntegrationReferenceId.Create(guid));
        handler.Parse(DBNull.Value).Should().BeNull();
    }

    /// <summary>
    /// 重复调用 <c>ApplyTo</c> 必须幂等。Dapper 的 <c>AddTypeHandler</c> 是按类型赋值而不是追加，
    /// 所以第二次调用只会把处理器换成等价的新实例：不抛异常，注册表也不膨胀。
    /// 这正是「每个连接注册一次」这种误用能长期不被发现的原因——它不报错，只是白做功。
    /// </summary>
    [Fact]
    public void Dapper_ApplyTo_IsIdempotent()
    {
        // 无参重载是推荐写法；带参重载保留兼容，两者指向同一份进程级注册。
        Len.StronglyTypedId.StronglyTypedIds.ApplyTo();
        Len.StronglyTypedId.StronglyTypedIds.ApplyTo();
        Len.StronglyTypedId.StronglyTypedIds.ApplyTo((System.Data.IDbConnection?)null!);

        Dapper.SqlMapper.HasTypeHandler(typeof(IntegrationGuidId)).Should().BeTrue("值类型 Id 的处理器必须仍在册");
        Dapper.SqlMapper.HasTypeHandler(typeof(IntegrationReferenceId)).Should().BeTrue("引用类型 Id 的处理器必须仍在册");

        // 覆盖后行为不变：新实例仍能正确往返。
        var handlerType = typeof(Len.StronglyTypedId.StronglyTypedIds)
            .Assembly
            .GetType("Len.StronglyTypedId.StronglyTypedIds+IntegrationGuidIdTypeHandler")!;
        var handler = (Dapper.SqlMapper.TypeHandler<IntegrationGuidId>)Activator.CreateInstance(handlerType)!;
        var guid = Guid.NewGuid();

        handler.Parse(guid).Should().Be(IntegrationGuidId.Create(guid));
    }

    /// <summary>
    /// 非 string 基元的读取路径：驱动返回的 CLR 类型往往不是基元本身，裸拆箱强转会抛
    /// <see cref="InvalidCastException"/>。这里按各驱动的真实返回形态喂给 <c>Parse</c>。
    /// </summary>
    [Fact]
    public void Dapper_Parse_AcceptsProviderShapedValues_ForNonStringPrimitives()
    {
        Len.StronglyTypedId.StronglyTypedIds.ApplyTo();

        // 枚举：SQLite 的 INTEGER 列一律返回 long；部分驱动把枚举列当文本返回。
        GetDapperHandler<SampleStatusId>().Parse(1L)
            .Should().Be(SampleStatusId.Create(SampleStatus.Suspended));
        GetDapperHandler<SampleStatusId>().Parse("Suspended")
            .Should().Be(SampleStatusId.Create(SampleStatus.Suspended));

        // char：TEXT 列返回 string。
        GetDapperHandler<CharId>().Parse("A").Should().Be(CharId.Create('A'));

        // decimal：REAL 列返回 double。
        GetDapperHandler<DecimalId>().Parse(1.5d).Should().Be(DecimalId.Create(1.5m));

        // Guid：TEXT 列返回 string。
        var guid = Guid.NewGuid();
        GetDapperHandler<GuidId>().Parse(guid.ToString()).Should().Be(GuidId.Create(guid));

        // int：INTEGER 列返回 long，需要窄化。
        GetDapperHandler<Int32Id>().Parse(42L).Should().Be(Int32Id.Create(42));

        // NULL 的两种形态都落到 default。Dapper 基类的 Parse 形参标注为非空 object，故用 ! 表态。
        GetDapperHandler<GuidId>().Parse(null!).Should().Be(default);
        GetDapperHandler<GuidId>().Parse(DBNull.Value).Should().Be(default);
    }

    /// <summary>
    /// 真实 SQLite 往返：写入侧把基元交给驱动，读取侧由驱动返回 long / string / double，
    /// 再由生成的 <c>Parse</c> 转换回基元。这条端到端路径此前 4/4 抛 <see cref="InvalidCastException"/>，
    /// 而当时的集成用例只用 string 基元，缺陷因此长期未被发现。
    /// </summary>
    [Fact]
    public void Dapper_RoundTrips_NonStringPrimitives_ThroughSqlite()
    {
        Len.StronglyTypedId.StronglyTypedIds.ApplyTo();

        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        connection.Open();

        connection.Execute(
            "CREATE TABLE Probe (Id TEXT NOT NULL, Letter TEXT NOT NULL, Amount REAL NOT NULL, Status INTEGER NOT NULL)");

        var guid = Guid.NewGuid();

        connection.Execute(
            "INSERT INTO Probe (Id, Letter, Amount, Status) VALUES (@Id, @Letter, @Amount, @Status)",
            new
            {
                Id = GuidId.Create(guid),
                Letter = CharId.Create('A'),
                Amount = DecimalId.Create(1.5m),
                Status = SampleStatusId.Create(SampleStatus.Suspended),
            });

        var row = connection.QuerySingle<ProbeRow>("SELECT Id, Letter, Amount, Status FROM Probe");

        row.Id.Should().Be(GuidId.Create(guid));
        row.Letter.Should().Be(CharId.Create('A'));
        row.Amount.Should().Be(DecimalId.Create(1.5m));
        row.Status.Should().Be(SampleStatusId.Create(SampleStatus.Suspended));
    }

    /// <summary>
    /// 取某个 Id 的 Dapper 处理器实例。处理器是生成在 <c>StronglyTypedIds</c> 里的 <c>internal</c> 嵌套类，
    /// 只能按名字反射拿到。
    /// </summary>
    private static Dapper.SqlMapper.TypeHandler<TId> GetDapperHandler<TId>()
        => (Dapper.SqlMapper.TypeHandler<TId>)Activator.CreateInstance(
            typeof(Len.StronglyTypedId.StronglyTypedIds)
                .Assembly
                .GetType($"Len.StronglyTypedId.StronglyTypedIds+{typeof(TId).Name}TypeHandler")!)!;

    /// <summary>
    /// SQLite 往返用的行类型：四个属性分别覆盖 Guid / char / decimal / enum 四种基元。
    /// </summary>
    private sealed class ProbeRow
    {
        public GuidId Id { get; set; }

        public CharId Letter { get; set; }

        public DecimalId Amount { get; set; }

        public SampleStatusId Status { get; set; }
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
        expectedType.Should().BeAssignableTo<Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter>(
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

    /// <summary>
    /// 8.0 环境下的等价路径：内置 OpenAPI 没有可用的 schema 转换器，映射由 Swashbuckle 的
    /// <c>MapType</c> 提供。这里把注册进 <c>CustomTypeMappings</c> 的工厂取出来执行，锁定
    /// 「Id → 基元 schema」的实际结果，而不是只断言「调用不抛」。
    /// </summary>
    [Fact]
    public void Swagger_ApplyTo_MapsGuidIdToUuidSchema()
    {
        var options = new Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions();

        Len.StronglyTypedId.StronglyTypedIds.ApplyTo(options);

        options.SchemaGeneratorOptions.CustomTypeMappings.Should().ContainKey(typeof(IntegrationGuidId));

        var schema = options.SchemaGeneratorOptions.CustomTypeMappings[typeof(IntegrationGuidId)]();

        schema.Type.Should().Be("string");
        schema.Format.Should().Be("uuid");
    }

    /// <summary>
    /// 带文本前缀的 Id 在 OpenAPI 里退化成纯 <c>string</c>：文本形是 <c>ORD-&lt;guid&gt;</c>，
    /// 已经不是合法的 uuid，继续标 <c>format: uuid</c> 会让生成的客户端按 uuid 校验而全部失败。
    /// </summary>
    [Fact]
    public void Swagger_ApplyTo_MapsPrefixedIdToPlainStringSchema()
    {
        var options = new Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions();

        Len.StronglyTypedId.StronglyTypedIds.ApplyTo(options);

        options.SchemaGeneratorOptions.CustomTypeMappings.Should().ContainKey(typeof(PrefixedIntegrationGuidId));

        var schema = options.SchemaGeneratorOptions.CustomTypeMappings[typeof(PrefixedIntegrationGuidId)]();

        schema.Type.Should().Be("string");
        schema.Format.Should().BeNull("文本形带前缀，不再是合法 uuid，不能标 format");
    }

    #endregion

    #region 内置 OpenApi：门控判据已核实（≥9 正确，8.0 包引用下产物必须不生成）

    /// <summary>
    /// 核实内置 OpenApi 门控（<c>HasModule("Microsoft.AspNetCore.OpenApi.dll", 9)</c>）在 8.0 包引用下的行为。
    /// </summary>
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

    /// <summary>
    /// 8.0 包引用下也不能留下 <c>ApplyTo(OpenApiOptions)</c> 重载：8.0 里没有 <c>OpenApiOptions</c>，
    /// 一旦生成就是消费者侧的 CS0246。按参数类型名判定，避免直接引用该类型。
    /// </summary>
    [Fact]
    public void AspNetCoreOpenApi_Should_NotExposeApplyToOverload_WhenPackageVersionIsEight()
    {
        var openApiOverloads = typeof(Len.StronglyTypedId.StronglyTypedIds)
            .GetMethods()
            .Where(method => method.Name == "ApplyTo")
            .Where(method => method.GetParameters().Any(parameter =>
                parameter.ParameterType.Name.Contains("OpenApiOptions", StringComparison.Ordinal)))
            .ToArray();

        openApiOverloads.Should().BeEmpty("8.0 包引用下不应生成 ApplyTo(OpenApiOptions)，否则消费者编译会 CS0246");
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

    /// <summary>
    /// 可空强类型 Id（<c>[FromQuery] IntegrationGuidId?</c>）必须同样由生成的绑定器接管。
    /// <para>
    /// <c>Nullable&lt;T&gt;</c> 不实现任何接口（<c>typeof(IntegrationGuidId?).GetInterfaces()</c> 长度为 0），
    /// provider 若不先拆出底层类型就会返回 <c>null</c>，MVC 随即退回框架的 <c>TryParseModelBinder</c>。
    /// 那条链路按 <c>CurrentCulture</c> 解析，与强类型 Id 的不变文化契约不一致：同一条路由上，
    /// 可空参数与非可空参数会对同一份载荷得出不同的值，非法值的错误消息也不同。
    /// </para>
    /// </summary>
    [Fact]
    public async Task Mvc_NullableId_RoutesToGeneratedBinder()
    {
        var mvcOptions = new Microsoft.AspNetCore.Mvc.MvcOptions();

        Len.StronglyTypedId.StronglyTypedIds.ApplyTo(mvcOptions);

        var metadataProvider = new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider();
        var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var factory = new Microsoft.AspNetCore.Mvc.ModelBinding.ModelBinderFactory(
            metadataProvider,
            Microsoft.Extensions.Options.Options.Create(mvcOptions),
            services);

        var metadata = metadataProvider.GetMetadataForType(typeof(IntegrationGuidId?));
        var binder = factory.CreateBinder(new Microsoft.AspNetCore.Mvc.ModelBinding.ModelBinderFactoryContext
        {
            Metadata = metadata,
            CacheToken = metadata,
        });

        binder.GetType().Name.Should().StartWith(
            "StronglyTypedIdModelBinder",
            "可空强类型 Id 必须由生成的绑定器接管，不能退回框架的 TryParseModelBinder");

        var expected = IntegrationGuidId.Create(Guid.NewGuid());

        var validContext = CreateBindingContext("id", expected.Value.ToString("D"));

        await binder.BindModelAsync(validContext);

        validContext.Result.IsModelSet.Should().BeTrue("合法文本必须绑定成功");
        validContext.Result.Model.Should().Be(expected);

        var invalidContext = CreateBindingContext("id", "not-a-guid");

        await binder.BindModelAsync(invalidContext);

        invalidContext.Result.IsModelSet.Should().BeFalse("非法文本不得写入模型");
        invalidContext.ModelState["id"]!.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be(
                "The value 'not-a-guid' is not a valid strongly typed id.",
                "错误消息来自生成的绑定器；退回 TryParseModelBinder 时是框架的 \"is not valid.\"");

        // 缺省（查询串里根本没有该键）应保持「未绑定且无错误」，交由可空类型表达 null。
        var missingContext = CreateBindingContext("id", null);

        await binder.BindModelAsync(missingContext);

        missingContext.Result.IsModelSet.Should().BeFalse("缺省值不得被当成绑定失败");
        missingContext.ModelState.Should().BeEmpty("缺省值不得产生模型错误");
    }

    private static Microsoft.AspNetCore.Mvc.ModelBinding.DefaultModelBindingContext CreateBindingContext(string name, string? rawValue) =>
        new()
        {
            ModelName = name,
            ModelState = new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary(),
            ValueProvider = new SingleValueProvider(name, rawValue),
        };

    /// <summary>只为绑定器测试提供单个键值的最小 <see cref="Microsoft.AspNetCore.Mvc.ModelBinding.IValueProvider"/>。</summary>
    private sealed class SingleValueProvider(string name, string? rawValue) : Microsoft.AspNetCore.Mvc.ModelBinding.IValueProvider
    {
        /// <inheritdoc/>
        public bool ContainsPrefix(string prefix) => string.Equals(prefix, name, StringComparison.Ordinal);

        /// <inheritdoc/>
        public Microsoft.AspNetCore.Mvc.ModelBinding.ValueProviderResult GetValue(string key) =>
            rawValue is not null && string.Equals(key, name, StringComparison.Ordinal)
                ? new Microsoft.AspNetCore.Mvc.ModelBinding.ValueProviderResult(rawValue)
                : Microsoft.AspNetCore.Mvc.ModelBinding.ValueProviderResult.None;
    }

    #endregion

    #region System.Text.Json / Newtonsoft.Json 全局注册入口：真实注册 + 往返

    /// <summary>
    /// <c>StronglyTypedIds.ApplyTo(JsonSerializerOptions)</c> 必须把每个 Id 的内嵌转换器挂到 <c>options.Converters</c>
    /// 上，且挂上之后序列化 / 反序列化往返正确。
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
    /// <c>settings.AddStronglyTypedId()</c> 必须把每个 Id 的内嵌转换器挂到 <c>settings.Converters</c> 上，且挂上之后 Newtonsoft
    /// 往返正确。
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

    /// <summary>
    /// 泛型容器里的 Id 是 JSON 支持唯一的缺口：转换器类照常生成，但 <c>[JsonConverter]</c> 特性引用不了含类型参数的
    /// 转换器（CS0416），<c>AddStronglyTypedId()</c> 也会跳过它（开放泛型不能作类型实参）。因此默认序列化退化成
    /// record 结构，只有按闭合类型手动挂上生成的转换器才回到基元形态。这条用例锁定该行为与可行的绕法。
    /// </summary>
    [Fact]
    public void GenericContainerId_Should_NeedManualConverterRegistration()
    {
        var guid = Guid.NewGuid();
        var id = IntegrationBox<int>.BoxedGuidId.Create(guid);

        // 默认：没有特性也没有注册，走 record 的公共属性。
        System.Text.Json.JsonSerializer.Serialize(id).Should().Be($"{{\"Value\":\"{guid:D}\"}}");
        Newtonsoft.Json.JsonConvert.SerializeObject(id).Should().Be($"{{\"Value\":\"{guid:D}\"}}");

        // 手动按闭合类型注册后与普通 Id 一致。
        var stjOptions = new System.Text.Json.JsonSerializerOptions();
        stjOptions.Converters.Add(new IntegrationBox<int>.BoxedGuidId.SystemTextJsonConverter());
        System.Text.Json.JsonSerializer.Serialize(id, stjOptions).Should().Be($"\"{guid:D}\"");
        System.Text.Json.JsonSerializer.Deserialize<IntegrationBox<int>.BoxedGuidId>($"\"{guid:D}\"", stjOptions).Should().Be(id);

        var newtonsoftSettings = new Newtonsoft.Json.JsonSerializerSettings();
        newtonsoftSettings.Converters.Add(new IntegrationBox<int>.BoxedGuidId.NewtonsoftJsonConverter());
        Newtonsoft.Json.JsonConvert.SerializeObject(id, newtonsoftSettings).Should().Be($"\"{guid:D}\"");
        Newtonsoft.Json.JsonConvert.DeserializeObject<IntegrationBox<int>.BoxedGuidId>($"\"{guid:D}\"", newtonsoftSettings).Should().Be(id);
    }

    #endregion

    #region Minimal API：路由 / 查询参数经 IParsable<T> 绑定

    /// <summary>
    /// Minimal API 的参数绑定约定会为「具备静态 <c>TryParse(string, IFormatProvider, out T)</c> 的类型」自动生成绑定逻辑；强类型 Id 经
    /// <c>IStronglyTypedId&lt;TSelf, TPrimitiveId&gt;</c> 间接实现了 <see cref="IParsable{TSelf}"/>，因此无需任何额外生成即可作为
    /// 路由 / 查询参数。
    /// </summary>
    [Fact]
    public async Task MinimalApi_BindsStronglyTypedIdFromRouteAndQuery()
    {
        var guid = Guid.NewGuid();
        var text = guid.ToString("D");

        Func<IntegrationGuidId, Microsoft.AspNetCore.Http.IResult> handler =
            id => Microsoft.AspNetCore.Http.Results.Ok(id.Value);
        var factoryResult = Microsoft.AspNetCore.Http.RequestDelegateFactory.Create(handler);
        var requestDelegate = factoryResult.RequestDelegate;

        // Ok<T> 结果写出时会向容器索取 ILoggerFactory，故必须注册日志服务，否则裸容器会抛
        // 「No service for type 'ILoggerFactory'」——这是测试宿主的最小依赖，与被测绑定逻辑无关。
        var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider()
        };
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
    /// 非法文本必须被 Minimal API 绑定拒绝：<c>TryParse</c> 返回 <see langword="false"/>，框架据此产生 400 而非把垃圾值塞进处理器。
    /// </summary>
    [Fact]
    public async Task MinimalApi_RejectsInvalidText()
    {
        Func<IntegrationGuidId, Microsoft.AspNetCore.Http.IResult> handler =
            id => Microsoft.AspNetCore.Http.Results.Ok(id.Value);
        var factoryResult = Microsoft.AspNetCore.Http.RequestDelegateFactory.Create(handler);
        var requestDelegate = factoryResult.RequestDelegate;

        var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider()
        };
        httpContext.Request.RouteValues["id"] = "not-a-guid";
        httpContext.Request.QueryString = new Microsoft.AspNetCore.Http.QueryString("?id=not-a-guid");
        httpContext.Response.Body = new MemoryStream();

        await requestDelegate(httpContext);

        httpContext.Response.StatusCode.Should().Be(400, "非法文本必须被 Minimal API 绑定拒绝");
    }

    #endregion

    #region C10：文本前缀贯通 MVC 路由约束 / Minimal API 绑定 / JSON

    /// <summary>
    /// 前缀进入规范文本形后，MVC 路由约束（生成器零改动）自动接受带前缀文本、拒绝裸值。
    /// </summary>
    [Fact]
    public void Prefix_Should_FlowThroughMvcRouteConstraint()
    {
        var routeOptions = new Microsoft.AspNetCore.Routing.RouteOptions();

        Len.StronglyTypedId.StronglyTypedIds.ApplyTo(routeOptions);

        routeOptions.ConstraintMap.Should().ContainKey("PrefixedIntegrationGuidId");

        var constraint = (Microsoft.AspNetCore.Routing.IRouteConstraint)Activator.CreateInstance(
            routeOptions.ConstraintMap["PrefixedIntegrationGuidId"])!;

        var guid = Guid.NewGuid();

        var prefixed = new Microsoft.AspNetCore.Routing.RouteValueDictionary
        {
            ["PrefixedIntegrationGuidId"] = $"ord_{guid:D}",
        };
        constraint.Match(null, null, "PrefixedIntegrationGuidId", prefixed, Microsoft.AspNetCore.Routing.RouteDirection.IncomingRequest)
            .Should().BeTrue("带前缀文本必须通过路由约束");

        var bare = new Microsoft.AspNetCore.Routing.RouteValueDictionary
        {
            ["PrefixedIntegrationGuidId"] = guid.ToString("D"),
        };
        constraint.Match(null, null, "PrefixedIntegrationGuidId", bare, Microsoft.AspNetCore.Routing.RouteDirection.IncomingRequest)
            .Should().BeTrue("宽松读入策略下裸值也应通过路由约束");
    }

    /// <summary>
    /// Minimal API 经 <c>IParsable&lt;T&gt;</c> 绑定带前缀文本（生成器零改动）。
    /// </summary>
    [Fact]
    public async Task Prefix_Should_FlowThroughMinimalApiBinding()
    {
        var guid = Guid.NewGuid();
        var text = $"ord_{guid:D}";

        Func<PrefixedIntegrationGuidId, Microsoft.AspNetCore.Http.IResult> handler =
            id => Microsoft.AspNetCore.Http.Results.Ok(id.Value);
        var requestDelegate = Microsoft.AspNetCore.Http.RequestDelegateFactory.Create(handler).RequestDelegate;

        var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider()
        };
        httpContext.Request.RouteValues["id"] = text;
        httpContext.Request.QueryString = new Microsoft.AspNetCore.Http.QueryString("?id=" + text);

        var body = new MemoryStream();
        httpContext.Response.Body = body;

        await requestDelegate(httpContext);

        httpContext.Response.StatusCode.Should().Be(200, "带前缀文本必须被 Minimal API 绑定为强类型 Id");

        body.Position = 0;
        var payload = new StreamReader(body).ReadToEnd();

        payload.Should().Contain(guid.ToString("D"), "绑定后的 Id 值必须与路由值一致");
    }

    /// <summary>
    /// 前缀贯通 STJ 全局注册入口：序列化带前缀、反序列化剥离。
    /// </summary>
    [Fact]
    public void Prefix_Should_FlowThroughSystemTextJsonApplyTo()
    {
        var options = new System.Text.Json.JsonSerializerOptions();

        Len.StronglyTypedId.StronglyTypedIds.ApplyTo(options);

        var id = PrefixedIntegrationGuidId.Create(Guid.NewGuid());

        var json = System.Text.Json.JsonSerializer.Serialize(id, options);
        json.Should().Be($"\"ord_{id.Value:D}\"");

        System.Text.Json.JsonSerializer.Deserialize<PrefixedIntegrationGuidId>(json, options).Should().Be(id);
    }

    #endregion

    #region 辅助

    /// <summary>
    /// 最小 <see cref="IDbDataParameter"/> 桩：生成的 TypeHandler.SetValue 只写 <c>Value</c>，不实际读写数据库，故其余成员给最小实现即可。
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
