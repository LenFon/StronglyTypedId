using Microsoft.EntityFrameworkCore;

namespace Len.StronglyTypedId.SampleIntegrations;

internal sealed class ShopDbContext(DbContextOptions<ShopDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // 为每个强类型 Id 注册 ValueConverter；注册后仍可继续配置该类型。
        configurationBuilder.AddStronglyTypedIds();

        configurationBuilder.Properties<Sku>().HaveMaxLength(64);
    }
}

/// <summary>
/// 用 <c>OnModelCreating</c> 里的 <c>AddStronglyTypedIds(ModelBuilder)</c> 注册转换器的上下文。
/// </summary>
/// <remarks>
/// 这个重载扫描的是<b>调用那一刻</b>的模型快照，因此必须放在 <c>OnModelCreating</c> 末尾。
/// 约定重载（<see cref="ShopDbContext"/>）没有这个顺序约束，是更好的默认选择。
/// <para>
/// 还有一条容易踩的前提：Id 属性必须先被显式加入模型。EF 的约定只认得它认识的 CLR 类型，
/// 一个从未被 <c>Property(...)</c> 声明过的 Id 属性根本不在模型快照里，扫描自然看不到它。
/// </para>
/// </remarks>
internal sealed class LedgerDbContext(DbContextOptions<LedgerDbContext> options) : DbContext(options)
{
    public DbSet<LedgerRecord> Entries => Set<LedgerRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var entity = modelBuilder.Entity<LedgerRecord>();
        entity.ToTable("Ledger");

        // 先把 Id 属性显式加入模型，扫描入口才看得见它们。
        entity.Property(x => x.Id);
        entity.Property(x => x.Kind);
        entity.Property(x => x.Amount);

        // 必须放在实体配置之后：它只看到此刻已加入模型的实体类型。
        modelBuilder.AddStronglyTypedIds();
    }
}

internal sealed class LedgerRecord
{
    public LedgerId Id { get; set; }

    public KindId Kind { get; set; }

    public PriceId Amount { get; set; }

    public int Sequence { get; set; }
}
