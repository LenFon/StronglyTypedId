namespace Len.StronglyTypedId;

/// <summary>
/// 统一读取 <c>[StronglyTypedId]</c> / <c>[StronglyTypedIdDefaults]</c> 命名实参的辅助方法。
/// </summary>
internal static class AttributeReader
{
    /// <summary>
    /// <c>[StronglyTypedId]</c> 的全限定显示名（含 <c>global::</c> 前缀）。
    /// </summary>
    internal const string StronglyTypedIdAttributeName = "global::Len.StronglyTypedId.StronglyTypedIdAttribute";

    /// <summary>
    /// <c>[StronglyTypedIdDefaults]</c> 的全限定显示名（含 <c>global::</c> 前缀）。
    /// </summary>
    internal const string StronglyTypedIdDefaultsAttributeName = "global::Len.StronglyTypedId.StronglyTypedIdDefaultsAttribute";

    /// <summary>
    /// 取符号上第一个匹配的属性数据；未找到时为 <see langword="null"/>。
    /// </summary>
    internal static AttributeData? Find(ISymbol symbol, string fullyQualifiedAttributeName)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == fullyQualifiedAttributeName)
            {
                return attribute;
            }
        }

        return null;
    }

    /// <summary>
    /// 取命名实参的原始值；未指定时为 <see langword="null"/>。
    /// </summary>
    internal static TypedConstant? GetNamedArgument(AttributeData? attribute, string argumentName)
    {
        if (attribute is null)
        {
            return null;
        }

        foreach (var argument in attribute.NamedArguments)
        {
            if (argument.Key == argumentName)
            {
                return argument.Value;
            }
        }

        return null;
    }

    /// <summary>
    /// 取命名实参的字符串值；未指定或类型不符时为 <see langword="null"/>。
    /// </summary>
    internal static string? GetString(AttributeData? attribute, string argumentName)
        => GetNamedArgument(attribute, argumentName) is { Value: string value } ? value : null;

    /// <summary>
    /// 取命名实参的布尔值；未指定时为 <see langword="false"/>。
    /// </summary>
    internal static bool GetBoolean(AttributeData? attribute, string argumentName)
        => GetNamedArgument(attribute, argumentName) is { Value: true };

    /// <summary>
    /// 取 <c>typeof(...)</c> 型命名实参的类型符号；未指定或不是类型实参时为 <see langword="null"/>。
    /// </summary>
    internal static ITypeSymbol? GetType(AttributeData? attribute, string argumentName)
        => GetNamedArgument(attribute, argumentName) is { Kind: TypedConstantKind.Type, Value: ITypeSymbol type }
            ? type
            : null;

    /// <summary>
    /// 判断枚举型命名实参是否等于指定底层值。
    /// </summary>
    internal static bool GetEnumFlag(AttributeData? attribute, string argumentName, int value)
        => GetNamedArgument(attribute, argumentName) is { Value: int actual } && actual == value;
}
