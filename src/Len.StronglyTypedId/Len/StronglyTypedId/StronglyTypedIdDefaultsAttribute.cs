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

    /// <summary>
    /// 获取或设置本程序集内强类型 Id 默认采用的 JSON 形态。默认为 <see cref="Len.StronglyTypedId.JsonShape.Primitive"/>。
    /// </summary>
    /// <remarks>
    /// 逐类型的 <see cref="StronglyTypedIdAttribute.JsonShape"/> 显式写了就覆盖本设置，包括显式写回
    /// <see cref="Len.StronglyTypedId.JsonShape.Primitive"/>。整库统一对象形态时用本属性，不必逐个 Id 重复。
    /// </remarks>
    public JsonShape JsonShape { get; set; }

    /// <summary>
    /// 获取或设置本程序集内强类型 Id 默认是否生成 <see cref="System.ComponentModel.TypeConverter"/>。
    /// 默认为 <see langword="false"/>。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="RedactValidationMessage"/> 一样只能单向打开：本属性打开后，单个 Id 无法再关回去。
    /// 原因是特性属性是 <see cref="bool"/>，未写与写了 <see langword="false"/> 在元数据里无法区分。
    /// </remarks>
    public bool TypeConverter { get; set; }

    /// <summary>
    /// 获取或设置本程序集内强类型 Id 配置了 <see cref="StronglyTypedIdAttribute.Prefix"/> 时默认的读回方式。
    /// 默认为 <see cref="Len.StronglyTypedId.PrefixReadMode.Lenient"/>。
    /// </summary>
    /// <remarks>
    /// 逐类型的 <see cref="StronglyTypedIdAttribute.PrefixReadMode"/> 显式写了就覆盖本设置。
    /// 本属性只决定读回策略，不提供默认前缀：程序集级前缀会让多个 Id 共用同一前缀并触发
    /// <c>STIA0023</c>，故刻意不提供。
    /// </remarks>
    public PrefixReadMode PrefixReadMode { get; set; }
}
