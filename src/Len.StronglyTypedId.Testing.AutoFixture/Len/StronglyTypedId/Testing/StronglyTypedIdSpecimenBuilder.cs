using AutoFixture.Kernel;

namespace Len.StronglyTypedId.Testing;

/// <summary>
/// Supplies strongly typed ids to AutoFixture requests.
/// </summary>
/// <param name="generator">The generator used to create ids.</param>
/// <remarks>
/// The builder claims a request only when it is a <see cref="Type"/> that implements
/// <see cref="IStronglyTypedId{TSelf, TPrimitiveId}"/>; everything else is left to the remaining builders.
/// </remarks>
public sealed class StronglyTypedIdSpecimenBuilder(AnyIdGenerator generator) : ISpecimenBuilder
{
    /// <inheritdoc/>
    public object Create(object request, ISpecimenContext context)
        => request is Type type && type.IsStronglyTypedId()
            ? generator.Create(type)
            : new NoSpecimen();
}
