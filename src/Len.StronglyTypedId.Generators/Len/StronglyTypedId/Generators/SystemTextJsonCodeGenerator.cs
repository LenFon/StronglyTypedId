using System.Text;

namespace Len.StronglyTypedId.Generators;

/// <summary>
/// 为强类型 Id 生成 System.Text.Json 转换器。
/// </summary>
/// <remarks>
/// 除值的读写外，还必须重写 <c>ReadAsPropertyName</c> / <c>WriteAsPropertyName</c>。STJ 处理字典键时
/// 走的是这两条路径，而 <c>JsonConverter&lt;T&gt;</c> 的默认实现只认得内建的键类型（<c>string</c> 等），
/// 其余一律抛 <c>NotSupportedException</c>，并在异常里直接提示「重写这两个方法以支持字典键」。
/// 也就是说，重写之前 <c>Dictionary&lt;OrderId, TValue&gt;</c> —— DDD 里「按 Id 索引的子集合」这种常见
/// 结构 —— 序列化与反序列化都完全不可用。
/// </remarks>
internal class SystemTextJsonCodeGenerator : ICoreCodeGenerator
{
    internal static readonly ICoreCodeGenerator Instance = new SystemTextJsonCodeGenerator();

    /// <summary>
    /// 抑制 IL2026 / IL3050 时给出的理由。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>JsonSerializer.Serialize&lt;T&gt;</c> / <c>Deserialize&lt;T&gt;</c> 的泛型重载被标注了
    /// <c>RequiresUnreferencedCode</c> 与 <c>RequiresDynamicCode</c>，因此在 <c>PublishAot</c> /
    /// <c>PublishTrimmed</c> 的宿主工程里，生成的转换器会带出一串 IL2026 / IL3050 警告。
    /// </para>
    /// <para>
    /// 但这里的 <c>T</c> 永远是<b>生成期就已确定的受支持基元</b>（<c>Guid</c> / <c>int</c> / <c>string</c> / 枚举 …），
    /// STJ 对它们有内建转换器，解析 <c>JsonTypeInfo</c> 时不需要反射、也不需要运行时代码生成。
    /// 换言之警告是保守的静态推断，而非真实的裁剪风险，故按惯例精确抑制这两条规则。
    /// </para>
    /// <para>
    /// 之所以不改成直接调用 <c>writer.WriteStringValue</c> 之类的底层 API：那会绕过宿主在
    /// <c>JsonSerializerOptions</c> 上配置的转换器（典型如 <c>JsonStringEnumConverter</c> 让枚举按字符串读写），
    /// 属于可观察的行为变更。抑制标注则是零行为变更。
    /// </para>
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
    /// 受支持的基元类型里除 <c>string</c> 外都实现了 <c>IFormattable</c>，这里以其为准取「不变文化」的文本形式：
    /// 属性名的格式不应随当前区域性漂移，否则同一份数据在不同区域设置下会写出不同的键。
    /// 枚举例外：<c>System.Enum</c> 的 <c>ToString(string, IFormatProvider)</c> 已被标记
    /// [Obsolete]（provider 参数被忽略），用它会触发 CS0618，故枚举直接走无参 <c>ToString()</c>。
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
    /// <para>
    /// 刻意读<b>非可空</b>的基元类型。写成 <c>Deserialize&lt;int?&gt;</c> 会让 AOT 宿主的
    /// <c>JsonSerializerContext</c> 不得不额外注册 <c>Nullable&lt;T&gt;</c> —— 用户只为用一个 Id 就得
    /// 记住这条冷知识，代价与收益完全不成比例。
    /// </para>
    /// <para>
    /// 之所以敢去掉可空形态：JSON 里的 <c>null</c> 已在调用前由 <c>reader.TokenType</c> 判定拦下，
    /// 此处不可能读到 null。引用类型（<c>string</c>）补 <c>!</c> 抑制 CS8600 —— 同一判定保证了它非空。
    /// </para>
    /// </remarks>
    private static string GetDeserializeExpression(StronglyTypedIdInfo idInfo)
        => idInfo.IsStringPrimitive
            ? "global::System.Text.Json.JsonSerializer.Deserialize<string>(ref reader, options)!"
            : $"global::System.Text.Json.JsonSerializer.Deserialize<{idInfo.PrimitiveIdTypeName}>(ref reader, options)";

    /// <summary>
    /// 取 <c>Read</c> 成员形参表之后的尾部：基元形态是表达式体，对象形态是方法体。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 基元形态（默认）把 Id 与其基元值视为同一形状，产物与引入本开关之前<b>逐字节一致</b>；
    /// 对象形态读入 <c>{ "value": … }</c>，供「已对外承诺对象形态」的既有契约迁移。
    /// </para>
    /// <para>
    /// 缩进按产物中的层级逐行写死（成员 8 空格、方法体 12 空格，依次递增），不依赖原始字符串字面量的
    /// 裁剪规则；行尾由 <see cref="GeneratedCode.NormalizeLineEndings"/> 在最后统一。
    /// </para>
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
    /// 基元形态写出基元值本身；对象形态写出 <c>{ "value": … }</c>。属性名固定为 <c>value</c>，不跟随
    /// <c>JsonSerializerOptions.PropertyNamingPolicy</c> —— Id 的 JSON 形状是对外契约，不应随宿主应用的
    /// 序列化配置漂移。
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
    /// <para>
    /// 属性名一定是 JSON 字符串，因此这里必须自行完成转换：<c>Read</c> 依赖 <c>TokenType</c> 为
    /// <c>String</c> / <c>Number</c>，遇到 <c>PropertyName</c> 会在内层转换器里失败，不能转调。
    /// </para>
    /// <para>
    /// <c>string</c> 基元直接取属性名构造，与 <c>Read</c> 的语义严格一致（都不过滤空串，故空串键可往返）；
    /// 其余基元按不变文化解析，失败时抛 <c>JsonException</c> —— 属性名路径上与框架一致的异常类型。
    /// </para>
    /// <para>
    /// 字典键在任何 JSON 形态下都携带基元文本：属性名本就是字符串，对象形态只作用于值的位置。
    /// </para>
    /// <para>
    /// 缩进按产物中的层级逐行写死（嵌套转换器类的成员为 8 空格），不依赖原始字符串字面量的缩进裁剪规则；
    /// 行尾由 <see cref="GeneratedCode.NormalizeLineEndings"/> 在最后统一。
    /// </para>
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
