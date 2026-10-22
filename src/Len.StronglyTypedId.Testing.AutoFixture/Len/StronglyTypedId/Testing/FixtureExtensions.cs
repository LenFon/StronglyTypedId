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
    public static IFixture WithStronglyTypedIds(this IFixture fixture, AnyIdGenerator? generator = null)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        fixture.Customize(new StronglyTypedIdCustomization(generator ?? new AnyIdGenerator()));

        return fixture;
    }
}
