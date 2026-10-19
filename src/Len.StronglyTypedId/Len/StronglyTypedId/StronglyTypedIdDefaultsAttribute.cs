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
    /// 获取或设置本程序集内强类型 Id 默认生成哪些集成代码。默认值为
    /// <see cref="StronglyTypedIdIntegrations.Default"/>，即「引用了就生成」。
    /// </summary>
    /// <remarks>
    /// 逐类型上的 <see cref="StronglyTypedIdAttribute.Integrations"/> 只要不是
    /// <see cref="StronglyTypedIdIntegrations.Default"/> 就覆盖此处的设置；想在被本属性关掉之后
    /// 为单个 Id 重新打开，写 <see cref="StronglyTypedIdIntegrations.All"/>。
    /// </remarks>
    public StronglyTypedIdIntegrations Integrations { get; set; }
}
