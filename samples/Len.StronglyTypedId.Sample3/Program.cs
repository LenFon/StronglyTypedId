using Len.StronglyTypedId;

var builder = WebApplication.CreateBuilder(args);

// MVC：模型绑定器 + 每个 Id 的路由约束。
builder.Services.AddControllers(options => StronglyTypedIds.ApplyTo(options));
builder.Services.AddRouting(options => StronglyTypedIds.ApplyTo(options));

builder.Services.AddEndpointsApiExplorer();

// Swashbuckle 10（Microsoft.OpenApi 2.x）：把每个 Id 映射到其基元的 schema。
builder.Services.AddSwaggerGen(StronglyTypedIds.ApplyTo);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();
