using System.Text;

namespace Len.StronglyTypedId.Generators;

/// <summary>
/// 为强类型 Id 生成 HotChocolate 的 <c>ScalarType&lt;TId&gt;</c> 与 <c>IRequestExecutorBuilder</c> 装配入口。
/// </summary>
/// <remarks>
/// scalar 放在 <c>StronglyTypedIds</c> 里而非嵌进 Id 名下：HotChocolate 的 scalar 只能按类型注册，
/// 放在生成的类里，引用程序集里的 Id 也能拿到 scalar。
/// <para>
/// 表示口径与 JSON 一致，这样同一个 Id 在 REST 与 GraphQL 两侧不会出现两种形态：
/// 文本基元（含带前缀的 Id）→ GraphQL <c>String</c>；整数与枚举 → <c>Int</c>；浮点 → <c>Float</c>。
/// 枚举按底层整数而非成员名，与 STJ 的默认行为一致。
/// </para>
/// </remarks>
internal class HotChocolateCodeGenerator : IIntegrationCodeGenerator
{
    /// <summary>
    /// 解析文本时使用的不变文化。GraphQL 的标量文本与区域性无关。
    /// </summary>
    private const string InvariantCulture = GeneratedCode.InvariantCulture;

    /// <summary>
    /// 生成代码里引用 <c>IValueNode</c> 的完整表达式。
    /// </summary>
    private const string ValueNode = "global::HotChocolate.Language.IValueNode";

    /// <summary>
    /// 生成代码里引用 <c>SerializationException</c> 的完整表达式。
    /// </summary>
    private const string SerializationException = "global::HotChocolate.Types.SerializationException";

    /// <summary>
    /// 生成代码里引用 <c>IRequestExecutorBuilder</c> 的完整表达式。
    /// </summary>
    private const string RequestExecutorBuilder = "global::HotChocolate.Execution.Configuration.IRequestExecutorBuilder";

    /// <summary>
    /// 注册入口产物的文件头前导：让 <c>TryAddSingleton</c> / <c>BindRuntimeType</c> 以扩展方法语法书写。
    /// </summary>
    /// <remarks>
    /// <c>TryAddSingleton</c> 在 <c>Microsoft.Extensions.DependencyInjection.Extensions</c>，
    /// <c>BindRuntimeType</c> 在 <c>Microsoft.Extensions.DependencyInjection</c>，两者都要引进来才能用点号语法；
    /// 否则只能写全限定静态调用，与 HotChocolate 自己的 <c>AddGraphQLServer()</c> 风格不一致。
    /// </remarks>
    private const string RegistrationPreamble =
        "using Microsoft.Extensions.DependencyInjection;" + GeneratedCode.NewLine
            + "using Microsoft.Extensions.DependencyInjection.Extensions;";

    /// <summary>
    /// 成员体内语句的缩进（产物中为 8 空格）。
    /// </summary>
    private static readonly string _statementIndent = new(' ', 8);

    /// <summary>
    /// 成员体内块语句的缩进（产物中为 12 空格）。
    /// </summary>
    private static readonly string _nestedIndent = new(' ', 12);

    /// <summary>
    /// 注册入口体内语句的缩进（产物中为 8 空格）。
    /// </summary>
    private static readonly string _registrationIndent = new(' ', 8);

    internal static readonly IIntegrationCodeGenerator Instance = new HotChocolateCodeGenerator();

    public int Order { get; } = 12;

    public void Generate(
        DiscoveredIds discoveredIds,
        ImmutableArray<ModuleInfo> modules,
        SourceProductionContext context,
        string version)
    {
        var stronglyTypedIds = discoveredIds.ExternalFor(IntegrationFlags.HotChocolate).ToList();

        // 一个 Id 都没有时不产出空壳装配入口：与其余集成一致，
        // 也让 Integrations = None 能真正关掉全部 HotChocolate 产物。
        if (stronglyTypedIds.Count == 0)
        {
            return;
        }

        var ambiguousNames = IntegrationNaming.GetAmbiguousNames(stronglyTypedIds);
        var collidingNames = IntegrationNaming.GetCollidingNames(stronglyTypedIds, ambiguousNames, "HotChocolateType");
        var collidingGraphQlNames = IntegrationNaming.GetCollidingGraphQlTypeNames(stronglyTypedIds, ambiguousNames);

        var registrations = new StringBuilder();

        foreach (var idInfo in stronglyTypedIds)
        {
            var scalarName = IntegrationNaming.GetTypeName(idInfo, ambiguousNames, "HotChocolateType", collidingNames);

            // GraphQL 的类型名有自己的一张全局表，与 C# 命名空间无关：两个不同命名空间的 OrderId
            // 即使 C# 类名已消歧，注册到 schema 时仍会撞成同一个名字，构建期报
            // 「An item with the same key has already been added. Key: OrderId」。
            // 故 GraphQL 名单独消歧，与 C# 类名分开计算。
            var graphQlName = IntegrationNaming.GetGraphQlTypeName(idInfo, ambiguousNames, collidingGraphQlNames);

            // 整数与浮点字面量经 CreateNode 的 AsMemory() 构造，那是 System.MemoryExtensions 的扩展方法，
            // 必须补 using；字符串字面量走 StringValueNode(string) 重载，不需要。
            var scalarCode = GeneratedCode.Header(
                "Len.StronglyTypedId",
                NeedsSystemUsing(idInfo) ? GeneratedCode.SystemUsing : null) + $$"""
                internal static partial class StronglyTypedIds
                {
                    /// <summary>
                    /// 用 GraphQL 表示 <see cref="{{idInfo.FullName}}"/>。
                    /// </summary>
                    {{GeneratedCode.CompilerGeneratedAttribute}}
                    {{GeneratedCode.GeneratedCodeAttribute(nameof(HotChocolateCodeGenerator), version)}}
                    internal sealed class {{scalarName}} : global::HotChocolate.Types.ScalarType<{{idInfo.FullyQualifiedName}}>
                    {
                        /// <summary>
                        /// 创建 scalar。
                        /// </summary>
                        {{GeneratedCode.CompilerGeneratedAttribute}}
                        {{GeneratedCode.GeneratedCodeAttribute(nameof(HotChocolateCodeGenerator), version)}}
                        public {{scalarName}}()
                            : base("{{graphQlName}}")
                        {
                        }

                {{GetIsInstanceOfTypeMember(idInfo, version)}}

                {{GetParseLiteralMember(idInfo, version)}}

                {{GetParseValueMember(idInfo, version)}}

                {{GetParseResultMember(idInfo, version)}}

                {{GetTrySerializeMember(idInfo, version)}}

                {{GetTryDeserializeMember(idInfo, version)}}

                {{GetSerializeMember(idInfo, version)}}

                {{GetCreateNodeMember(idInfo, version)}}
                    }
                }
                """;

            GeneratedCode.AddGeneratedSource(context, $"{idInfo.HintName}.HotChocolate.g.cs", scalarCode);

            registrations.Append($"{GeneratedCode.NewLine}{_registrationIndent}builder.Services.TryAddSingleton(typeof({scalarName}), typeof({scalarName}));{GeneratedCode.NewLine}");
            registrations.Append($"{_registrationIndent}builder.BindRuntimeType<{idInfo.FullyQualifiedName}, {scalarName}>();{GeneratedCode.NewLine}");
        }

        // 装配入口固定落在 Len.StronglyTypedId 命名空间（与其余集成的注册入口一致）：
        // Id 可以分散在任意命名空间，注册入口只该有一个。
        // 写成扩展方法而非静态方法：IRequestExecutorBuilder 的惯例就是链式扩展（AddGraphQLServer /
        // AddQueryType 都是），调用方因此能把它接在链上，不必先存一个局部变量。
        var registrationCode = GeneratedCode.Header("Len.StronglyTypedId", RegistrationPreamble) + $$"""
                internal static partial class StronglyTypedIds
                {
                    /// <summary>
                    /// 注册生成的 GraphQL scalar，并让 HotChocolate 用它们表示对应的强类型 Id。
                    /// </summary>
                    /// <param name="builder">要扩展的 GraphQL 服务器构建器。</param>
                    /// <returns>同一个 <paramref name="builder"/>，便于链式调用。</returns>
                    /// <remarks>
                    /// 幂等：<c>TryAddSingleton</c> 只在该 scalar 尚未注册时添加，重复调用不会留下第二条注册；
                    /// <c>BindRuntimeType</c> 重复调用也不会让 schema 出现重复条目（实测 schema 逐字符相同）。
                    /// <para>
                    /// 必须用 <c>BindRuntimeType</c> 而不是只 <c>AddType</c> —— 后者不会阻止 HotChocolate 把 Id
                    /// 推断成对象类型，schema 构建时会报「The name ... was already registered by another type」。
                    /// </para>
                    /// </remarks>
                    {{GeneratedCode.CompilerGeneratedAttribute}}
                    {{GeneratedCode.GeneratedCodeAttribute(nameof(HotChocolateCodeGenerator), version)}}
                    public static {{RequestExecutorBuilder}} AddStronglyTypedIds(this {{RequestExecutorBuilder}} builder)
                    {{{registrations}}
                        return builder;
                    }
                }
                """;

        GeneratedCode.AddGeneratedSource(context, "StronglyTypedIds.HotChocolate.g.cs", registrationCode);
    }

    /// <summary>
    /// 取 <c>IsInstanceOfType</c> 成员的完整文本（含签名与成员级缩进）。
    /// </summary>
    /// <remarks>
    /// 只接受与本 Id 表示形态一致的字面量：字符串基元的 Id 收到 <c>IntValueNode</c> 时报「类型不匹配」，
    /// 而不是把它当成文本再解析失败。
    /// </remarks>
    private static string GetIsInstanceOfTypeMember(StronglyTypedIdInfo idInfo, string version)
        => string.Join(
            GeneratedCode.NewLine,
            _statementIndent + "/// <inheritdoc/>",
            _statementIndent + GeneratedCode.CompilerGeneratedAttribute,
            _statementIndent + GeneratedCode.GeneratedCodeAttribute(nameof(HotChocolateCodeGenerator), version),
            _statementIndent + $"public override bool IsInstanceOfType({ValueNode} valueSyntax) =>",
            _nestedIndent + $"valueSyntax is {GetLiteralNodeType(idInfo)};");

    /// <summary>
    /// 取 <c>ParseLiteral</c> 成员的完整文本（含签名与成员级缩进）。
    /// </summary>
    /// <remarks>
    /// 失败必须抛 HotChocolate 的 <see cref="SerializationException"/>：框架把它转成带位置的 GraphQL 错误，
    /// 而 <see cref="FormatException"/> 会变成「Unexpected Execution Error」，客户端拿不到可读消息。
    /// </remarks>
    private static string GetParseLiteralMember(StronglyTypedIdInfo idInfo, string version)
        => string.Join(
            GeneratedCode.NewLine,
            _statementIndent + "/// <inheritdoc/>",
            _statementIndent + GeneratedCode.CompilerGeneratedAttribute,
            _statementIndent + GeneratedCode.GeneratedCodeAttribute(nameof(HotChocolateCodeGenerator), version),
            _statementIndent + $"public override object ParseLiteral({ValueNode} valueSyntax)",
            _statementIndent + "{",
            _nestedIndent + $"if (valueSyntax is {GetLiteralNodeType(idInfo)} literal",
            _nestedIndent + $"    && {idInfo.FullyQualifiedName}.TryParse(literal.Value, {InvariantCulture}, out var id))",
            _nestedIndent + "{",
            _nestedIndent + "    return id;",
            _nestedIndent + "}",
            string.Empty,
            _nestedIndent + $"throw new {SerializationException}($\"Cannot parse the given value as a {idInfo.Name}.\", this);",
            _statementIndent + "}");

    /// <summary>
    /// 取 <c>ParseValue</c> 成员的完整文本（含签名与成员级缩进）。
    /// </summary>
    private static string GetParseValueMember(StronglyTypedIdInfo idInfo, string version)
        => string.Join(
            GeneratedCode.NewLine,
            _statementIndent + "/// <inheritdoc/>",
            _statementIndent + GeneratedCode.CompilerGeneratedAttribute,
            _statementIndent + GeneratedCode.GeneratedCodeAttribute(nameof(HotChocolateCodeGenerator), version),
            _statementIndent + $"public override {ValueNode} ParseValue(object? runtimeValue) =>",
            _nestedIndent + $"CreateNode(ToText(Serialize(({idInfo.FullyQualifiedName})runtimeValue!)));");

    /// <summary>
    /// 取 <c>ParseResult</c> 成员的完整文本（含签名与成员级缩进）。
    /// </summary>
    /// <remarks>
    /// 变量路径上 HotChocolate 传进来的是 <c>TrySerialize</c> 的返回值（已序列化的形态），不是运行时值，
    /// 因此这里必须同时接受两种形态：Id 本身（直接调用路径）与已序列化的值。
    /// <para>
    /// 引用类型基元为 <see langword="null"/> 时，<c>Serialize</c> 会给出空串；这里改走
    /// <c>NullValueNode</c>，与 <c>TrySerialize</c> 返回 <see langword="false"/> 的结论一致。
    /// </para>
    /// </remarks>
    private static string GetParseResultMember(StronglyTypedIdInfo idInfo, string version)
    {
        var idArm = idInfo.IsReferenceTypePrimitive && !idInfo.HasPrefix
            ? _nestedIndent + $"    {idInfo.FullyQualifiedName} {{ Value: null }} => global::HotChocolate.Language.NullValueNode.Default,"
            : null;

        var lines = new List<string>
        {
            _statementIndent + "/// <inheritdoc/>",
            _statementIndent + GeneratedCode.CompilerGeneratedAttribute,
            _statementIndent + GeneratedCode.GeneratedCodeAttribute(nameof(HotChocolateCodeGenerator), version),
            _statementIndent + $"public override {ValueNode} ParseResult(object? resultValue) =>",
            _nestedIndent + "resultValue switch",
            _nestedIndent + "{",
            _nestedIndent + "    null => global::HotChocolate.Language.NullValueNode.Default,",
        };

        if (idArm is not null)
        {
            lines.Add(idArm);
        }

        lines.Add(_nestedIndent + $"    {idInfo.FullyQualifiedName} id => CreateNode(ToText(Serialize(id))),");
        lines.Add(_nestedIndent + "    _ => CreateNode(ToText(resultValue)),");
        lines.Add(_nestedIndent + "};");

        return string.Join(GeneratedCode.NewLine, lines);
    }

    /// <summary>
    /// 取 <c>TrySerialize</c> 成员的完整文本（含签名与成员级缩进）。
    /// </summary>
    /// <remarks>
    /// 返回的必须是 HotChocolate 能原生写出的 CLR 值（<see cref="string"/> / 整型 / 浮点），
    /// 否则框架会报「Unable to infer or resolve a schema type」。引用类型 Id 的 <see langword="null"/>
    /// 返回 <see langword="false"/>，由框架写出 GraphQL <c>null</c>。
    /// <para>
    /// 引用类型基元为 <see langword="null"/> 时同样返回 <see langword="false"/>：文本形的
    /// <c>ToString()</c> 会给出空串，而 System.Text.Json / Newtonsoft / MessagePack / BSON 都写
    /// <c>null</c>，两侧形态必须一致。带前缀的 Id 例外 —— 它的文本是「前缀 + 空串」，永远非 null。
    /// </para>
    /// </remarks>
    private static string GetTrySerializeMember(StronglyTypedIdInfo idInfo, string version)
    {
        var lines = new List<string>
        {
            _statementIndent + "/// <inheritdoc/>",
            _statementIndent + GeneratedCode.CompilerGeneratedAttribute,
            _statementIndent + GeneratedCode.GeneratedCodeAttribute(nameof(HotChocolateCodeGenerator), version),
            _statementIndent + "public override bool TrySerialize(object? runtimeValue, out object? resultValue)",
            _statementIndent + "{",
            _nestedIndent + $"if (runtimeValue is {idInfo.FullyQualifiedName} id)",
            _nestedIndent + "{",
        };

        if (idInfo.IsReferenceTypePrimitive && !idInfo.HasPrefix)
        {
            lines.Add(_nestedIndent + "    if (id.Value is null)");
            lines.Add(_nestedIndent + "    {");
            lines.Add(_nestedIndent + "        resultValue = null;");
            lines.Add(_nestedIndent + "        return false;");
            lines.Add(_nestedIndent + "    }");
            lines.Add(string.Empty);
        }

        lines.Add(_nestedIndent + "    resultValue = Serialize(id);");
        lines.Add(_nestedIndent + "    return true;");
        lines.Add(_nestedIndent + "}");
        lines.Add(string.Empty);
        lines.Add(_nestedIndent + "resultValue = null;");
        lines.Add(_nestedIndent + "return false;");
        lines.Add(_statementIndent + "}");

        return string.Join(GeneratedCode.NewLine, lines);
    }

    /// <summary>
    /// 取 <c>TryDeserialize</c> 成员的完整文本（含签名与成员级缩进）。
    /// </summary>
    /// <remarks>
    /// 必须同时接受 <see cref="string"/> 与 <c>System.Text.Json.JsonElement</c>：变量经真实 HTTP
    /// 管道进来时是后者，经 <c>QueryRequestBuilder</c> 直接构造时是前者。只处理 <see cref="string"/>
    /// 的实现在单元测试里能过，上线后每个变量都会失败。
    /// </remarks>
    private static string GetTryDeserializeMember(StronglyTypedIdInfo idInfo, string version)
        => string.Join(
            GeneratedCode.NewLine,
            _statementIndent + "/// <inheritdoc/>",
            _statementIndent + GeneratedCode.CompilerGeneratedAttribute,
            _statementIndent + GeneratedCode.GeneratedCodeAttribute(nameof(HotChocolateCodeGenerator), version),
            _statementIndent + "public override bool TryDeserialize(object? resultValue, out object? runtimeValue)",
            _statementIndent + "{",
            _nestedIndent + "var text = resultValue switch",
            _nestedIndent + "{",
            _nestedIndent + "    null => null,",
            _nestedIndent + "    string value => value,",
            _nestedIndent + "    global::System.Text.Json.JsonElement { ValueKind: global::System.Text.Json.JsonValueKind.String } element => element.GetString(),",
            _nestedIndent + "    global::System.Text.Json.JsonElement { ValueKind: global::System.Text.Json.JsonValueKind.Number } element => element.GetRawText(),",
            _nestedIndent + $"    _ => global::System.Convert.ToString(resultValue, {InvariantCulture}),",
            _nestedIndent + "};",
            string.Empty,
            _nestedIndent + $"if (text is not null && {idInfo.FullyQualifiedName}.TryParse(text, {InvariantCulture}, out var id))",
            _nestedIndent + "{",
            _nestedIndent + "    runtimeValue = id;",
            _nestedIndent + "    return true;",
            _nestedIndent + "}",
            string.Empty,
            _nestedIndent + "runtimeValue = null;",
            _nestedIndent + "return false;",
            _statementIndent + "}");

    /// <summary>
    /// 取 <c>Serialize</c> 辅助成员的完整文本（含签名与成员级缩进）。
    /// </summary>
    /// <remarks>
    /// 返回值必须是 HotChocolate 能原生写出的 CLR 值。超出 <see cref="long"/> 的整数基元与
    /// <c>BigInteger</c> 只能按文本写出，否则 <c>IntValueNode</c> 会溢出。
    /// </remarks>
    private static string GetSerializeMember(StronglyTypedIdInfo idInfo, string version)
        => string.Join(
            GeneratedCode.NewLine,
            _statementIndent + "/// <summary>",
            _statementIndent + "/// 把 Id 转成 GraphQL 能原生写出的值。",
            _statementIndent + "/// </summary>",
            _statementIndent + GeneratedCode.CompilerGeneratedAttribute,
            _statementIndent + GeneratedCode.GeneratedCodeAttribute(nameof(HotChocolateCodeGenerator), version),
            _statementIndent + $"private static object Serialize({idInfo.FullyQualifiedName} id) =>",
            _nestedIndent + $"{GetSerializeExpression(idInfo)};");

    /// <summary>
    /// 取 <c>CreateNode</c> 辅助成员的完整文本（含签名与成员级缩进）。
    /// </summary>
    /// <remarks>
    /// <c>StringValueNode</c> 只有 <see cref="string"/> 重载；<c>IntValueNode</c> / <c>FloatValueNode</c>
    /// 一律经 UTF-8 字节构造：它们的数值重载最宽只到 <c>ulong</c>，而 <c>Int128</c> / <c>UInt128</c> /
    /// <c>BigInteger</c> 会超出，字节重载没有这个上限。
    /// </remarks>
    private static string GetCreateNodeMember(StronglyTypedIdInfo idInfo, string version)
    {
        var nodeType = GetLiteralNodeType(idInfo);
        var nodeArguments = GetLiteralKind(idInfo) switch
        {
            LiteralKind.String => "text",
            LiteralKind.Integer => "global::System.Text.Encoding.UTF8.GetBytes(text).AsMemory()",
            _ => "global::System.Text.Encoding.UTF8.GetBytes(text).AsMemory(), global::HotChocolate.Language.FloatFormat.FixedPoint",
        };

        return string.Join(
            GeneratedCode.NewLine,
            _statementIndent + "/// <summary>",
            _statementIndent + "/// 把文本转成 GraphQL 字面量节点。",
            _statementIndent + "/// </summary>",
            _statementIndent + GeneratedCode.CompilerGeneratedAttribute,
            _statementIndent + GeneratedCode.GeneratedCodeAttribute(nameof(HotChocolateCodeGenerator), version),
            _statementIndent + $"private static {ValueNode} CreateNode(string text) =>",
            _nestedIndent + $"new {nodeType}({nodeArguments});",
            string.Empty,
            _statementIndent + "/// <summary>",
            _statementIndent + "/// 把已序列化的值转成文本。",
            _statementIndent + "/// </summary>",
            _statementIndent + GeneratedCode.CompilerGeneratedAttribute,
            _statementIndent + GeneratedCode.GeneratedCodeAttribute(nameof(HotChocolateCodeGenerator), version),
            _statementIndent + "private static string ToText(object value) =>",
            _nestedIndent + $"value as string ?? global::System.Convert.ToString(value, {InvariantCulture}) ?? string.Empty;");
    }

    /// <summary>
    /// 取把 Id 转成「HotChocolate 能原生写出的值」的表达式。
    /// </summary>
    private static string GetSerializeExpression(StronglyTypedIdInfo idInfo)
        => GetLiteralKind(idInfo) switch
        {
            LiteralKind.String => "id.ToString()",
            LiteralKind.Integer => GetIntegerSerializeExpression(idInfo),
            _ => GetFloatingSerializeExpression(idInfo),
        };

    /// <summary>
    /// 取整数基元的序列化表达式。
    /// </summary>
    /// <remarks>
    /// 只有 <c>byte</c> … <c>ulong</c> 与枚举会走到这里：<c>Int128</c> / <c>UInt128</c> / <c>BigInteger</c>
    /// 的字面量种类是 <see cref="LiteralKind.String"/>（见 <see cref="GetLiteralKind"/>），不经此处。
    /// </remarks>
    private static string GetIntegerSerializeExpression(StronglyTypedIdInfo idInfo)
        => idInfo.IsEnum
            ? $"({idInfo.EnumUnderlyingTypeName})id.Value"
            : "id.Value";

    /// <summary>
    /// 取浮点基元的序列化表达式。
    /// </summary>
    /// <remarks>
    /// <c>Half</c> 没有对应的 GraphQL 表示，先转成 <see cref="double"/>。
    /// </remarks>
    private static string GetFloatingSerializeExpression(StronglyTypedIdInfo idInfo)
        => StronglyTypedIdInfo.GetPrimitiveIdTypeShortName(idInfo.PrimitiveIdTypeName) == "Half"
            ? "(double)id.Value"
            : "id.Value";

    /// <summary>
    /// 取该 Id 的 GraphQL 字面量种类。
    /// </summary>
    /// <remarks>
    /// 口径与 JSON / OpenAPI 一致：带前缀时对外是规范文本形，数字基元也一样。直接复用
    /// <see cref="PrimitiveIdSchemaMap.Resolve"/>，避免 GraphQL 与 OpenAPI 两处各写一份基元分类而漂移。
    /// <para>
    /// <c>Int128</c> / <c>UInt128</c> 是例外：OpenAPI 那边它们是 JSON 数字（STJ 原生支持），但 HotChocolate
    /// 的序列化器不认识这两个类型，<c>TrySerialize</c> 只能返回文本，写出来是字符串。字面量若仍是
    /// <c>IntValueNode</c>，就会出现「输入按 Int、输出是 String」的不对称 —— 客户端把服务端返回的
    /// <c>"42"</c> 原样传回会被拒。故这里把三者统一成字符串，输入输出对称。
    /// </para>
    /// </remarks>
    private static LiteralKind GetLiteralKind(StronglyTypedIdInfo idInfo)
        => IsTextualBigInteger(idInfo)
            ? LiteralKind.String
            : PrimitiveIdSchemaMap.Resolve(idInfo).Type switch
            {
                PrimitiveIdSchemaMap.Integer => LiteralKind.Integer,
                PrimitiveIdSchemaMap.Number => LiteralKind.Floating,
                _ => LiteralKind.String,
            };

    /// <summary>
    /// 判断该 Id 的基元是否属于「GraphQL 里只能按文本承载」的大整数。
    /// </summary>
    /// <remarks>
    /// 带前缀的 Id 本来就是字符串（<see cref="PrimitiveIdSchemaMap.Resolve"/> 已处理），此处不必重复判断。
    /// </remarks>
    private static bool IsTextualBigInteger(StronglyTypedIdInfo idInfo)
        => !idInfo.HasPrefix
            && StronglyTypedIdInfo.GetPrimitiveIdTypeShortName(idInfo.PrimitiveIdTypeName) is "Int128" or "UInt128" or "BigInteger";

    /// <summary>
    /// 判断该 Id 的产物是否需要 <c>using global::System;</c>。
    /// </summary>
    /// <remarks>
    /// 只有整数与浮点字面量会经 <c>CreateNode</c> 的 <c>Encoding.UTF8.GetBytes(text).AsMemory()</c> 构造，
    /// 而 <c>AsMemory</c> 是 <see cref="System.MemoryExtensions"/> 上的扩展方法。字符串字面量走
    /// <c>StringValueNode(string)</c> 重载，不需要 using。
    /// </remarks>
    private static bool NeedsSystemUsing(StronglyTypedIdInfo idInfo)
        => GetLiteralKind(idInfo) is LiteralKind.Integer or LiteralKind.Floating;

    /// <summary>
    /// 取该 Id 对应的 GraphQL 字面量节点类型全名。
    /// </summary>
    private static string GetLiteralNodeType(StronglyTypedIdInfo idInfo)
        => GetLiteralKind(idInfo) switch
        {
            LiteralKind.String => "global::HotChocolate.Language.StringValueNode",
            LiteralKind.Integer => "global::HotChocolate.Language.IntValueNode",
            _ => "global::HotChocolate.Language.FloatValueNode",
        };

    /// <summary>
    /// GraphQL 字面量种类。
    /// </summary>
    private enum LiteralKind
    {
        /// <summary>
        /// <c>StringValueNode</c>。
        /// </summary>
        String,

        /// <summary>
        /// <c>IntValueNode</c>。
        /// </summary>
        Integer,

        /// <summary>
        /// <c>FloatValueNode</c>。
        /// </summary>
        Floating,
    }
}
