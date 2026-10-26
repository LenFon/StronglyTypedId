using Len.StronglyTypedId;
using Len.StronglyTypedId.Sample1;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.CodeAnalysis;

[assembly: ExcludeFromCodeCoverage]

var builder = WebApplication.CreateBuilder(args);

// MVC：注册强类型 Id 的模型绑定器，[FromRoute] / [FromQuery] 才能绑上。
// AddNewtonsoftJson 会把 MVC 的 JSON 格式化器整体换成 Newtonsoft，因此同时注册它的转换器；
// 想保留 System.Text.Json 的话去掉这一行即可（[JsonConverter] 特性仍然生效）。
builder.Services
    .AddControllers(options => StronglyTypedIds.ApplyTo(options))
    .AddNewtonsoftJson(options => options.SerializerSettings.AddStronglyTypedId());

// 路由约束：{id:MyId} 这类模板由这里注册，Minimal API 复用同一份注册。
builder.Services.AddRouting(options => StronglyTypedIds.ApplyTo(options));

builder.Services.AddDbContext<SampleDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Swagger/OpenAPI 的配置说明见 https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(StronglyTypedIds.ApplyTo);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<SampleDbContext>().Database.MigrateAsync();
}

// 配置 HTTP 请求管道。
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
