using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Len.StronglyTypedId.SampleIntegrations;

internal static class EfCoreDemo
{
    public static async Task RunAsync()
    {
        await ConventionRegistrationAsync();
        await ModelBuilderRegistrationAsync();
    }

    private static async Task ConventionRegistrationAsync()
    {
        Demo.Section("EF Core：ModelConfigurationBuilder 上的 AddStronglyTypedIds()");

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

        // 列类型与基元单独存在时完全一致：引入 Id 不产生任何迁移。
        await using var schemaCommand = connection.CreateCommand();
        schemaCommand.CommandText = "SELECT type FROM pragma_table_info('Orders') WHERE name IN ('Id', 'Line', 'Code') ORDER BY cid";

        await using var schemaReader = await schemaCommand.ExecuteReaderAsync();
        var columnTypes = new List<string>();

        while (await schemaReader.ReadAsync())
        {
            columnTypes.Add(schemaReader.GetString(0));
        }

        Demo.Line($"SQLite 列类型                       -> {string.Join(", ", columnTypes)}");
    }

    private static async Task ModelBuilderRegistrationAsync()
    {
        Demo.Section("EF Core：OnModelCreating 里的 AddStronglyTypedIds(ModelBuilder)");

        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<LedgerDbContext>().UseSqlite(connection).Options;

        await using var db = new LedgerDbContext(options);
        await db.Database.EnsureCreatedAsync();

        db.Entries.Add(new LedgerRecord
        {
            Id = LedgerId.Create(Guid.Parse("55555555-6666-7777-8888-999999999999")),
            Kind = KindId.Create(LedgerKind.Debit),
            Amount = PriceId.Create(new Money(9.99m, "USD")),
            Sequence = 1,
        });

        await db.SaveChangesAsync();

        var entry = await db.Entries.AsNoTracking().SingleAsync();
        Demo.Line($"查询回来                          -> Id={entry.Id}, Kind={entry.Kind}, Amount={entry.Amount}");

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Kind, Amount FROM Ledger LIMIT 1";

        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();

        Demo.Line($"数据库列                           -> Id={reader.GetValue(0)}, Kind={reader.GetValue(1)}, Amount={reader.GetValue(2)}");

        Demo.Section("EF Core：自定义基元的 provider 类型是 string");

        // 数据库不认识用户自定义的 CLR 类型，因此自定义基元的转换器写的是规范文本。
        await using var priceCommand = connection.CreateCommand();
        priceCommand.CommandText = "SELECT typeof(Amount) FROM Ledger LIMIT 1";

        Demo.Line($"Amount 列的 SQLite 存储类型         -> {await priceCommand.ExecuteScalarAsync()}");
    }
}
