using Microsoft.EntityFrameworkCore;

namespace Len.StronglyTypedId.IntegrationMatrix;

// 集成版本矩阵探针的 Id 声明：每个基元类别各一个，覆盖文本、整数、小数、枚举与日期。
// 生成器要求类型落在命名空间里（STIA0004），故与 Program.cs 分开。

[StronglyTypedId]
public partial record struct MatrixGuidId(Guid Value);

[StronglyTypedId]
public partial record struct MatrixIntId(int Value);

[StronglyTypedId]
public partial record struct MatrixStringId(string Value);

[StronglyTypedId]
public partial record struct MatrixDecimalId(decimal Value);

[StronglyTypedId]
public partial record struct MatrixStatusId(MatrixStatus Value);

[StronglyTypedId]
public partial record struct MatrixDateOnlyId(DateOnly Value);

public enum MatrixStatus
{
    Active,
    Suspended,
}

/// <summary>
/// EF Core 的宿主实体：非可空 Id 与可空 Id 各一个，覆盖两条映射路径。
/// </summary>
public sealed class MatrixEntity
{
    public int Id { get; set; }

    public MatrixGuidId Required { get; set; }

    public MatrixGuidId? Optional { get; set; }
}

/// <summary>
/// Dapper 的读取行：驱动返回 TEXT / REAL / INTEGER，与基元类型都不一致。
/// </summary>
public sealed class ProbeRow
{
    public MatrixGuidId Id { get; set; }

    public MatrixDecimalId Amount { get; set; }

    public MatrixStatusId Status { get; set; }
}

/// <summary>
/// HotChocolate 的查询根：每个 Id 参数都会让生成的 scalar 参与 schema 构建。
/// </summary>
public sealed class MatrixQuery
{
#pragma warning disable CA1822
    public MatrixGuidId Guid(MatrixGuidId id) => id;

    public MatrixIntId Int(MatrixIntId id) => id;

    public MatrixStatusId Status(MatrixStatusId id) => id;
#pragma warning restore CA1822
}

internal sealed class MatrixDbContext : DbContext
{
    public DbSet<MatrixEntity> Entities => Set<MatrixEntity>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseSqlite("Data Source=:memory:");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.AddStronglyTypedIds();
    }
}
