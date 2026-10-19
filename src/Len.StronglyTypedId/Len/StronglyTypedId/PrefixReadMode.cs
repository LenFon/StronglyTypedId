namespace Len.StronglyTypedId;

/// <summary>
/// 指定强类型 Id 读回带前缀的文本时采用的策略。
/// </summary>
public enum PrefixReadMode
{
    /// <summary>
    /// 前缀可选：带前缀则剥离，不带则按原样解析。这是默认值。
    /// </summary>
    Lenient = 0,

    /// <summary>
    /// 前缀必须存在：缺失即解析失败。
    /// </summary>
    Strict = 1,
}
