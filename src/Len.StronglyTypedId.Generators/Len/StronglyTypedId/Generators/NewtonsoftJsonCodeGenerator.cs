using System.Text;

namespace Len.StronglyTypedId.Generators;

/// <summary>
/// 为强类型 Id 生成 Newtonsoft.Json 转换器。
/// </summary>
internal class NewtonsoftJsonCodeGenerator : ICoreCodeGenerator
{
    internal static readonly ICoreCodeGenerator Instance = new NewtonsoftJsonCodeGenerator();

    public int Order { get; } = 2;

    public void Generate(
        StronglyTypedIdInfo idInfo,
        ImmutableArray<ModuleInfo> modules,
        SourceProductionContext context,
        string version)
    {
        // Integrations 可以把这个 Id 的 Newtonsoft.Json 集成关掉；程序集门控已在调用方做过。
        if (!idInfo.Supports(IntegrationFlags.NewtonsoftJson))
        {
            return;
        }

        var header = GeneratedCode.Header(idInfo.Namespace);

        // 与 System.Text.Json 侧同理：泛型容器里的 Id 无法在特性实参里引用含类型参数的转换器（CS0416）。
        var converterAttribute = idInfo.IsInGenericContainer
            ? string.Empty
            : "[global::Newtonsoft.Json.JsonConverter(typeof(NewtonsoftJsonConverter))]\n";

        var declaration = $$"""
                {{converterAttribute}}partial record{{idInfo.TypeKindSuffix}} {{idInfo.Name}}
                {
                    /// <inheritdoc/>
                    {{GeneratedCode.CompilerGeneratedAttribute}}
                    {{GeneratedCode.GeneratedCodeAttribute(nameof(NewtonsoftJsonCodeGenerator), version)}}
                    public class NewtonsoftJsonConverter : global::Newtonsoft.Json.JsonConverter<{{idInfo.Name}}?>
                    {
                        /// <inheritdoc/>
                        {{GeneratedCode.CompilerGeneratedAttribute}}
                        {{GeneratedCode.GeneratedCodeAttribute(nameof(NewtonsoftJsonCodeGenerator), version)}}
                        public override {{idInfo.Name}}? ReadJson(
                            global::Newtonsoft.Json.JsonReader reader,
                            global::System.Type objectType,
                            {{idInfo.Name}}? existingValue,
                            bool hasExistingValue,
                            global::Newtonsoft.Json.JsonSerializer serializer){{GetReadTail(idInfo)}}
                
                        /// <inheritdoc/>
                        {{GeneratedCode.CompilerGeneratedAttribute}}
                        {{GeneratedCode.GeneratedCodeAttribute(nameof(NewtonsoftJsonCodeGenerator), version)}}
                        public override void WriteJson(
                            global::Newtonsoft.Json.JsonWriter writer,
                            {{idInfo.Name}}? value,
                            global::Newtonsoft.Json.JsonSerializer serializer){{GetWriteTail(idInfo)}}
                    }
                }
                """;

        var code = header + GeneratedCode.NestInContainingTypes(idInfo, declaration);

        // 嵌套包装按 \n 逐行拼接，与字面量相接会得到混排行尾，故统一行尾。
        context.AddSource($"{idInfo.HintName}.NewtonsoftJson.g.cs", GeneratedCode.NormalizeLineEndings(code));
    }

    /// <summary>
    /// 取 <c>ReadJson</c> 成员形参表之后的尾部：基元形态是表达式体，对象形态是方法体。
    /// </summary>
    private static string GetReadTail(StronglyTypedIdInfo idInfo)
    {
        var builder = new StringBuilder();

        if (!idInfo.IsObjectJsonShape)
        {
            builder.Append(" =>").Append('\n');

            if (idInfo.HasPrefix || idInfo.IsNewtonsoftTextPrimitive)
            {
                // 有前缀时文本形是字符串，读回一律经 Id.TryParse（前缀策略由生成的 TryParse 承担）。
                // Newtonsoft 不认识 Int128 / UInt128 / Half / DateOnly / TimeOnly，也只能按文本读。
                builder.Append("            serializer.Deserialize<string>(reader) switch").Append('\n');
                builder.Append("            {").Append('\n');
                builder.Append("                { } text when ").Append(idInfo.Name).Append(".TryParse(text, global::System.Globalization.CultureInfo.InvariantCulture, out var id) => id,").Append('\n');
                builder.Append("                null when (objectType.IsClass || global::System.Nullable.GetUnderlyingType(objectType) is not null) => null,").Append('\n');
                builder.Append("                _ => throw new global::Newtonsoft.Json.JsonSerializationException($\"Cannot convert the JSON token '{reader.TokenType}' to a ").Append(idInfo.Name).Append(".\")").Append('\n');
                builder.Append("            };");

                return builder.ToString();
            }

            builder.Append("            serializer.Deserialize<").Append(idInfo.PrimitiveIdTypeName).Append("?>(reader) switch").Append('\n');
            builder.Append("            {").Append('\n');
            // 必须走 TryCreate：Create 在验证器拒绝时抛 ArgumentException（或 ExceptionType 指定的类型），
            // 它会穿透 JsonConvert.DeserializeObject —— 而 Newtonsoft 的转换器契约要求失败一律抛 JsonSerializationException。
            builder.Append("                { } value when ").Append(idInfo.Name).Append(".TryCreate(value, out var id) => id,").Append('\n');
            builder.Append("                null when (objectType.IsClass || global::System.Nullable.GetUnderlyingType(objectType) is not null) => null,").Append('\n');
            builder.Append("                _ => throw new global::Newtonsoft.Json.JsonSerializationException($\"Cannot convert the JSON token '{reader.TokenType}' to a ").Append(idInfo.Name).Append(".\")").Append('\n');
            builder.Append("            };");

            return builder.ToString();
        }

        builder.Append('\n');
        builder.Append("        {").Append('\n');
        builder.Append("            if (reader.TokenType == global::Newtonsoft.Json.JsonToken.Null)").Append('\n');
        builder.Append("            {").Append('\n');
        builder.Append("                return null;").Append('\n');
        builder.Append("            }").Append('\n');
        builder.Append('\n');
        builder.Append("            if (reader.TokenType != global::Newtonsoft.Json.JsonToken.StartObject)").Append('\n');
        builder.Append("            {").Append('\n');
        builder.Append("                throw new global::Newtonsoft.Json.JsonSerializationException($\"Cannot convert the JSON token '{reader.TokenType}' to a ").Append(idInfo.Name).Append(": an object with a 'value' property was expected.\");").Append('\n');
        builder.Append("            }").Append('\n');
        builder.Append('\n');
        builder.Append("            var json = global::Newtonsoft.Json.Linq.JObject.Load(reader);").Append('\n');
        builder.Append('\n');
        // 属性名按大小写不敏感匹配：Newtonsoft 对普通 POCO 就是这么做的（`{"Value":…}` 与 `{"value":…}`
        // 都能填进 Value 属性），而 JObject.TryGetValue 是序数比较、只认小写。生成的转换器若比它所在的
        // 序列化器更严，同一个载荷会在 Id 这一层突然失败。精确匹配优先，与 POCO 的解析顺序一致。
        builder.Append("            var valueToken = json.GetValue(\"value\", global::System.StringComparison.OrdinalIgnoreCase);").Append('\n');
        builder.Append('\n');
        builder.Append("            if (valueToken is null)").Append('\n');
        builder.Append("            {").Append('\n');
        builder.Append("                throw new global::Newtonsoft.Json.JsonSerializationException(\"Cannot convert a JSON object without a 'value' property to a ").Append(idInfo.Name).Append(".\");").Append('\n');
        builder.Append("            }").Append('\n');
        builder.Append('\n');

        if (idInfo.HasPrefix || idInfo.IsNewtonsoftTextPrimitive)
        {
            // 同上：有前缀或基元本身不被 Newtonsoft 认识时，值都以规范文本形存放。
            builder.Append("            return valueToken.ToObject<string>() switch").Append('\n');
            builder.Append("            {").Append('\n');
            builder.Append("                { } text when ").Append(idInfo.Name).Append(".TryParse(text, global::System.Globalization.CultureInfo.InvariantCulture, out var id) => id,").Append('\n');
            builder.Append("                _ => throw new global::Newtonsoft.Json.JsonSerializationException($\"Cannot convert the JSON token '{valueToken.Type}' to a ").Append(idInfo.Name).Append(".\")").Append('\n');
            builder.Append("            };").Append('\n');
            builder.Append("        }");

            return builder.ToString();
        }

        builder.Append("            return valueToken.ToObject<").Append(idInfo.PrimitiveIdTypeName).Append("?>() switch").Append('\n');
        builder.Append("            {").Append('\n');
        // 同上：TryCreate 让验证失败落在下面的 JsonSerializationException 分支，而不是穿透 ArgumentException。
        builder.Append("                { } value when ").Append(idInfo.Name).Append(".TryCreate(value, out var id) => id,").Append('\n');
        builder.Append("                _ => throw new global::Newtonsoft.Json.JsonSerializationException($\"Cannot convert the JSON token '{valueToken.Type}' to a ").Append(idInfo.Name).Append(".\")").Append('\n');
        builder.Append("            };").Append('\n');
        builder.Append("        }");

        return builder.ToString();
    }

    /// <summary>
    /// 取 <c>WriteJson</c> 成员形参表之后的方法体。
    /// </summary>
    private static string GetWriteTail(StronglyTypedIdInfo idInfo)
    {
        var builder = new StringBuilder();

        // 有前缀时写出规范文本形（字符串），无前缀时写出基元值本身。
        // Newtonsoft 不认识 Int128 / UInt128 / Half / DateOnly / TimeOnly，按值写会抛 JsonWriterException，
        // 故这几个类型同样写规范文本。
        var writeValue = idInfo.HasPrefix
            ? "writer.WriteValue(value.ToString());"
            : idInfo.IsNewtonsoftTextPrimitive
                ? "writer.WriteValue(primitiveId.ToString(null, global::System.Globalization.CultureInfo.InvariantCulture));"
                : "writer.WriteValue(primitiveId);";

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
        builder.Append("                writer.WriteNull();").Append('\n');
        builder.Append("            }").Append('\n');
        builder.Append("        }");

        return builder.ToString();
    }
}
