using AutoFixture;

namespace Len.StronglyTypedId.Testing;

/// <summary>
/// 把强类型 Id 生成接入 AutoFixture 的扩展方法。
/// </summary>
public static class FixtureExtensions
{
    /// <summary>
    /// 为 fixture 添加强类型 Id 生成能力。
    /// </summary>
    public static IFixture WithStronglyTypedIds(this IFixture fixture, AnyIdGenerator? generator = null)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        fixture.Customize(new StronglyTypedIdCustomization(generator ?? new AnyIdGenerator()));

        return fixture;
    }
}
