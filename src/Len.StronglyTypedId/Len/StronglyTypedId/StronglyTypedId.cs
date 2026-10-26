using System.Numerics;

namespace Len.StronglyTypedId;

/// <summary>
/// 强类型 Id 的接口，不暴露所包装的基元类型。
/// </summary>
public interface IStronglyTypedId<TSelf> : IParsable<TSelf>, IEqualityOperators<TSelf, TSelf, bool>
    where TSelf : IStronglyTypedId<TSelf>?, IParsable<TSelf>?, IEqualityOperators<TSelf, TSelf, bool>?
{
}

/// <summary>
/// 强类型 Id 的接口。
/// </summary>
public interface IStronglyTypedId<TSelf, TPrimitiveId> : IStronglyTypedId<TSelf>
    where TSelf : IStronglyTypedId<TSelf, TPrimitiveId>?, IParsable<TSelf>?, IEqualityOperators<TSelf, TSelf, bool>?
    where TPrimitiveId : notnull, IComparable
{
    /// <summary>
    /// 获取强类型 Id 所包装的基元值。
    /// </summary>
    TPrimitiveId Value { get; }

    /// <summary>
    /// 创建包装指定基元值的新强类型 Id。
    /// </summary>
    abstract static TSelf Create(TPrimitiveId value);
}

/// <summary>
/// 强类型 Id 的接口，其包装的基元支持区域性感知的格式化。
/// </summary>
public interface IFormattableStronglyTypedId<TSelf> : IStronglyTypedId<TSelf>, IFormattable
    where TSelf : IFormattableStronglyTypedId<TSelf>?, IParsable<TSelf>?, IEqualityOperators<TSelf, TSelf, bool>?
{
}