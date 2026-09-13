using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Len.StronglyTypedId.Sample1;

public class SampleDbContext(DbContextOptions<SampleDbContext> options) : DbContext(options)
{
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        StronglyTypedIds.ApplyTo(configurationBuilder);

        configurationBuilder.Properties<UserId>().HaveMaxLength(100);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SampleDbContext).Assembly);
    }
}

internal class OrderMap : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.OwnsMany(p => p.Items, b =>
        {
            b.ToJson();
            b.Property(p => p.Key).HasJsonPropertyName("Id");
        });
    }
}