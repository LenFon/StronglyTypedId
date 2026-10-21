using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Len.StronglyTypedId.SampleIntegrations;

internal static class EfCoreDemo
{
    public static async Task RunAsync()
    {
        Demo.Section("EF Core：StronglyTypedIds.ApplyTo(ModelConfigurationBuilder)");

        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ShopDbContext>().UseSqlite(connection).Options;

        await using var db = new ShopDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var order = new Order
        {
            Id = OrderId.Create(Guid.Parse("66666666-7777-8888-9999-aaaaaaaaaaaa")),
            Line = LineId.Create(1),
            Code = Sku.Create("SKU-001"),
            Quantity = 3,
        };

        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var loaded = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == order.Id);
        Demo.Line($"查询回来                          -> Id={loaded.Id}, Line={loaded.Line}, Code={loaded.Code}, Qty={loaded.Quantity}");

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Line, Code FROM Orders LIMIT 1";

        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();

        Demo.Line($"数据库列（基元）                   -> Id={reader.GetValue(0)}, Line={reader.GetValue(1)}, Code={reader.GetValue(2)}");
    }
}
