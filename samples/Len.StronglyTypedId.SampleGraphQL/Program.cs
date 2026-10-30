using HotChocolate;
using HotChocolate.Execution;
using Microsoft.Extensions.DependencyInjection;

namespace Len.StronglyTypedId.SampleGraphQL;

/// <summary>
/// GraphQL 集成的可运行示例：构建 schema、执行查询，并打印每个 Id 在 GraphQL 里的表示形态。
/// </summary>
/// <remarks>
/// 用 <c>AddGraphQLServer</c> + <c>GetRequestExecutorAsync</c> 直接跑执行器，不启动 HTTP 服务器 ——
/// 示例要展示的是 scalar 的表示口径，而不是宿主配置。
/// </remarks>
public static class Program
{
    public static async Task Main()
    {
        var services = new ServiceCollection();

        // 生成入口：注册每个 Id 的 scalar，并让 HotChocolate 用它们表示对应的强类型 Id。
        // 与 AddGraphQLServer / AddQueryType 一样是链式扩展方法，幂等，重复调用不会重复注册。
        services
            .AddGraphQLServer()
            .AddQueryType<Query>()
            .AddStronglyTypedIds();

        await using var provider = services.BuildServiceProvider();
        var executor = await provider.GetRequestExecutorAsync();

        Demo.Section("GraphQL：Id 在 schema 里是 SCALAR");
        await RunAsync(executor, "{ __type(name: \"OrderId\") { kind } }");

        Demo.Section("文本基元（Guid）→ GraphQL String");
        await RunAsync(executor, "{ order(id: \"11111111-1111-1111-1111-111111111111\") { id } }");

        Demo.Section("整数基元（int）→ GraphQL Int");
        await RunAsync(executor, "{ line(id: 42) { id } }");

        Demo.Section("枚举基元 → 底层整数，不按成员名");
        await RunAsync(executor, "{ status(id: 1) { id } }");

        Demo.Section("带前缀的 Id → 规范文本形（前缀一起写出）");
        await RunAsync(executor, "{ sku(id: \"sku_SKU-002\") { id } }");

        Demo.Section("变量输入：真实 HTTP 管道交给 scalar 的是 JsonElement");
        await RunAsync(
            executor,
            "query($id: OrderId!) { order(id: $id) { id } }",
            new Dictionary<string, object?> { ["id"] = "22222222-2222-2222-2222-222222222222" });

        Demo.Section("失败路径：格式非法时报带位置的 GraphQL 错误");
        await RunAsync(executor, "{ order(id: \"not-a-guid\") { id } }");

        Demo.Section("失败路径：未定义的枚举数值被拒绝");
        await RunAsync(executor, "{ status(id: 999) { id } }");

        Demo.End();
    }

    private static async Task RunAsync(
        IRequestExecutor executor,
        string query,
        Dictionary<string, object?>? variables = null)
    {
        var request = QueryRequestBuilder.New().SetQuery(query);

        if (variables is not null)
        {
            request.SetVariableValues(variables);
        }

        var result = await executor.ExecuteAsync(request.Create());

        Demo.Line(query);
        Demo.Line("  -> " + result.ToJson().Replace("\r\n", " ").Replace("\n", " "));
    }
}
