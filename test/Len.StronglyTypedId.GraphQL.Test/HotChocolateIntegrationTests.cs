using System.Globalization;
using System.Text.Json;
using FluentAssertions;
using HotChocolate;
using HotChocolate.Execution;
using Microsoft.Extensions.DependencyInjection;

namespace Len.StronglyTypedId.GraphQL;

/// <summary>
/// HotChocolate 集成的端到端回归：用真实包构建 schema 并执行查询，验证 scalar 的序列化、反序列化、
/// 字面量解析、失败路径与注册入口幂等。
/// </summary>
/// <remarks>
/// 生成器层只锁定文本形态（<c>HotChocolateCodeGeneratorTests</c>）；本文件补运行时语义 ——
/// 缺口往往出在「生成的代码能编译，但 scalar 契约没满足框架期望」这一层。
/// </remarks>
public class HotChocolateIntegrationTests
{
    #region 输出：Id 按 JSON 口径写出

    /// <summary>
    /// <c>Guid</c> 基元在 GraphQL 里是字符串，与 JSON 形态一致。
    /// </summary>
    [Fact]
    public async Task Query_Should_SerializeGuidId_AsString()
    {
        var executor = await CreateExecutorAsync();

        var result = await ExecuteAsync(executor, "{ order(id: \"11111111-1111-1111-1111-111111111111\") { id } }");

        GetId(result, "order").GetString().Should().Be("11111111-1111-1111-1111-111111111111");
    }

    /// <summary>
    /// 整数基元在 GraphQL 里是数字，不是字符串 —— 与 JSON 形态一致。
    /// </summary>
    [Fact]
    public async Task Query_Should_SerializeIntId_AsNumber()
    {
        var executor = await CreateExecutorAsync();

        var result = await ExecuteAsync(executor, "{ product(id: 42) { id } }");

        GetId(result, "product").ValueKind.Should().Be(JsonValueKind.Number);
        GetId(result, "product").GetInt32().Should().Be(42);
    }

    /// <summary>
    /// 枚举基元按底层整数写出，与 STJ 默认口径一致；不按成员名。
    /// </summary>
    [Fact]
    public async Task Query_Should_SerializeEnumId_AsUnderlyingNumber()
    {
        var executor = await CreateExecutorAsync();

        var result = await ExecuteAsync(executor, "{ status(id: 1) { id } }");

        GetId(result, "status").ValueKind.Should().Be(JsonValueKind.Number);
        GetId(result, "status").GetInt32().Should().Be(1);
    }

    /// <summary>
    /// 带前缀的 Id 对外是规范文本形，前缀必须一起写出。
    /// </summary>
    [Fact]
    public async Task Query_Should_SerializePrefixedId_WithPrefix()
    {
        var executor = await CreateExecutorAsync();

        var result = await ExecuteAsync(executor, "{ prefixed(id: \"ord_22222222-2222-2222-2222-222222222222\") { id } }");

        GetId(result, "prefixed").GetString().Should().Be("ord_22222222-2222-2222-2222-222222222222");
    }

    #endregion

    #region 输入：字面量与变量

    [Fact]
    public async Task Query_Should_AcceptGuidId_AsVariable()
    {
        var executor = await CreateExecutorAsync();

        var result = await ExecuteAsync(
            executor,
            "query($id: OrderId!) { order(id: $id) { id } }",
            new Dictionary<string, object?> { ["id"] = "33333333-3333-3333-3333-333333333333" });

        GetId(result, "order").GetString().Should().Be("33333333-3333-3333-3333-333333333333");
    }

    /// <summary>
    /// 真实 HTTP 管道把变量交给 scalar 时是 <see cref="JsonElement"/>，不是 <see cref="string"/>。
    /// 只处理 <c>string</c> 的实现在单元测试里能过，上线后每个变量都会失败。
    /// </summary>
    [Fact]
    public async Task Query_Should_AcceptJsonElementVariable()
    {
        var executor = await CreateExecutorAsync();

        using var document = JsonDocument.Parse("\"44444444-4444-4444-4444-444444444444\"");

        var result = await ExecuteAsync(
            executor,
            "query($id: OrderId!) { order(id: $id) { id } }",
            new Dictionary<string, object?> { ["id"] = document.RootElement });

        GetId(result, "order").GetString().Should().Be("44444444-4444-4444-4444-444444444444");
    }

    [Fact]
    public async Task Query_Should_AcceptIntId_AsVariable()
    {
        var executor = await CreateExecutorAsync();

        var result = await ExecuteAsync(
            executor,
            "query($id: ProductId!) { product(id: $id) { id } }",
            new Dictionary<string, object?> { ["id"] = 7 });

        GetId(result, "product").GetInt32().Should().Be(7);
    }

    [Fact]
    public async Task Query_Should_AcceptEnumId_AsVariable()
    {
        var executor = await CreateExecutorAsync();

        var result = await ExecuteAsync(
            executor,
            "query($id: StatusId!) { status(id: $id) { id } }",
            new Dictionary<string, object?> { ["id"] = 1 });

        GetId(result, "status").GetInt32().Should().Be(1);
    }

    #endregion

    #region 失败路径

    /// <summary>
    /// 格式非法的字面量必须报错，且消息来自本库的 scalar（不是框架的「Unexpected Execution Error」）。
    /// </summary>
    [Fact]
    public async Task Query_Should_ReportError_ForMalformedLiteral()
    {
        var executor = await CreateExecutorAsync();

        var result = await ExecuteAsync(executor, "{ order(id: \"not-a-guid\") { id } }");

        GetErrors(result).Should().NotBeEmpty();
    }

    /// <summary>
    /// 变量格式非法时同样要报错，而不是静默落成 <c>default</c>。
    /// </summary>
    [Fact]
    public async Task Query_Should_ReportError_ForMalformedVariable()
    {
        var executor = await CreateExecutorAsync();

        var result = await ExecuteAsync(
            executor,
            "query($id: OrderId!) { order(id: $id) { id } }",
            new Dictionary<string, object?> { ["id"] = "not-a-guid" });

        GetErrors(result).Should().NotBeEmpty();
    }

    /// <summary>
    /// 未定义的枚举数值必须被拒绝：<c>TryCreate</c> 只跑用户配的 Validator，而枚举的已定义性由
    /// 生成的 <c>TryParse</c> 负责，scalar 必须走 <c>TryParse</c> 而不是直接构造。
    /// </summary>
    [Fact]
    public async Task Query_Should_RejectUndefinedEnumValue()
    {
        var executor = await CreateExecutorAsync();

        var result = await ExecuteAsync(executor, "{ status(id: 999) { id } }");

        GetErrors(result).Should().NotBeEmpty();
    }

    /// <summary>
    /// 验证器拒绝的值必须报错，且不能让 <c>ArgumentException</c> 穿透成「Unexpected Execution Error」。
    /// </summary>
    [Fact]
    public async Task Query_Should_RejectValue_WhenValidatorFails()
    {
        var executor = await CreateExecutorAsync();

        var result = await ExecuteAsync(executor, "{ validated(id: \"00000000-0000-0000-0000-000000000000\") { id } }");

        GetErrors(result).Should().NotBeEmpty();
    }

    #endregion

    #region 注册入口

    /// <summary>
    /// <c>AddStronglyTypedIds</c> 必须幂等：重复调用不能让 schema 构建失败。
    /// </summary>
    [Fact]
    public async Task AddStronglyTypedIds_Should_BeIdempotent()
    {
        var services = new ServiceCollection();

        services.AddGraphQLServer()
            .AddQueryType<Query>()
            .AddStronglyTypedIds()
            .AddStronglyTypedIds();

        var act = async () => await services.BuildServiceProvider().GetRequestExecutorAsync();

        await act.Should().NotThrowAsync();
    }

    /// <summary>
    /// 重复调用不得留下第二条 scalar 注册：<c>TryAddSingleton</c> 是这条保证的来源，
    /// 换成 <c>AddSingleton</c> 会累积注册（虽然仍能工作，但语义上不幂等）。
    /// </summary>
    [Fact]
    public void AddStronglyTypedIds_Should_RegisterEachScalarOnce()
    {
        var services = new ServiceCollection();

        services.AddGraphQLServer()
            .AddQueryType<Query>()
            .AddStronglyTypedIds()
            .AddStronglyTypedIds()
            .AddStronglyTypedIds();

        services.Count(descriptor => descriptor.ServiceType == typeof(Len.StronglyTypedId.StronglyTypedIds.OrderIdHotChocolateType))
            .Should().Be(1);
    }

    /// <summary>
    /// 重复调用不得改变 schema：重复的 <c>BindRuntimeType</c> 不会让 scalar 在 schema 里出现两次。
    /// </summary>
    [Fact]
    public async Task AddStronglyTypedIds_Should_NotChangeSchema_WhenCalledRepeatedly()
    {
        var once = await BuildSchemaAsync(1);
        var thrice = await BuildSchemaAsync(3);

        thrice.Should().Be(once);
        once.Split("scalar OrderId").Length.Should().Be(2, "schema 里只应出现一次 scalar OrderId");
    }

    /// <summary>
    /// 注册后 Id 在 schema 里必须是 <c>SCALAR</c>，而不是被推断成对象类型。
    /// </summary>
    [Fact]
    public async Task AddStronglyTypedIds_Should_RegisterIdAsScalar()
    {
        var executor = await CreateExecutorAsync();

        var result = await ExecuteAsync(executor, "{ __type(name: \"OrderId\") { kind } }");

        using var document = JsonDocument.Parse(result);
        document.RootElement.GetProperty("data").GetProperty("__type").GetProperty("kind").GetString()
            .Should().Be("SCALAR");
    }

    #endregion

    #region 辅助

    private static async Task<IRequestExecutor> CreateExecutorAsync()
    {
        var services = new ServiceCollection();

        services.AddGraphQLServer()
            .AddQueryType<Query>()
            .AddStronglyTypedIds();

        return await services.BuildServiceProvider().GetRequestExecutorAsync();
    }

    /// <summary>
    /// 构建 schema，并按需重复调用 <c>AddStronglyTypedIds</c>。
    /// </summary>
    private static async Task<string> BuildSchemaAsync(int times)
    {
        var services = new ServiceCollection();
        var builder = services.AddGraphQLServer().AddQueryType<Query>();

        for (var index = 0; index < times; index++)
        {
            builder.AddStronglyTypedIds();
        }

        var executor = await services.BuildServiceProvider().GetRequestExecutorAsync();

        return executor.Schema.ToString();
    }

    private static async Task<string> ExecuteAsync(
        IRequestExecutor executor,
        string query,
        Dictionary<string, object?>? variables = null)
    {
        var builder = QueryRequestBuilder.New().SetQuery(query);

        if (variables is not null)
        {
            builder.SetVariableValues(variables);
        }

        var result = await executor.ExecuteAsync(builder.Create());

        return result.ToJson();
    }

    /// <summary>
    /// 取 <c>data.{field}.id</c> 节点。
    /// </summary>
    private static JsonElement GetId(string result, string field)
    {
        using var document = JsonDocument.Parse(result);

        return document.RootElement
            .GetProperty("data")
            .GetProperty(field)
            .GetProperty("id")
            .Clone();
    }

    /// <summary>
    /// 取错误数组；没有 <c>errors</c> 时返回空数组。
    /// </summary>
    private static JsonElement[] GetErrors(string result)
    {
        using var document = JsonDocument.Parse(result);

        return document.RootElement.TryGetProperty("errors", out var errors)
            ? [.. errors.EnumerateArray().Select(static error => error.Clone())]
            : [];
    }

    #endregion
}

/// <summary>
/// 端到端用例的查询根：每个字段只回显收到的 Id，让 scalar 的两侧都被走到。
/// </summary>
/// <remarks>
/// 方法刻意保持实例成员：HotChocolate 只把实例方法当作字段解析，改成 static 会让 schema 里一个字段都没有。
/// </remarks>
#pragma warning disable CA1822
public sealed class Query
{
    public OrderDto Order(OrderId id) => new(id);

    public ProductDto Product(ProductId id) => new(id);

    public StatusDto Status(StatusId id) => new(id);

    public PrefixedDto Prefixed(PrefixedOrderId id) => new(id);

    public ValidatedDto Validated(ValidatedOrderId id) => new(id);
}
#pragma warning restore CA1822

public sealed record OrderDto(OrderId Id);

public sealed record ProductDto(ProductId Id);

public sealed record StatusDto(StatusId Id);

public sealed record PrefixedDto(PrefixedOrderId Id);

public sealed record ValidatedDto(ValidatedOrderId Id);
