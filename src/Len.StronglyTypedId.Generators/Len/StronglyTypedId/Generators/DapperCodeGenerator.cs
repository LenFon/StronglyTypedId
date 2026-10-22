using System.Text;

namespace Len.StronglyTypedId.Generators;

/// <summary>
/// 为强类型 Id 生成 Dapper 的 <c>SqlMapper.TypeHandler&lt;TId&gt;</c> 与统一注册入口。
/// </summary>
internal class DapperCodeGenerator : IIntegrationCodeGenerator
{
    /// <summary>
    /// 生成代码里引用不变文化的完整表达式。数据库里的值与区域性无关，转换与解析一律走不变文化，
    /// 否则「服务器区域设置为 de-DE」这类环境会让 <c>1.5</c> 之类的值解析失败。
    /// </summary>
    private const string InvariantCulture = "global::System.Globalization.CultureInfo.InvariantCulture";

    /// <summary>
    /// <c>Parse</c> 方法体的缩进（产物中为 8 空格，与声明同列）。
    /// </summary>
    private static readonly string _parseBodyIndent = new(' ', 8);

    /// <summary>
    /// <c>Parse</c> 方法体内语句的缩进（产物中为 12 空格）。
    /// </summary>
    private static readonly string _parseStatementIndent = new(' ', 12);

    /// <summary>
    /// <c>Parse</c> 方法体内嵌套块的缩进（产物中为 16 空格）。
    /// </summary>
    private static readonly string _parseNestedIndent = new(' ', 16);

    /// <summary>
    /// <c>ApplyTo</c> 方法体内语句的缩进（产物中为 8 空格）。
    /// </summary>
    private static readonly string _registrationIndent = new(' ', 8);

    internal static readonly IIntegrationCodeGenerator Instance = new DapperCodeGenerator();

    public int Order { get; } = 5;

    public void Generate(
        DiscoveredIds discoveredIds,
        ImmutableArray<ModuleInfo> modules,
        SourceProductionContext context,
        string version)
    {
        var stronglyTypedIds = discoveredIds.ExternalFor(IntegrationFlags.Dapper).ToList();

        // 一个 Id 都没有时不产出空壳注册类：与 System.Text.Json / Newtonsoft.Json 的注册入口保持一致，
        // 也让 Integrations = None 能真正关掉全部 Dapper 产物。
        if (stronglyTypedIds.Count == 0)
        {
            return;
        }

        var ambiguousNames = stronglyTypedIds
            .GroupBy(idInfo => idInfo.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToImmutableHashSet(StringComparer.Ordinal);

        var registrations = new StringBuilder();

        foreach (var idInfo in stronglyTypedIds)
        {
            var handlerName = GetHandlerName(idInfo, ambiguousNames.Contains(idInfo.Name));

            // 基类签名是 SetValue(IDbDataParameter, T?) / Parse(object) → T。值类型 T 的可空标注会被擦除，
            // 引用类型 T 必须照抄 T?，否则 CS8765。Parse 返回非可空 T，引用类型 Id 遇 NULL 时用 default! 表态。
            var setValueParameter = idInfo.TypeKindSuffix is null
                ? $"{idInfo.FullyQualifiedName}? value"
                : $"{idInfo.FullyQualifiedName} value";

            // 两条分支都必须写出基元值：DbParameter.Value 只接受驱动认识的类型，
            // 直接塞 Id 包装对象会让 SqlClient / Sqlite 在绑定时抛「No mapping exists」。
            var setValueBody = idInfo.TypeKindSuffix is null
                ? "parameter.Value = value is null ? (object)global::System.DBNull.Value : value.Value;"
                : "parameter.Value = value.Value;";

            var handlerCode = GeneratedCode.Header("Len.StronglyTypedId") + $$"""
            internal static partial class StronglyTypedIds
            {
                /// <inheritdoc/>
                {{GeneratedCode.CompilerGeneratedAttribute}}
                {{GeneratedCode.GeneratedCodeAttribute(nameof(DapperCodeGenerator), version)}}
                class {{handlerName}} : global::Dapper.SqlMapper.TypeHandler<{{idInfo.FullyQualifiedName}}>
                {
                    /// <inheritdoc/>
                    {{GeneratedCode.CompilerGeneratedAttribute}}
                    {{GeneratedCode.GeneratedCodeAttribute(nameof(DapperCodeGenerator), version)}}
                    public override void SetValue(global::System.Data.IDbDataParameter parameter, {{setValueParameter}}) =>
                        {{setValueBody}}

                    /// <inheritdoc/>
                    {{GeneratedCode.CompilerGeneratedAttribute}}
                    {{GeneratedCode.GeneratedCodeAttribute(nameof(DapperCodeGenerator), version)}}
                    public override {{idInfo.FullyQualifiedName}} Parse(object? value)
            {{GetParseBody(idInfo)}}
                }
            }
            """;

            context.AddSource($"{idInfo.HintName}.Dapper.g.cs", GeneratedCode.NormalizeLineEndings(handlerCode));

            registrations.Append($"\n{_registrationIndent}global::Dapper.SqlMapper.AddTypeHandler<{idInfo.FullyQualifiedName}>(new {handlerName}());");
        }

        var registrationCode = GeneratedCode.Header("Len.StronglyTypedId") + $$"""
                internal static partial class StronglyTypedIds
                {
                    /// <summary>
                    /// Registers the generated Dapper type handlers.
                    /// </summary>
                    /// <remarks>Dapper keeps type handlers in a process-wide registry, so this only needs to run once
                    /// per process. Calling it again replaces each handler with an equivalent instance.</remarks>
                    {{GeneratedCode.CompilerGeneratedAttribute}}
                    {{GeneratedCode.GeneratedCodeAttribute(nameof(DapperCodeGenerator), version)}}
                    public static void ApplyTo()
                    {{{registrations}}
                    }

                    /// <summary>
                    /// Registers the generated Dapper type handlers.
                    /// </summary>
                    /// <param name="connection">Ignored. Dapper registers type handlers globally rather than per
                    /// connection, so there is nothing to scope to this argument; it is kept for source compatibility.</param>
                    {{GeneratedCode.CompilerGeneratedAttribute}}
                    {{GeneratedCode.GeneratedCodeAttribute(nameof(DapperCodeGenerator), version)}}
                    public static void ApplyTo(global::System.Data.IDbConnection connection) => ApplyTo();
                }
                """;

        context.AddSource("StronglyTypedIds.Dapper.g.cs", GeneratedCode.NormalizeLineEndings(registrationCode));
    }

    /// <summary>
    /// 取 <c>Parse</c> 方法体的完整文本（含两侧大括号），各行自带产物中的绝对缩进。
    /// </summary>
    /// <remarks>
    /// 原始字符串模板里的插值洞不会随模板重新缩进，多行洞必须自带缩进，洞本身写在去缩进基准列上。
    /// </remarks>
    private static string GetParseBody(StronglyTypedIdInfo idInfo)
    {
        var create = $"{idInfo.FullyQualifiedName}.Create";
        var defaultValue = idInfo.TypeKindSuffix is null ? "default!" : "default";

        var lines = new List<string>
        {
            _parseBodyIndent + "{",
            _parseStatementIndent + "if (value is null or global::System.DBNull)",
            _parseStatementIndent + "{",
            _parseNestedIndent + $"return {defaultValue};",
            _parseStatementIndent + "}",
            string.Empty,

            // 驱动已经返回基元本身的 CLR 类型时直接还原（SQL Server / PostgreSQL 的常规情形），
            // 省掉一次转换；只有类型对不上时才走下面的逐基元转换。
            _parseStatementIndent + $"if (value is {idInfo.PrimitiveIdTypeName} primitiveValue)",
            _parseStatementIndent + "{",
            _parseNestedIndent + $"return {create}(primitiveValue);",
            _parseStatementIndent + "}",
            string.Empty,
        };

        if (idInfo.IsEnum)
        {
            // 枚举列通常以底层整数返回（SQLite 一律 Int64），也可能以成员名文本返回。
            // 两条路径各自成句：拼成一个三元表达式会有近 300 字符的单行。
            var primitive = idInfo.PrimitiveIdTypeName;

            lines.Add(_parseStatementIndent + "if (value is string enumText)");
            lines.Add(_parseStatementIndent + "{");
            lines.Add(_parseNestedIndent + $"return {create}(({primitive})global::System.Enum.Parse(typeof({primitive}), enumText));");
            lines.Add(_parseStatementIndent + "}");
            lines.Add(string.Empty);
            lines.Add(_parseStatementIndent + $"return {create}(({primitive})global::System.Enum.ToObject(typeof({primitive}), value!));");
        }
        else
        {
            lines.Add(_parseStatementIndent + $"return {create}({GetConversionExpression(idInfo)});");
        }

        lines.Add(_parseBodyIndent + "}");

        return string.Join("\n", lines);
    }

    /// <summary>
    /// 取「把 <c>value</c>（<see cref="object"/>）还原成基元值」的表达式（枚举除外，它在
    /// <see cref="GetParseBody"/> 里单独成段）。
    /// </summary>
    /// <remarks>
    /// 不能直接写 <c>(TPrimitive)value!</c>：那是拆箱语义，SQLite 下枚举列返回 <c>long</c>、<c>char</c> / <c>Guid</c> 列返回 <c>string</c>、<c>decimal</c> 列返回 <c>double</c>，四种基元全抛 <see cref="InvalidCastException"/>。
    /// </remarks>
    private static string GetConversionExpression(StronglyTypedIdInfo idInfo)
    {
        // string 基元：Convert.ToString 对 string 原样返回，对其它 IConvertible 走 ToString(provider)。
        if (idInfo.IsStringPrimitive)
        {
            return $"global::System.Convert.ToString(value, {InvariantCulture})!";
        }

        // char 基元：IConvertible.ToChar 只接受长度为 1 的 string，正好就是「合法文本」的定义。
        if (idInfo.IsCharPrimitive)
        {
            return $"global::System.Convert.ToChar(value, {InvariantCulture})";
        }

        var primitiveShortName = StronglyTypedIdInfo.GetPrimitiveIdTypeShortName(idInfo.PrimitiveIdTypeName);

        // 其余基元分两类：System.Convert 上有对应 ToXxx 的走转换（保留 DateTime 的 Tick 精度、
        // 数值的溢出检查），没有的（Guid / DateTimeOffset / TimeSpan / DateOnly / TimeOnly / Int128 /
        // UInt128 / Half / BigInteger）只能经 IParsable<TSelf> 的文本入口还原。
        return GetConvertExpression(primitiveShortName)
            ?? $"{idInfo.PrimitiveIdTypeName}.Parse(global::System.Convert.ToString(value, {InvariantCulture})!, {InvariantCulture})";
    }

    /// <summary>
    /// 取 <c>System.Convert</c> 上对应的转换表达式；没有对应重载时返回 <see langword="null"/>。
    /// </summary>
    /// <remarks>
    /// <paramref name="primitiveShortName"/> 来自 <see cref="SymbolDisplayFormat.FullyQualifiedFormat"/>，故 C# 有关键字的基元呈现为关键字（<c>int</c>），其余为类型名（<c>Guid</c>）。
    /// </remarks>
    private static string? GetConvertExpression(string primitiveShortName)
    {
        var convertMethodName = primitiveShortName switch
        {
            // Convert.ToXxx 的方法名与基元 CLR 类型名一一对应（ToInt32 / ToUInt64 / ToDecimal / ToDateTime …）。
            // TimeSpan 刻意不在此列：System.Convert 没有 ToTimeSpan，它走文本入口。
            "byte" or "Byte" => "ToByte",
            "sbyte" or "SByte" => "ToSByte",
            "short" or "Int16" => "ToInt16",
            "ushort" or "UInt16" => "ToUInt16",
            "int" or "Int32" => "ToInt32",
            "uint" or "UInt32" => "ToUInt32",
            "long" or "Int64" => "ToInt64",
            "ulong" or "UInt64" => "ToUInt64",
            "float" or "Single" => "ToSingle",
            "double" or "Double" => "ToDouble",
            "decimal" or "Decimal" => "ToDecimal",
            "DateTime" => "ToDateTime",
            _ => null,
        };

        return convertMethodName is null
            ? null
            : $"global::System.Convert.{convertMethodName}(value, {InvariantCulture})";
    }

    /// <summary>
    /// 取类型处理器的类名，消歧规则与 EF Core 转换器一致。
    /// </summary>
    private static string GetHandlerName(StronglyTypedIdInfo idInfo, bool requiresNamespaceQualifier)
        => requiresNamespaceQualifier
            ? $"{idInfo.HintName.Replace('.', '_')}TypeHandler"
            : $"{idInfo.Name}TypeHandler";
}
