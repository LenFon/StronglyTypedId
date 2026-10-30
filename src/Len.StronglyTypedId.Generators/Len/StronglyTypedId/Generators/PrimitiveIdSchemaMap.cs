namespace Len.StronglyTypedId.Generators;

/// <summary>
/// 基元类型 → OpenAPI schema 的 <c>(type, format)</c> 映射，Swagger（Swashbuckle）与内置 OpenAPI 两个生成器共用，
/// 避免两处各写一份 switch 而逐渐漂移。
/// </summary>
/// <remarks>
/// 口径对齐 System.Text.Json 的默认写出形态：整数 → <c>integer</c>，浮点与 <c>decimal</c> → <c>number</c>，
/// 枚举 → 底层整数，日期时间 → <c>string</c> 加 format；有前缀时恒为 <c>string</c> 且不带 format。
/// </remarks>
internal static class PrimitiveIdSchemaMap
{
    /// <summary>
    /// OpenAPI 的 <c>string</c> 类型关键字。
    /// </summary>
    public const string String = "string";

    /// <summary>
    /// OpenAPI 的 <c>integer</c> 类型关键字。
    /// </summary>
    public const string Integer = "integer";

    /// <summary>
    /// OpenAPI 的 <c>number</c> 类型关键字。
    /// </summary>
    public const string Number = "number";

    /// <summary>
    /// 取该 Id 对应的 OpenAPI <c>(type, format)</c>；无特定格式时 <c>Format</c> 为 <see langword="null"/>。
    /// </summary>
    public static (string Type, string? Format) Resolve(StronglyTypedIdInfo idInfo)
    {
        // 有前缀时对外暴露的是规范文本形，数字 / 枚举 / 日期基元也一样。
        if (idInfo.HasPrefix)
        {
            return (String, null);
        }

        // 枚举在 JSON 里按底层整数写出（STJ 默认行为），不按成员名。
        if (idInfo.IsEnum)
        {
            return (Integer, null);
        }

        // 自定义基元的 JSON 形是规范文本（见 StronglyTypedIdInfo.IsCustomPrimitive），故对外是裸 string。
        // 不能落到下面的短名 switch：用户自定义的 MyNs.Guid 会被误判成 uuid。
        if (idInfo.IsCustomPrimitive)
        {
            return (String, null);
        }

        return StronglyTypedIdInfo.GetPrimitiveIdTypeShortName(idInfo.PrimitiveIdTypeName) switch
        {
            "Guid" => (String, "uuid"),
            "string" or "char" => (String, null),
            "byte" => (Integer, "byte"),
            "sbyte" => (Integer, "sbyte"),
            "short" => (Integer, "int16"),
            "ushort" => (Integer, "uint16"),
            "int" => (Integer, "int32"),
            "uint" => (Integer, "uint32"),
            "long" => (Integer, "int64"),
            "ulong" => (Integer, "uint64"),
            // 超出 int64 / uint64 的范围，OpenAPI 没有对应 format，只能给裸 integer。
            "Int128" or "UInt128" or "BigInteger" => (Integer, null),
            "decimal" => (Number, null),
            "double" => (Number, "double"),
            "float" => (Number, "float"),
            "Half" => (Number, null),
            "DateTime" or "DateTimeOffset" => (String, "date-time"),
            "DateOnly" => (String, "date"),
            "TimeOnly" => (String, "time"),
            // TimeSpan 的 JSON 形是 "hh:mm:ss"，不是 ISO 8601 duration，故不给 format。
            "TimeSpan" => (String, null),
            _ => (String, null),
        };
    }
}
