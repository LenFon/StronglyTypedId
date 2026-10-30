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
}
