using Microsoft.EntityFrameworkCore;

namespace Len.StronglyTypedId;

/// <summary>
/// 只为触发 EF Core 集成生成器而存在的上下文：生成器的门控条件是「引用了
/// <c>Microsoft.EntityFrameworkCore</c> ≥ 7 且存在重写 <c>ConfigureConventions</c> 的 <c>DbContext</c>」。
/// </summary>
/// <remarks>
/// 没有它，本工程里根本不会产出 <c>*Converter</c> 与 <c>StronglyTypedIds.ApplyTo(ModelConfigurationBuilder)</c>，
/// EF Core 侧的运行时断言就只能退化成「类型能解析」这种恒真检查。
/// </remarks>
internal sealed class IntegrationDbContext : DbContext
{
    /// <inheritdoc/>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        // 转换器由生成的 StronglyTypedIds.ApplyTo(configurationBuilder) 统一注册，这里刻意留空。
        => ArgumentNullException.ThrowIfNull(configurationBuilder);
}
