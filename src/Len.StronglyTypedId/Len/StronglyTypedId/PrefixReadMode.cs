namespace Len.StronglyTypedId;

/// <summary>
/// 指定带前缀的文本如何读回为强类型 Id。
/// </summary>
public enum PrefixReadMode
{
    /// <summary>
    /// 前缀可选：存在时剥离，不存在时按原样解析。默认值。
    /// </summary>
    Lenient = 0,

    /// <summary>
    /// 前缀必需：缺失时解析失败。
    /// </summary>
    Strict = 1,
}
