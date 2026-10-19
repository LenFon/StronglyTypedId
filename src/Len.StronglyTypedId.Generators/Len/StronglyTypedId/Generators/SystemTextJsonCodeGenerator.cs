using System.Text;

namespace Len.StronglyTypedId.Generators;

/// <summary>
/// 为强类型 Id 生成 System.Text.Json 转换器。
/// </summary>
/// <remarks>
/// 除值的读写外还必须重写 <c>ReadAsPropertyName</c> / <c>WriteAsPropertyName</c>：STJ 处理字典键走这两条路径，
/// 默认实现只认得内建键类型，否则 <c>Dictionary&lt;OrderId, TValue&gt;</c> 完全不可用。
/// </remarks>
internal class SystemTextJsonCodeGenerator : ICoreCodeGenerator
{
    internal static readonly ICoreCodeGenerator Instance = new SystemTextJsonCodeGenerator();

    /// <summary>
    /// 抑制 IL2026 / IL3050 时给出的理由。
    /// </summary>
    /// <remarks>
    /// <c>Serialize&lt;T&gt;</c> / <c>Deserialize&lt;T&gt;</c> 带 <c>RequiresUnreferencedCode</c> 与
    /// <c>RequiresDynamicCode</c>，AOT / Trim 宿主会报 IL2026 / IL3050；但这里的 <c>T</c> 永远是受支持基元，
    /// STJ 有内建转换器，警告只是保守推断。改用底层 API 会绕过宿主的转换器配置，抑制标注则零行为变更。
    /// </remarks>
    private const string AotSuppressionJustification =
        "The primitive id type is a supported BCL primitive with a built-in System.Text.Json converter; no reflection or dynamic code is required.";

    public int Order { get; } = 1;

    public void Generate(
        StronglyTypedIdInfo idInfo,
        ImmutableArray<ModuleInfo> modules,
        SourceProductionContext context,
        string version)
    {
        var header = GeneratedCode.Header(idInfo.Namespace);

        var declaration = $$"""
                [global::System.Text.Json.Serialization.JsonConverter(typeof(SystemTextJsonConverter))]
                partial record{{idInfo.TypeKindSuffix}} {{idInfo.Name}}
                {
                    /// <inheritdoc/>
                    {{GeneratedCode.CompilerGeneratedAttribute}}
                    {{GeneratedCode.GeneratedCodeAttribute(nameof(SystemTextJsonCodeGenerator), version)}}
                    public class SystemTextJsonConverter : global::System.Text.Json.Serialization.JsonConverter<{{idInfo.Name}}>
                    {
                        /// <inheritdoc/>
                        {{GeneratedCode.CompilerGeneratedAttribute}}
                        {{GeneratedCode.GeneratedCodeAttribute(nameof(SystemTextJsonCodeGenerator), version)}}
                        [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "{{AotSuppressionJustification}}")]
                        [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "{{AotSuppressionJustification}}")]
                        public override {{idInfo.Name}} Read(
                            ref global::System.Text.Json.Utf8JsonReader reader, 
                            global::System.Type typeToConvert, 
                            global::System.Text.Json.JsonSerializerOptions options){{GetReadTail(idInfo)}}
                
                        /// <inheritdoc/>
                        {{GeneratedCode.CompilerGeneratedAttribute}}
                        {{GeneratedCode.GeneratedCodeAttribute(nameof(SystemTextJsonCodeGenerator), version)}}
                        [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "{{AotSuppressionJustification}}")]
                        [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "{{AotSuppressionJustification}}")]
                        public override void Write(
                            global::System.Text.Json.Utf8JsonWriter writer, 
                            {{idInfo.Name}} value, 
                            global::System.Text.Json.JsonSerializerOptions options){{GetWriteTail(idInfo)}}
                
                        /// <inheritdoc/>
                        {{GeneratedCode.CompilerGeneratedAttribute}}
                        {{GeneratedCode.GeneratedCodeAttribute(nameof(SystemTextJsonCodeGenerator), version)}}
                        public override void WriteAsPropertyName(
                            global::System.Text.Json.Utf8JsonWriter writer,
                            {{idInfo.Name}} value,
                            global::System.Text.Json.JsonSerializerOptions options)
                        {
                            if (value is { Value: var primitiveId })
                            {
                                writer.WritePropertyName({{GetPropertyNameExpression(idInfo)}});
                            }
                            else
                            {
                                throw new global::System.NotSupportedException($"A null {{idInfo.Name}} cannot be written as a JSON property name.");
                            }
                        }{{GetReadAsPropertyName(idInfo, version)}}
                    }
                }
                """;

        var code = header + GeneratedCode.NestInContainingTypes(idInfo, declaration);

        context.AddSource($"{idInfo.HintName}.SystemTextJson.g.cs", GeneratedCode.NormalizeLineEndings(code));
    }

    /// <summary>
    /// 取写属性名时用的表达式。
    /// </summary>
    /// <remarks>
    /// 属性名按不变文化写出，否则同一份数据在不同区域下会得到不同的键。枚举例外：其
    /// <c>ToString(string, IFormatProvider)</c> 已 [Obsolete]，用它会 CS0618。
    /// </remarks>
    private static string GetPropertyNameExpression(StronglyTypedIdInfo idInfo)
        => idInfo.HasPrefix
            ? "value.ToString()"
            : idInfo.IsStringPrimitive
                ? "primitiveId"
                : idInfo.IsEnum
                    ? "primitiveId.ToString()"
                    : "primitiveId.ToString(null, global::System.Globalization.CultureInfo.InvariantCulture)";

    /// <summary>
    /// 取读入基元值时用的表达式。
    /// </summary>
    /// <remarks>
    /// 刻意读非可空基元：<c>Deserialize&lt;int?&gt;</c> 会迫使 AOT 宿主额外注册 <c>Nullable&lt;T&gt;</c>。
    /// JSON 的 <c>null</c> 已在调用前由 <c>reader.TokenType</c> 拦下，故 <c>string</c> 补 <c>!</c> 抑制 CS8600。
    /// </remarks>
    private static string GetDeserializeExpression(StronglyTypedIdInfo idInfo)
        => idInfo.IsStringPrimitive
            ? "global::System.Text.Json.JsonSerializer.Deserialize<string>(ref reader, options)!"
            : $"global::System.Text.Json.JsonSerializer.Deserialize<{idInfo.PrimitiveIdTypeName}>(ref reader, options)";

    /// <summary>
    /// 取 <c>Read</c> 成员形参表之后的尾部：基元形态是表达式体，对象形态是方法体。
    /// </summary>
    /// <remarks>
    /// 基元形态（默认）把 Id 与基元值视为同一形状，产物逐字节不变；对象形态读入 <c>{ "value": … }</c>。
    /// 缩进按产物层级写死，行尾由 <see cref="GeneratedCode.NormalizeLineEndings"/> 统一。
    /// </remarks>
    private static string GetReadTail(StronglyTypedIdInfo idInfo)
    {
        var builder = new StringBuilder();

        if (!idInfo.IsObjectJsonShape)
        {
            builder.Append('\n');
            builder.Append("        {").Append('\n');
            builder.Append("            if (reader.TokenType == global::System.Text.Json.JsonTokenType.Null)").Append('\n');
            builder.Append("            {").Append('\n');
            builder.Append("                throw new global::System.InvalidOperationException($\"Cannot get the value of a token type '{reader.TokenType}' as a ").Append(idInfo.Name).Append("\");").Append('\n');
            builder.Append("            }").Append('\n');
            builder.Append('\n');

            if (idInfo.HasPrefix)
            {
                // 有前缀时文本形是字符串，读回一律经 Id.TryParse（前缀策略由生成的 TryParse 承担）。
                builder.Append("            var text = global::System.Text.Json.JsonSerializer.Deserialize<string>(ref reader, options);").Append('\n');
                builder.Append('\n');
                builder.Append("            return text is not null && ").Append(idInfo.Name).Append(".TryParse(text, global::System.Globalization.CultureInfo.InvariantCulture, out var id)").Append('\n');
                builder.Append("                ? id").Append('\n');
                builder.Append("                : throw new global::System.Text.Json.JsonException($\"Cannot convert the JSON token '{reader.TokenType}' to a ").Append(idInfo.Name).Append(".\");").Append('\n');
                builder.Append("        }");

                return builder.ToString();
            }

            builder.Append("            return ").Append(idInfo.Name).Append(".Create(").Append(GetDeserializeExpression(idInfo)).Append(");").Append('\n');
            builder.Append("        }");

            return builder.ToString();
        }

        builder.Append('\n');
        builder.Append("        {").Append('\n');
        builder.Append("            if (reader.TokenType != global::System.Text.Json.JsonTokenType.StartObject)").Append('\n');
        builder.Append("            {").Append('\n');
        builder.Append("                throw new global::System.Text.Json.JsonException($\"Cannot convert the JSON token '{reader.TokenType}' to a ").Append(idInfo.Name).Append(": an object with a 'value' property was expected.\");").Append('\n');
        builder.Append("            }").Append('\n');
        builder.Append('\n');
        builder.Append("            ").Append(idInfo.Name).Append("? result = null;").Append('\n');
        builder.Append('\n');
        builder.Append("            while (reader.Read())").Append('\n');
        builder.Append("            {").Append('\n');
        builder.Append("                if (reader.TokenType == global::System.Text.Json.JsonTokenType.EndObject)").Append('\n');
        builder.Append("                {").Append('\n');
        builder.Append("                    break;").Append('\n');
        builder.Append("                }").Append('\n');
        builder.Append('\n');
        builder.Append("                if (reader.TokenType != global::System.Text.Json.JsonTokenType.PropertyName)").Append('\n');
        builder.Append("                {").Append('\n');
        builder.Append("                    continue;").Append('\n');
        builder.Append("                }").Append('\n');
        builder.Append('\n');
        builder.Append("                if (reader.ValueTextEquals(\"value\"))").Append('\n');
        builder.Append("                {").Append('\n');
        builder.Append("                    reader.Read();").Append('\n');

        if (idInfo.HasPrefix)
        {
            builder.Append("                    var text = global::System.Text.Json.JsonSerializer.Deserialize<string>(ref reader, options);").Append('\n');
            builder.Append('\n');
            builder.Append("                    result = text is not null && ").Append(idInfo.Name).Append(".TryParse(text, global::System.Globalization.CultureInfo.InvariantCulture, out var id)").Append('\n');
            builder.Append("                        ? id").Append('\n');
            builder.Append("                        : throw new global::System.Text.Json.JsonException($\"Cannot convert the JSON token '{reader.TokenType}' to a ").Append(idInfo.Name).Append(".\");").Append('\n');
        }
        else
        {
            // 与基元形态同理：null 在这里显式拦下，故下面可以读非可空基元。
            builder.Append("                    if (reader.TokenType == global::System.Text.Json.JsonTokenType.Null)").Append('\n');
            builder.Append("                    {").Append('\n');
            builder.Append("                        throw new global::System.InvalidOperationException($\"Cannot get the value of a token type '{reader.TokenType}' as a ").Append(idInfo.Name).Append("\");").Append('\n');
            builder.Append("                    }").Append('\n');
            builder.Append('\n');
            builder.Append("                    result = ").Append(idInfo.Name).Append(".Create(").Append(GetDeserializeExpression(idInfo)).Append(");").Append('\n');
        }

        builder.Append("                }").Append('\n');
        builder.Append("                else").Append('\n');
        builder.Append("                {").Append('\n');
        builder.Append("                    reader.Skip();").Append('\n');
        builder.Append("                }").Append('\n');
        builder.Append("            }").Append('\n');
        builder.Append('\n');
        builder.Append("            return result ?? throw new global::System.Text.Json.JsonException(\"Cannot convert a JSON object without a 'value' property to a ").Append(idInfo.Name).Append(".\");").Append('\n');
        builder.Append("        }");

        return builder.ToString();
    }

    /// <summary>
    /// 取 <c>Write</c> 成员形参表之后的方法体。
    /// </summary>
    /// <remarks>
    /// 基元形态写出基元值本身，对象形态写出 <c>{ "value": … }</c>。属性名固定，不跟随
    /// <c>PropertyNamingPolicy</c>：JSON 形状是对外契约。
    /// </remarks>
    private static string GetWriteTail(StronglyTypedIdInfo idInfo)
    {
        var builder = new StringBuilder();

        // 有前缀时写出规范文本形（字符串），无前缀时写出基元值本身。
        var writeValue = idInfo.HasPrefix
            ? "writer.WriteStringValue(value.ToString());"
            : "global::System.Text.Json.JsonSerializer.Serialize(writer, primitiveId, options);";

        builder.Append('\n');
        builder.Append("        {").Append('\n');
        builder.Append("            if (value is { Value: var primitiveId })").Append('\n');
        builder.Append("            {").Append('\n');

        if (idInfo.IsObjectJsonShape)
        {
            builder.Append("                writer.WriteStartObject();").Append('\n');
            builder.Append("                writer.WritePropertyName(\"value\");").Append('\n');
            builder.Append("                ").Append(writeValue).Append('\n');
            builder.Append("                writer.WriteEndObject();").Append('\n');
        }
        else
        {
            builder.Append("                ").Append(writeValue).Append('\n');
        }

        builder.Append("            }").Append('\n');
        builder.Append("            else").Append('\n');
        builder.Append("            {").Append('\n');
        builder.Append("                writer.WriteNullValue();").Append('\n');
        builder.Append("            }").Append('\n');
        builder.Append("        }");

        return builder.ToString();
    }

    /// <summary>
    /// 生成 <c>ReadAsPropertyName</c> 成员，整体拼接在最后一个成员的同一行行尾。
    /// </summary>
    /// <remarks>
    /// 属性名一定是 JSON 字符串，必须自行转换：<c>Read</c> 依赖 <c>TokenType</c> 为 String / Number，
    /// 遇到 PropertyName 会在内层转换器里失败。字典键在任何 JSON 形态下都携带基元文本。
    /// </remarks>
    private static string GetReadAsPropertyName(StronglyTypedIdInfo idInfo, string version)
    {
        var builder = new StringBuilder();

        builder.Append('\n').Append('\n');
        builder.Append("        /// <inheritdoc/>").Append('\n');
        builder.Append("        ").Append(GeneratedCode.CompilerGeneratedAttribute).Append('\n');
        builder.Append("        ").Append(GeneratedCode.GeneratedCodeAttribute(nameof(SystemTextJsonCodeGenerator), version)).Append('\n');
        builder.Append($"        public override {idInfo.Name} ReadAsPropertyName(").Append('\n');
        builder.Append("            ref global::System.Text.Json.Utf8JsonReader reader,").Append('\n');
        builder.Append("            global::System.Type typeToConvert,").Append('\n');
        builder.Append("            global::System.Text.Json.JsonSerializerOptions options)").Append('\n');
        builder.Append("        {").Append('\n');

        if (idInfo.IsStringPrimitive && !idInfo.HasPrefix)
        {
            builder.Append($"            return {idInfo.Name}.Create(reader.GetString()!);").Append('\n');
        }
        else
        {
            // 有前缀时键文本也带前缀，故一律经 Id.TryParse（前缀策略由生成的 TryParse 承担）。
            builder.Append("            var propertyName = reader.GetString();").Append('\n').Append('\n');
            builder.Append($"            if ({idInfo.Name}.TryParse(propertyName, global::System.Globalization.CultureInfo.InvariantCulture, out var id))").Append('\n');
            builder.Append("            {").Append('\n');
            builder.Append("                return id;").Append('\n');
            builder.Append("            }").Append('\n').Append('\n');
            builder.Append($"            throw new global::System.Text.Json.JsonException($\"Cannot convert the JSON property name '{{propertyName}}' to a {idInfo.Name}.\");").Append('\n');
        }

        builder.Append("        }");

        return builder.ToString();
    }
}
