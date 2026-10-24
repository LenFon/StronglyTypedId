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

        // 基元序列化器也必须由本入口显式注册：BSON 的 PrimitiveSerializationProvider 靠 Activator 反射创建
        // Int32Serializer / EnumSerializer<T> 等实例，Native AOT 下未被静态引用的构造器会被裁剪，创建直接抛
        // MissingMethodException（"No suitable constructor found"，CI aot job 实证）。这里预注册默认配置的实例：
        // 序列化器的 Equals 按类型（及表示）比较，与 provider 已缓存的默认实例相等，TryRegisterSerializer 只会
        // 报 false 而不会抛冲突；与 Id 本身的注册语义一致。
        // Guid 不在此列：3.0 起没有全局默认表示，消费者必须自选表示后自行注册（见下方 remarks）。
        var primitiveRegistrations = stronglyTypedIds
            .Where(idInfo => !idInfo.IsBinaryTextPrimitive)
            .GroupBy(idInfo => idInfo.PrimitiveIdTypeName, StringComparer.Ordinal)
            .Select(group => group.First())
            .Select(GetPrimitiveSerializerRegistration)
            .Where(registration => registration is not null)
            .ToList();

        foreach (var primitiveRegistration in primitiveRegistrations)
        {
            registrations.Append($"\n{_registrationIndent}{primitiveRegistration}");
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
                    /// Primitive-backed ids also get their underlying primitive serializers registered here with the
                    /// default configuration (int, enums, and so on). Without this, BSON's
                    /// <c>PrimitiveSerializationProvider</c> would create them via reflection, which throws under
                    /// Native AOT where non-referenced constructors are trimmed. The registered instances match the
                    /// provider-created defaults, so existing behavior is unchanged.
                    /// </para>
                    /// <para>
                    /// Guid-backed ids are the exception: since driver 3.0 there is no global default, and
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
    /// 取基元类型的 <c>TryRegisterSerializer</c> 注册语句；返回 <c>null</c> 表示该基元不需要（或无法）预注册。
    /// </summary>
    /// <remarks>
    /// 注册的实例一律用无参构造（与 <c>PrimitiveSerializationProvider</c> 反射创建的默认实例配置相同），
    /// 保证非 AOT 下与已缓存的默认实例 <c>Equals</c> 相等而不抛冲突。
    /// </remarks>
    private static string? GetPrimitiveSerializerRegistration(StronglyTypedIdInfo idInfo)
    {
        var primitiveTypeName = idInfo.PrimitiveIdTypeName;

        var serializerExpression = idInfo.IsEnum
            ? $"new global::MongoDB.Bson.Serialization.Serializers.EnumSerializer<{primitiveTypeName}>()"
            : primitiveTypeName switch
            {
                // 特型基元按 FullyQualifiedFormat 渲染成关键字短名；非特型 BCL 类型才带 global:: 前缀。
                "bool" => "new global::MongoDB.Bson.Serialization.Serializers.BooleanSerializer()",
                "byte" => "new global::MongoDB.Bson.Serialization.Serializers.ByteSerializer()",
                "sbyte" => "new global::MongoDB.Bson.Serialization.Serializers.SByteSerializer()",
                "short" => "new global::MongoDB.Bson.Serialization.Serializers.Int16Serializer()",
                "ushort" => "new global::MongoDB.Bson.Serialization.Serializers.UInt16Serializer()",
                "int" => "new global::MongoDB.Bson.Serialization.Serializers.Int32Serializer()",
                "uint" => "new global::MongoDB.Bson.Serialization.Serializers.UInt32Serializer()",
                "long" => "new global::MongoDB.Bson.Serialization.Serializers.Int64Serializer()",
                "ulong" => "new global::MongoDB.Bson.Serialization.Serializers.UInt64Serializer()",
                "float" => "new global::MongoDB.Bson.Serialization.Serializers.SingleSerializer()",
                "double" => "new global::MongoDB.Bson.Serialization.Serializers.DoubleSerializer()",
                "decimal" => "new global::MongoDB.Bson.Serialization.Serializers.DecimalSerializer()",
                "char" => "new global::MongoDB.Bson.Serialization.Serializers.CharSerializer()",
                "string" => "new global::MongoDB.Bson.Serialization.Serializers.StringSerializer()",
                "global::System.DateTime" => "new global::MongoDB.Bson.Serialization.Serializers.DateTimeSerializer()",
                "global::System.DateTimeOffset" => "new global::MongoDB.Bson.Serialization.Serializers.DateTimeOffsetSerializer()",
                "global::System.TimeSpan" => "new global::MongoDB.Bson.Serialization.Serializers.TimeSpanSerializer()",
                "global::System.DateOnly" => "new global::MongoDB.Bson.Serialization.Serializers.DateOnlySerializer()",
                "global::System.TimeOnly" => "new global::MongoDB.Bson.Serialization.Serializers.TimeOnlySerializer()",
                "global::System.Half" => "new global::MongoDB.Bson.Serialization.Serializers.HalfSerializer()",
                _ => null,
            };

        return serializerExpression is null
            ? null
            : $"global::MongoDB.Bson.Serialization.BsonSerializer.TryRegisterSerializer(typeof({primitiveTypeName}), {serializerExpression});";
    }

    /// <summary>
    /// 取序列化器的类名，消歧规则与 Dapper 的处理器一致。
    /// </summary>
    private static string GetSerializerName(StronglyTypedIdInfo idInfo, bool requiresNamespaceQualifier)
        => requiresNamespaceQualifier
            ? $"{idInfo.HintName.Replace('.', '_')}BsonSerializer"
            : $"{idInfo.EscapedName}BsonSerializer";
}
