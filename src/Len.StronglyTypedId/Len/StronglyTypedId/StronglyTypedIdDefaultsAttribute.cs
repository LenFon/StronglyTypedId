namespace Len.StronglyTypedId;

/// <summary>
/// 为 <see cref="StronglyTypedIdAttribute"/> 提供程序集级默认值，免去在每个类型上重复书写。
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, Inherited = false, AllowMultiple = false)]
public class StronglyTypedIdDefaultsAttribute : Attribute
{
    /// <summary>
    /// 获取或设置本程序集内强类型 Id 默认使用的验证方法名。
    /// </summary>
    public string? Validator { get; set; }

    /// <summary>
    /// 获取或设置本程序集内强类型 Id 默认使用的归一化方法名。
    /// </summary>
    public string? Normalizer { get; set; }

    /// <summary>
    /// 获取或设置本程序集内强类型 Id 验证失败时默认抛出的异常类型。该类型须派生自 <see cref="Exception"/>、
    /// 不可为抽象类，并暴露接收单个 <see cref="string"/> 消息的构造函数。
    /// </summary>
    public Type? ExceptionType { get; set; }

    /// <summary>
    /// 获取或设置本程序集内强类型 Id 验证失败时是否默认隐去被拒绝的值。默认为 <see langword="false"/>，即保留该值。
    /// </summary>
    /// <remarks>
    /// 被拒绝的值会随异常消息进入日志与 APM 追踪；对基于 <see cref="string"/> 的 Id 而言那往往是用户输入。
    /// 本属性用于整个程序集脱敏；若只想对单个 Id 脱敏，请在类型上写 <see cref="StronglyTypedIdAttribute.RedactValidationMessage"/>。
    /// </remarks>
    public bool RedactValidationMessage { get; set; }

    /// <summary>
    /// 获取或设置本程序集内强类型 Id 默认生成哪些集成代码。默认为 <see cref="StronglyTypedIdIntegrations.Default"/>，
    /// 即「编译引用了什么就生成什么」。
    /// </summary>
    /// <remarks>
    /// 逐类型的 <see cref="StronglyTypedIdAttribute.Integrations"/> 只要不是 <see cref="StronglyTypedIdIntegrations.Default"/>
    /// 就会覆盖本设置。若本属性已关闭集成、而某个 Id 需要恢复，请写 <see cref="StronglyTypedIdIntegrations.All"/>。
    /// </remarks>
    public StronglyTypedIdIntegrations Integrations { get; set; }
}
