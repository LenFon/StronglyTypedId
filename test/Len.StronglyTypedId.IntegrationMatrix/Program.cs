using System.Text.Json;
using Dapper;
using HotChocolate.Execution;
using Len.StronglyTypedId;
using Len.StronglyTypedId.IntegrationMatrix;
using MessagePack;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using ProtoBuf;
using Swashbuckle.AspNetCore.SwaggerGen;

// 集成版本矩阵探针：编译通过是主判据，这里再对每个集成跑一次真实往返。
// 任何一项失败都以非零退出码结束，CI 据此判断该版本组合是否可用。
var failures = new List<string>();

void Check(string name, Action action)
{
    try
    {
        action();
        Console.WriteLine($"  ok   {name}");
    }
    catch (Exception exception)
    {
        failures.Add($"{name}: {exception.GetType().Name}: {exception.Message}");
        Console.WriteLine($"  FAIL {name}: {exception.GetType().Name}: {exception.Message}");
    }
}

Console.WriteLine($"Integration matrix probe on {Environment.Version}");

var guid = Guid.Parse("11111111-2222-3333-4444-555555555555");

Check("System.Text.Json", () =>
{
    var id = MatrixGuidId.Create(guid);
    var json = JsonSerializer.Serialize(id);
    Ensure(json == $"\"{guid}\"", $"unexpected JSON: {json}");
    Ensure(JsonSerializer.Deserialize<MatrixGuidId>(json) == id, "round-trip mismatch");
});

Check("Newtonsoft.Json", () =>
{
    var id = MatrixIntId.Create(42);
    var settings = new Newtonsoft.Json.JsonSerializerSettings();
    settings.AddStronglyTypedIds();
    var json = Newtonsoft.Json.JsonConvert.SerializeObject(id, settings);
    var back = Newtonsoft.Json.JsonConvert.DeserializeObject<MatrixIntId>(json, settings);
    Ensure(back == id, $"round-trip mismatch: {json}");
});

Check("MessagePack", () =>
{
    var options = MessagePackSerializerOptions.Standard.AddStronglyTypedIds();
    var id = MatrixDecimalId.Create(12.5m);
    var bytes = MessagePackSerializer.Serialize(id, options);
    Ensure(MessagePackSerializer.Deserialize<MatrixDecimalId>(bytes, options) == id, "round-trip mismatch");
});

Check("MongoDB.Bson", () =>
{
    BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
    StronglyTypedIds.RegisterBsonSerializers();

    // BSON 的根只能是文档，标量必须包一层字段再读写。
    var id = MatrixStringId.Create("abc");
    var document = new BsonDocument();

    using (var writer = new BsonDocumentWriter(document))
    {
        writer.WriteStartDocument();
        writer.WriteName("v");
        BsonSerializer.Serialize(writer, id);
        writer.WriteEndDocument();
    }

    // 读回也必须从字段位置读：标量序列化器不接受整篇文档。
    using var reader = new BsonDocumentReader(new BsonDocument { { "v", document["v"] } });
    reader.ReadStartDocument();
    reader.ReadName();
    var back = BsonSerializer.Deserialize<MatrixStringId>(reader);
    Ensure(back == id, "round-trip mismatch");
});
Check("protobuf-net", () =>
{
    var id = MatrixDateOnlyId.Create(new DateOnly(2026, 11, 9));
    using var stream = new MemoryStream();
    Serializer.Serialize(stream, id);
    stream.Position = 0;
    Ensure(Serializer.Deserialize<MatrixDateOnlyId>(stream) == id, "round-trip mismatch");
});

Check("EF Core + SQLite", () =>
{
    using var context = new MatrixDbContext();
    context.Database.OpenConnection();
    context.Database.EnsureCreated();

    var id = MatrixGuidId.Create(guid);
    context.Set<MatrixEntity>().Add(new MatrixEntity { Id = 1, Required = id, Optional = null });
    context.SaveChanges();
    context.ChangeTracker.Clear();

    var row = context.Set<MatrixEntity>().Single();
    Ensure(row.Required == id, "round-trip mismatch");
    Ensure(row.Optional is null, "nullable id must stay null");
});

Check("Dapper + SQLite", () =>
{
    StronglyTypedIds.AddStronglyTypedIds();

    using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
    connection.Open();
    connection.Execute("CREATE TABLE Probe (Id TEXT NOT NULL, Amount REAL NOT NULL, Status INTEGER NOT NULL)");

    connection.Execute(
        "INSERT INTO Probe (Id, Amount, Status) VALUES (@Id, @Amount, @Status)",
        new
        {
            Id = MatrixGuidId.Create(guid),
            Amount = MatrixDecimalId.Create(1.5m),
            Status = MatrixStatusId.Create(MatrixStatus.Suspended),
        });

    var row = connection.QuerySingle<ProbeRow>("SELECT Id, Amount, Status FROM Probe");
    Ensure(row.Id == MatrixGuidId.Create(guid), "Guid round-trip mismatch");
    Ensure(row.Amount == MatrixDecimalId.Create(1.5m), "decimal round-trip mismatch");
    Ensure(row.Status == MatrixStatusId.Create(MatrixStatus.Suspended), "enum round-trip mismatch");
});

Check("Swashbuckle", () =>
{
    var options = new SwaggerGenOptions();
    options.AddStronglyTypedIds();
    Ensure(options.SchemaGeneratorOptions.CustomTypeMappings.Count > 0, "no schema mappings registered");
});

#if !EXCLUDE_ASPNETCORE_OPENAPI
Check("ASP.NET Core OpenAPI", () =>
{
    var options = new Microsoft.AspNetCore.OpenApi.OpenApiOptions();
    options.AddStronglyTypedIds();

    // 转换器集合是私有的，只能通过「注册不抛异常」证明入口接得上；
    // schema 内容由常规测试工程的端到端用例覆盖。
    Ensure(options is not null, "options was not returned");
});
#else
Console.WriteLine("  skip ASP.NET Core OpenAPI (excluded to keep Microsoft.OpenApi 1.x for Swashbuckle)");
#endif

Check("ASP.NET Core MVC", () =>
{
    var mvc = new MvcOptions();
    mvc.AddStronglyTypedIds();
    Ensure(mvc.ModelBinderProviders.Count > 0, "no model binder registered");

    var routing = new RouteOptions();
    routing.AddStronglyTypedIds();
    Ensure(routing.ConstraintMap.ContainsKey(nameof(MatrixGuidId)), "route constraint not registered");
});

Check("HotChocolate", () =>
{
    var services = new ServiceCollection();
    services.AddGraphQLServer()
        .AddQueryType<MatrixQuery>()
        .AddStronglyTypedIds();

    // 构建 schema 才会真正实例化生成的 scalar；只注册不构建时，scalar 契约不满足框架期望也发现不了。
    using var provider = services.BuildServiceProvider();
    var executor = provider.GetRequestExecutorAsync().GetAwaiter().GetResult();
    Ensure(executor is not null, "executor was not built");
});

if (failures.Count > 0)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine($"{failures.Count} integration(s) failed:");

    foreach (var failure in failures)
    {
        Console.Error.WriteLine($"  {failure}");
    }

    return 1;
}

Console.WriteLine();
Console.WriteLine("All integrations passed.");

return 0;

static void Ensure(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
