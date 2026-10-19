using System.Text;

namespace Len.StronglyTypedId.Generators;

/// <summary>
/// 为强类型 Id 生成 System.Text.Json 转换器。
/// </summary>
internal class SystemTextJsonCodeGenerator : ICoreCodeGenerator
{
    internal static readonly ICoreCodeGenerator Instance = new SystemTextJsonCodeGenerator();

    /// <summary>
    /// 抑制 IL2026 / IL3050 时给出的理由。
    /// </summary>
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

        // 泛型容器里的 Id 不能带 [JsonConverter]：特性实参必须是封闭类型，而
        // Box<T>.BoxedId.SystemTextJsonConverter 含类型参数，会 CS0416。转换器本身照常生成，
        // 使用者可按封闭实例手动注册（new Box<int>.BoxedId.SystemTextJsonConverter()）。
        var converterAttribute = idInfo.IsInGenericContainer
            ? string.Empty
            : "[global::System.Text.Json.Serialization.JsonConverter(typeof(SystemTextJsonConverter))]\n";

        var declaration = $$"""
                {{converterAttribute}}partial record{{idInfo.TypeKindSuffix}} {{idInfo.Name}}
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
    private static string GetPropertyNameExpression(StronglyTypedIdInfo idInfo)
        => idInfo.HasPrefix
            ? "value.ToString()"
            : idInfo.IsStringPrimitive
                ? "primitiveId"
                : idInfo.IsEnum || idInfo.IsCharPrimitive
                    // char 的 IFormattable.ToString(string, IFormatProvider) 是显式接口实现（CS1501），
                    // 且其无参 ToString() 与文化无关。
                    ? "primitiveId.ToString()"
                    : "primitiveId.ToString(null, global::System.Globalization.CultureInfo.InvariantCulture)";

    /// <summary>
    /// 取读入基元值时用的表达式。
    /// </summary>
    private static string GetDeserializeExpression(StronglyTypedIdInfo idInfo)
        => idInfo.IsStringPrimitive
            ? "global::System.Text.Json.JsonSerializer.Deserialize<string>(ref reader, options)!"
            : $"global::System.Text.Json.JsonSerializer.Deserialize<{idInfo.PrimitiveIdTypeName}>(ref reader, options)";

    /// <summary>
    /// 取 <c>Read</c> 成员形参表之后的尾部：基元形态是表达式体，对象形态是方法体。
    /// </summary>
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
