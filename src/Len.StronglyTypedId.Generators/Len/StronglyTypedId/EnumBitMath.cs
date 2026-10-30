namespace Len.StronglyTypedId;

/// <summary>
/// 枚举基元的位运算：底层类型宽度、常量按位展开、<c>[Flags]</c> 未定义位掩码。
/// </summary>
/// <remarks>
/// 生成器与分析器共用同一份判据，保证「报错的那条」与「生成时拒绝的那条」口径一致。
/// 这些运算只依赖符号本身，与某个 Id 的其余属性无关，故从 <see cref="StronglyTypedIdInfo"/> 独立出来。
/// </remarks>
internal static class EnumBitMath
{
    /// <summary>
    /// 判断枚举是否标注了 <c>[System.Flags]</c>。
    /// </summary>
    internal static bool HasFlagsAttribute(ITypeSymbol type)
        => type.GetAttributes().Any(attribute =>
            attribute.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.FlagsAttribute");

    /// <summary>
    /// 计算 <c>[Flags]</c> 枚举里「没有任何成员占用」的位，供 <c>TryParse</c> 做位校验。
    /// </summary>
    /// <remarks>
    /// <c>[Flags]</c> 不能用 <c>Enum.IsDefined</c> 收紧，故改为「不许出现未定义的位」。
    /// 掩码按底层类型宽度裁剪，否则 <c>sbyte</c> 的 <c>-1</c> 在 <see cref="ulong"/> 里是满位，会吃掉整个掩码。
    /// </remarks>
    internal static ulong? GetUndefinedBitsMask(ITypeSymbol enumType)
    {
        if (enumType is not INamedTypeSymbol { EnumUnderlyingType: { } underlying } namedEnum)
        {
            return null;
        }

        var width = GetIntegralBitWidth(underlying.SpecialType);

        if (width == 0)
        {
            return null;
        }

        var widthMask = width == 64 ? ulong.MaxValue : (1UL << width) - 1;
        var definedBits = 0UL;

        foreach (var member in namedEnum.GetMembers())
        {
            if (member is IFieldSymbol { HasConstantValue: true, ConstantValue: { } constant })
            {
                definedBits |= ToBits(constant);
            }
        }

        var undefinedBits = ~definedBits & widthMask;

        return undefinedBits == 0 ? null : undefinedBits;
    }

    /// <summary>
    /// 取整型基元类型的位宽；非整型返回 0。
    /// </summary>
    internal static int GetIntegralBitWidth(SpecialType type)
        => type switch
        {
            SpecialType.System_SByte or SpecialType.System_Byte => 8,
            SpecialType.System_Int16 or SpecialType.System_UInt16 => 16,
            SpecialType.System_Int32 or SpecialType.System_UInt32 => 32,
            SpecialType.System_Int64 or SpecialType.System_UInt64 => 64,
            _ => 0,
        };

    /// <summary>
    /// 把枚举成员的常量值按位展开成 <see cref="ulong"/>（负数按补码展开，随后由宽度掩码裁掉高位）。
    /// </summary>
    internal static ulong ToBits(object constant)
        => constant switch
        {
            sbyte value => unchecked((ulong)value),
            byte value => value,
            short value => unchecked((ulong)value),
            ushort value => value,
            int value => unchecked((ulong)value),
            uint value => value,
            long value => unchecked((ulong)value),
            ulong value => value,
            _ => 0UL,
        };
}
