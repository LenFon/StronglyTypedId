namespace Len.StronglyTypedId;

/// <summary>
/// 将 partial record（或 record struct）标记为强类型 Id。
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public class StronglyTypedIdAttribute : Attribute
{
    /// <summary>
    /// 获取或设置声明在强类型 Id 类型自身的静态验证方法名。
    /// </summary>
    public string? Validator { get; set; }

    /// <summary>
    /// 获取或设置声明在强类型 Id 类型自身的静态归一化方法名。
    /// </summary>
    public string? Normalizer { get; set; }

    /// <summary>
    /// 获取或设置验证器拒绝取值时由 <c>Create</c> 抛出的异常类型。该类型须派生自 <see cref="Exception"/>、
    /// 不可为抽象类，并暴露接收单个 <see cref="string"/> 消息的构造函数。默认为 <see cref="ArgumentException"/>。
    /// </summary>
    /// <remarks>
    /// 仅 <c>Create</c> 中的验证器拒绝使用该类型；<c>Parse</c> 按 <see cref="IParsable{TSelf}"/> 的约定抛 <see cref="FormatException"/>。
    /// </remarks>
    public Type? ExceptionType { get; set; }

    /// <summary>
    /// 获取或设置验证器拒绝时是否在异常消息中隐去被拒绝的值。默认为 <see langword="false"/>，即保留该值。
    /// </summary>
    /// <remarks>
    /// 被拒绝的文本会进入日志与 APM 追踪；对 <see cref="string"/> 类型的 Id 而言那往往是用户输入。
    /// 打开后消息为 <c>The value is not a valid MyId.</c>，不再内插原始值。
    /// </remarks>
    public bool RedactValidationMessage { get; set; }

    /// <summary>
    /// 获取或设置是否为强类型 Id 生成 <see cref="System.ComponentModel.TypeConverter"/>。
    /// </summary>
    public bool TypeConverter { get; set; }

    /// <summary>
    /// 获取或设置强类型 Id 生成哪些集成代码。默认为 <see cref="StronglyTypedIdIntegrations.Default"/>，
    /// 即「编译引用了什么就生成什么」。
    /// </summary>
    /// <remarks>
    /// 这是过滤器而非开关：只有对应程序集被引用<em>且</em>标志位被设置时才会生成。
    /// <see cref="StronglyTypedIdIntegrations.None"/> 会关闭全部集成，但不影响核心实现与显式开启的 <see cref="TypeConverter"/>。
    /// </remarks>
    public StronglyTypedIdIntegrations Integrations { get; set; }

    /// <summary>
    /// 获取或设置强类型 Id 在 JSON 中的形态。默认为 <see cref="Len.StronglyTypedId.JsonShape.Primitive"/>。
    /// </summary>
    public JsonShape JsonShape { get; set; }

    /// <summary>
    /// 获取或设置强类型 Id 规范文本形携带的字面前缀。
    /// </summary>
    public string? Prefix { get; set; }

    /// <summary>
    /// 获取或设置配置了 <see cref="Prefix"/> 时规范文本形的读回方式。默认为
    /// <see cref="Len.StronglyTypedId.PrefixReadMode.Lenient"/>。
    /// </summary>
    public PrefixReadMode PrefixReadMode { get; set; }
}
