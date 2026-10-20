using System.Text;

namespace Len.StronglyTypedId.Generators;

/// <summary>
/// 生成强类型 Id 的核心实现：<c>IStronglyTypedId&lt;TSelf, TPrimitiveId&gt;</c>、解析与格式化接口、比较接口以及比较运算符。
/// </summary>
internal class StronglyTypedIdCodeGenerator : ICoreCodeGenerator
{
    /// <summary>
    /// <c>TryCreate</c> 的 <c>out</c> 形参前缀：失败时允许为 <see langword="null"/>。
    /// </summary>
    private const string MaybeNullOutAttribute = "[global::System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out ";

    /// <summary>
    /// <c>TryCreate</c> 的 XML 文档注释。
    /// </summary>
    private static readonly string[] _tryCreateDocLines =
    [
        "<summary>",
        "Attempts to create a new strongly typed id that wraps the specified primitive value.",
        "</summary>",
        "<param name=\"value\">The primitive value to wrap.</param>",
        "<param name=\"result\">The created id when this method returns <see langword=\"true\"/>.</param>",
        "<returns><see langword=\"true\"/> when <paramref name=\"value\"/> is acceptable; otherwise, <see langword=\"false\"/>.</returns>",
    ];

    internal static readonly ICoreCodeGenerator Instance = new StronglyTypedIdCodeGenerator();

    /// <summary>
    /// <c>TryParse</c> 方法体内语句的缩进（产物中为 8 空格）。
    /// </summary>
    private static readonly string _tryParseBodyIndent = new(' ', 8);

    /// <summary>
    /// 类型成员体的缩进（产物中为 4 空格）。
    /// </summary>
    private static readonly string _memberBodyIndent = new(' ', 4);

    /// <summary>
    /// 表达式体续行的缩进（产物中为 8 空格）。
    /// </summary>
    private static readonly string _expressionBodyIndent = new(' ', 8);

    /// <summary>
    /// 归一化结果的局部变量名。
    /// </summary>
    private const string NormalizedLocal = "normalized";

    /// <summary>
    /// 解析时传给基元的 <c>IFormatProvider</c> 实参：调用方未指定时回退到不变文化。
    /// </summary>
    private const string ProviderOrDefault = "provider ?? global::System.Globalization.CultureInfo.InvariantCulture";

    public int Order { get; } = 0;

    public void Generate(
        StronglyTypedIdInfo idInfo,
        ImmutableArray<ModuleInfo> modules,
        SourceProductionContext context,
        string version)
    {
        var header = GeneratedCode.Header(idInfo.Namespace);

        // Parse 的失败一律抛 FormatException（IParsable<TSelf> 的惯例，与 int.Parse / Guid.Parse 一致），
        // 而不是 ArgumentException：后者是「调用方传了不该传的东西」，前者才是「文本格式不对」。
        // 注意 FormatException 没有带 paramName 的构造函数，故只传消息。
        var declaration = $$"""
                {{GetTypeAttributes(idInfo)}}{{idInfo.ReadOnlyRecordModifier}}partial record{{idInfo.TypeKindSuffix}} {{idInfo.Name}} :
                    global::Len.StronglyTypedId.IStronglyTypedId<{{idInfo.Name}}, {{idInfo.PrimitiveIdTypeName}}>,
                    global::System.IParsable<{{idInfo.Name}}>,
                    global::System.ISpanParsable<{{idInfo.Name}}>,
                    global::System.IComparable<{{idInfo.Name}}>,
                    global::System.Numerics.IEqualityOperators<{{idInfo.Name}}, {{idInfo.Name}}, bool>,
                    global::System.Numerics.IComparisonOperators<{{idInfo.Name}}, {{idInfo.Name}}, bool>{{GetOptionalInterfaces(idInfo)}}
                {
                    /// <inheritdoc/>
                    {{GeneratedCode.CompilerGeneratedAttribute}}
                    {{GeneratedCode.GeneratedCodeAttribute(nameof(StronglyTypedIdCodeGenerator), version)}}
                {{GetCreateMember(idInfo)}}{{idInfo.InstancesBlock}}

                    /// <inheritdoc/>
                    {{GeneratedCode.CompilerGeneratedAttribute}}
                    {{GeneratedCode.GeneratedCodeAttribute(nameof(StronglyTypedIdCodeGenerator), version)}}
                    public static {{idInfo.Name}} Parse(string value, global::System.IFormatProvider? provider)
                    {
                        if (!TryParse(value, provider, out var id))
                        {
                            throw new global::System.FormatException("Could not parse supplied value.");
                        }

                        return id;
                    }

                    /// <inheritdoc/>
                    {{GeneratedCode.CompilerGeneratedAttribute}}
                    {{GeneratedCode.GeneratedCodeAttribute(nameof(StronglyTypedIdCodeGenerator), version)}}
                    public static bool TryParse(
                        [global::System.Diagnostics.CodeAnalysis.NotNullWhen(true)] string? value,
                        global::System.IFormatProvider? provider,
                        [global::System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out {{idInfo.Name}} result)
                    {
                {{GetTryParseHead(idInfo, GetTryParseCondition(idInfo, isSpan: false), PrefixTextKind.String)}}
                {{GetTryParseSuccessBlock(idInfo, isSpan: false)}}

                        result = default;
                        return false;
                    }

                    /// <inheritdoc/>
                    {{GeneratedCode.CompilerGeneratedAttribute}}
                    {{GeneratedCode.GeneratedCodeAttribute(nameof(StronglyTypedIdCodeGenerator), version)}}
                    public static {{idInfo.Name}} Parse(global::System.ReadOnlySpan<char> value, global::System.IFormatProvider? provider)
                    {
                        if (!TryParse(value, provider, out var id))
                        {
                            throw new global::System.FormatException("Could not parse supplied value.");
                        }

                        return id;
                    }

                    /// <inheritdoc/>
                    {{GeneratedCode.CompilerGeneratedAttribute}}
                    {{GeneratedCode.GeneratedCodeAttribute(nameof(StronglyTypedIdCodeGenerator), version)}}
                    public static bool TryParse(
                        global::System.ReadOnlySpan<char> value,
                        global::System.IFormatProvider? provider,
                        [global::System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out {{idInfo.Name}} result)
                    {
                {{GetTryParseHead(idInfo, GetTryParseCondition(idInfo, isSpan: true), PrefixTextKind.Span)}}
                {{GetTryParseSuccessBlock(idInfo, isSpan: true)}}

                        result = default;
                        return false;
                    }

                    /// <inheritdoc/>
                    {{GeneratedCode.CompilerGeneratedAttribute}}
                    {{GeneratedCode.GeneratedCodeAttribute(nameof(StronglyTypedIdCodeGenerator), version)}}
                    public int CompareTo({{GetCompareToParameter(idInfo)}}) => {{GetCompareToBody(idInfo)}};

                    /// <inheritdoc/>
                    {{GeneratedCode.CompilerGeneratedAttribute}}
                    {{GeneratedCode.GeneratedCodeAttribute(nameof(StronglyTypedIdCodeGenerator), version)}}
                    {{GetComparisonOperator(idInfo, "<", "< 0")}}

                    /// <inheritdoc/>
                    {{GeneratedCode.CompilerGeneratedAttribute}}
                    {{GeneratedCode.GeneratedCodeAttribute(nameof(StronglyTypedIdCodeGenerator), version)}}
                    {{GetComparisonOperator(idInfo, ">", "> 0")}}

                    /// <inheritdoc/>
                    {{GeneratedCode.CompilerGeneratedAttribute}}
                    {{GeneratedCode.GeneratedCodeAttribute(nameof(StronglyTypedIdCodeGenerator), version)}}
                    {{GetComparisonOperator(idInfo, "<=", "<= 0")}}

                    /// <inheritdoc/>
                    {{GeneratedCode.CompilerGeneratedAttribute}}
                    {{GeneratedCode.GeneratedCodeAttribute(nameof(StronglyTypedIdCodeGenerator), version)}}
                    {{GetComparisonOperator(idInfo, ">=", ">= 0")}}{{GetComparisonHelper(idInfo)}}

                    /// <inheritdoc/>
                    {{GeneratedCode.CompilerGeneratedAttribute}}
                    {{GeneratedCode.GeneratedCodeAttribute(nameof(StronglyTypedIdCodeGenerator), version)}}
                    public override string ToString() => {{GetToStringBody(idInfo)}};{{GetAdditionalMembers(idInfo, version)}}
                }
                """;

        var code = header + GeneratedCode.NestInContainingTypes(idInfo, declaration);

        context.AddSource($"{idInfo.HintName}.g.cs", GeneratedCode.NormalizeLineEndings(code));
    }

    /// <summary>
    /// 追加到基类型列表末尾的「可选接口」，一项都不具备时返回空串。
    /// </summary>
    private static string GetOptionalInterfaces(StronglyTypedIdInfo idInfo)
    {
        var interfaces = new List<string>(4);

        if (idInfo.IsFormattable)
        {
            // 用 IFormattableStronglyTypedId<TSelf> 取代裸 IFormattable：能力不变，且泛型代码可按接口约束。
            interfaces.Add($"global::Len.StronglyTypedId.IFormattableStronglyTypedId<{idInfo.Name}>");
        }

        if (idInfo.IsSpanFormattable)
        {
            interfaces.Add("global::System.ISpanFormattable");
        }

        if (idInfo.IsUtf8SpanParsable)
        {
            interfaces.Add($"global::System.IUtf8SpanParsable<{idInfo.Name}>");
        }

        if (idInfo.IsUtf8SpanFormattable)
        {
            interfaces.Add("global::System.IUtf8SpanFormattable");
        }

        return interfaces.Count == 0 ? string.Empty : ",\n    " + string.Join(",\n    ", interfaces);
    }

    /// <summary>
    /// 取 <c>ToString()</c> 的表达式体。
    /// </summary>
    private static string GetToStringBody(StronglyTypedIdInfo idInfo)
    {
        // char 的 IFormattable.ToString(string, IFormatProvider) 是显式接口实现（CS1501），
        // 而其无参 ToString() 本就与文化无关，直接调用即可。
        var body = idInfo.IsCharPrimitive
            ? "Value.ToString()"
            : idInfo.IsFormattable
                ? "Value.ToString(null, global::System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty"
                : "Value.ToString() ?? string.Empty";

        // 前缀属于规范文本形，故直接外包一层字符串拼接；无前缀时返回原表达式，产物逐字节不变。
        return idInfo.HasPrefix ? $"{Literal(idInfo.Prefix!)} + ({body})" : body;
    }

    /// <summary>
    /// 生成基元 <c>TryFormat</c> 的调用表达式。
    /// </summary>
    /// <param name="kind">基元的直呼形态，见 <see cref="TryFormatKind"/>。</param>
    /// <param name="isUtf8">目标是 UTF-8 缓冲区（<see cref="TryFormatKind.Interface"/> 时决定强转哪个接口）。</param>
    /// <param name="destination">目标缓冲区表达式，可含 <c>.Slice(n)</c>。</param>
    /// <param name="writtenArgument">
    /// <c>out</c> 实参写法：<c>charsWritten</c> / <c>bytesWritten</c> / <c>var written</c>。
    /// </param>
    private static string GetTryFormatCall(TryFormatKind kind, bool isUtf8, string destination, string writtenArgument)
        => kind switch
        {
            TryFormatKind.Direct => $"Value.TryFormat({destination}, out {writtenArgument}, format, provider)",
            TryFormatKind.DirectWithoutProvider => $"Value.TryFormat({destination}, out {writtenArgument}, format)",
            _ => isUtf8
                ? $"((global::System.IUtf8SpanFormattable)Value).TryFormat({destination}, out {writtenArgument}, format, provider)"
                : $"((global::System.ISpanFormattable)Value).TryFormat({destination}, out {writtenArgument}, format, provider)",
        };

    /// <summary>
    /// 生成类型声明上方的前置特性：<see cref="System.Diagnostics.DebuggerDisplayAttribute"/> 无条件附加，
    /// <see cref="System.ComponentModel.TypeConverterAttribute"/> 仅在 
    /// <see cref="StronglyTypedIdInfo.HasTypeConverter"/> 开启时附加。
    /// </summary>
    private static string GetTypeAttributes(StronglyTypedIdInfo idInfo)
    {
        var builder = new StringBuilder();

        builder.Append("[global::System.Diagnostics.DebuggerDisplay(\"{Value}\")]").Append('\n');

        if (idInfo.HasTypeConverter)
        {
            // 必须用 FullyQualifiedName（含包含类型链，如 global::Ns.Outer.Inner）而不是
            // FullyQualifiedNamespace（只有命名空间）：嵌套 Id 的转换器挂在 Id 自己名下，
            // 用命名空间拼会指向不存在的 global::Ns.Inner.InnerTypeConverter → CS0246。
            builder.Append($"[global::System.ComponentModel.TypeConverter(typeof({idInfo.FullyQualifiedName}.{idInfo.Name}TypeConverter))]").Append('\n');
        }

        return builder.ToString();
    }

    /// <summary>
    /// 追加嵌套的 <see cref="System.ComponentModel.TypeConverter"/> 实现，供 <c>TypeDescriptor</c>
    /// 体系（<c>XmlSerializer</c>、<c>IConfiguration</c> 绑定、XAML / DataGrid）在字符串与强类型 Id 间往返。
    /// </summary>
    private static void AppendTypeConverter(StringBuilder builder, StronglyTypedIdInfo idInfo, string version)
    {
        builder.Append('\n').Append('\n');
        builder.Append("    /// <summary>").Append('\n');
        builder.Append("    /// Converts the strongly typed id to and from its underlying primitive string representation.").Append('\n');
        builder.Append("    /// </summary>").Append('\n');
        builder.Append("    ").Append(GeneratedCode.CompilerGeneratedAttribute).Append('\n');
        builder.Append("    ").Append(GeneratedCode.GeneratedCodeAttribute(nameof(StronglyTypedIdCodeGenerator), version)).Append('\n');
        builder.Append($"    public sealed class {idInfo.Name}TypeConverter : global::System.ComponentModel.TypeConverter").Append('\n');
        builder.Append("    {").Append('\n');
        builder.Append("        public override bool CanConvertFrom(global::System.ComponentModel.ITypeDescriptorContext? context, global::System.Type sourceType) => sourceType == typeof(string);").Append('\n');
        builder.Append('\n');
        builder.Append($"        public override object? ConvertFrom(global::System.ComponentModel.ITypeDescriptorContext? context, global::System.Globalization.CultureInfo? culture, object value) =>").Append('\n');
        builder.Append($"            value is string text ? {idInfo.Name}.Parse(text, global::System.Globalization.CultureInfo.InvariantCulture) : throw new global::System.NotSupportedException($\"Cannot convert from {{value?.GetType()}}.\");").Append('\n');
        builder.Append('\n');
        builder.Append("        public override object? ConvertTo(global::System.ComponentModel.ITypeDescriptorContext? context, global::System.Globalization.CultureInfo? culture, object? value, global::System.Type? destinationType) =>").Append('\n');
        builder.Append("            destinationType == typeof(string) ? value?.ToString() : throw new global::System.NotSupportedException($\"Cannot convert to {destinationType}.\");").Append('\n');
        builder.Append("    }");
    }

    /// <summary>
    /// 取完整的 <c>Create</c> 成员（含签名）。
    /// </summary>
    /// <remarks>
    /// 返回值自带 <see cref="_memberBodyIndent"/>：模板里的占位符落在原始字符串的去缩进基准列上，
    /// 首行拿不到模板的缩进，多行内容更是逐行原样插入 —— 缩进只能由这里补齐。
    /// </remarks>
    private static string GetCreateMember(StronglyTypedIdInfo idInfo)
    {
        var signature = $"public static {idInfo.Name} Create({idInfo.PrimitiveIdTypeName} value)";

        if (idInfo.NormalizerName is null)
        {
            return $"{_memberBodyIndent}{signature} =>{GetCreateExpression(idInfo)};";
        }

        var lines = new List<string>
        {
            signature,
            "{",
            $"    value = {idInfo.NormalizerName}(value);",
        };

        if (idInfo.ValidatorName is not null)
        {
            lines.Add(string.Empty);
            lines.Add($"    if (!{idInfo.ValidatorName}(value))");
            lines.Add("    {");
            lines.Add($"        {GetValidationFailureThrow(idInfo)};");
            lines.Add("    }");
        }

        lines.Add(string.Empty);
        lines.Add($"    return new {idInfo.Name}(value);");
        lines.Add("}");

        return string.Join("\n", lines.Select(line => line.Length == 0 ? string.Empty : _memberBodyIndent + line));
    }

    /// <summary>
    /// 取 <c>Create</c> 的表达式体。
    /// </summary>
    private static string GetCreateExpression(StronglyTypedIdInfo idInfo)
        => idInfo.ValidatorName is null
            ? $" new {idInfo.Name}(value)"
            : $" {idInfo.ValidatorName}(value) ? new {idInfo.Name}(value) : {GetValidationFailureThrow(idInfo)}";

    /// <summary>
    /// 取「验证失败」的 <c>throw</c> 表达式：未配置 <c>ExceptionType</c> 时产物与加入该能力之前逐字符一致，
    /// 配置后改用该类型的单字符串构造函数（不带 <c>paramName</c>，因为自定义异常未必有双参构造）。
    /// </summary>
    private static string GetValidationFailureThrow(StronglyTypedIdInfo idInfo)
        => idInfo.ExceptionTypeName is null
            ? $"throw new global::System.ArgumentException($\"The value '{{value}}' is not a valid {idInfo.Name}.\", nameof(value))"
            : $"throw new {idInfo.ExceptionTypeName}($\"The value '{{value}}' is not a valid {idInfo.Name}.\")";

    /// <summary>
    /// 取 <c>TryParse</c> 的解析条件。
    /// </summary>
    private static string GetTryParseCondition(StronglyTypedIdInfo idInfo, bool isSpan)
    {
        var condition = GetTryParseConditionCore(idInfo, isSpan);

        return idInfo.ValidatorName is null || idInfo.NormalizerName is not null
            ? condition
            : $"{condition} && {idInfo.ValidatorName}({GetTryParseValue(idInfo, isSpan)})";
    }

    /// <summary>
    /// 取 <c>TryParse</c> 体内「解析成功」分支的完整文本（含两侧大括号）。
    /// </summary>
    private static string GetTryParseSuccessBlock(StronglyTypedIdInfo idInfo, bool isSpan)
    {
        var value = idInfo.NormalizerName is null
            ? GetTryParseValue(idInfo, isSpan)
            : NormalizedLocal;

        var lines = new List<string> { "{" };

        if (idInfo.NormalizerName is not null)
        {
            lines.Add($"    var {NormalizedLocal} = {idInfo.NormalizerName}({GetTryParseValue(idInfo, isSpan)});");
            lines.Add(string.Empty);

            if (idInfo.ValidatorName is not null)
            {
                lines.Add($"    if (!{idInfo.ValidatorName}({NormalizedLocal}))");
                lines.Add("    {");
                lines.Add("        result = default;");
                lines.Add("        return false;");
                lines.Add("    }");
                lines.Add(string.Empty);
            }
        }

        lines.Add($"    result = new {idInfo.Name}({value});");
        lines.Add("    return true;");
        lines.Add("}");

        return string.Join("\n", lines.Select(line => line.Length == 0 ? string.Empty : _tryParseBodyIndent + line));
    }

    private static string GetTryParseConditionCore(StronglyTypedIdInfo idInfo, bool isSpan)
    {
        if (idInfo.IsStringPrimitive)
        {
            // 字符串基元没有可用的 TryParse，用非空即合法来表达；span 重载按 Length 判定，避免额外分配。
            return isSpan ? "value.Length > 0" : "value is { Length: > 0 }";
        }

        if (idInfo.IsEnum)
        {
            // 枚举只能走 Enum.TryParse<TEnum>，它只接受 string，故 span 重载先转成字符串。
            var parseArgument = isSpan ? "value.ToString()" : "value";
            var tryParse = $"global::System.Enum.TryParse<{idInfo.PrimitiveIdTypeName}>({parseArgument}, out var val)";

            if (!idInfo.IsFlagsEnum)
            {
                // Enum.TryParse 对「未定义的数值」（"999"）、「逗号组合」（"1,2"）与前后空白都返回 true，
                // 会产出一个不对应任何成员的 Id，后续 switch 落空、序列化写出野值，故再查一次 IsDefined 收紧。
                // 用非泛型重载：泛型 Enum.IsDefined<T>(T) 是 .NET 5+ 才有的，生成代码要照顾老 TFM 的消费者。
                return $"{tryParse} && global::System.Enum.IsDefined(typeof({idInfo.PrimitiveIdTypeName}), val)";
            }

            // [Flags] 的合法取值本就是位组合，IsDefined 会误杀，改用「不许出现未定义的位」：
            // 掩码在编译期由全部成员的常量算出，运行期只剩一次按位与。
            // 必须显式 unchecked：调用方若开了 CheckForOverflowUnderflow，负数枚举值转 ulong 会抛 OverflowException。
            return idInfo.FlagsEnumUndefinedBitsMask is { } undefinedBits
                ? $"{tryParse} && (unchecked((ulong)val) & {GeneratedCode.UndefinedBitsLiteral(undefinedBits)}) == 0UL"
                : tryParse;
        }

        if (idInfo.IsCharPrimitive)
        {
            // char 的 TryParse 是显式接口实现（CS1501），且「合法文本」的定义就是长度恰为 1；
            // 取值直接下标取，不经过 out 参数，故 GetTryParseValue 也走同一分支。
            return isSpan ? "value.Length == 1" : "value is { Length: 1 }";
        }

        return $"{idInfo.PrimitiveIdTypeName}.TryParse(value, {ProviderOrDefault}, out var val)";
    }

    /// <summary>
    /// 取传给构造函数的基元值表达式。
    /// </summary>
    private static string GetTryParseValue(StronglyTypedIdInfo idInfo, bool isSpan)
        => idInfo.IsCharPrimitive
            ? "value[0]"
            : idInfo.IsStringPrimitive
                ? isSpan ? "value.ToString()" : "value"
                : "val";

    /// <summary>
    /// 取 <c>TryParse</c> 体中「前缀处理」之后那一行 <c>if</c> 之前的完整文本（含该 <c>if</c> 行）。
    /// </summary>
    private static string GetTryParseHead(StronglyTypedIdInfo idInfo, string condition, PrefixTextKind kind)
    {
        var ifStatement = _tryParseBodyIndent + $"if ({condition})";

        if (!idInfo.HasPrefix)
        {
            return ifStatement;
        }

        var guardLines = GetPrefixGuardLines(idInfo, kind)
            .Select(line => line.Length == 0 ? string.Empty : _tryParseBodyIndent + line);

        return string.Join("\n", guardLines) + "\n\n" + ifStatement;
    }

    /// <summary>
    /// 取前缀处理语句的行（不含缩进）；无前缀时返回空数组。
    /// </summary>
    private static string[] GetPrefixGuardLines(StronglyTypedIdInfo idInfo, PrefixTextKind kind)
    {
        if (!idInfo.HasPrefix)
        {
            return [];
        }

        var prefix = idInfo.Prefix!;
        var literal = Literal(prefix);
        var value = kind == PrefixTextKind.Utf8 ? "utf8Text" : "value";
        var length = kind == PrefixTextKind.Utf8 ? Encoding.UTF8.GetByteCount(prefix) : prefix.Length;
        var slice = kind == PrefixTextKind.String ? $"{value}.Substring({length})" : $"{value}.Slice({length})";
        var startsWith = kind == PrefixTextKind.Utf8
            ? $"{value}.StartsWith({literal}u8)"
            : $"{value}.StartsWith({literal}, global::System.StringComparison.Ordinal)";

        if (idInfo.IsStrictPrefixRead)
        {
            // 严格：缺前缀即失败。string 形参可空，先挡 null 再判前缀。
            var nullCheck = kind == PrefixTextKind.String ? "value is null || " : string.Empty;

            return
            [
                $"if ({nullCheck}!{startsWith})",
                "{",
                "    result = default;",
                "    return false;",
                "}",
                string.Empty,
                $"{value} = {slice};",
            ];
        }

        // 宽松：前缀可选，带前缀则剥离。string 形参可空，null 直接跳过剥离交给既有条件处理。
        var guard = kind == PrefixTextKind.String ? $"value is not null && {startsWith}" : startsWith;

        return
        [
            $"if ({guard})",
            "{",
            $"    {value} = {slice};",
            "}",
        ];
    }

    /// <summary>
    /// 把字符串渲染成 C# 字符串字面量（含两侧引号并按需转义）。
    /// </summary>
    private static string Literal(string value)
        => Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(value, quote: true);

    /// <summary>
    /// 前缀处理语句针对的文本载体。
    /// </summary>
    private enum PrefixTextKind
    {
        /// <summary>
        /// <c>string?</c> 形参。
        /// </summary>
        String,

        /// <summary>
        /// <c>ReadOnlySpan&lt;char&gt;</c> 形参。
        /// </summary>
        Span,

        /// <summary>
        /// <c>ReadOnlySpan&lt;byte&gt;</c> 形参。
        /// </summary>
        Utf8,
    }

    /// <summary>
    /// 取 <c>CompareTo</c> 的形参。
    /// </summary>
    private static string GetCompareToParameter(StronglyTypedIdInfo idInfo)
        => idInfo.TypeKindSuffix is null ? $"{idInfo.Name}? other" : $"{idInfo.Name} other";

    /// <summary>
    /// 取 <c>CompareTo</c> 的表达式体。
    /// </summary>
    private static string GetCompareToBody(StronglyTypedIdInfo idInfo)
        => idInfo.TypeKindSuffix is null
            ? "other is null ? 1 : Value.CompareTo(other.Value)"
            : "Value.CompareTo(other.Value)";

    /// <summary>
    /// 取一个比较运算符的完整声明行。
    /// </summary>
    /// <remarks>
    /// 引用类型 Id 的操作数可以是 <c>null</c>，直接取 <c>left.Value</c> 会抛 <see cref="NullReferenceException"/>；
    /// 这里改走 <c>CompareValues</c>，与 <c>CompareTo</c> 的「null 最小」约定保持一致。值类型不存在 null 操作数，
    /// 保持直连基元比较，避免多一层调用。
    /// </remarks>
    private static string GetComparisonOperator(StronglyTypedIdInfo idInfo, string @operator, string comparison)
    {
        var parameterType = idInfo.TypeKindSuffix is null ? $"{idInfo.Name}?" : idInfo.Name;
        var body = idInfo.TypeKindSuffix is null
            ? $"CompareValues(left, right) {comparison}"
            : $"left.Value.CompareTo(right.Value) {comparison}";

        return $"public static bool operator {@operator}({parameterType} left, {parameterType} right) => {body};";
    }

    /// <summary>
    /// 取引用类型 Id 专用的 null 安全比较辅助方法；值类型返回空串。
    /// </summary>
    private static string GetComparisonHelper(StronglyTypedIdInfo idInfo)
    {
        if (idInfo.TypeKindSuffix is not null)
        {
            return string.Empty;
        }

        // 该片段经插值洞注入，洞本身写在模板的去缩进基准列上，故各行必须自带产物中的绝对缩进：
        // 成员声明与同级成员一样是 4 空格，表达式体的续行再多一层。
        var signature = $"private static int CompareValues({idInfo.Name}? left, {idInfo.Name}? right)";

        var builder = new StringBuilder();

        builder.Append('\n').Append('\n');
        builder.Append(_memberBodyIndent).Append("/// <summary>").Append('\n');
        builder.Append(_memberBodyIndent).Append("/// 按「null 最小」的约定比较两个值，与 <see cref=\"CompareTo\"/> 保持一致。").Append('\n');
        builder.Append(_memberBodyIndent).Append("/// </summary>").Append('\n');
        builder.Append(_memberBodyIndent).Append(signature).Append('\n');
        builder.Append(_expressionBodyIndent).Append("=> left is null ? (right is null ? 0 : -1) : left.CompareTo(right);");

        return builder.ToString();
    }

    /// <summary>
    /// 生成追加在模板末尾的成员：非抛异常的工厂 <c>TryCreate</c>，以及按基元能力条件生成的各种格式化与 UTF-8 解析成员。
    /// </summary>
    private static string GetAdditionalMembers(StronglyTypedIdInfo idInfo, string version)
    {
        var members = new StringBuilder();

        AppendTryCreateMember(members, idInfo, version);

        AppendDeconstructMember(members, idInfo, version);

        if (idInfo.IsFormattable)
        {
            // char 的 IFormattable.ToString(string, IFormatProvider) 是显式接口实现，只能经接口调用（多一次装箱）。
            var formatCall = idInfo.IsCharPrimitive
                ? "((global::System.IFormattable)Value).ToString(format, formatProvider)"
                : "Value.ToString(format, formatProvider)";

            // format 为空时带前缀，非空时裸基元：$"{id}" 等常见路径走 IFormattable，一律裸基元会静默丢前缀。
            AppendMember(
                members,
                version,
                ["<inheritdoc/>"],
                "public string ToString(string? format, global::System.IFormatProvider? formatProvider) =>",
                idInfo.HasPrefix
                    ? $"    global::System.String.IsNullOrEmpty(format) ? {Literal(idInfo.Prefix!)} + {formatCall} : {formatCall};"
                    : $"    {formatCall};");
        }

        if (idInfo.IsSpanFormattable)
        {
            // 有公开的实例 TryFormat 重载就按具体类型直呼，省掉装箱；否则退回接口调用。
            if (idInfo.HasPrefix)
            {
                var prefix = idInfo.Prefix!;
                var charCount = prefix.Length;

                AppendMember(
                    members,
                    version,
                    ["<inheritdoc/>"],
                    "public bool TryFormat(",
                    "    global::System.Span<char> destination,",
                    "    out int charsWritten,",
                    "    global::System.ReadOnlySpan<char> format,",
                    "    global::System.IFormatProvider? provider)",
                    "{",
                    "    if (!format.IsEmpty)",
                    "    {",
                    $"        return {GetTryFormatCall(idInfo.CharTryFormatKind, isUtf8: false, "destination", "charsWritten")};",
                    "    }",
                    string.Empty,
                    $"    if (!{Literal(prefix)}.AsSpan().TryCopyTo(destination))",
                    "    {",
                    "        charsWritten = 0;",
                    "        return false;",
                    "    }",
                    string.Empty,
                    $"    if ({GetTryFormatCall(idInfo.CharTryFormatKind, isUtf8: false, $"destination.Slice({charCount})", "var written")})",
                    "    {",
                    $"        charsWritten = {charCount} + written;",
                    "        return true;",
                    "    }",
                    string.Empty,
                    "    charsWritten = 0;",
                    "    return false;",
                    "}");
            }
            else
            {
                AppendMember(
                    members,
                    version,
                    ["<inheritdoc/>"],
                    "public bool TryFormat(",
                    "    global::System.Span<char> destination,",
                    "    out int charsWritten,",
                    "    global::System.ReadOnlySpan<char> format,",
                    "    global::System.IFormatProvider? provider) =>",
                    $"    {GetTryFormatCall(idInfo.CharTryFormatKind, isUtf8: false, "destination", "charsWritten")};");
            }
        }

        if (idInfo.IsUtf8SpanParsable)
        {
            AppendUtf8ParseMembers(members, idInfo, version);
        }

        if (idInfo.IsUtf8SpanFormattable)
        {
            // 与 UTF-16 的 TryFormat 同构：按基元能力直呼，format 为空时带前缀。
            if (idInfo.HasPrefix)
            {
                var prefix = idInfo.Prefix!;
                var byteCount = Encoding.UTF8.GetByteCount(prefix);

                AppendMember(
                    members,
                    version,
                    ["<inheritdoc/>"],
                    "public bool TryFormat(",
                    "    global::System.Span<byte> utf8Destination,",
                    "    out int bytesWritten,",
                    "    global::System.ReadOnlySpan<char> format,",
                    "    global::System.IFormatProvider? provider)",
                    "{",
                    "    if (!format.IsEmpty)",
                    "    {",
                    $"        return {GetTryFormatCall(idInfo.Utf8TryFormatKind, isUtf8: true, "utf8Destination", "bytesWritten")};",
                    "    }",
                    string.Empty,
                    $"    if (!{Literal(prefix)}u8.TryCopyTo(utf8Destination))",
                    "    {",
                    "        bytesWritten = 0;",
                    "        return false;",
                    "    }",
                    string.Empty,
                    $"    if ({GetTryFormatCall(idInfo.Utf8TryFormatKind, isUtf8: true, $"utf8Destination.Slice({byteCount})", "var written")})",
                    "    {",
                    $"        bytesWritten = {byteCount} + written;",
                    "        return true;",
                    "    }",
                    string.Empty,
                    "    bytesWritten = 0;",
                    "    return false;",
                    "}");
            }
            else
            {
                AppendMember(
                    members,
                    version,
                    ["<inheritdoc/>"],
                    "public bool TryFormat(",
                    "    global::System.Span<byte> utf8Destination,",
                    "    out int bytesWritten,",
                    "    global::System.ReadOnlySpan<char> format,",
                    "    global::System.IFormatProvider? provider) =>",
                    $"    {GetTryFormatCall(idInfo.Utf8TryFormatKind, isUtf8: true, "utf8Destination", "bytesWritten")};");
            }
        }

        if (idInfo.HasTypeConverter)
        {
            AppendTypeConverter(members, idInfo, version);
        }

        return members.ToString();
    }

    /// <summary>
    /// 追加 <c>TryCreate</c>：<c>Create</c> 的非抛异常版本。
    /// </summary>
    private static void AppendTryCreateMember(StringBuilder builder, StronglyTypedIdInfo idInfo, string version)
    {
        var signature = $"public static bool TryCreate({idInfo.PrimitiveIdTypeName} value, {MaybeNullOutAttribute}{idInfo.Name} result)";

        var lines = new List<string> { "{" };

        if (idInfo.NormalizerName is not null)
        {
            lines.Add($"    value = {idInfo.NormalizerName}(value);");
            lines.Add(string.Empty);
        }

        if (idInfo.ValidatorName is not null)
        {
            lines.Add($"    if (!{idInfo.ValidatorName}(value))");
            lines.Add("    {");
            lines.Add("        result = default;");
            lines.Add("        return false;");
            lines.Add("    }");
            lines.Add(string.Empty);
        }

        lines.Add($"    result = new {idInfo.Name}(value);");
        lines.Add("    return true;");
        lines.Add("}");

        AppendMember(builder, version, _tryCreateDocLines, [signature, .. lines]);
    }

    /// <summary>
    /// 追加 <c>Deconstruct</c>：让用户以 <c>(var value) = id;</c> 解构出基元值。
    /// </summary>
    private static void AppendDeconstructMember(StringBuilder builder, StronglyTypedIdInfo idInfo, string version)
    {
        AppendMember(
            builder,
            version,
            ["<summary>",
             "Deconstructs the strongly typed id into its underlying primitive value.",
             "</summary>",
             "<param name=\"value\">The underlying primitive value.</param>"],
            $"public void Deconstruct(out {idInfo.PrimitiveIdTypeName} value) => value = Value;");
    }

    /// <summary>
    /// 追加 UTF-8 的 <c>Parse</c> / <c>TryParse</c> 重载。
    /// </summary>
    private static void AppendUtf8ParseMembers(StringBuilder builder, StronglyTypedIdInfo idInfo, string version)
    {
        // 经接口名调用静态抽象成员会 CS8926，按具体基元类型调用则合法；该重载在基元下唯一，不会歧义。
        var condition = $"{idInfo.PrimitiveIdTypeName}.TryParse(utf8Text, {ProviderOrDefault}, out var val)";

        // 与 UTF-16 路径同理：归一化存在时校验必须挪到归一化之后，不能并入解析条件。
        if (idInfo.ValidatorName is not null && idInfo.NormalizerName is null)
        {
            condition = $"{condition} && {idInfo.ValidatorName}(val)";
        }

        AppendMember(
            builder,
            version,
            ["<inheritdoc/>"],
            $"public static {idInfo.Name} Parse(global::System.ReadOnlySpan<byte> utf8Text, global::System.IFormatProvider? provider)",
            "{",
            "    if (!TryParse(utf8Text, provider, out var id))",
            "    {",
            "        throw new global::System.FormatException(\"Could not parse supplied value.\");",
            "    }",
            string.Empty,
            "    return id;",
            "}");

        var declarationLines = new List<string>
        {
            "public static bool TryParse(",
            "    global::System.ReadOnlySpan<byte> utf8Text,",
            "    global::System.IFormatProvider? provider,",
            $"    {MaybeNullOutAttribute}{idInfo.Name} result)",
            "{",
        };

        foreach (var guardLine in GetPrefixGuardLines(idInfo, PrefixTextKind.Utf8))
        {
            declarationLines.Add(guardLine.Length == 0 ? string.Empty : "    " + guardLine);
        }

        if (idInfo.HasPrefix)
        {
            declarationLines.Add(string.Empty);
        }

        declarationLines.Add($"    if ({condition})");
        declarationLines.Add("    {");

        if (idInfo.NormalizerName is not null)
        {
            declarationLines.Add($"        var {NormalizedLocal} = {idInfo.NormalizerName}(val);");
            declarationLines.Add(string.Empty);

            if (idInfo.ValidatorName is not null)
            {
                declarationLines.Add($"        if (!{idInfo.ValidatorName}({NormalizedLocal}))");
                declarationLines.Add("        {");
                declarationLines.Add("            result = default;");
                declarationLines.Add("            return false;");
                declarationLines.Add("        }");
                declarationLines.Add(string.Empty);
            }
        }

        declarationLines.Add($"        result = new {idInfo.Name}({(idInfo.NormalizerName is null ? "val" : NormalizedLocal)});");
        declarationLines.Add("        return true;");
        declarationLines.Add("    }");
        declarationLines.Add(string.Empty);
        declarationLines.Add("    result = default;");
        declarationLines.Add("    return false;");
        declarationLines.Add("}");

        AppendMember(builder, version, ["<inheritdoc/>"], [.. declarationLines]);
    }

    /// <summary>
    /// 向 <paramref name="builder"/> 追加一个带生成器标注的成员。
    /// </summary>
    /// <param name="builder">目标缓冲区。</param>
    /// <param name="version">写入 <c>GeneratedCodeAttribute</c> 的生成器版本。</param>
    /// <param name="docLines">XML 文档注释内容行，不含 <c>/// </c> 前缀。</param>
    /// <param name="declarationLines">
    /// 成员自身的各行（不含缩进）；<see cref="string.Empty"/> 表示空行，不补缩进以免留下行尾空白。
    /// </param>
    private static void AppendMember(
        StringBuilder builder,
        string version,
        string[] docLines,
        params string[] declarationLines)
    {
        builder.Append('\n').Append('\n');

        foreach (var docLine in docLines)
        {
            builder.Append("    /// ").Append(docLine).Append('\n');
        }

        builder.Append("    ").Append(GeneratedCode.CompilerGeneratedAttribute).Append('\n');
        builder.Append("    ").Append(GeneratedCode.GeneratedCodeAttribute(nameof(StronglyTypedIdCodeGenerator), version));

        foreach (var declarationLine in declarationLines)
        {
            builder.Append('\n').Append(declarationLine.Length == 0 ? string.Empty : "    " + declarationLine);
        }
    }
}
