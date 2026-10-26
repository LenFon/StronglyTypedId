using AutoFixture;
using AutoFixture.Kernel;

namespace Len.StronglyTypedId.Testing;

/// <summary>
/// 让 AutoFixture 能够创建强类型 Id。
/// </summary>
public sealed class StronglyTypedIdCustomization : ICustomization
{
    private readonly AnyIdGenerator _generator;

    /// <summary>
    /// 使用默认的 <see cref="AnyIdGenerator"/> 初始化新实例。
    /// </summary>
    public StronglyTypedIdCustomization()
        : this(new AnyIdGenerator())
    {
    }

    /// <summary>
    /// 使用指定生成器初始化新实例。
    /// </summary>
    public StronglyTypedIdCustomization(AnyIdGenerator generator)
    {
        ArgumentNullException.ThrowIfNull(generator);

        _generator = generator;
    }

    /// <inheritdoc/>
    public void Customize(IFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        fixture.Customizations.Insert(0, new StronglyTypedIdSpecimenBuilder(_generator));
    }
}
