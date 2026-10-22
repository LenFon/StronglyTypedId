namespace Len.StronglyTypedId;

/// <summary>
/// 在程序集级别为 <see cref="StronglyTypedIdAttribute"/> 提供默认值，消掉逐类型的重复声明。
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, Inherited = false, AllowMultiple = false)]
public class StronglyTypedIdDefaultsAttribute : Attribute
{
    /// <summary>
    /// 获取或设置本程序集内强类型 Id 默认采用的验证器方法名。
    /// </summary>
    public string? Validator { get; set; }

    /// <summary>
    /// 获取或设置本程序集内强类型 Id 默认采用的归一化方法名。
    /// </summary>
    public string? Normalizer { get; set; }

    /// <summary>
    /// 获取或设置本程序集内强类型 Id 在验证失败时默认抛出的异常类型。该类型必须派生自
    /// <see cref="Exception"/>、不能是抽象类，且必须具有接收单个 <see cref="string"/> 消息的构造函数。
    /// </summary>
    public Type? ExceptionType { get; set; }

    /// <summary>
    /// 获取或设置本程序集内强类型 Id 默认是否在验证失败消息中隐去被拒绝的原始值。默认值为
    /// <see langword="false"/>，即保留原始值。
    /// </summary>
    /// <remarks>
    /// 原始值会随异常消息进入日志与 APM；对 <see cref="string"/> 基元来说那往往是用户输入。
    /// 整库脱敏用本属性；单个 Id 需要例外时在类型上写 <see cref="StronglyTypedIdAttribute.RedactValidationMessage"/>。
    /// </remarks>
    public bool RedactValidationMessage { get; set; }

    /// <summary>
    /// 获取或设置本程序集内强类型 Id 默认生成哪些集成代码。默认值为
    /// <see cref="StronglyTypedIdIntegrations.Default"/>，即「引用了就生成」。
    /// </summary>
    /// <remarks>
    /// 逐类型的 <see cref="StronglyTypedIdAttribute.Integrations"/> 只要不是 <see cref="StronglyTypedIdIntegrations.Default"/> 就覆盖此处设置。
    /// 被本属性关掉后要为单个 Id 重开，写 <see cref="StronglyTypedIdIntegrations.All"/>。
    /// </remarks>
    public StronglyTypedIdIntegrations Integrations { get; set; }
}
