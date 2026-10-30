using AutoFixture.Kernel;

namespace Len.StronglyTypedId.Testing;

/// <summary>
/// 为 AutoFixture 的请求提供强类型 Id。
/// </summary>
public sealed class StronglyTypedIdSpecimenBuilder(AnyIdGenerator generator) : ISpecimenBuilder
{
    /// <inheritdoc/>
    /// <remarks>
    /// 开放泛型（如 <c>Box&lt;T&gt;.BoxedId</c>）交给 AutoFixture 自己处理：它无法被构造，
    /// 而返回 <see cref="NoSpecimen"/> 让后续 builder 决定，比在这里抛异常更符合 AutoFixture 的约定。
    /// </remarks>
    public object Create(object request, ISpecimenContext context)
        => request is Type type && !type.ContainsGenericParameters && type.IsStronglyTypedId()
            ? generator.Create(type)
            : new NoSpecimen();
}
