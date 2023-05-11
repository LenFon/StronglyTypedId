using Len.StronglyTypedId;
using Len.StronglyTypedId.Sample1;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.CodeAnalysis;

[assembly: ExcludeFromCodeCoverage]

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    //.AddNewtonsoftJson(options =>
    //{
    //    options.SerializerSettings.AddStronglyTypedId();
    //})
    ;
builder.Services.AddDbContext<SampleDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(StronglyTypedIds.ApplyTo);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

app.Run();