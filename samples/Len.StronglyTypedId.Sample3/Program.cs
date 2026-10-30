using Len.StronglyTypedId;

var builder = WebApplication.CreateBuilder(args);

// MVC：模型绑定器 + 每个 Id 的路由约束。
builder.Services.AddControllers(options => options.AddStronglyTypedIds());
builder.Services.AddRouting(options => options.AddStronglyTypedIds());

builder.Services.AddEndpointsApiExplorer();

// Swashbuckle 10（Microsoft.OpenApi 2.x）：把每个 Id 映射到其基元的 schema。
builder.Services.AddSwaggerGen(options => options.AddStronglyTypedIds());

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();
