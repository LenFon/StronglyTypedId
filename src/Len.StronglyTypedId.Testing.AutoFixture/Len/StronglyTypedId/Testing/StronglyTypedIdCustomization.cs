using AutoFixture;
using AutoFixture.Kernel;

namespace Len.StronglyTypedId.Testing;

/// <summary>
/// Teaches AutoFixture to create strongly typed ids.
/// </summary>
public sealed class StronglyTypedIdCustomization : ICustomization
{
    private readonly AnyIdGenerator _generator;

    /// <summary>
    /// Initializes a new instance backed by a default <see cref="AnyIdGenerator"/>.
    /// </summary>
    public StronglyTypedIdCustomization()
        : this(new AnyIdGenerator())
    {
    }

    /// <summary>
    /// Initializes a new instance backed by the specified generator.
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
