using AutoFixture.Kernel;

namespace Len.StronglyTypedId.Testing;

/// <summary>
/// Supplies strongly typed ids to AutoFixture requests.
/// </summary>
public sealed class StronglyTypedIdSpecimenBuilder(AnyIdGenerator generator) : ISpecimenBuilder
{
    /// <inheritdoc/>
    public object Create(object request, ISpecimenContext context)
        => request is Type type && type.IsStronglyTypedId()
            ? generator.Create(type)
            : new NoSpecimen();
}
