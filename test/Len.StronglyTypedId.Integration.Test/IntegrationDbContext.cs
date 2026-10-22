using Microsoft.EntityFrameworkCore;

namespace Len.StronglyTypedId;

/// <summary>
/// 承载 EF Core 集成运行时断言的上下文：生成器的门控条件只是「引用了
/// <c>Microsoft.EntityFrameworkCore</c> ≥ 7」，与是否重写 <c>ConfigureConventions</c> 无关。
/// </summary>
/// <remarks>
/// 仍重写 <c>ConfigureConventions</c>，是因为运行时用例要走约定注册这条路：没有它，<c>ApplyTo(ModelConfigurationBuilder)</c> 就没有调用点。
/// </remarks>
internal sealed class IntegrationDbContext : DbContext
{
    /// <inheritdoc/>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        // 转换器由生成的 StronglyTypedIds.ApplyTo(configurationBuilder) 统一注册，这里刻意留空。
        => ArgumentNullException.ThrowIfNull(configurationBuilder);
}
