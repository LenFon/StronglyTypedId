using Len.StronglyTypedId;
using Len.StronglyTypedId.Domain;

var builder = WebApplication.CreateBuilder(args);

// 路由约束 {id:OrderId} 复用 MVC 集成生成的 RouteOptions 注册。
builder.Services.AddRouting(options => StronglyTypedIds.ApplyTo(options));

builder.Services.AddEndpointsApiExplorer();

// 8.0 线没有内置的文档生成器 —— AddOpenApi / MapOpenApi / IOpenApiSchemaTransformer 都是 9.0.0 才引入的，
// 所以这里没有 StronglyTypedIds.ApplyTo(OpenApiOptions) 可调，文档由 Swashbuckle 产出，
// Id 的 schema 映射走 ApplyTo(SwaggerGenOptions)。
builder.Services.AddSwaggerGen(StronglyTypedIds.ApplyTo);

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

// 路由参数经 IParsable<OrderId>.TryParse 绑定，格式非法时框架直接返回 400。
// WithOpenApi() 是 8.0 包里与 OpenAPI 相关的唯一 API：给操作打注解，由 Swashbuckle 读走。
//
// 实测（/swagger/v1/swagger.json）：请求 / 响应体里的 Id 属性拿到 type: string + format: uuid，
// 而路径参数只有 type: string —— 8.0 的 ApiExplorer 把实现了 IParsable 的参数报成 string，
// MapType 按类型注册，对这类参数无从下手。
app.MapGet("/orders/{id}", (OrderId id) => Results.Ok(id.Value))
    .WithName("GetOrder")
    .WithOpenApi();

// 路由约束：段格式在进入处理器之前就被校验。
app.MapGet("/orders/v2/{id:OrderId}", (OrderId id) => Results.Ok(id.Value))
    .WithName("GetOrderConstrained")
    .WithOpenApi();

// JSON 请求体 / 响应体：由 STJ 转换器负责，schema 取 Id 的基元形态。
app.MapPost("/orders", (CreateOrderRequest request) => TypedResults.Ok(request.Id))
    .WithName("CreateOrder")
    .WithOpenApi();

app.Run();

internal sealed record CreateOrderRequest(OrderId Id);
