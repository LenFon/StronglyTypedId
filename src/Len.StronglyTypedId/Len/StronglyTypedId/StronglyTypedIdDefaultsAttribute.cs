namespace Len.StronglyTypedId;

/// <summary>
/// 在程序集级别为 <see cref="StronglyTypedIdAttribute"/> 提供默认值，消掉逐类型的重复声明。
/// </summary>
/// <remarks>
/// 目前支持 <see cref="Validator"/>：声明
/// <c>[assembly: StronglyTypedIdDefaults(Validator = "Validate")]</c> 后，本程序集内所有强类型 Id 都默认
/// 把名为 <c>Validate</c> 的静态方法当作验证器，除非某个类型用 <c>[StronglyTypedId(Validator = ...)]</c>
/// 显式覆盖。默认值只作用于当前程序集声明的类型，不影响所引用的程序集。
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly, Inherited = false, AllowMultiple = false)]
public class StronglyTypedIdDefaultsAttribute : Attribute
{
    /// <summary>
    /// 获取或设置本程序集内强类型 Id 默认采用的验证器方法名。
    /// </summary>
    public string? Validator { get; set; }
}
