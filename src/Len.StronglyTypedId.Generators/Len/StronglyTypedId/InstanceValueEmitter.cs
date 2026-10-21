using System.Globalization;

namespace Len.StronglyTypedId;

/// <summary>
/// 把命名实例声明的文本值翻译成生成代码里的表达式，并在编译期完成校验。
/// </summary>
/// <remarks>
/// <para>
/// 生成器跑在 netstandard2.0 上，<c>Half</c> / <c>Int128</c> / <c>UInt128</c> / <c>DateOnly</c> /
/// <c>TimeOnly</c> 在那里并不存在，无法直接调用它们的 <c>TryParse</c>。这五种改为手写文本校验，
/// 再生成运行期 <c>Parse</c> 调用 —— 入参已被编译期校验过，运行期不会失败。
/// </para>
/// <para>
/// 其余基元一律翻译成字面量或构造函数表达式，运行期零解析。校验与发射共用同一份代码，
/// 分析器据此报 <c>STIA0021</c>，生成器据此决定是否跳过该成员。
/// </para>
/// </remarks>
internal static class InstanceValueEmitter
{
    /// <summary>
    /// 生成代码里传给 <c>Parse</c> 的不变文化实参。
    /// </summary>
    private const string Invariant = "global::System.Globalization.CultureInfo.InvariantCulture";

    /// <summary>
    /// 数值型 <c>TryParse(string, NumberStyles, IFormatProvider, out T)</c> 的统一签名。
    /// </summary>
    private delegate bool IntegralParser<T>(string text, NumberStyles style, IFormatProvider provider, out T value);

    /// <summary>
    /// 把实例声明的文本值翻译成可直接写进生成代码的表达式。
    /// </summary>
    /// <param name="primitiveType">强类型 Id 所包装的基元类型。</param>
    /// <param name="text">实例声明的文本值。</param>
    /// <param name="expression">
    /// 成功时为表达式文本（单行、无缩进）；失败时为 <see langword="null"/>。
    /// </param>
    /// <returns>文本是该基元的合法取值时为 <see langword="true"/>。</returns>
    internal static bool TryEmit(ITypeSymbol primitiveType, string text, out string? expression)
    {
        expression = null;

        if (primitiveType.TypeKind == TypeKind.Enum)
        {
            return TryEmitEnum(primitiveType, text, out expression);
        }

        if (primitiveType is not INamedTypeSymbol namedType)
        {
            return false;
        }

        switch (namedType.SpecialType)
        {
            case SpecialType.System_String:
                expression = SymbolDisplay.FormatLiteral(text, quote: true);
                return true;

            case SpecialType.System_Char:
                // char 的合法文本就是「长度恰为 1」，与生成器里 TryParse 的判定一致。
                if (text.Length != 1)
                {
                    return false;
                }

                expression = SymbolDisplay.FormatLiteral(text[0], quote: true);
                return true;

            case SpecialType.System_Byte:
                return TryEmitIntegral(text, "byte", (IntegralParser<byte>)byte.TryParse, out expression);

            case SpecialType.System_SByte:
                return TryEmitIntegral(text, "sbyte", (IntegralParser<sbyte>)sbyte.TryParse, out expression);

            case SpecialType.System_Int16:
                return TryEmitIntegral(text, "short", (IntegralParser<short>)short.TryParse, out expression);

            case SpecialType.System_UInt16:
                return TryEmitIntegral(text, "ushort", (IntegralParser<ushort>)ushort.TryParse, out expression);

            case SpecialType.System_Int32:
                return TryEmitIntegral(text, "int", (IntegralParser<int>)int.TryParse, out expression);

            case SpecialType.System_UInt32:
                return TryEmitIntegral(text, "uint", (IntegralParser<uint>)uint.TryParse, out expression);

            case SpecialType.System_Int64:
                return TryEmitIntegral(text, "long", (IntegralParser<long>)long.TryParse, out expression);

            case SpecialType.System_UInt64:
                return TryEmitIntegral(text, "ulong", (IntegralParser<ulong>)ulong.TryParse, out expression);

            case SpecialType.System_Single:
                return TryEmitSingle(text, out expression);

            case SpecialType.System_Double:
                return TryEmitDouble(text, out expression);

            case SpecialType.System_Decimal:
                return TryEmitDecimal(text, out expression);
        }

        var containingNamespace = namedType.ContainingNamespace.ToDisplayString();

        if (containingNamespace == "System")
        {
            switch (namedType.Name)
            {
                case "Guid":
                    return TryEmitGuid(text, out expression);
                case "DateTime":
                    return TryEmitDateTime(text, out expression);
                case "DateTimeOffset":
                    return TryEmitDateTimeOffset(text, out expression);
                case "TimeSpan":
                    return TryEmitTimeSpan(text, out expression);
                case "Half":
                    return TryEmitHalf(text, out expression);
                case "Int128":
                    return TryEmitBigIntegerLike(text, "global::System.Int128", isSigned: true, out expression);
                case "UInt128":
                    return TryEmitBigIntegerLike(text, "global::System.UInt128", isSigned: false, out expression);
                case "DateOnly":
                    return TryEmitDateOnly(text, out expression);
                case "TimeOnly":
                    return TryEmitTimeOnly(text, out expression);
            }
        }

        return containingNamespace == "System.Numerics" && namedType.Name == "BigInteger"
            && TryEmitBigIntegerLike(text, "global::System.Numerics.BigInteger", isSigned: true, out expression);
    }

    /// <summary>
    /// 生成整型字面量：显式转换到目标类型，避免字面量默认推断成 <c>int</c> 后无法隐式转换。
    /// </summary>
    private static bool TryEmitIntegral<T>(string text, string typeName, IntegralParser<T> parser, out string? expression)
        where T : IFormattable
    {
        expression = null;

        // NumberStyles.Integer 只接受前后空白与正负号，挡掉千位分隔符与小数 —— 与 Parse 的整型语义一致。
        if (!parser(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        expression = $"({typeName})({value.ToString(null, CultureInfo.InvariantCulture)})";
        return true;
    }

    private static bool TryEmitSingle(string text, out string? expression)
    {
        expression = null;

        if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        expression = float.IsNaN(value)
            ? "global::System.Single.NaN"
            : float.IsPositiveInfinity(value)
                ? "global::System.Single.PositiveInfinity"
                : float.IsNegativeInfinity(value)
                    ? "global::System.Single.NegativeInfinity"
                    : value.ToString("R", CultureInfo.InvariantCulture) + "F";
        return true;
    }

    private static bool TryEmitDouble(string text, out string? expression)
    {
        expression = null;

        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        expression = double.IsNaN(value)
            ? "global::System.Double.NaN"
            : double.IsPositiveInfinity(value)
                ? "global::System.Double.PositiveInfinity"
                : double.IsNegativeInfinity(value)
                    ? "global::System.Double.NegativeInfinity"
                    : value.ToString("R", CultureInfo.InvariantCulture) + "D";
        return true;
    }

    private static bool TryEmitDecimal(string text, out string? expression)
    {
        expression = null;

        if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        // decimal 没有往返格式（"R" 会抛），不变文化的默认格式本身就是最短往返表示。
        expression = value.ToString(CultureInfo.InvariantCulture) + "M";
        return true;
    }

    private static bool TryEmitGuid(string text, out string? expression)
    {
        expression = null;

        if (!Guid.TryParse(text, out var value))
        {
            return false;
        }

        expression = $"new global::System.Guid({Literal(value.ToString("D", CultureInfo.InvariantCulture))})";
        return true;
    }

    private static bool TryEmitDateTime(string text, out string? expression)
    {
        expression = null;

        if (!DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var value))
        {
            return false;
        }

        // 用 ticks + Kind 发射：文本里的 "Z" / 偏移必须原样保留，否则 Utc 会被静默折成 Unspecified。
        expression = $"new global::System.DateTime({Ticks(value.Ticks)}, global::System.DateTimeKind.{value.Kind})";
        return true;
    }

    private static bool TryEmitDateTimeOffset(string text, out string? expression)
    {
        expression = null;

        if (!DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var value))
        {
            return false;
        }

        // 必须带偏移：只发 ticks 会把 "+08:00" 折成 UTC，往返后不再相等。
        expression = $"new global::System.DateTimeOffset({Ticks(value.Ticks)}, new global::System.TimeSpan({Ticks(value.Offset.Ticks)}))";
        return true;
    }

    private static bool TryEmitTimeSpan(string text, out string? expression)
    {
        expression = null;

        if (!TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        expression = $"new global::System.TimeSpan({Ticks(value.Ticks)})";
        return true;
    }

    private static bool TryEmitHalf(string text, out string? expression)
    {
        expression = null;

        // netstandard2.0 没有 Half，先用 float 校验再显式转换；超出 Half 范围的有限值会被转成无穷，故先挡掉。
        if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            || float.IsNaN(value)
            || float.IsInfinity(value)
            || Math.Abs(value) > 65504F)
        {
            return false;
        }

        expression = $"(global::System.Half)({value.ToString("R", CultureInfo.InvariantCulture)}F)";
        return true;
    }

    /// <summary>
    /// 生成 <c>Int128</c> / <c>UInt128</c> / <c>BigInteger</c> 的表达式。
    /// </summary>
    /// <remarks>
    /// 这三种在 netstandard2.0 里不存在，无法在编译期真正解析，故手写十进制校验后生成运行期 <c>Parse</c>。
    /// 入参已校验过，运行期不会失败。
    /// </remarks>
    private static bool TryEmitBigIntegerLike(string text, string typeName, bool isSigned, out string? expression)
    {
        expression = null;

        if (!TryNormalizeInteger(text, isSigned, out var normalized))
        {
            return false;
        }

        if (typeName != "global::System.Numerics.BigInteger" && !FitsIn128Bits(normalized, isSigned))
        {
            return false;
        }

        expression = $"{typeName}.Parse({Literal(normalized)}, {Invariant})";
        return true;
    }

    private static bool TryEmitDateOnly(string text, out string? expression)
    {
        expression = null;

        // 只接受规范形 yyyy-MM-dd："2020-1-1" 这类写法在 DateOnly.Parse 下也成立，但会让实例声明失去确定性。
        if (text.Length != 10
            || text[4] != '-'
            || text[7] != '-'
            || !TryParseDigits(text, 0, 4, out var year)
            || !TryParseDigits(text, 5, 2, out var month)
            || !TryParseDigits(text, 8, 2, out var day)
            || year < 1
            || year > 9999
            || month < 1
            || month > 12
            || day < 1
            || day > DateTime.DaysInMonth(year, month))
        {
            return false;
        }

        expression = $"new global::System.DateOnly({year}, {month}, {day})";
        return true;
    }

    private static bool TryEmitTimeOnly(string text, out string? expression)
    {
        expression = null;

        // 接受 HH:mm、HH:mm:ss 与 HH:mm:ss.f–fffffff；小数部分按 100ns 刻度右补零。
        if (text.Length < 5
            || text[2] != ':'
            || !TryParseDigits(text, 0, 2, out var hour)
            || !TryParseDigits(text, 3, 2, out var minute)
            || hour > 23
            || minute > 59)
        {
            return false;
        }

        var second = 0;
        var fraction = 0L;

        if (text.Length > 5)
        {
            if (text.Length < 8
                || text[5] != ':'
                || !TryParseDigits(text, 6, 2, out second)
                || second > 59)
            {
                return false;
            }

            if (text.Length > 8)
            {
                var digits = text.Length - 9;

                if (text[8] != '.'
                    || digits is < 1 or > 7
                    || !TryParseDigits(text, 9, digits, out var value))
                {
                    return false;
                }

                for (var index = digits; index < 7; index++)
                {
                    value *= 10;
                }

                fraction = value;
            }
        }

        var ticks = (((hour * 60L) + minute) * 60L + second) * TimeSpan.TicksPerSecond + fraction;

        expression = $"new global::System.TimeOnly({Ticks(ticks)})";
        return true;
    }

    /// <summary>
    /// 生成枚举字面量：解析成底层整数值后显式转换，避免依赖成员名的可读性差异。
    /// </summary>
    private static bool TryEmitEnum(ITypeSymbol enumType, string text, out string? expression)
    {
        expression = null;

        if (enumType is not INamedTypeSymbol { EnumUnderlyingType: { } underlying } namedEnum)
        {
            return false;
        }

        var width = StronglyTypedIdInfo.GetIntegralBitWidth(underlying.SpecialType);

        if (width == 0)
        {
            return false;
        }

        var widthMask = WidthMask(width);
        var isSigned = IsSigned(underlying.SpecialType);
        var members = new Dictionary<string, ulong>(StringComparer.Ordinal);

        foreach (var member in namedEnum.GetMembers())
        {
            if (member is IFieldSymbol { HasConstantValue: true, ConstantValue: { } constant } field)
            {
                members[field.Name] = StronglyTypedIdInfo.ToBits(constant) & widthMask;
            }
        }

        // 与 Enum.TryParse 一致：接受成员名、数值，以及逗号分隔的组合（组合按位或）。
        var bits = 0UL;

        foreach (var part in text.Split(','))
        {
            var trimmed = part.Trim();

            if (trimmed.Length == 0)
            {
                return false;
            }

            if (members.TryGetValue(trimmed, out var memberBits))
            {
                bits |= memberBits;
                continue;
            }

            if (!TryParseEnumNumeric(trimmed, width, isSigned, out var numeric))
            {
                return false;
            }

            bits |= numeric;
        }

        bits &= widthMask;

        if (StronglyTypedIdInfo.HasFlagsAttribute(namedEnum))
        {
            // 位组合本身就是合法取值，只要求不出现未定义的位。
            if (StronglyTypedIdInfo.GetFlagsEnumUndefinedBitsMask(namedEnum) is { } undefinedBits
                && (bits & undefinedBits) != 0)
            {
                return false;
            }
        }
        else if (!members.ContainsValue(bits))
        {
            // 非 flags 枚举必须恰好命中某个成员，与生成器里 TryParse 追加的 Enum.IsDefined 同源。
            return false;
        }

        var literal = isSigned
            ? SignExtend(bits, width).ToString(CultureInfo.InvariantCulture)
            : bits.ToString(CultureInfo.InvariantCulture);

        expression = $"({enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)})({literal})";
        return true;
    }

    /// <summary>
    /// 把枚举的数值文本解析成按位展开的 <see cref="ulong"/>，并按底层类型的宽度做范围校验。
    /// </summary>
    private static bool TryParseEnumNumeric(string text, int width, bool isSigned, out ulong bits)
    {
        bits = 0;

        if (isSigned)
        {
            if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                return false;
            }

            var minimum = width == 64 ? long.MinValue : -(1L << (width - 1));
            var maximum = width == 64 ? long.MaxValue : (1L << (width - 1)) - 1;

            if (value < minimum || value > maximum)
            {
                return false;
            }

            bits = unchecked((ulong)value) & WidthMask(width);
            return true;
        }

        if (!ulong.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unsigned))
        {
            return false;
        }

        if (unsigned > WidthMask(width))
        {
            return false;
        }

        bits = unsigned;
        return true;
    }

    /// <summary>
    /// 校验并归一化十进制整数文本：可选正负号 + 至少一位数字，无空白、无分隔符。
    /// </summary>
    private static bool TryNormalizeInteger(string text, bool isSigned, out string normalized)
    {
        normalized = string.Empty;

        var start = text.Length > 0 && (text[0] == '-' || text[0] == '+') ? 1 : 0;

        if (start == 1 && text[0] == '-' && !isSigned)
        {
            return false;
        }

        if (text.Length == start)
        {
            return false;
        }

        for (var index = start; index < text.Length; index++)
        {
            if (!IsDigit(text[index]))
            {
                return false;
            }
        }

        var digits = text[start..].TrimStart('0');

        if (digits.Length == 0)
        {
            normalized = "0";
            return true;
        }

        normalized = start == 1 && text[0] == '-' ? "-" + digits : digits;
        return true;
    }

    /// <summary>
    /// 判断归一化后的十进制整数是否落在 128 位整数范围内。
    /// </summary>
    private static bool FitsIn128Bits(string normalized, bool isSigned)
    {
        const string SignedMax = "170141183460469231731687303715884105727";
        const string SignedMinMagnitude = "170141183460469231731687303715884105728";
        const string UnsignedMax = "340282366920938463463374607431768211455";

        var isNegative = normalized.Length > 0 && normalized[0] == '-';
        var magnitude = isNegative ? normalized[1..] : normalized;
        var limit = isSigned
            ? isNegative ? SignedMinMagnitude : SignedMax
            : UnsignedMax;

        return magnitude.Length != limit.Length
            ? magnitude.Length < limit.Length
            : string.CompareOrdinal(magnitude, limit) <= 0;
    }

    /// <summary>
    /// 从 <paramref name="start"/> 起读取恰好 <paramref name="length"/> 位十进制数字。
    /// </summary>
    private static bool TryParseDigits(string text, int start, int length, out int value)
    {
        value = 0;

        if (start + length > text.Length)
        {
            return false;
        }

        for (var index = start; index < start + length; index++)
        {
            if (!IsDigit(text[index]))
            {
                return false;
            }

            value = (value * 10) + (text[index] - '0');
        }

        return true;
    }

    private static bool IsDigit(char character) => character is >= '0' and <= '9';

    private static bool IsSigned(SpecialType type)
        => type is SpecialType.System_SByte
            or SpecialType.System_Int16
            or SpecialType.System_Int32
            or SpecialType.System_Int64;

    private static ulong WidthMask(int width) => width == 64 ? ulong.MaxValue : (1UL << width) - 1;

    /// <summary>
    /// 把按位展开的值还原成该宽度下的有符号数（补码符号扩展）。
    /// </summary>
    private static long SignExtend(ulong bits, int width)
        => width == 64 ? unchecked((long)bits) : unchecked((long)(bits << (64 - width)) >> (64 - width));

    private static string Ticks(long ticks) => ticks.ToString(CultureInfo.InvariantCulture) + "L";

    private static string Literal(string value) => SymbolDisplay.FormatLiteral(value, quote: true);
}
