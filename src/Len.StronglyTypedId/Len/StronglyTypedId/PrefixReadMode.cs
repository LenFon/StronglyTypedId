namespace Len.StronglyTypedId;

/// <summary>
/// 指定强类型 Id 读回带前缀的文本时采用的策略。
/// </summary>
/// <remarks>
/// <para>
/// 仅当 <see cref="StronglyTypedIdAttribute.Prefix"/> 配置了非空前缀时才起作用；未配置前缀时本设置无意义。
/// </para>
/// <para>
/// 默认 <see cref="Lenient"/>：<c>Parse</c> / <c>TryParse</c> 对前缀可选 —— 带前缀则剥离，不带则按原样解析。
/// 读侧向后兼容，库里 / 老 API 里的裸值仍可读入，迁移期不必一次性切换。
/// </para>
/// <para>
/// <see cref="Strict"/>：<c>Parse</c> / <c>TryParse</c> 要求前缀必须存在，缺失即失败。此时
/// <c>ToString</c> 与 <c>Parse</c> 构成双射，且 <c>string</c> 基元下不会出现「裸值恰好以前缀开头」的歧义。
/// </para>
/// </remarks>
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
