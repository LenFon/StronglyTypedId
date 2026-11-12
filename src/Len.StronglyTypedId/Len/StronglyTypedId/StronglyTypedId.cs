using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace Len.StronglyTypedId;

/// <summary>
/// 强类型 Id 的接口，不暴露所包装的基元类型。
/// </summary>
public interface IStronglyTypedId<TSelf> :
    IParsable<TSelf>,
    IComparable<TSelf>,
    IComparisonOperators<TSelf, TSelf, bool>
    where TSelf : IStronglyTypedId<TSelf>?, IParsable<TSelf>?, IComparisonOperators<TSelf, TSelf, bool>?
{
}

/// <summary>
/// 强类型 Id 的接口。
/// </summary>
public interface IStronglyTypedId<TSelf, TPrimitiveId> : IStronglyTypedId<TSelf>
    where TSelf : IStronglyTypedId<TSelf, TPrimitiveId>?, IParsable<TSelf>?, IComparisonOperators<TSelf, TSelf, bool>?
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

    /// <summary>
    /// 尝试创建包装指定基元值的新强类型 Id；值不被接受时返回 <see langword="false"/>，而不是抛异常。
    /// </summary>
    /// <param name="value">要包装的基元值。</param>
    /// <param name="result">方法返回 <see langword="true"/> 时创建出的 Id；否则为 <see langword="default"/>。</param>
    /// <returns>当 <paramref name="value"/> 可接受时返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    abstract static bool TryCreate(TPrimitiveId value, [MaybeNullWhen(false)] out TSelf result);

    /// <summary>
    /// 尝试创建包装指定基元值的新强类型 Id，并带回验证器给出的拒绝原因。
    /// </summary>
    /// <param name="value">要包装的基元值。</param>
    /// <param name="result">方法返回 <see langword="true"/> 时创建出的 Id；否则为 <see langword="default"/>。</param>
    /// <param name="validation">
    /// 验证结果。方法返回 <see langword="true"/> 时为 <see cref="StronglyTypedIdValidationResult.Success"/>；
    /// 否则为验证器返回的 <see cref="StronglyTypedIdValidationResult.Failure"/>，其中带有原因与可选错误码。
    /// </param>
    /// <returns>当 <paramref name="value"/> 可接受时返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    /// <remarks>
    /// 验证器返回 <see langword="bool"/> 的 Id 同样提供这个重载。此时 <paramref name="validation"/> 的
    /// <see cref="StronglyTypedIdValidationResult.Message"/> 是一条库生成的通用消息
    /// （<c>The value is not a valid MyId.</c>），<see cref="StronglyTypedIdValidationResult.Code"/>
    /// 为 <see langword="null"/> —— <see langword="bool"/> 验证器没有地方给出原因与错误码。
    /// </remarks>
    abstract static bool TryCreate(
        TPrimitiveId value,
        [MaybeNullWhen(false)] out TSelf result,
        out StronglyTypedIdValidationResult validation);
}

/// <summary>
/// 强类型 Id 的接口，其包装的基元支持区域性感知的格式化。
/// </summary>
public interface IFormattableStronglyTypedId<TSelf> : IStronglyTypedId<TSelf>, IFormattable
    where TSelf : IFormattableStronglyTypedId<TSelf>?, IParsable<TSelf>?, IComparisonOperators<TSelf, TSelf, bool>?
{
}