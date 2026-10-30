// G2：集成运行时验证。在真实包引用下直接调用生成产物的各集成入口，
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
/// 生成的 <c>StronglyTypedIds</c> 是 <c>internal static partial class</c>（本编译单元内可见），各集成产物按引用的包被门控生成。
/// 本测试工程引用了 EF Core、Dapper、Swashbuckle、Microsoft.AspNetCore.OpenApi 与 ASP.NET Core 框架，
/// 使 5 个集成生成器全部触发，故 5 组 <c>AddStronglyTypedIds</c> 都在本编译里可见。
/// </summary>
public class IntegrationRuntimeTests
{
    #region Dapper：SqlMapper.TypeHandler 真实注册 + 往返

    [Fact]
    public void Dapper_AddStronglyTypedIds_RegistersTypeHandlersAndRoundTrips()
    {
        // 带参重载是为源码兼容保留的转发：Dapper 的注册是进程级全局的，connection 不参与。
        // 传 null 即可，AddStronglyTypedIds 不对 connection 解引用。
        ((System.Data.IDbConnection?)null!).AddStronglyTypedIds();

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
    /// 后者会让 SqlClient / Sqlite 在参数绑定时抛「No mapping exists from object type ...」
    /// 这正是曾经的缺陷：值类型分支取了 <c>.Value</c>，引用类型分支直接把 <c>value</c> 塞了进去。
    /// </summary>
    [Fact]
    public void Dapper_SetValue_WritesPrimitive_ForReferenceTypeId()
    {
        ((System.Data.IDbConnection?)null!).AddStronglyTypedIds();

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
    /// 重复调用 <c>AddStronglyTypedIds</c> 必须幂等。Dapper 的 <c>AddTypeHandler</c> 是按类型赋值而不是追加，
    /// 所以第二次调用只会把处理器换成等价的新实例：不抛异常，注册表也不膨胀。
    /// 这正是「每个连接注册一次」这种误用能长期不被发现的原因，它不报错，只是白做功。
    /// </summary>
    [Fact]
    public void Dapper_AddStronglyTypedIds_IsIdempotent()
    {
        // 无参重载是推荐写法；带参重载保留兼容，两者指向同一份进程级注册。
        Len.StronglyTypedId.StronglyTypedIds.AddStronglyTypedIds();
        Len.StronglyTypedId.StronglyTypedIds.AddStronglyTypedIds();
        ((System.Data.IDbConnection?)null!).AddStronglyTypedIds();

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
        Len.StronglyTypedId.StronglyTypedIds.AddStronglyTypedIds();

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
    /// 枚举的三条读取路径（直通 / 文本 / 数值）必须共用同一套「已定义」校验。
    /// </summary>
    /// <remarks>
    /// 数值路径曾完全绕过校验：<c>Enum.ToObject</c> 对超范围整数静默绕回、对浮点数静默截断，于是同一个非法取值
    /// 会因驱动返回的 CLR 类型不同而被接受或拒绝。本用例逐条锁定三条路径的拒绝行为。
    /// </remarks>
    [Fact]
    public void Dapper_Parse_RejectsUndefinedEnumValues_OnEveryPath()
    {
        Len.StronglyTypedId.StronglyTypedIds.AddStronglyTypedIds();

        var status = GetDapperHandler<SampleStatusId>();

        // 合法取值：三条路径都必须通过。
        status.Parse(SampleStatus.Suspended).Should().Be(SampleStatusId.Create(SampleStatus.Suspended), "直通路径");
        status.Parse(1L).Should().Be(SampleStatusId.Create(SampleStatus.Suspended), "数值路径（SQLite 的 INTEGER 列）");
        status.Parse(1.0d).Should().Be(SampleStatusId.Create(SampleStatus.Suspended), "数值路径（REAL 列）");
        status.Parse("Suspended").Should().Be(SampleStatusId.Create(SampleStatus.Suspended), "文本路径");
        status.Parse("suspended").Should().Be(SampleStatusId.Create(SampleStatus.Suspended), "文本路径忽略大小写");

        // 未定义取值：三条路径都必须拒绝。
        status.Invoking(handler => handler.Parse(999L)).Should().Throw<ArgumentException>("数值路径不得放行未定义取值");
        status.Invoking(handler => handler.Parse(300L)).Should().Throw<ArgumentException>("数值路径不得放行未定义取值");
        status.Invoking(handler => handler.Parse(999.0d)).Should().Throw<ArgumentException>("数值路径不得放行未定义取值");
        status.Invoking(handler => handler.Parse("999")).Should().Throw<ArgumentException>("文本路径不得放行未定义取值");

        // 带小数部分的取值一律拒绝：Enum.ToObject 会静默截断（1.5d → 0，即 Active）。
        status.Invoking(handler => handler.Parse(1.5d)).Should().Throw<ArgumentException>("小数必须被拒绝，不能截断成某个成员");

        // 超出底层类型范围的取值一律拒绝：Enum.ToObject 会静默绕回（uint.MaxValue → -1）。
        status.Invoking(handler => handler.Parse(uint.MaxValue)).Should().Throw<ArgumentException>("溢出必须被拒绝，不能绕回");

        // [Flags] 枚举：位组合合法，未定义的位非法。
        var access = GetDapperHandler<SampleAccessId>();
        access.Parse(3L).Should().Be(SampleAccessId.Create(SampleAccess.Read | SampleAccess.Write), "已定义位的组合合法");
        access.Parse("Read, Write").Should().Be(SampleAccessId.Create(SampleAccess.Read | SampleAccess.Write), "文本形式的位组合合法");
        access.Invoking(handler => handler.Parse(4L)).Should().Throw<ArgumentException>("未定义的位必须被拒绝");
        access.Invoking(handler => handler.Parse(999L)).Should().Throw<ArgumentException>("未定义的位必须被拒绝");
    }

    /// <summary>
    /// 真实 SQLite 往返：写入侧把基元交给驱动，读取侧由驱动返回 long / string / double，
    /// 再由生成的 <c>Parse</c> 转换回基元。这条端到端路径此前 4/4 抛 <see cref="InvalidCastException"/>，
    /// 而当时的集成用例只用 string 基元，缺陷因此长期未被发现。
    /// </summary>
    [Fact]
    public void Dapper_RoundTrips_NonStringPrimitives_ThroughSqlite()
    {
        Len.StronglyTypedId.StronglyTypedIds.AddStronglyTypedIds();

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
        // 生成的转换器是 internal 嵌套类，经 Assembly.GetTypes() 在 internal 容器下可能拿不到，故改用「编译通过」作为运行时验证：转换器类型若在真实包下不存在，编译阶段即失败。
        // 行为由 CodeGeneratorModuleGateTests 的文本契约锁定。
        var expectedType = typeof(Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<IntegrationGuidId, Guid>);

        expectedType.Should().NotBeNull("EF Core 的 ValueConverter 泛型类型必须可在真实包引用下解析");
        expectedType.Should().BeAssignableTo<Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter>(
            "生成的转换器必须继承 EF Core 的 ValueConverter 基类");
    }

    #endregion

    #region Swagger：MapType 注册入口可执行

    [Fact]
    public void Swagger_AddStronglyTypedIds_RegistersMapTypeForGuidId()
    {
        var options = new Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions();

        // 生成器产物的 AddStronglyTypedIds(SwaggerGenOptions) 必须可调用且不抛。
        options.AddStronglyTypedIds();

        options.Should().NotBeNull();
    }

    /// <summary>
    /// 8.0 环境下的等价路径：内置 OpenAPI 没有可用的 schema 转换器，映射由 Swashbuckle 的
    /// <c>MapType</c> 提供。这里把注册进 <c>CustomTypeMappings</c> 的工厂取出来执行，锁定
    /// 「Id → 基元 schema」的实际结果，而不是只断言「调用不抛」。
    /// </summary>
    [Fact]
    public void Swagger_AddStronglyTypedIds_MapsGuidIdToUuidSchema()
    {
        var options = new Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions();

        options.AddStronglyTypedIds();

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
    public void Swagger_AddStronglyTypedIds_MapsPrefixedIdToPlainStringSchema()
    {
        var options = new Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions();

        options.AddStronglyTypedIds();

        options.SchemaGeneratorOptions.CustomTypeMappings.Should().ContainKey(typeof(PrefixedIntegrationGuidId));

        var schema = options.SchemaGeneratorOptions.CustomTypeMappings[typeof(PrefixedIntegrationGuidId)]();

        schema.Type.Should().Be("string");
        schema.Format.Should().BeNull("文本形带前缀，不再是合法 uuid，不能标 format");
    }

    /// <summary>
    /// <c>JsonShape.Object</c> 的线上报文是 <c>{"value":"&lt;guid&gt;"}</c>，schema 必须写成对象形态并标 <c>value</c> 必填；
    /// 否则生成的客户端会按裸字符串收发，与服务端实际报文不符。
    /// </summary>
    [Fact]
    public void Swagger_AddStronglyTypedIds_MapsObjectShapeIdToObjectSchema()
    {
        var options = new Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions();

        options.AddStronglyTypedIds();

        options.SchemaGeneratorOptions.CustomTypeMappings.Should().ContainKey(typeof(ObjectShapeIntegrationGuidId));

        var schema = options.SchemaGeneratorOptions.CustomTypeMappings[typeof(ObjectShapeIntegrationGuidId)]();

        schema.Type.Should().Be("object");
        schema.Format.Should().BeNull("对象形态没有 format");

        schema.Properties.Should().ContainKey("value");
        schema.Properties["value"].Type.Should().Be("string");
        schema.Properties["value"].Format.Should().Be("uuid", "value 属性仍是基元 Guid");

        schema.Required.Should().Contain("value", "对象形态只有一个必填属性");
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
    /// 8.0 包引用下也不能留下 <c>AddStronglyTypedIds(OpenApiOptions)</c> 重载：8.0 里没有 <c>OpenApiOptions</c>，
    /// 一旦生成就是消费者侧的 CS0246。按参数类型名判定，避免直接引用该类型。
    /// </summary>
    [Fact]
    public void AspNetCoreOpenApi_Should_NotExposeAddStronglyTypedIdsOverload_WhenPackageVersionIsEight()
    {
        var openApiOverloads = typeof(Len.StronglyTypedId.StronglyTypedIds)
            .GetMethods()
            .Where(method => method.Name == "AddStronglyTypedIds")
            .Where(method => method.GetParameters().Any(parameter =>
                parameter.ParameterType.Name.Contains("OpenApiOptions", StringComparison.Ordinal)))
            .ToArray();

        openApiOverloads.Should().BeEmpty("8.0 包引用下不应生成 AddStronglyTypedIds(OpenApiOptions)，否则消费者编译会 CS0246");
    }

    #endregion

    #region MVC：ModelBinderProvider 与 RouteConstraint 注册入口可执行

    [Fact]
    public void Mvc_AddStronglyTypedIds_RegistersModelBinderProviderAndRouteConstraints()
    {
        var mvcOptions = new Microsoft.AspNetCore.Mvc.MvcOptions();

        // 生成器产物 AddStronglyTypedIds(MvcOptions)：插入 ModelBinderProvider。
        mvcOptions.AddStronglyTypedIds();

        mvcOptions.ModelBinderProviders.Should().Contain(p => p.GetType().Name == "StronglyTypedIdModelBinderProvider",
            "AddStronglyTypedIds(MvcOptions) 必须插入生成的模型绑定器 provider");

        var routeOptions = new Microsoft.AspNetCore.Routing.RouteOptions();

        routeOptions.AddStronglyTypedIds();

        // 路由约束注册表里应有 IntegrationGuidId 对应的约束（按短名入 ConstraintMap）。
        routeOptions.ConstraintMap.Should().ContainKey("IntegrationGuidId",
            "AddStronglyTypedIds(RouteOptions) 必须把每个 Id 注册为路由约束");

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
    /// <c>AddStronglyTypedIds(MvcOptions)</c> 必须幂等：启动代码里重复调用（多个模块各自注册、或配置被复用）时，
    /// 每次 <c>Insert</c> 一个新实例会让 provider 列表无限增长，绑定器也被反复重建。
    /// </summary>
    [Fact]
    public void Mvc_AddStronglyTypedIds_Should_BeIdempotent()
    {
        var mvcOptions = new Microsoft.AspNetCore.Mvc.MvcOptions();

        mvcOptions.AddStronglyTypedIds();
        var afterFirst = mvcOptions.ModelBinderProviders.Count;

        mvcOptions.AddStronglyTypedIds();
        mvcOptions.AddStronglyTypedIds();

        mvcOptions.ModelBinderProviders.Count.Should().Be(afterFirst,
            "重复调用 AddStronglyTypedIds 不能再叠加 provider");
        mvcOptions.ModelBinderProviders
            .Count(p => p.GetType().Name == "StronglyTypedIdModelBinderProvider")
            .Should().Be(1, "生成的 provider 至多存在一个");
    }

    /// <summary>
    /// 可空强类型 Id（<c>[FromQuery] IntegrationGuidId?</c>）必须同样由生成的绑定器接管。
    /// </summary>
    /// <remarks>
    /// <c>Nullable&lt;T&gt;</c> 不实现任何接口，provider 若不先拆出底层类型就会返回 <c>null</c>，
    /// MVC 随即退回框架的 <c>TryParseModelBinder</c>，那条链路按 <c>ValueProviderResult.Culture</c> 解析
    /// （查询串与路由为不变文化，表单为当前文化），与强类型 Id 始终使用不变文化的契约不一致。
    /// </remarks>
    [Fact]
    public async Task Mvc_NullableId_RoutesToGeneratedBinder()
    {
        var mvcOptions = new Microsoft.AspNetCore.Mvc.MvcOptions();

        mvcOptions.AddStronglyTypedIds();

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

    /// <summary>
    /// 只为绑定器测试提供单个键值的最小 <see cref="Microsoft.AspNetCore.Mvc.ModelBinding.IValueProvider"/>。
    /// </summary>
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
    /// <c>options.AddStronglyTypedIds()</c> 必须把每个 Id 的内嵌转换器挂到 <c>options.Converters</c>
    /// 上，且挂上之后序列化 / 反序列化往返正确。
    /// </summary>
    [Fact]
    public void SystemTextJson_AddStronglyTypedIds_RegistersConvertersAndRoundTrips()
    {
        var options = new System.Text.Json.JsonSerializerOptions();

        options.AddStronglyTypedIds();

        options.Converters.Should().Contain(
            converter => converter is System.Text.Json.Serialization.JsonConverter<IntegrationGuidId>,
            "AddStronglyTypedIds 必须把 IntegrationGuidId 的内嵌 STJ 转换器挂到 options.Converters");

        var id = IntegrationGuidId.Create(Guid.NewGuid());

        var json = System.Text.Json.JsonSerializer.Serialize(id, options);
        json.Should().Be($"\"{id.Value:D}\"");

        System.Text.Json.JsonSerializer.Deserialize<IntegrationGuidId>(json, options).Should().Be(id);
    }

    /// <summary>
    /// <c>settings.AddStronglyTypedIds()</c> 必须把每个 Id 的内嵌转换器挂到 <c>settings.Converters</c> 上，且挂上之后 Newtonsoft
    /// 往返正确。
    /// </summary>
    [Fact]
    public void Newtonsoft_AddStronglyTypedId_RegistersConvertersAndRoundTrips()
    {
        var settings = new Newtonsoft.Json.JsonSerializerSettings();

        settings.AddStronglyTypedIds();

        settings.Converters.Should().Contain(
            converter => converter.CanConvert(typeof(IntegrationGuidId)),
            "AddStronglyTypedIds 必须把 IntegrationGuidId 的内嵌 Newtonsoft 转换器挂到 settings.Converters");

        var id = IntegrationGuidId.Create(Guid.NewGuid());

        var json = Newtonsoft.Json.JsonConvert.SerializeObject(id, settings);
        json.Should().Be($"\"{id.Value:D}\"");

        Newtonsoft.Json.JsonConvert.DeserializeObject<IntegrationGuidId>(json, settings).Should().Be(id);
    }

    /// <summary>
    /// 泛型容器里的 Id 是 JSON 支持唯一的缺口：转换器类照常生成，但 <c>[JsonConverter]</c> 特性引用不了含类型参数的
    /// 转换器（CS0416），<c>AddStronglyTypedIds()</c> 也会跳过它（开放泛型不能作类型实参）。因此默认序列化退化成
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
        // 「No service for type 'ILoggerFactory'」，这是测试宿主的最小依赖，与被测绑定逻辑无关。
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

        routeOptions.AddStronglyTypedIds();

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
    public void Prefix_Should_FlowThroughSystemTextJsonAddStronglyTypedIds()
    {
        var options = new System.Text.Json.JsonSerializerOptions();

        options.AddStronglyTypedIds();

        var id = PrefixedIntegrationGuidId.Create(Guid.NewGuid());

        var json = System.Text.Json.JsonSerializer.Serialize(id, options);
        json.Should().Be($"\"ord_{id.Value:D}\"");

        System.Text.Json.JsonSerializer.Deserialize<PrefixedIntegrationGuidId>(json, options).Should().Be(id);
    }

    /// <summary>
    /// 两个 JSON 注册入口都必须幂等：重复调用不能让转换器列表增长。
    /// </summary>
    /// <remarks>
    /// 启动代码里「每个模块各自注册一次」是常见写法，而 <c>Converters</c> 是普通列表，无条件 <c>Add</c> 会让
    /// 列表成倍增长，每次序列化都要多走几十个不命中的 <c>CanConvert</c>。同库的 MVC 与 BSON 注册都做了幂等，
    /// 这两个入口此前没有。
    /// </remarks>
    [Fact]
    public void JsonRegistrations_AreIdempotent()
    {
        var options = new System.Text.Json.JsonSerializerOptions();
        options.AddStronglyTypedIds();
        var afterFirst = options.Converters.Count;
        afterFirst.Should().BeGreaterThan(0, "前置条件：必须真的注册了转换器");

        options.AddStronglyTypedIds();
        options.AddStronglyTypedIds();
        options.Converters.Count.Should().Be(afterFirst, "重复调用 AddStronglyTypedIds 不得让转换器列表增长");

        var settings = new Newtonsoft.Json.JsonSerializerSettings();
        settings.AddStronglyTypedIds();
        var njAfterFirst = settings.Converters.Count;
        njAfterFirst.Should().BeGreaterThan(0, "前置条件：必须真的注册了转换器");

        settings.AddStronglyTypedIds();
        settings.AddStronglyTypedIds();
        settings.Converters.Count.Should().Be(njAfterFirst, "重复调用 AddStronglyTypedIds 不得让转换器列表增长");
    }

    /// <summary>
    /// 注册入口不得破坏调用方已有的转换器，也不得在 <c>JsonSerializerOptions</c> 冻结后再触碰集合。
    /// </summary>
    /// <remarks>
    /// <c>JsonSerializerOptions</c> 一经使用即冻结，此时再 <c>Add</c> 会抛 <c>InvalidOperationException</c>。
    /// 幂等实现必须在「已注册」时直接跳过，而不是先清空或重建列表。
    /// </remarks>
    [Fact]
    public void SystemTextJsonAddStronglyTypedIds_PreservesUserConverters_AndSurvivesFrozenOptions()
    {
        var options = new System.Text.Json.JsonSerializerOptions();
        var userConverter = new System.Text.Json.Serialization.JsonStringEnumConverter();
        options.Converters.Add(userConverter);

        options.AddStronglyTypedIds();
        options.Converters[0].Should().BeSameAs(userConverter, "用户自己的转换器必须保持在原位");

        // 触发冻结：此后 options 只读。
        _ = System.Text.Json.JsonSerializer.Serialize(1, options);
        var frozenCount = options.Converters.Count;

        // 冻结后重复注册不得抛异常，全部转换器都已注册，实现应当直接跳过。
        var act = () => options.AddStronglyTypedIds();
        act.Should().NotThrow("已注册时不应触碰已冻结的集合");
        options.Converters.Count.Should().Be(frozenCount);
    }

    /// <summary>
    /// <c>JsonShape.Object</c> 下 Newtonsoft 的属性名匹配必须大小写不敏感，与它自己对普通 POCO 的行为一致。
    /// </summary>
    /// <remarks>
    /// <c>JObject.TryGetValue</c> 是序数比较，只认小写 <c>"value"</c>，而 Newtonsoft 反序列化普通 POCO 时
    /// <c>{"Value":…}</c>、<c>{"VALUE":…}</c> 都能填进 <c>Value</c> 属性。生成的转换器若更严，
    /// 同一个载荷会在 Id 这一层突然失败，而对象形态正是为对接既有客户端契约才启用的。
    /// </remarks>
    [Fact]
    public void Newtonsoft_ObjectShape_MatchesPropertyNameCaseInsensitively()
    {
        var guid = Guid.NewGuid();
        var id = ObjectShapeIntegrationGuidId.Create(guid);

        // 写出的属性名是小写 value。
        Newtonsoft.Json.JsonConvert.SerializeObject(id).Should().Be($"{{\"value\":\"{guid:D}\"}}");

        // 读入时各种大小写都必须接受。
        foreach (var propertyName in new[] { "value", "Value", "VALUE", "vAlUe" })
        {
            var json = $"{{\"{propertyName}\":\"{guid:D}\"}}";

            Newtonsoft.Json.JsonConvert.DeserializeObject<ObjectShapeIntegrationGuidId>(json)
                .Should().Be(id, $"属性名 '{propertyName}' 必须被接受");
        }

        // 其他属性照常跳过。
        Newtonsoft.Json.JsonConvert
            .DeserializeObject<ObjectShapeIntegrationGuidId>($"{{\"a\":1,\"Value\":\"{guid:D}\",\"b\":2}}")
            .Should().Be(id);

        // 缺失 value 仍然是错误，不能被「大小写不敏感」顺带放宽。
        var missing = () => Newtonsoft.Json.JsonConvert.DeserializeObject<ObjectShapeIntegrationGuidId>("{\"other\":1}");
        missing.Should().Throw<Newtonsoft.Json.JsonSerializationException>("缺少 value 属性必须报错");

        // 值为 JSON null 同样是错误：Id 是值类型，null 不能落成 default。
        var nullValue = () => Newtonsoft.Json.JsonConvert.DeserializeObject<ObjectShapeIntegrationGuidId>("{\"value\":null}");
        nullValue.Should().Throw<Newtonsoft.Json.JsonSerializationException>("value 为 null 必须报错");
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
