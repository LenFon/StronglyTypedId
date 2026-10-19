namespace Len.StronglyTypedId;

/// <summary>
/// 在程序集级别为 <see cref="StronglyTypedIdAttribute"/> 提供默认值，消掉逐类型的重复声明。
/// </summary>
/// <remarks>
/// Currently only <see cref="Validator"/>: <c>[assembly: StronglyTypedIdDefaults(Validator = "Validate")]</c>
/// applies to every strongly typed id declared in this assembly, unless a type overrides it explicitly.
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly, Inherited = false, AllowMultiple = false)]
public class StronglyTypedIdDefaultsAttribute : Attribute
{
    /// <summary>
    /// 获取或设置本程序集内强类型 Id 默认采用的验证器方法名。
    /// </summary>
    public string? Validator { get; set; }
}
