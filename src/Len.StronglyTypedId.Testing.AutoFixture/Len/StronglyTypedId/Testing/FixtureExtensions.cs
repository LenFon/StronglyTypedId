using AutoFixture;

namespace Len.StronglyTypedId.Testing;

/// <summary>
/// Extension methods that wire strongly typed id generation into AutoFixture.
/// </summary>
public static class FixtureExtensions
{
    /// <summary>
    /// Adds strongly typed id generation to the fixture.
    /// </summary>
    /// <param name="fixture">The fixture to customize.</param>
    /// <param name="generator">
    /// The generator to use, or <see langword="null"/> to use a default one. Pass a seeded generator to make
    /// the created ids reproducible.
    /// </param>
    /// <returns>The same fixture, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="fixture"/> is <see langword="null"/>.</exception>
    public static IFixture WithStronglyTypedIds(this IFixture fixture, AnyIdGenerator? generator = null)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        fixture.Customize(new StronglyTypedIdCustomization(generator ?? new AnyIdGenerator()));

        return fixture;
    }
}
