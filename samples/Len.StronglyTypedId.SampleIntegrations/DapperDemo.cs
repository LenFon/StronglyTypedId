using Dapper;
using Microsoft.Data.Sqlite;

namespace Len.StronglyTypedId.SampleIntegrations;

internal static class DapperDemo
{
    public static async Task RunAsync()
    {
        Demo.Section("Dapper：StronglyTypedIds.ApplyTo()");

        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        // 注册每个 Id 的 SqlMapper.TypeHandler。Dapper 的注册表是进程级的，与具体连接无关，
        // 因此只需调用一次；带参重载 ApplyTo(connection) 仅为源码兼容保留，会忽略实参。
        StronglyTypedIds.ApplyTo();

        await connection.ExecuteAsync("CREATE TABLE Stock (Sku TEXT NOT NULL PRIMARY KEY, Quantity INTEGER NOT NULL)");

        // 写入：SetValue 把基元值交给参数，库里存的是裸基元而不是 JSON。
        await connection.ExecuteAsync(
            "INSERT INTO Stock (Sku, Quantity) VALUES (@Sku, @Quantity)",
            new { Sku = Sku.Create("SKU-002"), Quantity = 5 });

        // 读取：Parse 经 Create 还原 Id，验证器随之生效。
        var stock = await connection.QuerySingleAsync<StockRow>("SELECT Sku, Quantity FROM Stock");
        Demo.Line($"查询回来                          -> Sku={stock.Sku}, Quantity={stock.Quantity}");

        // 非 string 基元：SQLite 的 TEXT 列以 string 返回、INTEGER 列以 long 返回，都不是基元本身的
        // CLR 类型。生成的 TypeHandler 会先按基元转换（Guid 走文本解析、int 走 Convert.ToInt32），
        // 再经 Create 还原，所以 Guid / int 基元同样能直接往返，无需「返回原生类型的提供程序」。
        await connection.ExecuteAsync("CREATE TABLE Audit (Id TEXT NOT NULL, Line INTEGER NOT NULL)");
        await connection.ExecuteAsync(
            "INSERT INTO Audit (Id, Line) VALUES (@Id, @Line)",
            new
            {
                Id = OrderId.Create(Guid.Parse("77777777-8888-9999-aaaa-bbbbbbbbbbbb")),
                Line = LineId.Create(7),
            });

        var audit = await connection.QuerySingleAsync<AuditRow>("SELECT Id, Line FROM Audit");
        Demo.Line($"Guid / int 基元往返               -> Id={audit.Id}, Line={audit.Line}");
    }
}

internal sealed class StockRow
{
    public Sku Sku { get; set; }

    public int Quantity { get; set; }
}

internal sealed class AuditRow
{
    public OrderId Id { get; set; }

    public LineId Line { get; set; }
}
