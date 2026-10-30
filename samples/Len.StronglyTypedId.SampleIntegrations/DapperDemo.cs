using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Len.StronglyTypedId.SampleIntegrations;

internal static class DapperDemo
{
    public static async Task RunAsync()
    {
        Demo.Section("Dapper：StronglyTypedIds.AddStronglyTypedIds()");

        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        // 注册每个 Id 的 SqlMapper.TypeHandler。Dapper 的注册表是进程级的，与具体连接无关，
        // 因此只需调用一次；带参重载 AddStronglyTypedIds(connection) 仅为源码兼容保留，会忽略实参。
        StronglyTypedIds.AddStronglyTypedIds();

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

        Demo.Section("Dapper：驱动返回类型与基元不一致时的转换路径");

        // SQLite 对枚举列返回 long、对 decimal 列返回 double、对 char / Guid 列返回 string。
        // 处理器按基元类型逐条转换，因此这些列都能直接映射回 Id，不需要「返回原生类型的提供程序」。
        await connection.ExecuteAsync("CREATE TABLE Ledger (Kind INTEGER NOT NULL, Amount TEXT NOT NULL, Id TEXT NOT NULL)");
        await connection.ExecuteAsync(
            "INSERT INTO Ledger (Kind, Amount, Id) VALUES (@Kind, @Amount, @Id)",
            new
            {
                Kind = KindId.Create(LedgerKind.Credit),
                Amount = PriceId.Create(new Money(9.99m, "USD")),
                Id = LedgerId.Create(Guid.Parse("55555555-6666-7777-8888-999999999999")),
            });

        var ledger = await connection.QuerySingleAsync<LedgerRow>("SELECT Kind, Amount, Id FROM Ledger");
        Demo.Line($"枚举 / 自定义基元 / Guid 往返       -> Kind={ledger.Kind}, Amount={ledger.Amount}, Id={ledger.Id}");

        Demo.Section("Dapper：非法取值在读取时被拒绝");

        // 枚举列存了未定义数值：处理器会校验已定义性，抛 ArgumentException 而不是造出野值 Id。
        // Dapper 会把处理器抛出的异常包进 DataException，内层才是真正的原因。
        await connection.ExecuteAsync("INSERT INTO Ledger (Kind, Amount, Id) VALUES (999, 'USD 1', '55555555-6666-7777-8888-999999999999')");

        try
        {
            _ = await connection.QuerySingleAsync<LedgerRow>("SELECT Kind, Amount, Id FROM Ledger WHERE Kind = 999");
        }
        catch (DataException ex)
        {
            Demo.Line($"Kind = 999                        -> 抛出 {ex.GetType().Name}（内层 {ex.InnerException?.GetType().Name}）");
        }

        Demo.Section("Dapper：引用类型 Id 的 null 写成 DBNull，读回 null");

        // 引用类型 Id 的 null 必须写成 DBNull.Value，列上才是真正的 SQL NULL；
        // 读回时 DBNull / 裸 null 都映射为 null（而不是 default! 的空引用包装）。
        await connection.ExecuteAsync("CREATE TABLE Sessions (Id TEXT NULL)");
        await connection.ExecuteAsync("INSERT INTO Sessions (Id) VALUES (NULL)");

        var session = await connection.QuerySingleAsync<SessionRow>("SELECT Id FROM Sessions");
        Demo.Line($"NULL 列读回                        -> {session.Id?.ToString() ?? "<null>"}");

        await connection.ExecuteAsync(
            "INSERT INTO Sessions (Id) VALUES (@Id)",
            new { Id = SessionId.Create(Guid.Parse("22222222-3333-4444-5555-666666666666")) });

        var sessions = (await connection.QueryAsync<SessionRow>("SELECT Id FROM Sessions ORDER BY Id")).ToList();
        Demo.Line($"有值 / null 各一行                  -> {string.Join(", ", sessions.Select(row => row.Id?.ToString() ?? "<null>"))}");
    }
}

internal sealed class SessionRow
{
    public SessionId? Id { get; set; }
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

internal sealed class LedgerRow
{
    public KindId Kind { get; set; }

    public PriceId Amount { get; set; }

    public LedgerId Id { get; set; }
}
