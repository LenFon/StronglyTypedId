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
    /// <c>[StronglyTypedIdInstance]</c> 的全限定显示名（含 <c>global::</c> 前缀）。
    /// </summary>
    internal const string StronglyTypedIdInstanceAttributeName = "global::Len.StronglyTypedId.StronglyTypedIdInstanceAttribute";

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
    /// 取符号上全部匹配的属性数据，保持声明顺序；一个都没有时返回空数组。
    /// </summary>
    /// <remarks>
    /// <c>[StronglyTypedIdInstance]</c> 是 <c>AllowMultiple = true</c>，顺序即生成顺序，故不能用 <see cref="Find"/>。
    /// </remarks>
    internal static ImmutableArray<AttributeData> FindAll(ISymbol symbol, string fullyQualifiedAttributeName)
    {
        var builder = ImmutableArray.CreateBuilder<AttributeData>();

        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == fullyQualifiedAttributeName)
            {
                builder.Add(attribute);
            }
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// 取第 <paramref name="index"/> 个位置实参的字符串值；缺失或不是字符串常量时为 <see langword="null"/>。
    /// </summary>
    internal static string? GetPositionalString(AttributeData attribute, int index)
        => attribute.ConstructorArguments.Length > index
            && attribute.ConstructorArguments[index] is { Kind: TypedConstantKind.Primitive, Value: string value }
                ? value
                : null;

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
    /// 取枚举型命名实参的底层值；未指定或类型不符时为 <see langword="null"/>。
    /// </summary>
    /// <remarks>
    /// 返回可空值而非直接比对：未指定时是 <see langword="null"/>，与「指定为 0」可区分。
    /// 程序集级默认值依赖这一点才能被逐类型双向覆盖（枚举型），而 <see cref="bool"/> 型做不到。
    /// </remarks>
    internal static int? GetEnumValue(AttributeData? attribute, string argumentName)
        => GetNamedArgument(attribute, argumentName) is { Value: int value } ? value : null;

    /// <summary>
    /// 读取 <c>[StronglyTypedId]</c> 上 <c>Validator</c> 命名实参的方法名；未指定时为 <see langword="null"/>。
    /// </summary>
    internal static string? GetValidatorName(INamedTypeSymbol type)
        => GetString(Find(type, StronglyTypedIdAttributeName), StronglyTypedIdInfo.ValidatorArgumentName);

    /// <summary>
    /// 读取 <c>[StronglyTypedId]</c> 上 <c>Normalizer</c> 命名实参的方法名；未指定时为 <see langword="null"/>。
    /// </summary>
    internal static string? GetNormalizerName(INamedTypeSymbol type)
        => GetString(Find(type, StronglyTypedIdAttributeName), StronglyTypedIdInfo.NormalizerArgumentName);

    /// <summary>
    /// 读取 <c>[StronglyTypedId]</c> 上 <c>ExceptionType</c> 命名实参的类型；未指定时为 <see langword="null"/>。
    /// </summary>
    internal static ITypeSymbol? GetExceptionType(INamedTypeSymbol type)
        => GetType(Find(type, StronglyTypedIdAttributeName), StronglyTypedIdInfo.ExceptionTypeArgumentName);
}
