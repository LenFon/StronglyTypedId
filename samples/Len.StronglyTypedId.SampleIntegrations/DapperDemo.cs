using Dapper;
using Microsoft.Data.Sqlite;

namespace Len.StronglyTypedId.SampleIntegrations;

internal static class DapperDemo
{
    public static async Task RunAsync()
    {
        Demo.Section("Dapper：StronglyTypedIds.ApplyTo(IDbConnection)");

        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        // 注册每个 Id 的 SqlMapper.TypeHandler。
        StronglyTypedIds.ApplyTo(connection);

        await connection.ExecuteAsync("CREATE TABLE Stock (Sku TEXT NOT NULL PRIMARY KEY, Quantity INTEGER NOT NULL)");

        // 写入：SetValue 把基元值交给参数，库里存的是裸基元而不是 JSON。
        await connection.ExecuteAsync(
            "INSERT INTO Stock (Sku, Quantity) VALUES (@Sku, @Quantity)",
            new { Sku = Sku.Create("SKU-002"), Quantity = 5 });

        // 读取：Parse 经 Create 还原 Id，验证器随之生效。
        var stock = await connection.QuerySingleAsync<StockRow>("SELECT Sku, Quantity FROM Stock");
        Demo.Line($"查询回来                          -> Sku={stock.Sku}, Quantity={stock.Quantity}");

        // 非 string 基元：SQLite 的 TEXT / INTEGER 列以 string / long 返回，而生成的 TypeHandler
        // 按基元类型强转，因此 Guid / int 基元需要返回原生类型的提供程序（如 SQL Server）才能直接往返。
        // 这里只演示写入侧：库里存的同样是裸基元。
        await connection.ExecuteAsync("CREATE TABLE Audit (Id TEXT NOT NULL)");
        await connection.ExecuteAsync(
            "INSERT INTO Audit (Id) VALUES (@Id)",
            new { Id = OrderId.Create(Guid.Parse("77777777-8888-9999-aaaa-bbbbbbbbbbbb")) });

        var stored = await connection.QuerySingleAsync<string>("SELECT Id FROM Audit");
        Demo.Line($"Guid 基元写入数据库                -> {stored}");
    }
}

internal sealed class StockRow
{
    public Sku Sku { get; set; }

    public int Quantity { get; set; }
}
