using AutoFixture.Kernel;

namespace Len.StronglyTypedId.Testing;

/// <summary>
/// 为 AutoFixture 的请求提供强类型 Id。
/// </summary>
public sealed class StronglyTypedIdSpecimenBuilder(AnyIdGenerator generator) : ISpecimenBuilder
{
    /// <inheritdoc/>
    public object Create(object request, ISpecimenContext context)
        => request is Type type && type.IsStronglyTypedId()
            ? generator.Create(type)
            : new NoSpecimen();
}
