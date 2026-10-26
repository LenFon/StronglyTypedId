using System.Text.Json;
using FluentAssertions;
using Len.StronglyTypedId.Layered;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Len.StronglyTypedId.Swagger;

/// <summary>
/// Minimal API 参数在 Swagger 文档里的 schema 回归。
/// </summary>
/// <remarks>
/// 缺口出在框架与 Swashbuckle 的交界处：API explorer 把实现 <c>IParsable</c> 的参数按「可解析的文本」报告，
/// <c>ApiParameterDescription.Type</c> 仍是真实 Id 类型，但 <c>ModelMetadata.ModelType</c> 已被换成
/// <see cref="string"/>。Swashbuckle 据后者生成 schema，于是 Id 参数在文档里只剩裸的 <c>type: string</c>。
/// 这类缺口最难在评审里发现：文档照常生成、客户端照常能调，只是少了 <c>format: uuid</c> 这类信息。
/// </remarks>
public class SwaggerParameterSchemaTests
{
    /// <summary>
    /// 路由参数必须带着 Id 的 schema 一起出现。
    /// </summary>
    [Fact]
    public async Task RouteParameter_Should_KeepTheIdSchema()
    {
        var document = await GetSwaggerDocumentAsync("/orders/{id}", (LayeredOrderId id) => Results.Ok(id.Value));

        var schema = GetParameterSchema(document, "/orders/{id}", "id");

        schema.GetProperty("type").GetString().Should().Be("string");
        schema.GetProperty("format").GetString().Should().Be("uuid");
    }

    /// <summary>
    /// 查询参数走的是同一条路径（框架同样把它按文本报），必须一起被补上。
    /// </summary>
    [Fact]
    public async Task QueryParameter_Should_KeepTheIdSchema()
    {
        var document = await GetSwaggerDocumentAsync("/orders", (LayeredOrderId id) => Results.Ok(id.Value));

        var schema = GetParameterSchema(document, "/orders", "id");

        schema.GetProperty("type").GetString().Should().Be("string");
        schema.GetProperty("format").GetString().Should().Be("uuid");
    }

    /// <summary>
    /// 请求体里的 Id 属性本来就被 <c>MapType</c> 照顾到；这条断言是为了守住「补参数没有把请求体弄坏」。
    /// </summary>
    [Fact]
    public async Task RequestBody_Should_KeepTheIdSchema()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(StronglyTypedIds.ApplyTo);

        var app = builder.Build();
        app.UseSwagger();
        app.MapPost("/orders", (CreateOrderRequest request) => Results.Ok(request.Id));

        await app.StartAsync(TestContext.Current.CancellationToken);

        try
        {
            var document = await GetSwaggerDocumentAsync(app);

            var idSchema = document
                .GetProperty("components")
                .GetProperty("schemas")
                .GetProperty("CreateOrderRequest")
                .GetProperty("properties")
                .GetProperty("id");

            idSchema.GetProperty("format").GetString().Should().Be("uuid");
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    /// <summary>
    /// 注册入口必须幂等：重复调用不让过滤器列表增长。
    /// </summary>
    [Fact]
    public void ApplyTo_Should_NotRegisterTheFilterTwice()
    {
        var options = new Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions();

        StronglyTypedIds.ApplyTo(options);
        var countAfterFirstCall = options.SwaggerGeneratorOptions.OperationFilters.Count;

        StronglyTypedIds.ApplyTo(options);

        options.SwaggerGeneratorOptions.OperationFilters.Should().HaveCount(countAfterFirstCall);
    }

    private static async Task<JsonElement> GetSwaggerDocumentAsync(string pattern, Delegate handler)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(StronglyTypedIds.ApplyTo);

        var app = builder.Build();
        app.UseSwagger();
        app.MapGet(pattern, handler);

        await app.StartAsync(TestContext.Current.CancellationToken);

        try
        {
            return await GetSwaggerDocumentAsync(app);
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    private static async Task<JsonElement> GetSwaggerDocumentAsync(WebApplication app)
    {
        var client = app.GetTestClient();

        var json = await client.GetStringAsync("/swagger/v1/swagger.json", TestContext.Current.CancellationToken);

        using var document = JsonDocument.Parse(json);

        return document.RootElement.Clone();
    }

    private static JsonElement GetParameterSchema(JsonElement document, string path, string parameterName)
    {
        var parameters = document
            .GetProperty("paths")
            .GetProperty(path)
            .GetProperty("get")
            .GetProperty("parameters");

        var parameter = parameters
            .EnumerateArray()
            .Single(item => item.GetProperty("name").GetString() == parameterName);

        return parameter.GetProperty("schema");
    }

    internal sealed record CreateOrderRequest(LayeredOrderId Id);
}
