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
        "The primitive id type is a supported BCL primitive; it is read and written directly through "
        + "Utf8JsonReader / Utf8JsonWriter, or through System.Text.Json's built-in converter for the few "
        + "types that have no direct form. No reflection or dynamic code is required.";

    public int Order { get; } = 1;

    public void Generate(
        StronglyTypedIdInfo idInfo,
        ImmutableArray<ModuleInfo> modules,
        SourceProductionContext context,
        string version)
    {
        // Integrations 可以把这个 Id 的 System.Text.Json 集成关掉；程序集门控已在调用方做过。
        if (!idInfo.Supports(IntegrationFlags.SystemTextJson))
        {
            return;
        }

        var header = GeneratedCode.Header(idInfo.Namespace);

        // 泛型容器里的 Id 不能带 [JsonConverter]：特性实参必须是封闭类型，而
        // Box<T>.BoxedId.SystemTextJsonConverter 含类型参数，会 CS0416。转换器本身照常生成，
        // 使用者可按封闭实例手动注册（new Box<int>.BoxedId.SystemTextJsonConverter()）。
        var converterAttribute = idInfo.IsInGenericContainer
            ? string.Empty
            : "[global::System.Text.Json.Serialization.JsonConverter(typeof(SystemTextJsonConverter))]" + GeneratedCode.NewLine;

        var declaration = $$"""
                {{converterAttribute}}partial record{{idInfo.TypeKindSuffix}} {{idInfo.EscapedName}}
                {
                    /// <inheritdoc/>
                    {{GeneratedCode.CompilerGeneratedAttribute}}
                    {{GeneratedCode.GeneratedCodeAttribute(nameof(SystemTextJsonCodeGenerator), version)}}
                    public class SystemTextJsonConverter : global::System.Text.Json.Serialization.JsonConverter<{{idInfo.EscapedName}}>
                    {
                        /// <inheritdoc/>
                        {{GeneratedCode.CompilerGeneratedAttribute}}
                        {{GeneratedCode.GeneratedCodeAttribute(nameof(SystemTextJsonCodeGenerator), version)}}
                        [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "{{AotSuppressionJustification}}")]
                        [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "{{AotSuppressionJustification}}")]
                        public override {{idInfo.EscapedName}} Read(
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
                            {{idInfo.EscapedName}} value, 
                            global::System.Text.Json.JsonSerializerOptions options){{GetWriteTail(idInfo)}}
                
                        /// <inheritdoc/>
                        {{GeneratedCode.CompilerGeneratedAttribute}}
                        {{GeneratedCode.GeneratedCodeAttribute(nameof(SystemTextJsonCodeGenerator), version)}}
                        public override void WriteAsPropertyName(
                            global::System.Text.Json.Utf8JsonWriter writer,
                            {{idInfo.EscapedName}} value,
                            global::System.Text.Json.JsonSerializerOptions options)
                        {
                            if (value is { Value: var primitiveId })
                            {
                {{GetWriteAsPropertyNameBody(idInfo)}}
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

        GeneratedCode.AddGeneratedSource(context, $"{idInfo.HintName}.SystemTextJson.g.cs", code);
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
                    // 自定义基元未必实现 IFormattable，故由 PrimitiveTextExpression 决定用哪个重载。
                    : idInfo.PrimitiveTextExpression;

    /// <summary>
    /// 生成 <c>WriteAsPropertyName</c> 的方法体（不含外层大括号）。
    /// </summary>
    private static string GetWriteAsPropertyNameBody(StronglyTypedIdInfo idInfo)
    {
        // 原始字符串会剥掉闭合分隔符处的 16 个空格，故此处只需补足剥掉的那部分。
        const string Indent = "                ";

        // 引用类型基元可以是 null（default(TId) 与失败的 TryCreate / TryParse），而 JSON 属性名不能为 null。
        // 不拦下来会在 WritePropertyName 里抛 NRE；这里与「null Id 作键」一致地给出明确的 NotSupportedException。
        // 带前缀时同样要拦：键文本是「前缀 + 空串」，读回时剥离前缀只剩空文本，必然失败。
        var nullGuard = idInfo.IsReferenceTypePrimitive
            ? $"{Indent}if (primitiveId is null){GeneratedCode.NewLine}{Indent}{{{GeneratedCode.NewLine}{Indent}    throw new global::System.NotSupportedException($\"A null {idInfo.Name} cannot be written as a JSON property name.\");{GeneratedCode.NewLine}{Indent}}}{GeneratedCode.NewLine}{GeneratedCode.NewLine}"
            : string.Empty;

        if (!ShouldApplyDictionaryKeyPolicy(idInfo))
        {
            return $"{nullGuard}{Indent}writer.WritePropertyName({GetPropertyNameExpression(idInfo)});";
        }

        // 枚举成员名是「名字」而非「数据」，DictionaryKeyPolicy 的语义恰好是命名风格转换，
        // 故与 STJ 的原生枚举键一致地应用它（原生 Dictionary<Status,int> 在 CamelCase 下写出 {"active":1}）。
        return nullGuard + string.Join(
            GeneratedCode.NewLine,
            $"{Indent}var propertyName = {GetPropertyNameExpression(idInfo)};",
            $"{Indent}writer.WritePropertyName(options.DictionaryKeyPolicy is null ? propertyName : options.DictionaryKeyPolicy.ConvertName(propertyName));");
    }

    /// <summary>
    /// 该 Id 作字典键时是否需要应用 <c>JsonSerializerOptions.DictionaryKeyPolicy</c>。
    /// </summary>
    /// <remarks>
    /// 只有枚举跟随 STJ：string / Guid 的键值是数据本身而非名字，改写它等于改写键值，写出去就读不回来。
    /// 带前缀时键文本含用户定义的前缀，policy 会连前缀一起改写导致读回匹配失败，故一并排除。
    /// </remarks>
    private static bool ShouldApplyDictionaryKeyPolicy(StronglyTypedIdInfo idInfo)
        => idInfo.IsEnum && !idInfo.HasPrefix;

    /// <summary>
    /// 追加「读出基元值并交给 <c>TryCreate</c>」的语句，结果放在局部变量 <c>value</c> 中。
    /// </summary>
    /// <remarks>
    /// 必须走 <c>TryCreate</c>：<c>Create</c> 在验证器拒绝时抛 <see cref="ArgumentException"/>（或 <c>ExceptionType</c>
    /// 指定的类型），会穿透 <c>JsonSerializer.Deserialize</c>，而 STJ 的转换器契约要求失败一律抛 <c>JsonException</c>。
    /// </remarks>
    /// <param name="builder">产物构建器。</param>
    /// <param name="idInfo">当前 Id 的描述信息。</param>
    /// <param name="indent">每行前缀的绝对缩进。</param>
    private static void AppendReadValueStatements(StringBuilder builder, StronglyTypedIdInfo idInfo, string indent)
    {
        if (idInfo.IsBigIntegerPrimitive)
        {
            // BigInteger 没有内置转换器，按值读会落到「按属性反序列化」而得到 default，故改读规范文本。
            // 用 TryParse 而非 Parse：文本不是合法数字时 Parse 抛的 FormatException 同样会穿透转换器。
            AppendDirectRead(builder, idInfo, indent, "string?", "text", "reader.GetString()");
            builder.Append(indent).Append("if (text is null || !global::System.Numerics.BigInteger.TryParse(text, global::System.Globalization.NumberStyles.Integer, global::System.Globalization.CultureInfo.InvariantCulture, out var value))").Append(GeneratedCode.NewLine);
            builder.Append(indent).Append("{").Append(GeneratedCode.NewLine);
            builder.Append(indent).Append("    throw new global::System.Text.Json.JsonException($\"Cannot convert the JSON value to a ").Append(idInfo.Name).Append(".\");").Append(GeneratedCode.NewLine);
            builder.Append(indent).Append("}").Append(GeneratedCode.NewLine);

            return;
        }

        if (idInfo.IsCustomPrimitive)
        {
            // 自定义基元交给 JsonSerializer 会按公开属性写出、读回 default，故与 BigInteger 同样走规范文本。
            // 契约要求基元暴露公开静态的 TryParse(string, IFormatProvider, out TSelf)（见 STIA0008）。
            AppendDirectRead(builder, idInfo, indent, "string?", "text", "reader.GetString()");
            builder.Append(indent).Append("if (text is null || !").Append(idInfo.GetPrimitiveTextTryParseCondition("text", "value")).Append(')').Append(GeneratedCode.NewLine);
            builder.Append(indent).Append("{").Append(GeneratedCode.NewLine);
            builder.Append(indent).Append("    throw new global::System.Text.Json.JsonException($\"Cannot convert the JSON value to a ").Append(idInfo.Name).Append(".\");").Append(GeneratedCode.NewLine);
            builder.Append(indent).Append("}").Append(GeneratedCode.NewLine);

            return;
        }

        if (PrimitiveJsonIo.TryGetReadExpression(idInfo) is { } read)
        {
            // 直读：token 类型不符时 Utf8JsonReader 抛 InvalidOperationException，文本格式不符时抛
            // FormatException（如 GetGuid）。两者都不是 JsonException，会穿透转换器、让按契约
            // catch (JsonException) 的调用方漏掉这条路径，故在此统一转成 JsonException。
            AppendDirectRead(builder, idInfo, indent, idInfo.PrimitiveIdTypeName, "value", read);

            return;
        }

        // 委托路径：这些基元没有可直写的形式（Int128 / UInt128 / Half / TimeSpan / DateOnly / TimeOnly / char），
        // 由 JsonSerializer 处理，因而要求 options 里有该基元的 JsonTypeInfo。源生成的上下文未登记时会抛
        // NotSupportedException，此处转成带指引的 JsonException。
        builder.Append(indent).Append(idInfo.PrimitiveIdTypeName).Append(" value;").Append(GeneratedCode.NewLine);
        builder.Append(indent).Append("try").Append(GeneratedCode.NewLine);
        builder.Append(indent).Append("{").Append(GeneratedCode.NewLine);
        builder.Append(indent).Append("    value = global::System.Text.Json.JsonSerializer.Deserialize<").Append(idInfo.PrimitiveIdTypeName).Append(">(ref reader, options);").Append(GeneratedCode.NewLine);
        builder.Append(indent).Append("}").Append(GeneratedCode.NewLine);
        builder.Append(indent).Append("catch (global::System.NotSupportedException e)").Append(GeneratedCode.NewLine);
        builder.Append(indent).Append("{").Append(GeneratedCode.NewLine);
        builder.Append(indent).Append("    throw new global::System.Text.Json.JsonException($\"Cannot convert the JSON token '{reader.TokenType}' to a ").Append(idInfo.Name).Append(": the JSON source generation context does not provide metadata for '").Append(idInfo.PrimitiveIdTypeName).Append("'. Add [JsonSerializable(typeof(").Append(idInfo.PrimitiveIdTypeName).Append("))] to that context.\", e);").Append(GeneratedCode.NewLine);
        builder.Append(indent).Append("}").Append(GeneratedCode.NewLine);
    }

    /// <summary>
    /// 追加一段「直读基元值」的语句：先声明变量，再在 try 里赋值，把读取期的失败统一转成
    /// <c>JsonException</c>。
    /// </summary>
    /// <param name="builder">产物构建器。</param>
    /// <param name="idInfo">当前 Id 的描述信息。</param>
    /// <param name="indent">每行前缀的绝对缩进。</param>
    /// <param name="typeName">变量的类型（含可空标注）。</param>
    /// <param name="variableName">变量名。</param>
    /// <param name="expression">读取表达式。</param>
    private static void AppendDirectRead(StringBuilder builder, StronglyTypedIdInfo idInfo, string indent, string typeName, string variableName, string expression)
    {
        builder.Append(indent).Append(typeName).Append(' ').Append(variableName).Append(';').Append(GeneratedCode.NewLine);
        builder.Append(indent).Append("try").Append(GeneratedCode.NewLine);
        builder.Append(indent).Append("{").Append(GeneratedCode.NewLine);
        builder.Append(indent).Append("    ").Append(variableName).Append(" = ").Append(expression).Append(';').Append(GeneratedCode.NewLine);
        builder.Append(indent).Append("}").Append(GeneratedCode.NewLine);
        builder.Append(indent).Append("catch (global::System.Exception e) when (e is global::System.FormatException or global::System.InvalidOperationException)").Append(GeneratedCode.NewLine);
        builder.Append(indent).Append("{").Append(GeneratedCode.NewLine);
        builder.Append(indent).Append("    throw new global::System.Text.Json.JsonException($\"Cannot convert the JSON token '{reader.TokenType}' to a ").Append(idInfo.Name).Append(".\", e);").Append(GeneratedCode.NewLine);
        builder.Append(indent).Append("}").Append(GeneratedCode.NewLine);
    }

    /// <summary>
    /// 追加「把 <c>value</c> 转成 Id」的语句，验证失败抛 <c>JsonException</c>。
    /// </summary>
    /// <remarks>
    /// 枚举基元在此追加已定义性校验：<c>TryCreate</c> 只跑用户配的 Validator，而 STJ 对未定义数值（<c>999</c>）
    /// 照常读出，不补这一道就会反序列化出不对应任何成员的 Id。
    /// </remarks>
    /// <param name="builder">产物构建器。</param>
    /// <param name="idInfo">当前 Id 的描述信息。</param>
    /// <param name="indent">每行前缀的绝对缩进。</param>
    /// <param name="assignmentPrefix">赋值目标前缀：<c>return </c> 或 <c>result = </c>。</param>
    private static void AppendCreateFromValue(StringBuilder builder, StronglyTypedIdInfo idInfo, string indent, string assignmentPrefix)
    {
        // 用全限定名而非短名：转换器里 `value` / `id` / `options` 等形参与局部变量会遮蔽同名 Id 类型，
        // 短名引用会被解析成那个变量（CS1061 / CS0841）。全限定名不受作用域影响。
        builder.Append(indent).Append(assignmentPrefix).Append(idInfo.GetTryCreateCondition(idInfo.FullyQualifiedName)).Append(GeneratedCode.NewLine);
        builder.Append(indent).Append("    ? id").Append(GeneratedCode.NewLine);
        builder.Append(indent).Append("    : throw new global::System.Text.Json.JsonException($\"Cannot convert the JSON value to a ").Append(idInfo.Name).Append(".\");").Append(GeneratedCode.NewLine);
    }

    /// <summary>
    /// 取 <c>Read</c> 成员形参表之后的尾部：基元形态是表达式体，对象形态是方法体。
    /// </summary>
    private static string GetReadTail(StronglyTypedIdInfo idInfo)
    {
        var builder = new StringBuilder();

        if (!idInfo.IsObjectJsonShape)
        {
            builder.Append(GeneratedCode.NewLine);
            builder.Append("        {").Append(GeneratedCode.NewLine);
            builder.Append("            if (reader.TokenType == global::System.Text.Json.JsonTokenType.Null)").Append(GeneratedCode.NewLine);
            builder.Append("            {").Append(GeneratedCode.NewLine);

            if (idInfo.IsReferenceTypePrimitive)
            {
                // 写侧对 null 基元一律写 JSON null（含带前缀的情形），读侧必须接受它，
                // 否则「写出去就读不回来」。值类型基元没有 null 可写，仍按契约拒绝。
                builder.Append("                return default!;").Append(GeneratedCode.NewLine);
            }
            else
            {
                // 契约：转换器失败一律抛 JsonException（STJ 只会给它补 Path / LineNumber），
                // 抛 InvalidOperationException 会让按契约 catch (JsonException) 的调用方漏掉这条路径。
                builder.Append("                throw new global::System.Text.Json.JsonException($\"Cannot convert the JSON token '{reader.TokenType}' to a ").Append(idInfo.Name).Append(".\");").Append(GeneratedCode.NewLine);
            }

            builder.Append("            }").Append(GeneratedCode.NewLine);
            builder.Append(GeneratedCode.NewLine);

            if (idInfo.HasPrefix)
            {
                // 有前缀时文本形是字符串，读回一律经 Id.TryParse（前缀策略由生成的 TryParse 承担）。
                AppendDirectRead(builder, idInfo, "            ", "string?", "text", "reader.GetString()");
                builder.Append(GeneratedCode.NewLine);
                builder.Append("            return text is not null && ").Append(idInfo.EscapedName).Append(".TryParse(text, global::System.Globalization.CultureInfo.InvariantCulture, out var id)").Append(GeneratedCode.NewLine);
                builder.Append("                ? id").Append(GeneratedCode.NewLine);
                builder.Append("                : throw new global::System.Text.Json.JsonException($\"Cannot convert the JSON token '{reader.TokenType}' to a ").Append(idInfo.Name).Append(".\");").Append(GeneratedCode.NewLine);
                builder.Append("        }");

                return builder.ToString();
            }

            AppendReadValueStatements(builder, idInfo, "            ");
            builder.Append(GeneratedCode.NewLine);
            AppendCreateFromValue(builder, idInfo, "            ", "return ");
            builder.Append("        }");

            return builder.ToString();
        }

        builder.Append(GeneratedCode.NewLine);
        builder.Append("        {").Append(GeneratedCode.NewLine);
        builder.Append("            if (reader.TokenType != global::System.Text.Json.JsonTokenType.StartObject)").Append(GeneratedCode.NewLine);
        builder.Append("            {").Append(GeneratedCode.NewLine);
        builder.Append("                throw new global::System.Text.Json.JsonException($\"Cannot convert the JSON token '{reader.TokenType}' to a ").Append(idInfo.Name).Append(": an object with a 'value' property was expected.\");").Append(GeneratedCode.NewLine);
        builder.Append("            }").Append(GeneratedCode.NewLine);
        builder.Append(GeneratedCode.NewLine);
        builder.Append("            ").Append(idInfo.EscapedName).Append("? result = null;").Append(GeneratedCode.NewLine);

        // 引用类型基元的 null 值不能就地 return：转换器必须把整个对象读到 EndObject，
        // 否则 {"value":null,"extra":1} 这类「value 后面还有属性」的载荷会让 STJ 报
        // 「read too much or not enough」。故先记下「见过 null」，循环读完后统一返回 default。
        if (idInfo.IsReferenceTypePrimitive)
        {
            builder.Append("            var hasNullPrimitive = false;").Append(GeneratedCode.NewLine);
        }

        builder.Append(GeneratedCode.NewLine);
        builder.Append("            while (reader.Read())").Append(GeneratedCode.NewLine);
        builder.Append("            {").Append(GeneratedCode.NewLine);
        builder.Append("                if (reader.TokenType == global::System.Text.Json.JsonTokenType.EndObject)").Append(GeneratedCode.NewLine);
        builder.Append("                {").Append(GeneratedCode.NewLine);
        builder.Append("                    break;").Append(GeneratedCode.NewLine);
        builder.Append("                }").Append(GeneratedCode.NewLine);
        builder.Append(GeneratedCode.NewLine);
        builder.Append("                if (reader.TokenType != global::System.Text.Json.JsonTokenType.PropertyName)").Append(GeneratedCode.NewLine);
        builder.Append("                {").Append(GeneratedCode.NewLine);
        builder.Append("                    continue;").Append(GeneratedCode.NewLine);
        builder.Append("                }").Append(GeneratedCode.NewLine);
        builder.Append(GeneratedCode.NewLine);
        // ValueTextEquals 是逐字节比较、只认小写 value，故需跟随 options.PropertyNameCaseInsensitive，
        // 否则开了大小写不敏感后 {"Value":"..."} 会走进 Skip 分支并报「缺少 value 属性」。
        builder.Append("                if (reader.ValueTextEquals(\"value\")").Append(GeneratedCode.NewLine);
        builder.Append("                    || (options.PropertyNameCaseInsensitive && global::System.String.Equals(reader.GetString(), \"value\", global::System.StringComparison.OrdinalIgnoreCase)))").Append(GeneratedCode.NewLine);
        builder.Append("                {").Append(GeneratedCode.NewLine);
        builder.Append("                    reader.Read();").Append(GeneratedCode.NewLine);

        if (idInfo.HasPrefix)
        {
            // 写侧对 null 基元写 JSON null（含带前缀的情形），读侧必须接受它。
            // 只记标记并 continue：循环顶部的 reader.Read() 会消费掉 null 令牌，
            // 这里再读一次会跳过紧随其后的属性名。
            if (idInfo.IsReferenceTypePrimitive)
            {
                builder.Append("                    if (reader.TokenType == global::System.Text.Json.JsonTokenType.Null)").Append(GeneratedCode.NewLine);
                builder.Append("                    {").Append(GeneratedCode.NewLine);
                builder.Append("                        hasNullPrimitive = true;").Append(GeneratedCode.NewLine);
                builder.Append("                        continue;").Append(GeneratedCode.NewLine);
                builder.Append("                    }").Append(GeneratedCode.NewLine);
                builder.Append(GeneratedCode.NewLine);
            }

            builder.Append("                    var text = global::System.Text.Json.JsonSerializer.Deserialize<string>(ref reader, options);").Append(GeneratedCode.NewLine);
            builder.Append(GeneratedCode.NewLine);
            builder.Append("                    result = text is not null && ").Append(idInfo.EscapedName).Append(".TryParse(text, global::System.Globalization.CultureInfo.InvariantCulture, out var id)").Append(GeneratedCode.NewLine);
            builder.Append("                        ? id").Append(GeneratedCode.NewLine);
            builder.Append("                        : throw new global::System.Text.Json.JsonException($\"Cannot convert the JSON token '{reader.TokenType}' to a ").Append(idInfo.Name).Append(".\");").Append(GeneratedCode.NewLine);
        }
        else
        {
            // 引用类型基元可以是 null，而写侧会把它写成 {"value":null}。读侧必须接受这个形态，
            // 否则「写出去就读不回来」。直接返回 default（基元为 null 的 Id），而不是走 TryCreate：
            // 写侧写出的正是 default 值，走 TryCreate 会让「验证器拒绝 null」的 Id 再次写读不对称。
            // 值类型基元没有 null 可写，仍按契约拒绝 null 令牌。
            // 不能就地 return：对象可能还有后续属性，转换器必须读到 EndObject，否则 STJ 报
            // 「read too much or not enough」。故只记标记并 continue，由循环顶部的 reader.Read()
            // 消费掉 null 令牌（这里再读一次会跳过紧随其后的属性名）。
            if (idInfo.IsReferenceTypePrimitive)
            {
                builder.Append("                    if (reader.TokenType == global::System.Text.Json.JsonTokenType.Null)").Append(GeneratedCode.NewLine);
                builder.Append("                    {").Append(GeneratedCode.NewLine);
                builder.Append("                        hasNullPrimitive = true;").Append(GeneratedCode.NewLine);
                builder.Append("                        continue;").Append(GeneratedCode.NewLine);
                builder.Append("                    }").Append(GeneratedCode.NewLine);
                builder.Append(GeneratedCode.NewLine);
            }
            else
            {
                // 与基元形态同理：null 在这里显式拦下，故下面可以读非可空基元。
                builder.Append("                    if (reader.TokenType == global::System.Text.Json.JsonTokenType.Null)").Append(GeneratedCode.NewLine);
                builder.Append("                    {").Append(GeneratedCode.NewLine);
                builder.Append("                        throw new global::System.Text.Json.JsonException($\"Cannot convert the JSON token '{reader.TokenType}' to a ").Append(idInfo.Name).Append(".\");").Append(GeneratedCode.NewLine);
                builder.Append("                    }").Append(GeneratedCode.NewLine);
                builder.Append(GeneratedCode.NewLine);
            }

            AppendReadValueStatements(builder, idInfo, "                    ");
            builder.Append(GeneratedCode.NewLine);
            AppendCreateFromValue(builder, idInfo, "                    ", "result = ");
        }

        builder.Append("                }").Append(GeneratedCode.NewLine);
        builder.Append("                else").Append(GeneratedCode.NewLine);
        builder.Append("                {").Append(GeneratedCode.NewLine);
        builder.Append("                    reader.Skip();").Append(GeneratedCode.NewLine);
        builder.Append("                }").Append(GeneratedCode.NewLine);
        builder.Append("            }").Append(GeneratedCode.NewLine);
        builder.Append(GeneratedCode.NewLine);

        if (idInfo.IsReferenceTypePrimitive)
        {
            // 见过 {"value":null} 就还原成 default（基元为 null 的 Id），与写侧对称。
            // 只在 result 仍为空时才走这条：同一对象里既有 null 又有真实值时（重复键），保留真实值。
            builder.Append("            if (result is null && hasNullPrimitive)").Append(GeneratedCode.NewLine);
            builder.Append("            {").Append(GeneratedCode.NewLine);
            builder.Append("                return default!;").Append(GeneratedCode.NewLine);
            builder.Append("            }").Append(GeneratedCode.NewLine);
            builder.Append(GeneratedCode.NewLine);
        }

        builder.Append("            return result ?? throw new global::System.Text.Json.JsonException(\"Cannot convert a JSON object without a 'value' property to a ").Append(idInfo.Name).Append(".\");").Append(GeneratedCode.NewLine);
        builder.Append("        }");

        return builder.ToString();
    }

    /// <summary>
    /// 取 <c>Write</c> 成员形参表之后的方法体。
    /// </summary>
    private static string GetWriteTail(StronglyTypedIdInfo idInfo)
    {
        var builder = new StringBuilder();

        // 有前缀时写规范文本形，无前缀时写基元值本身；BigInteger 无内置转换器，按值写会退化成对象，故也写文本。
        // 其余基元优先直写：WriteStringValue / WriteNumberValue 不需要 options 里有 JsonTypeInfo，
        // 源生成的 JsonSerializerContext 因此不必为每个基元补 [JsonSerializable] 登记，输出与 STJ 默认逐字节相同。
        // 直写不了的少数类型（Int128 / UInt128 / Half / TimeSpan / DateOnly / TimeOnly / char）仍委托给 JsonSerializer。
        // 引用类型基元为 null 时一律写 JSON null，带前缀也一样：ToString() 会给出裸前缀，而剥离前缀后是空文本，
        // 读回必然失败。写 null 才能与「非前缀路径写 null」以及读侧的 null 分支对上。
        // 非 null 时仍用 value.ToString()：它才带前缀。
        var writeValue = idInfo.HasPrefix
            ? idInfo.IsReferenceTypePrimitive
                ? "writer.WriteStringValue(primitiveId is null ? null : value.ToString());"
                : "writer.WriteStringValue(value.ToString());"
            : idInfo.RequiresTextSerialization
                // 引用类型基元为 null 时写 JSON null，而不是在 ToString() 上抛 NRE。
                ? $"writer.WriteStringValue({idInfo.GetPrimitiveTextExpressionOrNull("primitiveId")});"
                : PrimitiveJsonIo.TryGetWriteExpression(idInfo);

        // 委托路径与读侧对称：源生成的上下文没登记该基元时，JsonSerializer 抛的是框架内部的
        // NotSupportedException，调用方无从得知要补哪一条 [JsonSerializable]。
        var delegatesToSerializer = writeValue is null;

        builder.Append(GeneratedCode.NewLine);
        builder.Append("        {").Append(GeneratedCode.NewLine);
        builder.Append("            if (value is { Value: var primitiveId })").Append(GeneratedCode.NewLine);
        builder.Append("            {").Append(GeneratedCode.NewLine);

        if (idInfo.IsObjectJsonShape)
        {
            builder.Append("                writer.WriteStartObject();").Append(GeneratedCode.NewLine);
            builder.Append("                writer.WritePropertyName(\"value\");").Append(GeneratedCode.NewLine);
        }

        if (delegatesToSerializer)
        {
            AppendDelegatedWrite(builder, idInfo);
        }
        else
        {
            builder.Append("                ").Append(writeValue).Append(GeneratedCode.NewLine);
        }

        if (idInfo.IsObjectJsonShape)
        {
            builder.Append("                writer.WriteEndObject();").Append(GeneratedCode.NewLine);
        }

        builder.Append("            }").Append(GeneratedCode.NewLine);
        builder.Append("            else").Append(GeneratedCode.NewLine);
        builder.Append("            {").Append(GeneratedCode.NewLine);
        builder.Append("                writer.WriteNullValue();").Append(GeneratedCode.NewLine);
        builder.Append("            }").Append(GeneratedCode.NewLine);
        builder.Append("        }");

        return builder.ToString();
    }

    /// <summary>
    /// 追加「把基元委托给 <c>JsonSerializer</c> 写出」的语句，并把「上下文没登记该基元」转成带指引的
    /// <c>JsonException</c>。
    /// </summary>
    /// <remarks>
    /// 与读侧的委托路径对称：源生成的上下文只服务 <c>[JsonSerializable]</c> 登记过的类型，漏登记时
    /// <c>JsonSerializer</c> 抛的是框架内部的 <c>NotSupportedException</c>，消息里不会出现该补哪一条。
    /// </remarks>
    private static void AppendDelegatedWrite(StringBuilder builder, StronglyTypedIdInfo idInfo)
    {
        const string Indent = "                ";

        builder.Append(Indent).Append("try").Append(GeneratedCode.NewLine);
        builder.Append(Indent).Append("{").Append(GeneratedCode.NewLine);
        builder.Append(Indent).Append("    global::System.Text.Json.JsonSerializer.Serialize(writer, primitiveId, options);").Append(GeneratedCode.NewLine);
        builder.Append(Indent).Append("}").Append(GeneratedCode.NewLine);
        builder.Append(Indent).Append("catch (global::System.NotSupportedException e)").Append(GeneratedCode.NewLine);
        builder.Append(Indent).Append("{").Append(GeneratedCode.NewLine);
        builder.Append(Indent).Append("    throw new global::System.Text.Json.JsonException($\"Cannot write ").Append(idInfo.Name).Append(" to JSON: the JSON source generation context does not provide metadata for '").Append(idInfo.PrimitiveIdTypeName).Append("'. Add [JsonSerializable(typeof(").Append(idInfo.PrimitiveIdTypeName).Append("))] to that context.\", e);").Append(GeneratedCode.NewLine);
        builder.Append(Indent).Append("}").Append(GeneratedCode.NewLine);
    }

    /// <summary>
    /// 生成 <c>ReadAsPropertyName</c> 成员，整体拼接在最后一个成员的同一行行尾。
    /// </summary>
    private static string GetReadAsPropertyName(StronglyTypedIdInfo idInfo, string version)
    {
        var builder = new StringBuilder();

        builder.Append(GeneratedCode.NewLine).Append(GeneratedCode.NewLine);
        builder.Append("        /// <inheritdoc/>").Append(GeneratedCode.NewLine);
        builder.Append("        ").Append(GeneratedCode.CompilerGeneratedAttribute).Append(GeneratedCode.NewLine);
        builder.Append("        ").Append(GeneratedCode.GeneratedCodeAttribute(nameof(SystemTextJsonCodeGenerator), version)).Append(GeneratedCode.NewLine);
        builder.Append($"        public override {idInfo.EscapedName} ReadAsPropertyName(").Append(GeneratedCode.NewLine);
        builder.Append("            ref global::System.Text.Json.Utf8JsonReader reader,").Append(GeneratedCode.NewLine);
        builder.Append("            global::System.Type typeToConvert,").Append(GeneratedCode.NewLine);
        builder.Append("            global::System.Text.Json.JsonSerializerOptions options)").Append(GeneratedCode.NewLine);
        builder.Append("        {").Append(GeneratedCode.NewLine);

        if (ShouldApplyDictionaryKeyPolicy(idInfo))
        {
            // 键文本可能已被 DictionaryKeyPolicy 改写，故忽略大小写解析，与 STJ 的原生枚举键一致。
            // 不能复用 Id.TryParse：它对枚举走大小写敏感的 Enum.TryParse，那是公共 API 的既定语义。
            builder.Append("            var propertyName = reader.GetString();").Append(GeneratedCode.NewLine).Append(GeneratedCode.NewLine);
            builder.Append($"            if (global::System.Enum.TryParse<{idInfo.PrimitiveIdTypeName}>(propertyName, ignoreCase: true, out var val)").Append(GeneratedCode.NewLine);

            // 已定义性校验与 Id.TryParse、Dapper 等入口共用同一份判据：普通枚举查 IsDefined，
            // [Flags] 查「不许出现未定义的位」。
            if (idInfo.GetEnumValidityCondition("val") is { } validity)
            {
                builder.Append($"                && {validity}").Append(GeneratedCode.NewLine);
            }

            builder.Append($"                && {idInfo.FullyQualifiedName}.TryCreate(val, out var id))").Append(GeneratedCode.NewLine);
            builder.Append("            {").Append(GeneratedCode.NewLine);
            builder.Append("                return id;").Append(GeneratedCode.NewLine);
            builder.Append("            }").Append(GeneratedCode.NewLine).Append(GeneratedCode.NewLine);
            builder.Append($"            throw new global::System.Text.Json.JsonException($\"Cannot convert the JSON property name '{{propertyName}}' to a {idInfo.Name}.\");").Append(GeneratedCode.NewLine);
        }
        else if (idInfo.IsStringPrimitive && !idInfo.HasPrefix)
        {
            // 必须走 TryCreate：Create 在验证器拒绝时抛 ArgumentException（或 ExceptionType 指定的类型），
            // 它会穿透 JsonSerializer.Deserialize，而 STJ 的转换器契约要求失败一律抛 JsonException。
            builder.Append("            var propertyName = reader.GetString();").Append(GeneratedCode.NewLine).Append(GeneratedCode.NewLine);
            builder.Append($"            return propertyName is not null && {idInfo.FullyQualifiedName}.TryCreate(propertyName, out var id)").Append(GeneratedCode.NewLine);
            builder.Append("                ? id").Append(GeneratedCode.NewLine);
            builder.Append($"                : throw new global::System.Text.Json.JsonException($\"Cannot convert the JSON property name '{{propertyName}}' to a {idInfo.Name}.\");").Append(GeneratedCode.NewLine);
        }
        else
        {
            // 有前缀时键文本也带前缀，故一律经 Id.TryParse（前缀策略由生成的 TryParse 承担）。
            builder.Append("            var propertyName = reader.GetString();").Append(GeneratedCode.NewLine).Append(GeneratedCode.NewLine);
            builder.Append($"            if ({idInfo.FullyQualifiedName}.TryParse(propertyName, global::System.Globalization.CultureInfo.InvariantCulture, out var id))").Append(GeneratedCode.NewLine);
            builder.Append("            {").Append(GeneratedCode.NewLine);
            builder.Append("                return id;").Append(GeneratedCode.NewLine);
            builder.Append("            }").Append(GeneratedCode.NewLine).Append(GeneratedCode.NewLine);
            builder.Append($"            throw new global::System.Text.Json.JsonException($\"Cannot convert the JSON property name '{{propertyName}}' to a {idInfo.Name}.\");").Append(GeneratedCode.NewLine);
        }

        builder.Append("        }");

        return builder.ToString();
    }
}
