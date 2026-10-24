using System.Text;

namespace Len.StronglyTypedId.Generators;

/// <summary>
/// 为强类型 Id 生成 MongoDB BSON 的 <c>IBsonSerializer&lt;TId&gt;</c> 与注册入口。
/// </summary>
/// <remarks>
/// 与 MessagePack 同构：序列化器放在 <c>StronglyTypedIds</c> 里，靠 <c>RegisterBsonSerializers()</c> 注册进进程级注册表。
/// 不走 <c>[BsonSerializer]</c> 是因为它只能加在本次编译声明的类型上，「库作者声明、消费者使用」这条路径反而没有序列化器。
/// </remarks>
internal class MongoDbCodeGenerator : IIntegrationCodeGenerator
{
    /// <summary>
    /// 解析文本时使用的不变文化。
    /// </summary>
    private const string InvariantCulture = "global::System.Globalization.CultureInfo.InvariantCulture";

    /// <summary>
    /// 序列化器体内语句的缩进（产物中为 8 空格）。
    /// </summary>
    private static readonly string _statementIndent = new(' ', 8);

    /// <summary>
    /// 序列化器体内块语句的缩进（产物中为 12 空格）。
    /// </summary>
    private static readonly string _nestedIndent = new(' ', 12);

    /// <summary>
    /// 注册语句在产物中的缩进（产物中为 8 空格）。
    /// </summary>
    private static readonly string _registrationIndent = new(' ', 8);

    internal static readonly IIntegrationCodeGenerator Instance = new MongoDbCodeGenerator();

    public int Order { get; } = 11;

    public void Generate(
        DiscoveredIds discoveredIds,
        ImmutableArray<ModuleInfo> modules,
        SourceProductionContext context,
        string version)
    {
        var stronglyTypedIds = discoveredIds.ExternalFor(IntegrationFlags.MongoDb).ToList();

        if (stronglyTypedIds.Count == 0)
        {
            return;
        }

        // 分组键用 Name 而非 EscapedName：这里比的是「是否重名」，名字不落进生成代码；
        // 且两者是 1:1 映射（仅保留关键字差一个 @ 前缀），分组结果本来完全相同。
        var ambiguousNames = stronglyTypedIds
            .GroupBy(idInfo => idInfo.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToImmutableHashSet(StringComparer.Ordinal);

        var registrations = new StringBuilder();

        foreach (var idInfo in stronglyTypedIds)
        {
            var serializerName = GetSerializerName(idInfo, ambiguousNames.Contains(idInfo.Name));

            var serializerCode = GeneratedCode.Header("Len.StronglyTypedId") + $$"""
                internal static partial class StronglyTypedIds
                {
                    /// <summary>
                    /// Serializes <see cref="{{idInfo.FullName}}"/> to and from BSON.
                    /// </summary>
                    {{GeneratedCode.CompilerGeneratedAttribute}}
                    {{GeneratedCode.GeneratedCodeAttribute(nameof(MongoDbCodeGenerator), version)}}
                    internal sealed class {{serializerName}} : global::MongoDB.Bson.Serialization.Serializers.SerializerBase<{{idInfo.FullyQualifiedName}}>
                    {
                {{GetSerializeMember(idInfo, version)}}

                {{GetDeserializeMember(idInfo, version)}}
                    }
                }
                """;

            context.AddSource($"{idInfo.HintName}.MongoDb.g.cs", GeneratedCode.NormalizeLineEndings(serializerCode));

            registrations.Append($"\n{_registrationIndent}global::MongoDB.Bson.Serialization.BsonSerializer.TryRegisterSerializer(new {serializerName}());");
        }

        var registrationCode = GeneratedCode.Header("Len.StronglyTypedId") + $$"""
                internal static partial class StronglyTypedIds
                {
                    /// <summary>
                    /// Registers the generated BSON serializers for strongly typed ids.
                    /// </summary>
                    /// <remarks>
                    /// <c>BsonSerializer</c> keeps serializers in a process-wide registry, so this only needs to run once
                    /// per process, before the first document is serialized. Registration is idempotent: an already
                    /// registered serializer is left untouched.
                    /// <para>
                    /// "Left untouched" includes a serializer someone else registered for the same id type: the driver's
                    /// <c>TryRegisterSerializer</c> reports <see langword="false"/> and keeps the existing one, so a
                    /// conflicting registration silently wins. This is deliberate — overwriting would break the
                    /// idempotency above — but it means a serializer registered elsewhere takes precedence over the
                    /// generated one for that id.
                    /// </para>
                    /// <para>
                    /// Guid-backed ids also need a Guid representation: since driver 3.0 there is no global default, and
                    /// serializing a Guid without one throws. Register a <c>GuidSerializer</c> with the representation your
                    /// existing data uses before serializing.
                    /// </para>
                    /// </remarks>
                    {{GeneratedCode.CompilerGeneratedAttribute}}
                    {{GeneratedCode.GeneratedCodeAttribute(nameof(MongoDbCodeGenerator), version)}}
                    public static void RegisterBsonSerializers()
                    {{{registrations}}
                    }
                }
                """;

        context.AddSource("StronglyTypedIds.MongoDb.g.cs", GeneratedCode.NormalizeLineEndings(registrationCode));
    }

    /// <summary>
    /// 取 <c>Serialize</c> 成员的完整文本（含签名与成员级缩进）。
    /// </summary>
    private static string GetSerializeMember(StronglyTypedIdInfo idInfo, string version)
    {
        var lines = new List<string>
        {
            _statementIndent + "/// <inheritdoc/>",
            _statementIndent + GeneratedCode.CompilerGeneratedAttribute,
            _statementIndent + GeneratedCode.GeneratedCodeAttribute(nameof(MongoDbCodeGenerator), version),
            _statementIndent + $"public override void Serialize(",
            _nestedIndent + "global::MongoDB.Bson.Serialization.BsonSerializationContext context,",
            _nestedIndent + "global::MongoDB.Bson.Serialization.BsonSerializationArgs args,",
            _nestedIndent + $"{idInfo.FullyQualifiedName} value)",
            _statementIndent + "{",
        };

        if (idInfo.TypeKindSuffix is null)
        {
            // 引用类型 Id 的 null 写成 BSON null：序列化器是最后一道，没人替我们补 null。
            lines.Add(_nestedIndent + "if (value is null)");
            lines.Add(_nestedIndent + "{");
            lines.Add(_nestedIndent + "    context.Writer.WriteNull();");
            lines.Add(_nestedIndent + "    return;");
            lines.Add(_nestedIndent + "}");
            lines.Add(string.Empty);
        }

        if (idInfo.IsBinaryTextPrimitive)
        {
            lines.Add(_nestedIndent + "context.Writer.WriteString(value.ToString());");
        }
        else
        {
            lines.Add(_nestedIndent + $"global::MongoDB.Bson.Serialization.BsonSerializer.LookupSerializer<{idInfo.PrimitiveIdTypeName}>()");
            lines.Add(_nestedIndent + "    .Serialize(context, args, value.Value);");
        }

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
            _statementIndent + GeneratedCode.GeneratedCodeAttribute(nameof(MongoDbCodeGenerator), version),
            _statementIndent + $"public override {idInfo.FullyQualifiedName} Deserialize(",
            _nestedIndent + "global::MongoDB.Bson.Serialization.BsonDeserializationContext context,",
            _nestedIndent + "global::MongoDB.Bson.Serialization.BsonDeserializationArgs args)",
            _statementIndent + "{",
            _nestedIndent + "if (context.Reader.CurrentBsonType == global::MongoDB.Bson.BsonType.Null)",
            _nestedIndent + "{",
            _nestedIndent + "    context.Reader.ReadNull();",
        };

        if (idInfo.TypeKindSuffix is null)
        {
            lines.Add(_nestedIndent + "    return default!;");
        }
        else
        {
            // 值类型 Id 收到 BSON null 说明载荷与契约不符；静默落成 default 会把「数据缺失」
            // 伪装成「Guid.Empty 这样的合法值」，故与 JSON 转换器一致地抛错。
            lines.Add(_nestedIndent + $"    throw new global::MongoDB.Bson.BsonSerializationException(\"Cannot convert a BSON null to a {idInfo.Name}.\");");
        }

        lines.Add(_nestedIndent + "}");
        lines.Add(string.Empty);

        if (idInfo.IsBinaryTextPrimitive)
        {
            // 文本形：读回一律经 Id.TryParse，前缀策略与校验都由生成的 TryParse 承担。
            lines.Add(_nestedIndent + "var text = context.Reader.ReadString();");
            lines.Add(string.Empty);
            lines.Add(_nestedIndent + $"if ({idInfo.FullyQualifiedName}.TryParse(text, {InvariantCulture}, out var id))");
            lines.Add(_nestedIndent + "{");
            lines.Add(_nestedIndent + "    return id;");
            lines.Add(_nestedIndent + "}");
            lines.Add(string.Empty);
            lines.Add(_nestedIndent + $"throw new global::MongoDB.Bson.BsonSerializationException(\"Cannot convert the BSON string to a {idInfo.Name}.\");");
        }
        else
        {
            // 必须走 TryCreate：Create 在验证器拒绝时抛 ArgumentException（或 ExceptionType 指定的类型），
            // 它会穿透 BsonSerializer —— 而 BSON 序列化器的契约要求失败一律抛 BsonSerializationException。
            lines.Add(_nestedIndent + $"var value = global::MongoDB.Bson.Serialization.BsonSerializer.LookupSerializer<{idInfo.PrimitiveIdTypeName}>().Deserialize(context, args);");
            lines.Add(string.Empty);

            // 枚举基元在此追加已定义性校验：TryCreate 只跑用户配的 Validator，而 BSON 对未定义数值
            // （999）照常读出 —— 不补这一道就会反序列化出不对应任何成员的 Id（TryParse 与 Dapper 三条、
            // STJ、Newtonsoft 路径都已收紧，这里对齐）。
            var enumValidity = idInfo.GetEnumValidityCondition("value");
            lines.Add(_nestedIndent + $"if ({idInfo.FullyQualifiedName}.TryCreate(value, out var id){(enumValidity is null ? string.Empty : $" && {enumValidity}")})");
            lines.Add(_nestedIndent + "{");
            lines.Add(_nestedIndent + "    return id;");
            lines.Add(_nestedIndent + "}");
            lines.Add(string.Empty);
            lines.Add(_nestedIndent + $"throw new global::MongoDB.Bson.BsonSerializationException(\"Cannot convert the BSON value to a {idInfo.Name}.\");");
        }

        lines.Add(_statementIndent + "}");

        return string.Join("\n", lines);
    }

    /// <summary>
    /// 取序列化器的类名，消歧规则与 Dapper 的处理器一致。
    /// </summary>
    private static string GetSerializerName(StronglyTypedIdInfo idInfo, bool requiresNamespaceQualifier)
        => requiresNamespaceQualifier
            ? $"{idInfo.HintName.Replace('.', '_')}BsonSerializer"
            : $"{idInfo.EscapedName}BsonSerializer";
}
