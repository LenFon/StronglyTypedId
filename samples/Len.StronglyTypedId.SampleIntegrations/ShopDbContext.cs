using Microsoft.EntityFrameworkCore;

namespace Len.StronglyTypedId.SampleIntegrations;

internal sealed class ShopDbContext(DbContextOptions<ShopDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // 为每个强类型 Id 注册 ValueConverter；注册后仍可继续配置该类型。
        StronglyTypedIds.ApplyTo(configurationBuilder);

        configurationBuilder.Properties<Sku>().HaveMaxLength(64);
    }
}
