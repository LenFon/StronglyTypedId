using Newtonsoft.Json;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddStronglyTypedId(options =>
    {
        options.RegisterServicesFromAssembly(typeof(Program).Assembly);
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.AddStronglyTypedId();
    })
    //.AddNewtonsoftJson(options =>
    //{
    //    options.SerializerSettings.AddStronglyTypedId();
    //})
    ;


// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddStronglyTypedId(typeof(Program).Assembly);
});

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
