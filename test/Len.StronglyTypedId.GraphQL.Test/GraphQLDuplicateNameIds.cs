namespace Len.StronglyTypedId.GraphQL
{
    /// <summary>
    /// 与 <see cref="Shipping.InvoiceId"/> 短名相同的 Id。
    /// </summary>
    /// <remarks>
    /// GraphQL 的类型名是一张全局表，与 C# 命名空间无关：两个不同命名空间的 <c>InvoiceId</c> 若都按短名
    /// 注册，schema 构建期会抛「An item with the same key has already been added. Key: InvoiceId」。
    /// 本类型与 <see cref="Shipping.InvoiceId"/> 一起被 <see cref="Query"/> 引用，让这条回归真正走到 schema 构建。
    /// </remarks>
    [StronglyTypedId]
    public partial record struct InvoiceId(Guid Value);
}

namespace Len.StronglyTypedId.GraphQL.Shipping
{
    /// <summary>
    /// 与 <see cref="global::Len.StronglyTypedId.GraphQL.InvoiceId"/> 短名相同、命名空间不同。
    /// </summary>
    [StronglyTypedId]
    public partial record struct InvoiceId(Guid Value);
}
