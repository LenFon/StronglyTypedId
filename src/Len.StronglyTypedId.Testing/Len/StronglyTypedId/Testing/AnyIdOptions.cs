namespace Len.StronglyTypedId.Testing;

/// <summary>
/// 配置 <see cref="AnyIdGenerator"/> 如何生成基元值。
/// </summary>
public sealed class AnyIdOptions
{
    /// <summary>
    /// 获取或设置传给内置伪随机源的种子。
    /// </summary>
    /// <value>
    /// <see langword="null"/>（默认）表示按时钟取种；给定显式值可使序列可复现。可复现性跨 .NET 版本、
    /// 跨平台成立：内置的是自实现的 SplitMix64，不依赖 <see cref="Random"/> 的实现细节。
    /// </value>
    public int? Seed { get; set; }

    /// <summary>
    /// 获取或设置生成的 <see cref="string"/> 基元长度。
    /// </summary>
    /// <value>字符数，默认 8，最小为 1。</value>
    public int StringLength { get; set; } = 8;

    /// <summary>
    /// 获取或设置验证器拒绝取值时，放弃前最多尝试的随机值个数。
    /// </summary>
    /// <value>最大尝试次数，默认 100，最小为 1。</value>
    public int MaxAttempts { get; set; } = 100;

    /// <summary>
    /// 获取或设置按基元类型提供取值的回调。
    /// </summary>
    /// <value>
    /// 返回待包装值的委托；返回 <see langword="null"/> 表示回退到内置生成器。委托自身返回 <see langword="null"/>
    /// 同样表示「未处理」。
    /// </value>
    public Func<Type, object?>? PrimitiveValueFactory { get; set; }

    /// <summary>
    /// 获取或设置生成值是否偶尔取基元范围的极值。
    /// </summary>
    /// <value>
    /// 默认 <see langword="false"/>，取值保持普通。被测代码对边界敏感（格式化宽度、溢出检查、数据库列上限）时打开：
    /// 约每十六个值有一个是 <c>MinValue</c> 或 <c>MaxValue</c>。仅适用于整型与 <see cref="decimal"/>；
    /// 浮点边界是 <c>NaN</c> / <c>±Infinity</c>，会让 JSON 往返失败，故从不生成。
    /// 断言唯一性时不要打开：两个 Id 可能撞在同一个极值上。
    /// </value>
    public bool IncludeBoundaryValues { get; set; }
}
