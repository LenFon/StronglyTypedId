using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace Len.StronglyTypedId;

/// <summary>
/// 强类型 Id 的接口，不暴露所包装的基元类型。
/// </summary>
/// <remarks>
/// 继承 <see cref="ISpanParsable{TSelf}"/> 与 <see cref="IEquatable{TSelf}"/> 时，两者都由默认接口实现补齐，
/// 因此手写实现只需提供 <see cref="IParsable{TSelf}"/> 的两个成员即可编译 —— 这两条能力是<b>新增</b>的，
/// 不是新的实现负担。
/// </remarks>
public interface IStronglyTypedId<TSelf> :
    IParsable<TSelf>,
    ISpanParsable<TSelf>,
    IEquatable<TSelf>,
    IComparable<TSelf>,
    IComparisonOperators<TSelf, TSelf, bool>
    where TSelf : IStronglyTypedId<TSelf>?, IParsable<TSelf>?, ISpanParsable<TSelf>?, IComparisonOperators<TSelf, TSelf, bool>?
{
    /// <summary>
    /// 尝试把文本解析成强类型 Id，并带回失败原因。
    /// </summary>
    /// <param name="value">要解析的文本。</param>
    /// <param name="provider">格式提供程序；<see langword="null"/> 表示不变文化。</param>
    /// <param name="result">方法返回 <see langword="true"/> 时解析出的 Id；否则为 <see langword="default"/>。</param>
    /// <param name="validation">
    /// 失败原因。文本无法解析时为一条通用消息；解析成功但验证器拒绝时为验证器的结果；
    /// 成功时为 <see cref="StronglyTypedIdValidationResult.Success"/>。
    /// </param>
    /// <returns>当 <paramref name="value"/> 能解析成合法 Id 时返回 <see langword="true"/>。</returns>
    /// <remarks>
    /// <see cref="IParsable{TSelf}"/> 的 <c>TryParse</c> 只有布尔通道，模型绑定器之类的调用方拿不到
    /// 「为什么拒绝」。这个重载补上那条通道，且不依赖 <c>TPrimitiveId</c>，故泛型代码约束到
    /// <see cref="IStronglyTypedId{TSelf}"/> 也能调用。
    /// <para>
    /// 默认实现转发到两参 <c>TryParse</c>，并返回一条通用消息。生成器产出的 Id 覆盖它，
    /// 因此能给出验证器的真实原因；手写实现不覆盖也能编译，只是拿不到富消息。
    /// </para>
    /// </remarks>
    static virtual bool TryParse(
        string? value,
        IFormatProvider? provider,
        [MaybeNullWhen(false)] out TSelf result,
        out StronglyTypedIdValidationResult validation)
    {
        // 成功时必须报 Success：调用方按 validation.IsValid 判断，无条件写 Failure 会让成功也显示为失败。
        if (TryParseText<TSelf>(value, provider, out result))
        {
            validation = StronglyTypedIdValidationResult.Success;

            return true;
        }

        validation = StronglyTypedIdValidationResult.Failure($"The value is not a valid {typeof(TSelf).Name}.");

        return false;
    }

    /// <summary>
    /// <see cref="ISpanParsable{TSelf}"/> 的 <c>Parse</c> 默认实现：转发到 <see cref="IParsable{TSelf}"/>。
    /// </summary>
    static TSelf ISpanParsable<TSelf>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
        => ParseText<TSelf>(s.ToString(), provider);

    /// <summary>
    /// <see cref="ISpanParsable{TSelf}"/> 的 <c>TryParse</c> 默认实现：转发到 <see cref="IParsable{TSelf}"/>。
    /// </summary>
    static bool ISpanParsable<TSelf>.TryParse(
        ReadOnlySpan<char> s,
        IFormatProvider? provider,
        [MaybeNullWhen(false)] out TSelf result)
        => TryParseText<TSelf>(s.ToString(), provider, out result);

    /// <summary>
    /// <see cref="IEquatable{TSelf}"/> 的 <c>Equals</c> 默认实现：转发到 <see cref="object.Equals(object, object)"/>。
    /// </summary>
    /// <remarks>
    /// 生成器产出的 <c>record</c> / <c>record struct</c> 自带结构相等的 <c>Equals</c>，会覆盖这个默认实现；
    /// 手写实现不覆盖时退回值相等，行为与 <c>object.Equals</c> 一致，不会比之前更差。
    /// </remarks>
    bool IEquatable<TSelf>.Equals(TSelf? other) => object.Equals(this, other);

    /// <summary>
    /// 把文本转发给 <see cref="IParsable{TSelf}"/> 的 <c>Parse</c>。
    /// </summary>
    /// <remarks>
    /// 必须经这个约束只有 <see cref="IParsable{T}"/> 的泛型方法转发：在接口内部直接写
    /// <c>TSelf.Parse(text, provider)</c> 会被解析到本接口继承来的 span 重载（<see cref="string"/>
    /// 可隐式转 <see cref="ReadOnlySpan{T}"/>），从而无限递归。
    /// </remarks>
    private static T ParseText<T>(string text, IFormatProvider? provider)
        where T : IParsable<T>?
        => T.Parse(text, provider);

    /// <summary>
    /// 把文本转发给 <see cref="IParsable{TSelf}"/> 的 <c>TryParse</c>。
    /// </summary>
    /// <remarks>理由同 <see cref="ParseText{T}"/>：避免重载解析落到 span 版本上。</remarks>
    private static bool TryParseText<T>(string? text, IFormatProvider? provider, [MaybeNullWhen(false)] out T result)
        where T : IParsable<T>?
        => T.TryParse(text, provider, out result);
}

/// <summary>
/// 强类型 Id 的接口。
/// </summary>
public interface IStronglyTypedId<TSelf, TPrimitiveId> : IStronglyTypedId<TSelf>
    where TSelf : IStronglyTypedId<TSelf, TPrimitiveId>?, IParsable<TSelf>?, ISpanParsable<TSelf>?, IComparisonOperators<TSelf, TSelf, bool>?
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
    /// <para>
    /// 默认实现转发到两参 <c>TryCreate</c>，因此手写实现只需提供它就能编译。
    /// </para>
    /// </remarks>
    static virtual bool TryCreate(
        TPrimitiveId value,
        [MaybeNullWhen(false)] out TSelf result,
        out StronglyTypedIdValidationResult validation)
    {
        // 成功时必须报 Success：调用方按 validation.IsValid 判断，无条件写 Failure 会让成功也显示为失败。
        if (TSelf.TryCreate(value, out result))
        {
            validation = StronglyTypedIdValidationResult.Success;

            return true;
        }

        validation = StronglyTypedIdValidationResult.Failure($"The value is not a valid {typeof(TSelf).Name}.");

        return false;
    }
}

/// <summary>
/// 强类型 Id 的接口，其包装的基元支持区域性感知的格式化。
/// </summary>
public interface IFormattableStronglyTypedId<TSelf> : IStronglyTypedId<TSelf>, IFormattable
    where TSelf : IFormattableStronglyTypedId<TSelf>?, IParsable<TSelf>?, ISpanParsable<TSelf>?, IComparisonOperators<TSelf, TSelf, bool>?
{
}
