namespace Len.StronglyTypedId;

/// <summary>
/// 声明强类型 Id 的命名静态实例。
/// </summary>
/// <remarks>
/// 实例生成为 <c>public static readonly TSelf Name = Create(value)</c>，因此会经过与 <c>Create</c> 相同的归一化与验证。
/// 值采用不变文本形，不含 <see cref="StronglyTypedIdAttribute.Prefix"/>；它在编译期完成校验并以字面量输出，运行期不再解析。
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public sealed class StronglyTypedIdInstanceAttribute(string name, string value) : Attribute
{
    /// <summary>
    /// 获取生成的静态字段名。
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// 获取基元值的不变文本形。
    /// </summary>
    public string Value { get; } = value;
}

