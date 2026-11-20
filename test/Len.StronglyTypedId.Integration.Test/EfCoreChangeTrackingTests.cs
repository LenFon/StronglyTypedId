using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Len.StronglyTypedId;

/// <summary>
/// EF Core 的变更跟踪对强类型 Id 的实际行为，以及「不生成 <c>ValueComparer</c>」这个决定。
/// </summary>
/// <remarks>
/// 生成器只产出 <c>ValueConverter</c>，不产出 <c>ValueComparer</c>。这个决定依赖两个实测结论：
/// 值类型与引用类型 Id 的变更检测都按结构相等工作，以及经 Id 访问成员不翻译是转换器的固有限制、
/// comparer 帮不上。这些用例把结论钉住 —— 一旦 EF Core 改变默认行为，这里会先红。
/// </remarks>
public class EfCoreChangeTrackingTests
{
    #region 变更跟踪

    /// <summary>
    /// <c>record struct</c> 的 Id：赋新值必须被识别为已修改并落库。
    /// </summary>
    [Fact]
    public void RecordStructId_Should_BeTrackedAsModified_WhenValueChanges()
    {
        using var context = new TrackingDbContext();
        context.Database.OpenConnection();
        context.Database.EnsureCreated();

        var original = IntegrationGuidId.Create(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var updated = IntegrationGuidId.Create(Guid.Parse("22222222-2222-2222-2222-222222222222"));

        context.Add(new StructIdEntity { Id = 1, Value = original });
        context.SaveChanges();
        context.ChangeTracker.Clear();

        var loaded = context.Set<StructIdEntity>().Single();
        loaded.Value = updated;

        context.Entry(loaded).State.Should().Be(EntityState.Modified);

        context.SaveChanges();
        context.ChangeTracker.Clear();

        context.Set<StructIdEntity>().Single().Value.Should().Be(updated);
    }

    /// <summary>
    /// <c>record struct</c> 的 Id：赋一个值相等的新实例不算修改。
    /// </summary>
    [Fact]
    public void RecordStructId_Should_StayUnchanged_WhenEqualValueIsAssigned()
    {
        using var context = new TrackingDbContext();
        context.Database.OpenConnection();
        context.Database.EnsureCreated();

        var guid = Guid.Parse("11111111-1111-1111-1111-111111111111");

        context.Add(new StructIdEntity { Id = 1, Value = IntegrationGuidId.Create(guid) });
        context.SaveChanges();
        context.ChangeTracker.Clear();

        var loaded = context.Set<StructIdEntity>().Single();
        loaded.Value = IntegrationGuidId.Create(guid);

        context.Entry(loaded).State.Should().Be(EntityState.Unchanged);
    }

    /// <summary>
    /// <c>record</c> class 的 Id：赋新值必须被识别为已修改并落库。
    /// </summary>
    /// <remarks>
    /// 这是最容易被「引用类型默认用引用相等」这条直觉误判的场景。实测 EF Core 8 对不可变引用类型
    /// 的默认快照就是值本身，因此结构相等生效。
    /// </remarks>
    [Fact]
    public void RecordClassId_Should_BeTrackedAsModified_WhenValueChanges()
    {
        using var context = new TrackingDbContext();
        context.Database.OpenConnection();
        context.Database.EnsureCreated();

        var original = IntegrationReferenceId.Create(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var updated = IntegrationReferenceId.Create(Guid.Parse("22222222-2222-2222-2222-222222222222"));

        context.Add(new ClassIdEntity { Id = 1, Value = original });
        context.SaveChanges();
        context.ChangeTracker.Clear();

        var loaded = context.Set<ClassIdEntity>().Single();
        loaded.Value = updated;

        context.Entry(loaded).State.Should().Be(EntityState.Modified);

        context.SaveChanges();
        context.ChangeTracker.Clear();

        context.Set<ClassIdEntity>().Single().Value.Should().Be(updated);
    }

    /// <summary>
    /// <c>record</c> class 的 Id：值相等但引用不同的新实例不算修改。
    /// </summary>
    /// <remarks>
    /// 若 EF Core 退回引用相等，这条会失败 —— 那正是需要生成 <c>ValueComparer</c> 的信号。
    /// </remarks>
    [Fact]
    public void RecordClassId_Should_StayUnchanged_WhenEqualValueInNewInstanceIsAssigned()
    {
        using var context = new TrackingDbContext();
        context.Database.OpenConnection();
        context.Database.EnsureCreated();

        var guid = Guid.Parse("11111111-1111-1111-1111-111111111111");

        context.Add(new ClassIdEntity { Id = 1, Value = IntegrationReferenceId.Create(guid) });
        context.SaveChanges();
        context.ChangeTracker.Clear();

        var loaded = context.Set<ClassIdEntity>().Single();

        // 刻意构造一个新实例：引用不同、值相同。
        loaded.Value = IntegrationReferenceId.Create(guid);

        context.Entry(loaded).State.Should().Be(EntityState.Unchanged);
    }

    #endregion

    #region 查询翻译

    /// <summary>
    /// 比较整个 Id 可以翻译成 SQL。
    /// </summary>
    [Theory]
    [InlineData("alpha")]
    [InlineData("beta")]
    public void Query_Should_Translate_WhenWholeIdIsCompared(string value)
    {
        using var context = new TrackingDbContext();
        context.Database.OpenConnection();
        context.Database.EnsureCreated();

        context.Add(new StringIdEntity { Id = 1, Value = StringId.Create("alpha") });
        context.Add(new StringIdEntity { Id = 2, Value = StringId.Create("beta") });
        context.SaveChanges();
        context.ChangeTracker.Clear();

        var target = StringId.Create(value);

        context.Set<StringIdEntity>().Where(entity => entity.Value == target).ToList()
            .Should().ContainSingle().Which.Value.Should().Be(target);
    }

    /// <summary>
    /// 经 Id 访问 <c>Value</c> 成员不翻译：转换器映射的是整个 Id，不是它的成员。
    /// </summary>
    /// <remarks>
    /// 这条锁住的是「限制确实存在」而不是「行为正确」。若某天 EF Core 能翻译它，这条会红，
    /// 届时文档里的说明也该一并更新。
    /// </remarks>
    [Fact]
    public void Query_Should_NotTranslate_WhenMemberIsAccessedThroughId()
    {
        using var context = new TrackingDbContext();
        context.Database.OpenConnection();
        context.Database.EnsureCreated();

        context.Add(new StringIdEntity { Id = 1, Value = StringId.Create("alpha") });
        context.SaveChanges();
        context.ChangeTracker.Clear();

        var act = () => context.Set<StringIdEntity>().Where(entity => entity.Value.Value.Contains("alp")).ToList();

        act.Should().Throw<InvalidOperationException>().WithMessage("*could not be translated*");
    }

    #endregion

    #region 辅助

    /// <summary>
    /// 走约定注册路径的上下文：<c>AddStronglyTypedIds()</c> 由生成的
    /// <c>ConfigureConventions</c> 入口统一挂上转换器。
    /// </summary>
    private sealed class TrackingDbContext : DbContext
    {
        public DbSet<StructIdEntity> StructIdEntities => Set<StructIdEntity>();

        public DbSet<ClassIdEntity> ClassIdEntities => Set<ClassIdEntity>();

        public DbSet<StringIdEntity> StringIdEntities => Set<StringIdEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseSqlite("Data Source=:memory:");

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
            => configurationBuilder.AddStronglyTypedIds();
    }

    public sealed class StructIdEntity
    {
        public int Id { get; set; }

        public IntegrationGuidId Value { get; set; }
    }

    public sealed class ClassIdEntity
    {
        public int Id { get; set; }

        public IntegrationReferenceId Value { get; set; } = IntegrationReferenceId.Create(Guid.Empty);
    }

    public sealed class StringIdEntity
    {
        public int Id { get; set; }

        public StringId Value { get; set; }
    }

    #endregion
}
