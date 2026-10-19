using AutoFixture;
using AutoFixture.Kernel;

namespace Len.StronglyTypedId.Testing;

/// <summary>
/// Teaches AutoFixture to create strongly typed ids.
/// </summary>
/// <remarks>
/// <para>
/// The customization inserts a <see cref="StronglyTypedIdSpecimenBuilder"/> at the front of the
/// customization list, so it wins over AutoFixture's default "pick a constructor" strategy — which would
/// otherwise try to construct the id from a generated primitive and bypass <c>Create</c> (and therefore
/// the validator).
/// </para>
/// <para>
/// Apply it with <see cref="FixtureExtensions.WithStronglyTypedIds"/> or
/// <c>fixture.Customize(new StronglyTypedIdCustomization())</c>.
/// </para>
/// </remarks>
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
    /// <param name="generator">The generator used to create ids, typically seeded for reproducibility.</param>
    /// <exception cref="ArgumentNullException"><paramref name="generator"/> is <see langword="null"/>.</exception>
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
