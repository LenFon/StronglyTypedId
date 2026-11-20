namespace Len.StronglyTypedId.Generators;

/// <summary>
/// 基元类型 → <c>System.Text.Json</c> 的直写 / 直读表达式。
/// </summary>
/// <remarks>
/// 把基元读写外包给 <c>JsonSerializer</c> 会要求 <c>options</c> 里有该基元的 <c>JsonTypeInfo</c>，
/// 源生成的 <c>JsonSerializerContext</c> 只服务 <c>[JsonSerializable]</c> 登记过的类型，漏登记时报的是框架内部异常。
/// <para>
/// 大部分基元可以直接写，如 <c>Utf8JsonWriter.WriteStringValue(Guid)</c>，输出与 STJ 默认逐字节相同。
/// 少数类型（<c>Int128</c> / <c>Half</c> / <c>TimeSpan</c> 等）仍走委托。等价性由 <c>PrimitiveContractMatrixTests</c> 守着。
/// </para>
/// </remarks>
internal static class PrimitiveJsonIo
{
    /// <summary>
    /// 取写侧表达式：把已解包的基元值 <c>primitiveId</c> 写进 <c>writer</c>。
    /// </summary>
    /// <param name="idInfo">当前 Id 的描述信息。</param>
    /// <returns>
    /// 直写表达式（含结尾分号）；返回 <see langword="null"/> 表示该基元必须委托给
    /// <c>JsonSerializer.Serialize</c>，调用方需另作处理。
    /// </returns>
    public static string? TryGetWriteExpression(StronglyTypedIdInfo idInfo)
        => idInfo.IsCustomPrimitive ? null : idInfo.IsEnum ? GetEnumWriteExpression(idInfo) : GetWriteExpressionByShortName(idInfo);

    /// <summary>
    /// 按基元短名取写侧表达式。
    /// </summary>
    private static string? GetWriteExpressionByShortName(StronglyTypedIdInfo idInfo)
        => StronglyTypedIdInfo.GetPrimitiveIdTypeShortName(idInfo.PrimitiveIdTypeName) switch
        {
            // Guid 的 "D" 格式（小写带连字符）与 STJ 默认完全一致。
            "Guid" => "writer.WriteStringValue(primitiveId);",
            "string" => "writer.WriteStringValue(primitiveId);",
            // char 的 IFormattable.ToString(string, IFormatProvider) 是显式接口实现（CS1501），
            // 且其无参 ToString() 与文化无关。
            "char" => "writer.WriteStringValue(primitiveId.ToString());",
            // WriteNumberValue 没有 byte / short 重载；先转成 int，JSON 数字形态不变。
            "byte" or "sbyte" or "short" or "ushort" => "writer.WriteNumberValue((int)primitiveId);",
            "int" => "writer.WriteNumberValue(primitiveId);",
            "uint" => "writer.WriteNumberValue(primitiveId);",
            "long" => "writer.WriteNumberValue(primitiveId);",
            "ulong" => "writer.WriteNumberValue(primitiveId);",
            "decimal" => "writer.WriteNumberValue(primitiveId);",
            "float" => "writer.WriteNumberValue(primitiveId);",
            "double" => "writer.WriteNumberValue(primitiveId);",
            // DateTime / DateTimeOffset 走 STJ 同一套写出逻辑（trim 过的 ISO 8601），逐字节一致。
            "DateTime" => "writer.WriteStringValue(primitiveId);",
            "DateTimeOffset" => "writer.WriteStringValue(primitiveId);",
            _ => null,
        };

    /// <summary>
    /// 取读侧表达式：从 <c>reader</c> 读出基元值。
    /// </summary>
    /// <param name="idInfo">当前 Id 的描述信息。</param>
    /// <returns>
    /// 直读表达式（不含结尾分号）；返回 <see langword="null"/> 表示该基元必须委托给
    /// <c>JsonSerializer.Deserialize</c>，调用方需另作处理。
    /// </returns>
    public static string? TryGetReadExpression(StronglyTypedIdInfo idInfo)
        => idInfo.IsCustomPrimitive ? null : idInfo.IsEnum ? GetEnumReadExpression(idInfo) : GetReadExpressionByShortName(idInfo);

    /// <summary>
    /// 按基元短名取读侧表达式。
    /// </summary>
    private static string? GetReadExpressionByShortName(StronglyTypedIdInfo idInfo)
        => StronglyTypedIdInfo.GetPrimitiveIdTypeShortName(idInfo.PrimitiveIdTypeName) switch
        {
            "Guid" => "reader.GetGuid()",
            "string" => "reader.GetString()!",
            "byte" => "reader.GetByte()",
            "sbyte" => "reader.GetSByte()",
            "short" => "reader.GetInt16()",
            "ushort" => "reader.GetUInt16()",
            "int" => "reader.GetInt32()",
            "uint" => "reader.GetUInt32()",
            "long" => "reader.GetInt64()",
            "ulong" => "reader.GetUInt64()",
            "decimal" => "reader.GetDecimal()",
            "float" => "reader.GetSingle()",
            "double" => "reader.GetDouble()",
            "DateTime" => "reader.GetDateTime()",
            "DateTimeOffset" => "reader.GetDateTimeOffset()",
            _ => null,
        };

    /// <summary>
    /// 取「<c>AllowReadingFromString</c> 分支」的读计划：把字符串令牌解析成基元值。
    /// </summary>
    /// <param name="idInfo">当前 Id 的描述信息。</param>
    /// <returns>
    /// 解析表达式与可选的后置校验；返回 <see langword="null"/> 表示该基元不受
    /// <c>JsonNumberHandling</c> 影响，调用方不应生成分支。
    /// </returns>
    /// <remarks>
    /// 只覆盖走直读路径的数值基元。<c>Half</c> / <c>Int128</c> / <c>UInt128</c> 走委托路径，
    /// <c>JsonSerializer</c> 已原生遵守该选项；<c>BigInteger</c> 本来就是文本形。
    /// <para>
    /// 枚举被排除：STJ 的内置枚举转换器完全不看 <c>NumberHandling</c>（实测 <c>WriteAsString</c> 仍写
    /// <c>1</c>、<c>AllowReadingFromString</c> 仍拒绝 <c>"1"</c>）。让 Id 遵守它反而与裸枚举不一致。
    /// </para>
    /// <para>
    /// <c>NumberStyles</c> 按 STJ 的实测接受面挑选，而非 <c>Integer</c> / <c>Number</c> 这类含空白的组合：
    /// STJ 拒绝首尾空白，无符号类型拒绝前导 <c>+</c>，<c>decimal</c> 接受指数形却拒绝千位分隔符。
    /// </para>
    /// </remarks>
    public static StringReadPlan? TryGetStringReadPlan(StronglyTypedIdInfo idInfo)
    {
        if (idInfo.IsCustomPrimitive || idInfo.IsEnum || idInfo.IsBigIntegerPrimitive)
        {
            return null;
        }

        var shortName = StronglyTypedIdInfo.GetPrimitiveIdTypeShortName(idInfo.PrimitiveIdTypeName);

        var styles = shortName switch
        {
            // 有符号整数：STJ 接受前导 + / -，拒绝空白与小数点。
            "sbyte" or "short" or "int" or "long" => "AllowLeadingSign",
            // 无符号整数：STJ 连前导 + 都拒绝，只认纯数字。
            "byte" or "ushort" or "uint" or "ulong" => "None",
            // 浮点与 decimal：接受符号、小数点、指数；拒绝空白与千位分隔符。
            "decimal" or "float" or "double" => "AllowLeadingSign | global::System.Globalization.NumberStyles.AllowDecimalPoint | global::System.Globalization.NumberStyles.AllowExponent",
            _ => null,
        };

        if (styles is null)
        {
            return null;
        }

        var parse = $"{idInfo.PrimitiveIdTypeName}.Parse(text, global::System.Globalization.NumberStyles.{styles}, global::System.Globalization.CultureInfo.InvariantCulture)";

        // Parse 比 STJ 宽松两处：小写 nan / infinity 也认，且溢出到无穷时返回 Infinity 而不报错。
        // STJ 只接受三个精确拼写，其余一律拒绝，故补一道后置校验，否则「Id 与裸基元一致」就不成立。
        var guard = shortName is "float" or "double"
            ? $"({idInfo.PrimitiveIdTypeName}.IsNaN(value) || {idInfo.PrimitiveIdTypeName}.IsInfinity(value)) && text is not (\"NaN\" or \"Infinity\" or \"-Infinity\")"
            : null;

        return new StringReadPlan(parse, guard);
    }

    /// <summary>
    /// 取「<c>WriteAsString</c> 分支」的写表达式：把基元值写成不变文化的文本。
    /// </summary>
    /// <param name="idInfo">当前 Id 的描述信息。</param>
    /// <returns>
    /// 直写表达式（含结尾分号）；返回 <see langword="null"/> 表示该基元不受
    /// <c>NumberHandling</c> 影响，调用方不应生成分支。
    /// </returns>
    /// <remarks>
    /// 覆盖范围与 <see cref="TryGetStringReadPlan"/> 一致。<c>ToString(IFormatProvider)</c> 的默认
    /// 「G」格式与 STJ 的 <c>WriteAsString</c> 逐字符相同（含 <c>NaN</c> / <c>Infinity</c> / <c>-0</c> / 指数形）。
    /// <para>
    /// 必须用 <c>WriteRawValue</c> 而不是 <c>WriteStringValue</c>：后者会转义 <c>+</c>（写成 <c>\u002B</c>），
    /// 而 STJ 的数值路径写的是裸 <c>"1E+300"</c>。两者 JSON 语义等价，但「逐字节一致」不成立，
    /// 且会让按文本比较的契约测试与签名校验失配。
    /// </para>
    /// </remarks>
    public static string? TryGetStringWriteExpression(StronglyTypedIdInfo idInfo)
        => idInfo.IsCustomPrimitive || idInfo.IsEnum || idInfo.IsBigIntegerPrimitive
            ? null
            : StronglyTypedIdInfo.GetPrimitiveIdTypeShortName(idInfo.PrimitiveIdTypeName) switch
            {
                "byte" or "sbyte" or "short" or "ushort" or "int" or "uint" or "long" or "ulong"
                    or "decimal" or "float" or "double"
                    => "writer.WriteRawValue(\"\\\"\" + primitiveId.ToString(global::System.Globalization.CultureInfo.InvariantCulture) + \"\\\"\");",
                _ => null,
            };

    /// <summary>
    /// 取枚举基元的写侧表达式：STJ 默认把枚举写成底层数值，这里按底层类型分派。
    /// </summary>
    private static string? GetEnumWriteExpression(StronglyTypedIdInfo idInfo)
        => idInfo.EnumUnderlyingTypeName switch
        {
            "int" or "byte" or "sbyte" or "short" or "ushort" => "writer.WriteNumberValue((int)primitiveId);",
            "uint" => "writer.WriteNumberValue((uint)primitiveId);",
            "long" => "writer.WriteNumberValue((long)primitiveId);",
            "ulong" => "writer.WriteNumberValue((ulong)primitiveId);",
            _ => null,
        };

    /// <summary>
    /// 取枚举基元的读侧表达式：按底层类型读出数值后转成枚举。
    /// </summary>
    private static string? GetEnumReadExpression(StronglyTypedIdInfo idInfo)
    {
        var read = idInfo.EnumUnderlyingTypeName switch
        {
            "int" => "reader.GetInt32()",
            "byte" => "reader.GetByte()",
            "sbyte" => "reader.GetSByte()",
            "short" => "reader.GetInt16()",
            "ushort" => "reader.GetUInt16()",
            "uint" => "reader.GetUInt32()",
            "long" => "reader.GetInt64()",
            "ulong" => "reader.GetUInt64()",
            _ => null,
        };

        return read is null ? null : $"({idInfo.PrimitiveIdTypeName}){read}";
    }

    /// <summary>
    /// 「<c>AllowReadingFromString</c> 分支」的读计划。
    /// </summary>
    /// <param name="ParseExpression">把局部变量 <c>text</c> 解析成基元值的表达式。</param>
    /// <param name="InvalidCondition">
    /// 解析结果超出 STJ 接受面的条件表达式；为 <see langword="null"/> 时无需后置校验。
    /// 条件为真时调用方应抛 <see cref="FormatException"/>，由外层统一转成 <c>JsonException</c>。
    /// </param>
    internal readonly record struct StringReadPlan(string ParseExpression, string? InvalidCondition);
}
