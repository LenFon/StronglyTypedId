namespace Len.StronglyTypedId.Generators;

/// <summary>
/// 为强类型 Id 生成 protobuf-net 的 surrogate 契约。
/// </summary>
/// <remarks>
/// 这是核心生成器而非集成生成器：protobuf-net 的 surrogate 只能通过
/// <c>[ProtoContract(Surrogate = ...)]</c> 特性声明，而特性只能加在本次编译声明的类型上。
/// 引用程序集里的 Id 因此拿不到 protobuf 支持，由文档写明。
/// <para>
/// 不用 <c>RuntimeTypeModel.Add(...).SetSurrogate(...)</c> 的运行时注册：实测类型一旦被序列化过，
/// 再注册会抛「The type cannot be changed once a serializer has been generated」，
/// 而注册时机取决于用户代码的执行顺序，无法保证。
/// </para>
/// <para>
/// 也不把 Id 直接交给 protobuf-net 推断：实测用户只要在 partial 里加一个计算属性
/// （<c>public string Display =&gt; ...</c>），推断就失败并抛「no contract can be inferred」；
/// 自定义基元更是完全无法推断。
/// </para>
/// </remarks>
internal class ProtobufCodeGenerator : ICoreCodeGenerator
{
    /// <summary>
    /// 生成代码里引用不变文化的完整表达式。
    /// </summary>
    private const string InvariantCulture = GeneratedCode.InvariantCulture;

    /// <summary>
    /// 嵌套 surrogate 类成员的缩进（产物中为 8 空格）。
    /// </summary>
    private static readonly string _memberIndent = new(' ', 8);

    /// <summary>
    /// 嵌套 surrogate 类成员体内语句的缩进（产物中为 12 空格）。
    /// </summary>
    private static readonly string _bodyIndent = new(' ', 12);

    internal static readonly ICoreCodeGenerator Instance = new ProtobufCodeGenerator();

    public int Order { get; } = 3;

    public void Generate(
        StronglyTypedIdInfo idInfo,
        ImmutableArray<ModuleInfo> modules,
        SourceProductionContext context,
        string version)
    {
        // Integrations 可以把这个 Id 的 protobuf 集成关掉；程序集门控已在调用方做过。
        if (!idInfo.Supports(IntegrationFlags.Protobuf))
        {
            return;
        }

        // 泛型容器里的 Id 不能带 [ProtoContract(Surrogate = ...)]：特性实参必须是封闭类型，
        // 而 Box<T>.BoxedId.BoxedIdSurrogate 含类型参数，会 CS0416。surrogate 本身也不生成 ——
        // 没有特性接线，它只是一段死代码。
        if (idInfo.IsInGenericContainer)
        {
            return;
        }

        // 用户已经自己加了 [ProtoContract]：再补一个会 CS0579（特性重复）。此时由用户接管契约，
        // 生成器不插手，避免把「用户已配置」变成编译错误。
        if (idInfo.HasProtoContractAttribute)
        {
            return;
        }

        var surrogateName = $"{idInfo.EscapedName}Surrogate";

        // 引用类型 Id 的字段一律可空：protobuf-net 把根级 null 写成空载荷，读回时构造的是
        // 「字段全为默认值」的 surrogate 而不是 null。字段不可空时无法区分「null」与「Guid.Empty」，
        // 于是 null 会被静默还原成一个看起来合法的 Id（实测 Guid → Guid.Empty、int → 0、枚举 → 首成员）。
        // 文本形的基元与 string 基元本来就用 string? 承载。
        var isReferenceType = idInfo.TypeKindSuffix is null;
        var fieldType = idInfo.IsProtobufTextPrimitive || idInfo.IsStringPrimitive
            ? "string?"
            : isReferenceType
                ? $"{idInfo.PrimitiveIdTypeName}?"
                : idInfo.PrimitiveIdTypeName;

        var declaration = $$"""
                [global::ProtoBuf.ProtoContract(Surrogate = typeof({{idInfo.FullyQualifiedName}}.{{surrogateName}}))]
                {{idInfo.ReadOnlyRecordModifier}}partial record{{idInfo.TypeKindSuffix}} {{idInfo.EscapedName}}
                {
                    /// <summary>
                    /// protobuf-net 的 surrogate：以单个字段承载基元值。
                    /// </summary>
                    {{GeneratedCode.CompilerGeneratedAttribute}}
                    {{GeneratedCode.GeneratedCodeAttribute(nameof(ProtobufCodeGenerator), version)}}
                    [global::ProtoBuf.ProtoContract]
                    internal sealed class {{surrogateName}}
                    {
                        /// <summary>
                        /// 基元值。
                        /// </summary>
                        {{GeneratedCode.CompilerGeneratedAttribute}}
                        {{GeneratedCode.GeneratedCodeAttribute(nameof(ProtobufCodeGenerator), version)}}
                        [global::ProtoBuf.ProtoMember(1)]
                        public {{fieldType}} Value { get; set; }

                {{GetToSurrogateMember(idInfo, surrogateName, version)}}

                {{GetFromSurrogateMember(idInfo, surrogateName, version)}}
                    }
                }
                """;

        var code = GeneratedCode.Header(idInfo.Namespace) + GeneratedCode.NestInContainingTypes(idInfo, declaration);

        GeneratedCode.AddGeneratedSource(context, $"{idInfo.HintName}.Protobuf.g.cs", code);
    }

    /// <summary>
    /// 取「Id → surrogate」的隐式转换运算符。
    /// </summary>
    /// <remarks>
    /// protobuf-net 的 surrogate 必须配一对转换运算符，否则序列化时抛
    /// 「No suitable conversion operator found for surrogate」。实测确认：只声明
    /// <c>[ProtoContract(Surrogate = ...)]</c> 而不写运算符，编译能过、运行即失败。
    /// </remarks>
    private static string GetToSurrogateMember(StronglyTypedIdInfo idInfo, string surrogateName, string version)
    {
        var valueExpression = idInfo.IsProtobufTextPrimitive
            ? idInfo.GetProtobufTextExpression("id.Value")
            : "id.Value";

        var isReferenceType = idInfo.TypeKindSuffix is null;

        var signature = isReferenceType
            ? $"public static implicit operator {surrogateName}?({idInfo.EscapedName}? id)"
            : $"public static implicit operator {surrogateName}({idInfo.EscapedName} id)";

        // 引用类型 Id 的实例可以是 null，surrogate 也必须能表达 null，否则 protobuf-net 在
        // 转换时取 id.Value 会抛 NullReferenceException。
        var body = isReferenceType
            ? $"id is null ? null : new() {{ Value = {valueExpression} }}"
            : $"new() {{ Value = {valueExpression} }}";

        return string.Join(
            GeneratedCode.NewLine,
            _memberIndent + "/// <summary>",
            _memberIndent + "/// 把 Id 转成 surrogate。",
            _memberIndent + "/// </summary>",
            _memberIndent + GeneratedCode.CompilerGeneratedAttribute,
            _memberIndent + GeneratedCode.GeneratedCodeAttribute(nameof(ProtobufCodeGenerator), version),
            _memberIndent + $"{signature} => {body};");
    }

    /// <summary>
    /// 取「surrogate → Id」的隐式转换运算符。
    /// </summary>
    /// <remarks>
    /// 必须走 <c>TryCreate</c> 而不是 <c>Create</c>：后者在验证器拒绝时抛 <see cref="ArgumentException"/>
    /// （或 <c>ExceptionType</c> 指定的类型），而 protobuf-net 的契约要求失败时抛
    /// <c>ProtoBuf.ProtoException</c>，否则调用方拿到的是被包装过的无关异常。
    /// </remarks>
    private static string GetFromSurrogateMember(StronglyTypedIdInfo idInfo, string surrogateName, string version)
    {
        var isReferenceType = idInfo.TypeKindSuffix is null;

        var signature = isReferenceType
            ? $"public static implicit operator {idInfo.EscapedName}?({surrogateName}? surrogate)"
            : $"public static implicit operator {idInfo.EscapedName}({surrogateName} surrogate)";

        // 文本形先按基元的 Parse 还原，再交给 TryCreate 跑归一化与校验；string 基元的字段可空，
        // 也要先挡 null（TryCreate 的形参是非空 string）。其余基元直接 TryCreate。
        // 引用类型 Id 的值类型基元字段现在也是可空的（见 Generate），故要先解包再交给 TryCreate。
        var condition = idInfo.IsProtobufTextPrimitive
            ? "surrogate.Value is not null"
                + $" && {idInfo.PrimitiveIdTypeName}.TryParse(surrogate.Value, {InvariantCulture}, out var value)"
                + $" && {idInfo.GetTryCreateCondition(idInfo.FullyQualifiedName, "value", "id")}"
            : idInfo.IsStringPrimitive
                ? "surrogate.Value is not null"
                    + $" && {idInfo.GetTryCreateCondition(idInfo.FullyQualifiedName, "surrogate.Value", "id")}"
                : isReferenceType
                    ? "surrogate.Value is { } value"
                        + $" && {idInfo.GetTryCreateCondition(idInfo.FullyQualifiedName, "value", "id")}"
                    : idInfo.GetTryCreateCondition(idInfo.FullyQualifiedName, "surrogate.Value", "id");

        var lines = new List<string>
        {
            _memberIndent + "/// <summary>",
            _memberIndent + "/// 把 surrogate 转回 Id；载荷不合法时抛 <c>ProtoBuf.ProtoException</c>。",
            _memberIndent + "/// </summary>",
            _memberIndent + GeneratedCode.CompilerGeneratedAttribute,
            _memberIndent + GeneratedCode.GeneratedCodeAttribute(nameof(ProtobufCodeGenerator), version),
            _memberIndent + signature,
            _memberIndent + "{",
        };

        if (isReferenceType)
        {
            // 根级 null 被 protobuf-net 写成空载荷，读回时构造的是「字段为 null 的 surrogate」而不是 null。
            // 字段现在一律可空（文本形是 string?，值类型基元是 T?），故两种形态都还原成 null。
            lines.Add(_bodyIndent + "if (surrogate is null || surrogate.Value is null)");
            lines.Add(_bodyIndent + "{");
            lines.Add(_bodyIndent + "    return null;");
            lines.Add(_bodyIndent + "}");
            lines.Add(string.Empty);
        }
        else if (idInfo.IsReferenceTypePrimitive)
        {
            // 值类型 Id 包装引用类型基元（string 或自定义 class）时，基元可以是 null，写侧已把它写成空载荷。
            // 读侧必须还原成 default（基元为 null 的 Id），否则「写出去就读不回来」。
            // 与 STJ / Newtonsoft 同一口径：它们按基元是否为引用类型判定，与 Id 是 struct 还是 class 无关。
            // 不走 TryCreate：写侧写出的正是 default 值，走 TryCreate 会让「验证器拒绝 null」的 Id 再次写读不对称。
            lines.Add(_bodyIndent + "if (surrogate.Value is null)");
            lines.Add(_bodyIndent + "{");
            lines.Add(_bodyIndent + "    return default;");
            lines.Add(_bodyIndent + "}");
            lines.Add(string.Empty);
        }

        lines.Add(_bodyIndent + $"if ({condition})");
        lines.Add(_bodyIndent + "{");
        lines.Add(_bodyIndent + "    return id;");
        lines.Add(_bodyIndent + "}");
        lines.Add(string.Empty);
        lines.Add(_bodyIndent + $"throw new global::ProtoBuf.ProtoException(\"Cannot convert the protobuf value to a {idInfo.Name}.\");");
        lines.Add(_memberIndent + "}");

        return string.Join(GeneratedCode.NewLine, lines);
    }
}
