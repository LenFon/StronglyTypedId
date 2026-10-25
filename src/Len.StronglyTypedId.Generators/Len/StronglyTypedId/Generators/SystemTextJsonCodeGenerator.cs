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
            : "[global::System.Text.Json.Serialization.JsonConverter(typeof(SystemTextJsonConverter))]\n";

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
    /// 生成 <c>WriteAsPropertyName</c> 的方法体（不含外层大括号）。
    /// </summary>
    private static string GetWriteAsPropertyNameBody(StronglyTypedIdInfo idInfo)
    {
        // 原始字符串会剥掉闭合分隔符处的 16 个空格，故此处只需补足剥掉的那部分。
        const string Indent = "                ";

        if (!ShouldApplyDictionaryKeyPolicy(idInfo))
        {
            return $"{Indent}writer.WritePropertyName({GetPropertyNameExpression(idInfo)});";
        }

        // 枚举成员名是「名字」而非「数据」，DictionaryKeyPolicy 的语义恰好是命名风格转换，
        // 故与 STJ 的原生枚举键一致地应用它（原生 Dictionary<Status,int> 在 CamelCase 下写出 {"active":1}）。
        return string.Join(
            "\n",
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
    /// 指定的类型），它会穿透 <c>JsonSerializer.Deserialize</c> —— 而 STJ 的转换器契约要求失败一律抛 <c>JsonException</c>。
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
            builder.Append(indent).Append("if (text is null || !global::System.Numerics.BigInteger.TryParse(text, global::System.Globalization.NumberStyles.Integer, global::System.Globalization.CultureInfo.InvariantCulture, out var value))").Append('\n');
            builder.Append(indent).Append("{").Append('\n');
            builder.Append(indent).Append("    throw new global::System.Text.Json.JsonException($\"Cannot convert the JSON value to a ").Append(idInfo.Name).Append(".\");").Append('\n');
            builder.Append(indent).Append("}").Append('\n');

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
        // 仍由 JsonSerializer 处理 —— 而它要 options 里有该基元的 JsonTypeInfo。源生成的上下文若没登记
        // 该基元类型，框架抛的是 NotSupportedException；此处转成带指引的 JsonException，
        // 让调用方一眼看出「要给上下文补一条 [JsonSerializable]」。
        builder.Append(indent).Append(idInfo.PrimitiveIdTypeName).Append(" value;").Append('\n');
        builder.Append(indent).Append("try").Append('\n');
        builder.Append(indent).Append("{").Append('\n');
        builder.Append(indent).Append("    value = global::System.Text.Json.JsonSerializer.Deserialize<").Append(idInfo.PrimitiveIdTypeName).Append(">(ref reader, options);").Append('\n');
        builder.Append(indent).Append("}").Append('\n');
        builder.Append(indent).Append("catch (global::System.NotSupportedException e)").Append('\n');
        builder.Append(indent).Append("{").Append('\n');
        builder.Append(indent).Append("    throw new global::System.Text.Json.JsonException($\"Cannot convert the JSON token '{reader.TokenType}' to a ").Append(idInfo.Name).Append(": the JSON source generation context does not provide metadata for '").Append(idInfo.PrimitiveIdTypeName).Append("'. Add [JsonSerializable(typeof(").Append(idInfo.PrimitiveIdTypeName).Append("))] to that context.\", e);").Append('\n');
        builder.Append(indent).Append("}").Append('\n');
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
        builder.Append(indent).Append(typeName).Append(' ').Append(variableName).Append(';').Append('\n');
        builder.Append(indent).Append("try").Append('\n');
        builder.Append(indent).Append("{").Append('\n');
        builder.Append(indent).Append("    ").Append(variableName).Append(" = ").Append(expression).Append(';').Append('\n');
        builder.Append(indent).Append("}").Append('\n');
        builder.Append(indent).Append("catch (global::System.Exception e) when (e is global::System.FormatException or global::System.InvalidOperationException)").Append('\n');
        builder.Append(indent).Append("{").Append('\n');
        builder.Append(indent).Append("    throw new global::System.Text.Json.JsonException($\"Cannot convert the JSON token '{reader.TokenType}' to a ").Append(idInfo.Name).Append(".\", e);").Append('\n');
        builder.Append(indent).Append("}").Append('\n');
    }

    /// <summary>
    /// 追加「把 <c>value</c> 转成 Id」的语句，验证失败抛 <c>JsonException</c>。
    /// </summary>
    /// <remarks>
    /// 枚举基元在此追加已定义性校验：<c>TryCreate</c> 只跑用户配的 Validator，而 STJ 对未定义数值
    /// （<c>999</c>）照常读出 —— 不补这一道就会反序列化出不对应任何成员的 Id（<c>TryParse</c> 与
    /// Dapper 三条路径都已收紧，这里对齐）。
    /// </remarks>
    /// <param name="builder">产物构建器。</param>
    /// <param name="idInfo">当前 Id 的描述信息。</param>
    /// <param name="indent">每行前缀的绝对缩进。</param>
    /// <param name="assignmentPrefix">赋值目标前缀：<c>return </c> 或 <c>result = </c>。</param>
    private static void AppendCreateFromValue(StringBuilder builder, StronglyTypedIdInfo idInfo, string indent, string assignmentPrefix)
    {
        builder.Append(indent).Append(assignmentPrefix).Append(idInfo.EscapedName).Append(".TryCreate(value, out var id)");

        if (idInfo.GetEnumValidityCondition("value") is { } validity)
        {
            builder.Append(" && ").Append(validity);
        }

        builder.Append('\n');
        builder.Append(indent).Append("    ? id").Append('\n');
        builder.Append(indent).Append("    : throw new global::System.Text.Json.JsonException($\"Cannot convert the JSON value to a ").Append(idInfo.Name).Append(".\");").Append('\n');
    }

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
            // 契约：转换器失败一律抛 JsonException（STJ 只会给它补 Path / LineNumber），
            // 抛 InvalidOperationException 会让按契约 catch (JsonException) 的调用方漏掉这条路径。
            builder.Append("                throw new global::System.Text.Json.JsonException($\"Cannot convert the JSON token '{reader.TokenType}' to a ").Append(idInfo.Name).Append(".\");").Append('\n');
            builder.Append("            }").Append('\n');
            builder.Append('\n');

            if (idInfo.HasPrefix)
            {
                // 有前缀时文本形是字符串，读回一律经 Id.TryParse（前缀策略由生成的 TryParse 承担）。
                AppendDirectRead(builder, idInfo, "            ", "string?", "text", "reader.GetString()");
                builder.Append('\n');
                builder.Append("            return text is not null && ").Append(idInfo.EscapedName).Append(".TryParse(text, global::System.Globalization.CultureInfo.InvariantCulture, out var id)").Append('\n');
                builder.Append("                ? id").Append('\n');
                builder.Append("                : throw new global::System.Text.Json.JsonException($\"Cannot convert the JSON token '{reader.TokenType}' to a ").Append(idInfo.Name).Append(".\");").Append('\n');
                builder.Append("        }");

                return builder.ToString();
            }

            AppendReadValueStatements(builder, idInfo, "            ");
            builder.Append('\n');
            AppendCreateFromValue(builder, idInfo, "            ", "return ");
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
        builder.Append("            ").Append(idInfo.EscapedName).Append("? result = null;").Append('\n');
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
        // 属性名匹配要跟着 options.PropertyNameCaseInsensitive 走：`ValueTextEquals` 是逐字节比较，
        // 只认小写 `value`。用户开了大小写不敏感后，`{"Value":"..."}` 会走进下面的 Skip 分支，
        // 最终报「缺少 value 属性」—— 与 JsonSerializer 对普通对象的处理不一致。
        builder.Append("                if (reader.ValueTextEquals(\"value\")").Append('\n');
        builder.Append("                    || (options.PropertyNameCaseInsensitive && global::System.String.Equals(reader.GetString(), \"value\", global::System.StringComparison.OrdinalIgnoreCase)))").Append('\n');
        builder.Append("                {").Append('\n');
        builder.Append("                    reader.Read();").Append('\n');

        if (idInfo.HasPrefix)
        {
            builder.Append("                    var text = global::System.Text.Json.JsonSerializer.Deserialize<string>(ref reader, options);").Append('\n');
            builder.Append('\n');
            builder.Append("                    result = text is not null && ").Append(idInfo.EscapedName).Append(".TryParse(text, global::System.Globalization.CultureInfo.InvariantCulture, out var id)").Append('\n');
            builder.Append("                        ? id").Append('\n');
            builder.Append("                        : throw new global::System.Text.Json.JsonException($\"Cannot convert the JSON token '{reader.TokenType}' to a ").Append(idInfo.Name).Append(".\");").Append('\n');
        }
        else
        {
            // 与基元形态同理：null 在这里显式拦下，故下面可以读非可空基元。
            builder.Append("                    if (reader.TokenType == global::System.Text.Json.JsonTokenType.Null)").Append('\n');
            builder.Append("                    {").Append('\n');
            builder.Append("                        throw new global::System.Text.Json.JsonException($\"Cannot convert the JSON token '{reader.TokenType}' to a ").Append(idInfo.Name).Append(".\");").Append('\n');
            builder.Append("                    }").Append('\n');
            builder.Append('\n');
            AppendReadValueStatements(builder, idInfo, "                    ");
            builder.Append('\n');
            AppendCreateFromValue(builder, idInfo, "                    ", "result = ");
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
        // BigInteger 例外：它没有内置转换器，按值写会退化成 {"IsPowerOfTwo":…,"Sign":1}，故也写规范文本。
        //
        // 其余基元优先直写：WriteStringValue / WriteNumberValue 不需要 options 里有基元的
        // JsonTypeInfo，于是源生成的 JsonSerializerContext 不必为每个基元类型补一条
        // [JsonSerializable] 登记；输出与 STJ 默认逐字节相同（见 PrimitiveJsonIo 的说明）。
        // 直写不了的少数类型（Int128 / UInt128 / Half / TimeSpan / DateOnly / TimeOnly / char）
        // 仍委托给 JsonSerializer —— 那一档要 options 里有基元的元数据。
        var writeValue = idInfo.HasPrefix
            ? "writer.WriteStringValue(value.ToString());"
            : idInfo.IsBigIntegerPrimitive
                ? "writer.WriteStringValue(primitiveId.ToString(null, global::System.Globalization.CultureInfo.InvariantCulture));"
                : PrimitiveJsonIo.TryGetWriteExpression(idInfo)
                    ?? "global::System.Text.Json.JsonSerializer.Serialize(writer, primitiveId, options);";

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
        builder.Append($"        public override {idInfo.EscapedName} ReadAsPropertyName(").Append('\n');
        builder.Append("            ref global::System.Text.Json.Utf8JsonReader reader,").Append('\n');
        builder.Append("            global::System.Type typeToConvert,").Append('\n');
        builder.Append("            global::System.Text.Json.JsonSerializerOptions options)").Append('\n');
        builder.Append("        {").Append('\n');

        if (ShouldApplyDictionaryKeyPolicy(idInfo))
        {
            // 键文本可能已被 DictionaryKeyPolicy 改写（camelCase 等），故必须忽略大小写解析 ——
            // 与 STJ 的原生枚举键一致（{"ACTIVE":1} 也能读回 Active）。注意这里不能复用 Id.TryParse：
            // 它对枚举走大小写敏感的 Enum.TryParse，那是公共 API 的既定语义，不因字典键而改变。
            builder.Append("            var propertyName = reader.GetString();").Append('\n').Append('\n');
            builder.Append($"            if (global::System.Enum.TryParse<{idInfo.PrimitiveIdTypeName}>(propertyName, ignoreCase: true, out var val)").Append('\n');

            if (!idInfo.IsFlagsEnum)
            {
                builder.Append($"                && global::System.Enum.IsDefined(typeof({idInfo.PrimitiveIdTypeName}), val)").Append('\n');
            }
            else if (idInfo.FlagsEnumUndefinedBitsMask is { } undefinedBits)
            {
                // [Flags] 的位组合合法，但未定义的位不合法；掩码与 Id.TryParse 用的是同一份。
                builder.Append($"                && (unchecked((ulong)val) & {GeneratedCode.UndefinedBitsLiteral(undefinedBits)}) == 0UL").Append('\n');
            }

            builder.Append($"                && {idInfo.EscapedName}.TryCreate(val, out var id))").Append('\n');
            builder.Append("            {").Append('\n');
            builder.Append("                return id;").Append('\n');
            builder.Append("            }").Append('\n').Append('\n');
            builder.Append($"            throw new global::System.Text.Json.JsonException($\"Cannot convert the JSON property name '{{propertyName}}' to a {idInfo.Name}.\");").Append('\n');
        }
        else if (idInfo.IsStringPrimitive && !idInfo.HasPrefix)
        {
            // 必须走 TryCreate：Create 在验证器拒绝时抛 ArgumentException（或 ExceptionType 指定的类型），
            // 它会穿透 JsonSerializer.Deserialize —— 而 STJ 的转换器契约要求失败一律抛 JsonException。
            builder.Append("            var propertyName = reader.GetString();").Append('\n').Append('\n');
            builder.Append($"            return propertyName is not null && {idInfo.EscapedName}.TryCreate(propertyName, out var id)").Append('\n');
            builder.Append("                ? id").Append('\n');
            builder.Append($"                : throw new global::System.Text.Json.JsonException($\"Cannot convert the JSON property name '{{propertyName}}' to a {idInfo.Name}.\");").Append('\n');
        }
        else
        {
            // 有前缀时键文本也带前缀，故一律经 Id.TryParse（前缀策略由生成的 TryParse 承担）。
            builder.Append("            var propertyName = reader.GetString();").Append('\n').Append('\n');
            builder.Append($"            if ({idInfo.EscapedName}.TryParse(propertyName, global::System.Globalization.CultureInfo.InvariantCulture, out var id))").Append('\n');
            builder.Append("            {").Append('\n');
            builder.Append("                return id;").Append('\n');
            builder.Append("            }").Append('\n').Append('\n');
            builder.Append($"            throw new global::System.Text.Json.JsonException($\"Cannot convert the JSON property name '{{propertyName}}' to a {idInfo.Name}.\");").Append('\n');
        }

        builder.Append("        }");

        return builder.ToString();
    }
}
