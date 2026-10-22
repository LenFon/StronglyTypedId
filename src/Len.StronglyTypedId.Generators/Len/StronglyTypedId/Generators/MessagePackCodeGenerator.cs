using System.Text;

namespace Len.StronglyTypedId.Generators;

/// <summary>
/// 为强类型 Id 生成 MessagePack 的 <c>IMessagePackFormatter&lt;TId&gt;</c> 与选项装配入口。
/// </summary>
/// <remarks>
/// 格式化器放在 <c>StronglyTypedIds</c> 里而非嵌进 Id 名下，代价是少了 <c>[MessagePackFormatter]</c> 的零配置捷径，换来引用程序集里的 Id 也能拿到格式化器。
/// 载荷存放基元值本身；例外是 <see cref="StronglyTypedIdInfo.IsBinaryTextPrimitive"/>，那些基元没有格式化器，只能退回规范文本。
/// </remarks>
internal class MessagePackCodeGenerator : IIntegrationCodeGenerator
{
    /// <summary>
    /// 解析文本时使用的不变文化。二进制载荷与区域性无关，退回文本形时也必须按不变文化读写。
    /// </summary>
    private const string InvariantCulture = "global::System.Globalization.CultureInfo.InvariantCulture";

    /// <summary>
    /// <c>GetFormatterWithVerify&lt;T&gt;</c> 所在的静态类。
    /// </summary>
    /// <remarks>
    /// 它是扩展方法，而产物文件不带 <c>using</c>，扩展方法语法解析不到，只能按静态调用写全名。
    /// </remarks>
    private const string FormatterResolverExtensions = "global::MessagePack.FormatterResolverExtensions";

    /// <summary>
    /// 格式化器体内语句的缩进（产物中为 8 空格）。
    /// </summary>
    private static readonly string _statementIndent = new(' ', 8);

    /// <summary>
    /// 格式化器体内块语句的缩进（产物中为 12 空格）。
    /// </summary>
    private static readonly string _nestedIndent = new(' ', 12);

    /// <summary>
    /// 注册表条目在产物中的缩进（产物中为 16 空格）。
    /// </summary>
    private static readonly string _registrationIndent = new(' ', 16);

    internal static readonly IIntegrationCodeGenerator Instance = new MessagePackCodeGenerator();

    public int Order { get; } = 10;

    public void Generate(
        DiscoveredIds discoveredIds,
        ImmutableArray<ModuleInfo> modules,
        SourceProductionContext context,
        string version)
    {
        var stronglyTypedIds = discoveredIds.ExternalFor(IntegrationFlags.MessagePack).ToList();

        // 一个 Id 都没有时不产出空壳装配入口：与 Dapper / JSON 的注册入口保持一致，
        // 也让 Integrations = None 能真正关掉全部 MessagePack 产物。
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
            var formatterName = GetFormatterName(idInfo, ambiguousNames.Contains(idInfo.Name));

            var formatterCode = GeneratedCode.Header("Len.StronglyTypedId") + $$"""
                internal static partial class StronglyTypedIds
                {
                    /// <summary>
                    /// Serializes <see cref="{{idInfo.FullName}}"/> with MessagePack.
                    /// </summary>
                    {{GeneratedCode.CompilerGeneratedAttribute}}
                    {{GeneratedCode.GeneratedCodeAttribute(nameof(MessagePackCodeGenerator), version)}}
                    internal sealed class {{formatterName}} : global::MessagePack.Formatters.IMessagePackFormatter<{{idInfo.FullyQualifiedName}}>
                    {
                {{GetSerializeMember(idInfo, version)}}

                {{GetDeserializeMember(idInfo, version)}}
                    }
                }
                """;

            context.AddSource($"{idInfo.HintName}.MessagePack.g.cs", GeneratedCode.NormalizeLineEndings(formatterCode));

            registrations.Append($"\n{_registrationIndent}new {formatterName}(),");
        }

        // 装配入口固定落在 Len.StronglyTypedId 命名空间（与 System.Text.Json / Dapper 的注册入口一致）：
        // Id 可以分散在任意命名空间，注册入口只该有一个。
        var registrationCode = GeneratedCode.Header("Len.StronglyTypedId") + $$"""
                internal static partial class StronglyTypedIds
                {
                    /// <summary>
                    /// Returns MessagePack serializer options that resolve the generated formatters for strongly typed ids.
                    /// </summary>
                    /// <param name="options">The options to extend.</param>
                    /// <returns>
                    /// A new options instance whose resolver consults the generated formatters first and then
                    /// <paramref name="options"/>'s own resolver.
                    /// </returns>
                    /// <remarks>
                    /// <see cref="global::MessagePack.MessagePackSerializerOptions"/> is immutable, so the returned instance
                    /// must be used instead of the argument.
                    /// </remarks>
                    {{GeneratedCode.CompilerGeneratedAttribute}}
                    {{GeneratedCode.GeneratedCodeAttribute(nameof(MessagePackCodeGenerator), version)}}
                    public static global::MessagePack.MessagePackSerializerOptions ApplyTo(global::MessagePack.MessagePackSerializerOptions options) =>
                        options.WithResolver(global::MessagePack.Resolvers.CompositeResolver.Create(
                            new global::MessagePack.Formatters.IMessagePackFormatter[]
                            {{{registrations}}
                            },
                            new global::MessagePack.IFormatterResolver[] { options.Resolver }));
                }
                """;

        context.AddSource("StronglyTypedIds.MessagePack.g.cs", GeneratedCode.NormalizeLineEndings(registrationCode));
    }

    /// <summary>
    /// 取 <c>Serialize</c> 成员的完整文本（含签名与成员级缩进）。
    /// </summary>
    /// <remarks>
    /// 原始字符串模板里的插值洞不会随模板重新缩进，多行洞必须自带缩进，洞本身写在去缩进基准列上。
    /// </remarks>
    private static string GetSerializeMember(StronglyTypedIdInfo idInfo, string version)
    {
        var lines = new List<string>
        {
            _statementIndent + "/// <inheritdoc/>",
            _statementIndent + GeneratedCode.CompilerGeneratedAttribute,
            _statementIndent + GeneratedCode.GeneratedCodeAttribute(nameof(MessagePackCodeGenerator), version),
            _statementIndent + "public void Serialize(",
            _nestedIndent + "ref global::MessagePack.MessagePackWriter writer,",
            _nestedIndent + $"{idInfo.FullyQualifiedName} value,",
            _nestedIndent + "global::MessagePack.MessagePackSerializerOptions options)",
            _statementIndent + "{",
        };

        if (idInfo.TypeKindSuffix is null)
        {
            // 引用类型 Id 的 null 必须写成 nil：走到格式化器这一步已经没有别人替我们补 nil 了，
            // 直接取 value.Value 会抛 NullReferenceException。
            lines.Add(_nestedIndent + "if (value is null)");
            lines.Add(_nestedIndent + "{");
            lines.Add(_nestedIndent + "    writer.WriteNil();");
            lines.Add(_nestedIndent + "    return;");
            lines.Add(_nestedIndent + "}");
            lines.Add(string.Empty);
        }

        lines.Add(_nestedIndent + (idInfo.IsBinaryTextPrimitive
            ? "writer.Write(value.ToString());"
            : $"{FormatterResolverExtensions}.GetFormatterWithVerify<{idInfo.PrimitiveIdTypeName}>(options.Resolver).Serialize(ref writer, value.Value, options);"));
        lines.Add(_statementIndent + "}");

        return string.Join("\n", lines);
    }

    /// <summary>
    /// 取 <c>Deserialize</c> 成员的完整文本（含签名与成员级缩进）。
    /// </summary>
    private static string GetDeserializeMember(StronglyTypedIdInfo idInfo, string version)
    {
        var lines = new List<string>
        {
            _statementIndent + "/// <inheritdoc/>",
            _statementIndent + GeneratedCode.CompilerGeneratedAttribute,
            _statementIndent + GeneratedCode.GeneratedCodeAttribute(nameof(MessagePackCodeGenerator), version),
            _statementIndent + $"public {idInfo.FullyQualifiedName} Deserialize(",
            _nestedIndent + "ref global::MessagePack.MessagePackReader reader,",
            _nestedIndent + "global::MessagePack.MessagePackSerializerOptions options)",
            _statementIndent + "{",
            _nestedIndent + "if (reader.TryReadNil())",
            _nestedIndent + "{",
        };

        if (idInfo.TypeKindSuffix is null)
        {
            lines.Add(_nestedIndent + "    return default!;");
        }
        else
        {
            // 值类型 Id 收到 nil 说明载荷与契约不符（nil 只可能来自 Nullable<TId> 的写入）。
            // 静默落成 default 会把「数据缺失」伪装成「Guid.Empty 这样的合法值」，故与 JSON 转换器一致地抛错。
            lines.Add(_nestedIndent + $"    throw new global::MessagePack.MessagePackSerializationException(\"Cannot convert a nil MessagePack value to a {idInfo.Name}.\");");
        }

        lines.Add(_nestedIndent + "}");
        lines.Add(string.Empty);

        if (idInfo.IsBinaryTextPrimitive)
        {
            // 文本形：读回一律经 Id.TryParse，前缀策略与校验都由生成的 TryParse 承担。
            lines.Add(_nestedIndent + "var text = reader.ReadString();");
            lines.Add(string.Empty);
            lines.Add(_nestedIndent + $"if (text is not null && {idInfo.FullyQualifiedName}.TryParse(text, {InvariantCulture}, out var id))");
            lines.Add(_nestedIndent + "{");
            lines.Add(_nestedIndent + "    return id;");
            lines.Add(_nestedIndent + "}");
            lines.Add(string.Empty);
            lines.Add(_nestedIndent + $"throw new global::MessagePack.MessagePackSerializationException(\"Cannot convert the MessagePack value to a {idInfo.Name}.\");");
        }
        else
        {
            lines.Add(_nestedIndent + $"return {idInfo.FullyQualifiedName}.Create(");
            lines.Add(_nestedIndent + $"    {FormatterResolverExtensions}.GetFormatterWithVerify<{idInfo.PrimitiveIdTypeName}>(options.Resolver).Deserialize(ref reader, options));");
        }

        lines.Add(_statementIndent + "}");

        return string.Join("\n", lines);
    }

    /// <summary>
    /// 取格式化器的类名，消歧规则与 Dapper 的处理器一致。
    /// </summary>
    private static string GetFormatterName(StronglyTypedIdInfo idInfo, bool requiresNamespaceQualifier)
        => requiresNamespaceQualifier
            ? $"{idInfo.HintName.Replace('.', '_')}MessagePackFormatter"
            : $"{idInfo.Name}MessagePackFormatter";
}
