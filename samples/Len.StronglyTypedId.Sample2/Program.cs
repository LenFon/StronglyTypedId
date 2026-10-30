using Len.StronglyTypedId;
using Len.StronglyTypedId.Domain;

var builder = WebApplication.CreateBuilder(args);

// Minimal API 不需要额外的绑定代码：路由 / 查询参数经 IParsable<T> 绑定。
// 路由约束 {id:OrderId} 复用 MVC 集成生成的 RouteOptions 注册，所以这里只注册路由。
builder.Services.AddRouting(options => options.AddStronglyTypedIds());

// ASP.NET Core OpenAPI（≥ 9.0.0）：把每个 Id 映射到其基元的 schema。
builder.Services.AddOpenApi(options => options.AddStronglyTypedIds());

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// 路由参数："..." 经 IParsable<OrderId>.TryParse 绑定，格式非法时框架直接返回 400。
app.MapGet("/orders/{id}", (OrderId id) => Results.Ok(id.Value))
    .WithName("GetOrder");

// [AsParameters]：按名字把路由 / 查询值绑到记录的各个 Id 属性上。
app.MapGet("/orders", ([AsParameters] OrderQuery query) => Results.Ok(query.Id.Value))
    .WithName("QueryOrders");

// JSON 请求体 / 响应体：由 STJ 转换器负责，schema 取 Id 的基元形态。
app.MapPost("/orders", (CreateOrderRequest request) => TypedResults.Ok(request.Id))
    .WithName("CreateOrder");

// 路由约束：段格式在进入处理器之前就被校验。
app.MapGet("/orders/v2/{id:OrderId}", (OrderId id) => Results.Ok(id.Value))
    .WithName("GetOrderConstrained");

app.Run();

internal sealed record OrderQuery(OrderId Id);

internal sealed record CreateOrderRequest(OrderId Id);
