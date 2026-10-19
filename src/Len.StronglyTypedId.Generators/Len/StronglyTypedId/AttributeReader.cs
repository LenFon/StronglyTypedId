namespace Len.StronglyTypedId;

/// <summary>
/// 统一读取 <c>[StronglyTypedId]</c> / <c>[StronglyTypedIdDefaults]</c> 命名实参的辅助方法。
/// </summary>
/// <remarks>
/// 生成器、分析器与代码修复三处都需要同一套查找逻辑。分散实现时一处用 <c>ToDisplayString()</c>、
/// 另一处用 <c>FullyQualifiedFormat</c>，一旦类型改名或换命名空间就会静默失配（不报错，只是再也读不到实参）。
/// 故集中在此，只保留一种比较口径。
/// </remarks>
internal static class AttributeReader
{
    /// <summary><c>[StronglyTypedId]</c> 的全限定显示名（含 <c>global::</c> 前缀）。</summary>
    internal const string StronglyTypedIdAttributeName = "global::Len.StronglyTypedId.StronglyTypedIdAttribute";

    /// <summary><c>[StronglyTypedIdDefaults]</c> 的全限定显示名（含 <c>global::</c> 前缀）。</summary>
    internal const string StronglyTypedIdDefaultsAttributeName = "global::Len.StronglyTypedId.StronglyTypedIdDefaultsAttribute";

    /// <summary>取符号上第一个匹配的属性数据；未找到时为 <see langword="null"/>。</summary>
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

    /// <summary>取命名实参的原始值；未指定时为 <see langword="null"/>。</summary>
    /// <remarks>
    /// 返回 <see cref="TypedConstant"/> 而非 <see cref="object"/>：调用方偶尔需要区分「未指定」与「指定为 null」，
    /// 也要靠 <see cref="TypedConstant.Kind"/> 识别 <c>typeof(...)</c> 之类的非字面量实参。
    /// </remarks>
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

    /// <summary>取命名实参的字符串值；未指定或类型不符时为 <see langword="null"/>。</summary>
    internal static string? GetString(AttributeData? attribute, string argumentName)
        => GetNamedArgument(attribute, argumentName) is { Value: string value } ? value : null;

    /// <summary>取命名实参的布尔值；未指定时为 <see langword="false"/>。</summary>
    internal static bool GetBoolean(AttributeData? attribute, string argumentName)
        => GetNamedArgument(attribute, argumentName) is { Value: true };

    /// <summary>判断枚举型命名实参是否等于指定底层值。</summary>
    /// <remarks>命名实参在元数据里存的是枚举的底层值而非成员名，故只能按数值比较。</remarks>
    internal static bool GetEnumFlag(AttributeData? attribute, string argumentName, int value)
        => GetNamedArgument(attribute, argumentName) is { Value: int actual } && actual == value;
}
