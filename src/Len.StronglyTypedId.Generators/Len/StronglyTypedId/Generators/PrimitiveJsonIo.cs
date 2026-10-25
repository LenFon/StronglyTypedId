namespace Len.StronglyTypedId.Generators;

/// <summary>
/// 基元类型 → <c>System.Text.Json</c> 的直写 / 直读表达式。
/// </summary>
/// <remarks>
/// <para>
/// 转换器若把基元的读写外包给 <c>JsonSerializer.Serialize&lt;T&gt;</c> / <c>Deserialize&lt;T&gt;</c>，
/// 就必须要求 <c>options</c> 里有该基元类型的 <c>JsonTypeInfo</c> —— 源生成的
/// <c>JsonSerializerContext</c> 只服务 <c>[JsonSerializable]</c> 登记过的类型，于是调用方还得
/// 为每个用到的基元类型补一条登记。漏登记时报的是框架内部的
/// <c>NotSupportedException: JsonTypeInfo metadata for type 'System.Guid' was not provided</c>，
/// 调用方很难自行定位到「少登记了一个基元类型」。
/// </para>
/// <para>
/// 写一个 Guid 并不需要任何元数据：<c>Utf8JsonWriter.WriteStringValue(Guid)</c> 一行就够，
/// 且输出与 STJ 默认<b>逐字节相同</b>。本表把能直写的基元全部改成直写直读，让转换器自给自足；
/// 剩下的少数类型（<c>Int128</c> / <c>Half</c> / <c>TimeSpan</c> 等）
/// 仍走委托，并另行给出可操作的错误提示。
/// </para>
/// <para>
/// 直写与 STJ 默认的等价性由 <c>test/Len.StronglyTypedId.Test</c> 的
/// <c>PrimitiveContractMatrixTests</c> 守着：每种基元都拿「STJ 默认序列化结果」与「生成代码的写出结果」比字节。
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
        => idInfo.IsEnum ? GetEnumWriteExpression(idInfo) : GetWriteExpressionByShortName(idInfo);

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
        => idInfo.IsEnum ? GetEnumReadExpression(idInfo) : GetReadExpressionByShortName(idInfo);

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
