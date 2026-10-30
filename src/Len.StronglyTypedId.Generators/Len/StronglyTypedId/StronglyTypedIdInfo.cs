namespace Len.StronglyTypedId;

/// <summary>
/// 描述一个由源码生成器发现的强类型 Id 类型，供各代码生成器拼装文本模板使用。
/// </summary>
internal readonly record struct StronglyTypedIdInfo
{
    private const string GlobalPrefix = "global::";

    /// <summary>
    /// 运行时 <c>IStronglyTypedId&lt;TSelf, TPrimitiveId&gt;</c> 的简单名（生成器不引用运行时程序集，只能按名字匹配）。
    /// </summary>
    internal const string InterfaceName = "IStronglyTypedId";

    /// <summary>
    /// 运行时 <c>Len.StronglyTypedId</c> 命名空间的显示名（含 <c>global::</c> 前缀）。
    /// </summary>
    internal const string InterfaceNamespace = "global::Len.StronglyTypedId";

    /// <summary>
    /// System.Text.Json 转换器的内嵌类型名。
    /// </summary>
    private const string SystemTextJsonConverterTypeName = "SystemTextJsonConverter";

    /// <summary>
    /// Newtonsoft.Json 转换器的内嵌类型名。
    /// </summary>
    private const string NewtonsoftJsonConverterTypeName = "NewtonsoftJsonConverter";

    /// <summary>
    /// protobuf-net 契约特性的全限定显示名（含 <c>global::</c> 前缀）。
    /// </summary>
    private const string ProtoContractAttributeName = "global::ProtoBuf.ProtoContractAttribute";

    /// <summary>
    /// attribute 中验证器方法的命名实参名。
    /// </summary>
    internal const string ValidatorArgumentName = "Validator";

    /// <summary>
    /// attribute 中归一化方法的命名实参名。
    /// </summary>
    internal const string NormalizerArgumentName = "Normalizer";

    /// <summary>
    /// attribute 中 <c>TypeConverter</c> 开关的命名实参名。
    /// </summary>
    internal const string TypeConverterArgumentName = "TypeConverter";

    /// <summary>
    /// attribute 中验证失败异常类型的命名实参名。
    /// </summary>
    internal const string ExceptionTypeArgumentName = "ExceptionType";

    /// <summary>
    /// attribute 中 JSON 形态的命名实参名。
    /// </summary>
    internal const string JsonShapeArgumentName = "JsonShape";

    /// <summary>
    /// attribute 中规范文本形前缀的命名实参名。
    /// </summary>
    internal const string PrefixArgumentName = "Prefix";

    /// <summary>
    /// attribute 中前缀读入策略的命名实参名。
    /// </summary>
    internal const string PrefixReadModeArgumentName = "PrefixReadMode";

    /// <summary>
    /// attribute 中集成开关的命名实参名。
    /// </summary>
    internal const string IntegrationsArgumentName = "Integrations";

    /// <summary>
    /// attribute 中「验证失败消息是否隐去原始值」的命名实参名。
    /// </summary>
    internal const string RedactValidationMessageArgumentName = "RedactValidationMessage";

    /// <summary>
    /// <c>PrefixReadMode.Strict</c> 的底层值；命名实参在元数据里存的是枚举值而非成员名。
    /// </summary>
    private const int StrictPrefixReadModeValue = 1;

    /// <summary>
    /// <c>JsonShape.Object</c> 的底层值；命名实参在元数据里存的是枚举值而非成员名。
    /// </summary>
    private const int ObjectJsonShapeValue = 1;

    /// <summary>
    /// BCL 接口所属命名空间的显示名（含 <c>global::</c> 前缀）。
    /// </summary>
    private const string SystemNamespace = "global::System";

    /// <summary>
    /// <c>System.IFormattable</c> 的简单名。
    /// </summary>
    private const string FormattableName = "IFormattable";

    /// <summary>
    /// <c>System.ISpanFormattable</c> 的简单名。
    /// </summary>
    private const string SpanFormattableName = "ISpanFormattable";

    /// <summary>
    /// <c>System.IUtf8SpanParsable&lt;TSelf&gt;</c> 的简单名。
    /// </summary>
    private const string Utf8SpanParsableName = "IUtf8SpanParsable";

    /// <summary>
    /// <c>System.IUtf8SpanFormattable</c> 的简单名。
    /// </summary>
    private const string Utf8SpanFormattableName = "IUtf8SpanFormattable";

    /// <summary>
    /// <c>System.Numerics.BigInteger</c> 的显示名（它不在 CoreLib，故只能按名字识别）。
    /// </summary>
    private const string BigIntegerTypeName = "System.Numerics.BigInteger";

    /// <summary>
    /// Newtonsoft.Json 的 <c>JsonWriter.WriteValue</c> 不认识的基元：它们没有对应的
    /// <c>PrimitiveTypeCode</c>，写出去会抛 <c>JsonWriterException</c>。
    /// </summary>
    private static readonly ImmutableHashSet<string> _newtonsoftTextPrimitives = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "System.Int128",
        "System.UInt128",
        "System.Half",
        "System.DateOnly",
        "System.TimeOnly");

    /// <summary>
    /// 二进制序列化（MessagePack / BSON）走规范文本形的基元。
    /// </summary>
    /// <remarks>
    /// BSON 3.x 至今没有 <c>Int128</c> / <c>UInt128</c> / <c>BigInteger</c> 的序列化器（写出来是空文档、读回 0），
    /// 那边仍然必需；MessagePack 3.x 已能原生编码三者，继续用文本形只是为了兼容 2.x 的载荷，
    /// 改掉会变更线上格式。
    /// </remarks>
    private static readonly ImmutableHashSet<string> _binaryTextPrimitives = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "System.Numerics.BigInteger",
        "System.Int128",
        "System.UInt128");

    /// <summary>
    /// protobuf-net 没有原生表示、只能按规范文本承载的基元。
    /// </summary>
    /// <remarks>
    /// 实测 protobuf-net 3.2.30：<c>Int128</c> / <c>UInt128</c> / <c>Half</c> / <c>DateTimeOffset</c> /
    /// <c>BigInteger</c> 一律抛「Type is not expected, and no contract can be inferred」。
    /// 其余基元（含 <c>DateOnly</c> / <c>TimeOnly</c> / <c>decimal</c> / <c>char</c> / 枚举）都有原生表示。
    /// <para>
    /// 自定义基元同样走文本形：protobuf-net 无法推断它们（实测 Cysharp <c>Ulid</c> 直接抛
    /// 「No serializer defined for type」）。
    /// </para>
    /// </remarks>
    private static readonly ImmutableHashSet<string> _protobufTextPrimitives = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "System.Numerics.BigInteger",
        "System.Int128",
        "System.UInt128",
        "System.Half",
        "System.DateTimeOffset");

    /// <summary>
    /// 判断给定接口符号是否为运行时的 <c>IStronglyTypedId&lt;TSelf, TPrimitiveId&gt;</c>。
    /// </summary>
    internal static bool IsStronglyTypedIdInterface(INamedTypeSymbol @interface)
        => @interface.Name == InterfaceName
            && @interface.TypeArguments.Length == 2
            && @interface.ContainingNamespace.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == InterfaceNamespace;

    public StronglyTypedIdInfo(ITypeSymbol type, StronglyTypedIdDefaults defaults = default)
    {
        // 非具名类型（数组、指针、类型参数等）没有 ContainingNamespace。
        var containingNamespace = type.ContainingNamespace
            ?? throw new InvalidOperationException($"无法从类型“{type.ToDisplayString()}”解析强类型 Id 信息：该类型不是具名类型。");

        FullyQualifiedNamespace = containingNamespace.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        Namespace = FullyQualifiedNamespace[GlobalPrefix.Length..];
        Name = type.Name;
        FullyQualifiedName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        FullName = type.ToDisplayString();
        // readonly 必须写在 record 关键字之前，且各 partial 段必须一致，否则合并后编译失败。
        TypeKindSuffix = type.TypeKind == TypeKind.Struct ? " struct" : null;
        ReadOnlyRecordModifier = type.TypeKind == TypeKind.Struct && type.IsReadOnly ? "readonly " : string.Empty;

        var containingTypes = GetContainingTypeDeclarations(type);
        ContainingTypeDeclarations = containingTypes.Declarations;
        NestingDepth = containingTypes.Depth;
        IsInGenericContainer = containingTypes.IsInGenericContainer;
        HintName = IdentifierEscaping.SanitizeHintName(FullName);

        var primitiveIdType = GetPrimitiveIdType(type);

        PrimitiveIdTypeName = primitiveIdType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        IsStringPrimitive = primitiveIdType.SpecialType == SpecialType.System_String;
        IsCharPrimitive = primitiveIdType.SpecialType == SpecialType.System_Char;
        IsEnum = primitiveIdType.TypeKind == TypeKind.Enum;
        IsFlagsEnum = IsEnum && HasFlagsAttribute(primitiveIdType);
        FlagsEnumUndefinedBitsMask = IsFlagsEnum ? GetFlagsEnumUndefinedBitsMask(primitiveIdType) : null;
        EnumUnderlyingTypeName = IsEnum && primitiveIdType is INamedTypeSymbol { EnumUnderlyingType: { } enumUnderlying }
            ? enumUnderlying.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            : null;
        // 枚举继承的 IFormattable / ISpanFormattable 已被标记 [Obsolete]：透出会 CS0618，不透出又会 CS0535。
        IsFormattable = ImplementsInterface(primitiveIdType, FormattableName) && !IsEnum;
        IsSpanFormattable = ImplementsInterface(primitiveIdType, SpanFormattableName) && !IsEnum;
        // BigInteger 实现了 IUtf8SpanParsable 却把 TryParse 做成显式接口实现，按类型名调用会 CS1501。
        IsUtf8SpanParsable = ImplementsInterface(primitiveIdType, Utf8SpanParsableName)
            && HasPublicStaticTryParse(primitiveIdType, SpecialType.System_Byte);
        IsUtf8SpanFormattable = ImplementsInterface(primitiveIdType, Utf8SpanFormattableName);
        CharTryFormatKind = GetTryFormatKind(primitiveIdType, SpecialType.System_Char);
        Utf8TryFormatKind = GetTryFormatKind(primitiveIdType, SpecialType.System_Byte);
        // System.Text.Json 没有 BigInteger 的内置转换器：JsonSerializer 会把它当普通对象序列化成
        // {"IsPowerOfTwo":…,"Sign":1}，读回来又是 default，静默丢数据。故这类基元改走规范文本形。
        IsBigIntegerPrimitive = primitiveIdType.ToDisplayString() == BigIntegerTypeName;
        // 自定义基元（用户自己实现 IParsable + IComparable 的类型）比 BigInteger 更不可控：交给序列化器
        // 会按公开属性写出、读回 default。故一律走规范文本形，与 BigInteger 同一条路径。
        IsCustomPrimitive = SupportedPrimitiveTypes.IsCustomPrimitive(primitiveIdType);
        // Newtonsoft.Json 的 JsonWriter.WriteValue 不认识这几个类型，会抛 JsonWriterException；
        // 读侧同理。它们的转换器因此也改走规范文本形。
        IsNewtonsoftTextPrimitive = IsCustomPrimitive || IsNewtonsoftTextPrimitiveCore(primitiveIdType);
        // MessagePack / BSON 同理：走文本形的基元见 _binaryTextPrimitives。
        IsBinaryTextPrimitive = IsCustomPrimitive || _binaryTextPrimitives.Contains(primitiveIdType.ToDisplayString());
        // protobuf-net 的缺口集合与上面两处都不同，故单列一张表（见 _protobufTextPrimitives）。
        // 用不带 global:: 的显示名匹配：集合里存的就是这个形态。
        IsProtobufTextPrimitive = IsCustomPrimitive || _protobufTextPrimitives.Contains(primitiveIdType.ToDisplayString());
        // 属性只查一次：下面九项都要读命名实参，逐项重查会把 GetAttributes 与显示名格式化各做九遍。
        var attribute = AttributeReader.Find(type, AttributeReader.StronglyTypedIdAttributeName);
        ValidatorName = AttributeReader.GetString(attribute, ValidatorArgumentName) ?? defaults.ValidatorName;
        NormalizerName = AttributeReader.GetString(attribute, NormalizerArgumentName) ?? defaults.NormalizerName;
        // 存显示名而非符号：本类型是增量缓存的键，符号跨编译不稳定，显示名才是稳定值。
        ExceptionTypeName = AttributeReader.GetType(attribute, ExceptionTypeArgumentName)
                ?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            ?? defaults.ExceptionTypeName;
        // 下面两项是 bool，未写时读到的就是 false，无法区分「没写」与「写了 false」，因此逐类型只能单向打开：
        // 程序集默认打开后，单个 Id 无法再关回去。这是有意的：脱敏是安全策略，允许逐类型放宽等于把
        // 「整库不泄露用户输入」变成一句空话；TypeConverter 沿用同一规则以免引入第二种语义。
        HasTypeConverter = AttributeReader.GetBoolean(attribute, TypeConverterArgumentName) || defaults.TypeConverter;
        RedactsValidationMessage = AttributeReader.GetBoolean(attribute, RedactValidationMessageArgumentName)
            || defaults.RedactValidationMessage;
        // 枚举型默认值用「未指定」区分：GetEnumValue 未写时返回 null，故逐类型显式写回 Primitive / Lenient
        // 也能覆盖程序集默认（bool 型做不到这一点，见上一条）。
        IsObjectJsonShape = (AttributeReader.GetEnumValue(attribute, JsonShapeArgumentName) ?? defaults.JsonShape)
            == ObjectJsonShapeValue;
        Prefix = NormalizePrefix(AttributeReader.GetString(attribute, PrefixArgumentName));
        IsStrictPrefixRead = (AttributeReader.GetEnumValue(attribute, PrefixReadModeArgumentName) ?? defaults.PrefixReadMode)
            == StrictPrefixReadModeValue;
        // 逐类型优先，其次程序集默认，最后「引用了就生成」。存整数而非枚举：本类型是增量缓存的键。
        Integrations = IntegrationFlags.Normalize(AttributeReader.GetEnumValue(attribute, IntegrationsArgumentName))
            ?? defaults.Integrations
            ?? IntegrationFlags.All;
        HasSystemTextJsonConverter = WillHaveNestedType(type, SystemTextJsonConverterTypeName);
        HasNewtonsoftJsonConverter = WillHaveNestedType(type, NewtonsoftJsonConverterTypeName);
        RequiresExternalNewtonsoftJsonConverter = RequiresExternalConverter(type, NewtonsoftJsonConverterTypeName);
        // 用户自己加了 [ProtoContract] 时不能再补一个：特性不允许重复，会 CS0579。
        // 此时由用户接管 protobuf 契约，生成器跳过该 Id。
        HasProtoContractAttribute = AttributeReader.Find(type, ProtoContractAttributeName) is not null;
        // 命名实例在构造期一次性渲染成文本：本类型是增量缓存的键，集合成员会退化成引用比较而让缓存永久失效。
        InstancesBlock = RenderInstancesBlock(type, primitiveIdType, RedactsValidationMessage);
    }

    /// <summary>
    /// 基元是否为 Newtonsoft.Json 无法按值写入的类型。
    /// </summary>
    private static bool IsNewtonsoftTextPrimitiveCore(ITypeSymbol primitiveIdType)
        => _newtonsoftTextPrimitives.Contains(primitiveIdType.ToDisplayString());

    /// <summary>
    /// 强类型 Id 的命名空间，不含 <c>global::</c> 前缀。
    /// </summary>
    public string Namespace { get; }

    /// <summary>
    /// 强类型 Id 的命名空间，含 <c>global::</c> 前缀。
    /// </summary>
    public string FullyQualifiedNamespace { get; }

    /// <summary>
    /// 强类型 Id 的类型名，不含命名空间。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 强类型 Id 的类型名，不含命名空间；C# 保留关键字已加 <c>@</c> 前缀转义，可直接写进生成的源码。
    /// </summary>
    /// <remarks>
    /// <see cref="Name"/> 对关键字不加转义，原样写进类型声明位会编译失败（CS0260 / CS1520 / CS0101 / CS9348）。
    /// 加 <c>@</c> 对任何关键字都合法且不改变语义，普通名字下与 <see cref="Name"/> 相同。
    /// <para>
    /// 拼装源码时一律用本属性。唯一例外是名字作为非代码文本使用的场合，例如路由约束令牌
    /// （<c>options.ConstraintMap["MyId"]</c>），加 <c>@</c> 会改变语义。
    /// </para>
    /// </remarks>
    public string EscapedName => IdentifierEscaping.Escape(Name);

    /// <summary>
    /// 强类型 Id 的全限定名，含 <c>global::</c> 前缀。
    /// </summary>
    public string FullyQualifiedName { get; }

    /// <summary>
    /// 强类型 Id 的显示名，同时用作生成源的 hint 名。
    /// </summary>
    public string FullName { get; }

    /// <summary>
    /// 包含类型链的重开声明，最外层在前、每层一行；顶层类型为空串。
    /// </summary>
    public string ContainingTypeDeclarations { get; }

    /// <summary>
    /// 包含类型链的层数；顶层类型为 0。
    /// </summary>
    public int NestingDepth { get; }

    /// <summary>
    /// 可作为生成源 hint 名的类型显示名：只保留 Unicode 字母、数字与 <c>_</c> <c>.</c>。
    /// </summary>
    public string HintName { get; }

    /// <summary>
    /// 强类型 Id 是否声明在泛型包含类型内部（外部集成生成器会跳过此类 Id）。
    /// </summary>
    public bool IsInGenericContainer { get; }

    /// <summary>
    /// 该 Id 是否带有（或本次生成即将产出）内嵌的 <c>SystemTextJsonConverter</c>。
    /// </summary>
    public bool HasSystemTextJsonConverter { get; }

    /// <summary>
    /// 该 Id 是否带有（或本次生成即将产出）内嵌的 <c>NewtonsoftJsonConverter</c>。
    /// </summary>
    public bool HasNewtonsoftJsonConverter { get; }

    /// <summary>
    /// 该 Id 是否已经由用户标注了 <c>[ProtoContract]</c>。
    /// </summary>
    /// <remarks>
    /// 特性不允许重复，生成器再补一个会 CS0579。此时视为用户自行接管 protobuf 契约，跳过该 Id。
    /// </remarks>
    public bool HasProtoContractAttribute { get; }

    /// <summary>
    /// 该 Id 是否需要由消费方程序集提供 Newtonsoft 转换器。
    /// </summary>
    /// <remarks>
    /// 内嵌转换器只能生成在声明 Id 的程序集里，消费方补一份会撞 CS0426。因此声明程序集未引用 Newtonsoft.Json 时，
    /// 该 Id 在任何程序集里都没有 Newtonsoft 转换器，结果是在 STJ 下是字符串、在 Newtonsoft 下是对象
    /// （<c>{"Value":"…"}</c>），编译和运行都不报错，只在往返时暴露。STJ 没有这个问题，它在共享框架里。
    /// </remarks>
    public bool RequiresExternalNewtonsoftJsonConverter { get; }

    /// <summary>
    /// 紧跟在 <c>record</c> 关键字之后的类型种类后缀，如 <c>" struct"</c>；<see langword="null"/> 表示引用类型 record。
    /// </summary>
    public string? TypeKindSuffix { get; }

    /// <summary>
    /// <c>readonly record struct</c> 时插在 <c>partial</c> 与 <c>record</c> 之间的修饰符；否则为空串。
    /// </summary>
    public string ReadOnlyRecordModifier { get; }

    /// <summary>
    /// 强类型 Id 所包装基元类型的全限定名。
    /// </summary>
    public string PrimitiveIdTypeName { get; }

    /// <summary>
    /// 基元类型是否为 <c>string</c>。
    /// </summary>
    public bool IsStringPrimitive { get; }

    /// <summary>
    /// 基元类型是否为 <c>char</c>。
    /// </summary>
    public bool IsCharPrimitive { get; }

    /// <summary>
    /// 基元类型是否为 <c>System.Numerics.BigInteger</c>。
    /// </summary>
    /// <remarks>
    /// System.Text.Json 没有 <c>BigInteger</c> 的转换器，会退化成按公开属性序列化且读回 <c>default</c>，故改走规范文本形。
    /// </remarks>
    public bool IsBigIntegerPrimitive { get; }

    /// <summary>
    /// 基元是否为 Newtonsoft.Json 无法按值读写的类型（<c>Int128</c> / <c>UInt128</c> / <c>Half</c> /
    /// <c>DateOnly</c> / <c>TimeOnly</c>），或自定义基元。
    /// </summary>
    /// <remarks>
    /// 这些类型没有对应的 <c>PrimitiveTypeCode</c>，<c>JsonWriter.WriteValue</c> 会抛异常，故改走规范文本形。
    /// </remarks>
    public bool IsNewtonsoftTextPrimitive { get; }

    /// <summary>
    /// 基元是否缺少 MessagePack / BSON 的原生表示（<c>BigInteger</c> / <c>Int128</c> / <c>UInt128</c>），
    /// 或为自定义基元。
    /// </summary>
    /// <remarks>
    /// 为 <see langword="true"/> 时两种二进制格式改走规范文本形，否则会在运行期抛「格式化器未注册」。
    /// </remarks>
    public bool IsBinaryTextPrimitive { get; }

    /// <summary>
    /// 基元是否为用户自定义类型（不在内置白名单内，但实现了 <c>IParsable&lt;TSelf&gt;</c> 与
    /// <c>IComparable&lt;TSelf&gt;</c>）。
    /// </summary>
    /// <remarks>
    /// 自定义基元的全部序列化路径都走规范文本形：交给序列化器会按公开属性写出、读回 <c>default</c>，
    /// 属于「看起来成功了、值已经丢了」这类最难排查的形态。
    /// </remarks>
    public bool IsCustomPrimitive { get; }

    /// <summary>
    /// 基元的序列化载荷是否必须走规范文本形（<c>BigInteger</c> 或自定义基元）。
    /// </summary>
    public bool RequiresTextSerialization => IsBigIntegerPrimitive || IsCustomPrimitive;

    /// <summary>
    /// 基元在 protobuf-net 里是否只能按规范文本承载。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="IsBinaryTextPrimitive"/> 不同：protobuf-net 的缺口集合不一样 —— 它能原生编码
    /// <c>DateOnly</c> / <c>TimeOnly</c> / <c>decimal</c> / <c>char</c>，却不认识 <c>Half</c> 与
    /// <c>DateTimeOffset</c>。复用前者的判定会让这两类基元在运行期抛「no contract can be inferred」。
    /// </remarks>
    public bool IsProtobufTextPrimitive { get; }

    /// <summary>
    /// 把基元值写成 protobuf 文本载荷的表达式（不含结尾分号）。
    /// </summary>
    /// <remarks>
    /// <c>DateTimeOffset</c> 必须用 <c>"O"</c> 格式：默认格式会丢掉亚秒精度（实测 <c>AddTicks(9)</c>
    /// 往返后 ticks 不等），而 <c>"O"</c> 保留 ticks 与 offset 两者。其余基元的默认格式已是往返安全的
    /// （<c>Half</c> 全 65536 个位模式实测零失配，<c>Int128</c> / <c>UInt128</c> / <c>BigInteger</c> 亦然）。
    /// </remarks>
    public string GetProtobufTextExpression(string variableName)
        => PrimitiveIdTypeName == "global::System.DateTimeOffset"
            ? $"{variableName}.ToString(\"O\", {GeneratedCode.InvariantCulture})"
            : GetPrimitiveTextExpression(variableName);

    /// <summary>
    /// 把基元值写成规范文本的表达式（不含结尾分号），供各序列化生成器共用。
    /// </summary>
    /// <remarks>
    /// 只有基元实现 <c>IFormattable</c> 时才能调 <c>ToString(null, provider)</c>；自定义基元可能只实现
    /// <c>IParsable</c>，此时按类型名直呼该重载会 CS1501，只能退回无参 <c>ToString()</c>。
    /// 两种形态都要求「<c>ToString()</c> 的结果能被自己的 <c>Parse</c> 读回」，这是自定义基元的文档契约。
    /// </remarks>
    public string PrimitiveTextExpression => GetPrimitiveTextExpression("primitiveId");

    /// <summary>
    /// 把指定变量里的基元值写成规范文本的表达式（不含结尾分号）。
    /// </summary>
    /// <param name="variableName">持有基元值的变量或成员访问表达式。</param>
    /// <remarks>
    /// 只有基元实现 <c>IFormattable</c> 时才能调 <c>ToString(null, provider)</c>；自定义基元可能只实现
    /// <c>IParsable</c>，此时按类型名直呼该重载会 CS1501，只能退回无参 <c>ToString()</c>。
    /// 两种形态都要求「<c>ToString()</c> 的结果能被自己的 <c>Parse</c> 读回」，这是自定义基元的文档契约。
    /// </remarks>
    public string GetPrimitiveTextExpression(string variableName)
        => IsFormattable
            ? $"{variableName}.ToString(null, global::System.Globalization.CultureInfo.InvariantCulture)"
            : $"{variableName}.ToString()";

    /// <summary>
    /// 把规范文本读回基元的条件表达式（不含结尾分号），结果放在 <paramref name="variableName"/> 里。
    /// </summary>
    /// <remarks>
    /// 一律走基元自己的 <c>TryParse</c> 而非 <c>Parse</c>：文本不是合法取值时 <c>Parse</c> 抛的
    /// <c>FormatException</c> 会穿透各序列化库的转换器契约（它们要求失败一律抛自己的异常类型）。
    /// 自定义基元的契约保证该重载是公开静态成员（见 <c>SupportedPrimitiveTypes.IsCustomPrimitive</c>）。
    /// </remarks>
    public string GetPrimitiveTextTryParseCondition(string textExpression, string variableName)
        => $"{PrimitiveIdTypeName}.TryParse({textExpression}, global::System.Globalization.CultureInfo.InvariantCulture, out var {variableName})";

    /// <summary>
    /// 基元类型是否为枚举（枚举没有带 <c>IFormatProvider</c> 的静态 <c>TryParse</c>）。
    /// </summary>
    public bool IsEnum { get; }

    /// <summary>
    /// 基元枚举是否标注了 <c>[System.Flags]</c>。位组合本身就是合法取值，故 <c>TryParse</c> 不能对它做
    /// <c>Enum.IsDefined</c> 收紧，改用 <see cref="FlagsEnumUndefinedBitsMask"/> 做位校验。
    /// </summary>
    public bool IsFlagsEnum { get; }

    /// <summary>
    /// <c>[Flags]</c> 枚举里没有任何成员占用的位。运行期只要 <c>(value &amp; mask) != 0</c> 就说明出现了未定义的位。
    /// </summary>
    /// <remarks>
    /// 全部位都已定义时（典型是 <c>All = -1</c>）为 <see langword="null"/>，调用方据此跳过校验。
    /// </remarks>
    public ulong? FlagsEnumUndefinedBitsMask { get; }

    /// <summary>
    /// 基元枚举的底层整型类型名（含 <c>global::</c> 前缀）；非枚举基元为 <see langword="null"/>。
    /// </summary>
    /// <remarks>
    /// 驱动可能把枚举列返回成 <c>long</c> / <c>double</c> 等与底层类型不同的 CLR 类型。还原时必须先按这个
    /// 类型做范围检查再交给 <c>Enum.ToObject</c>，后者对超范围整数静默绕回（<c>300L</c> 交给 <c>byte</c>
    /// 枚举会变成 <c>44</c>，甚至恰好命中某个已定义成员），对浮点数则静默截断。
    /// </remarks>
    public string? EnumUnderlyingTypeName { get; }

    /// <summary>
    /// 基元类型是否实现 <c>System.IFormattable</c>。
    /// </summary>
    public bool IsFormattable { get; }

    /// <summary>
    /// 基元类型是否实现 <c>System.ISpanFormattable</c>。
    /// </summary>
    public bool IsSpanFormattable { get; }

    /// <summary>
    /// 基元类型是否实现 <c>IUtf8SpanParsable&lt;TSelf&gt;</c> 且暴露公开的 <c>TryParse(ReadOnlySpan&lt;byte&gt;, …)</c>。
    /// </summary>
    public bool IsUtf8SpanParsable { get; }

    /// <summary>
    /// 基元类型是否实现 <c>System.IUtf8SpanFormattable</c>。
    /// </summary>
    public bool IsUtf8SpanFormattable { get; }

    /// <summary>
    /// 基元 UTF-16 <c>TryFormat</c> 的直呼形态（决定是否经接口调用、是否装箱）。
    /// </summary>
    public TryFormatKind CharTryFormatKind { get; }

    /// <summary>
    /// 基元 UTF-8 <c>TryFormat</c> 的直呼形态（决定是否经接口调用、是否装箱）。
    /// </summary>
    public TryFormatKind Utf8TryFormatKind { get; }

    /// <summary>
    /// <c>[StronglyTypedId]</c> 上 <c>Validator</c> 指定的验证器方法名；未指定时为 <see langword="null"/>。
    /// </summary>
    public string? ValidatorName { get; }

    /// <summary>
    /// <c>[StronglyTypedId]</c> 上 <c>Normalizer</c> 指定的归一化方法名；未指定时为 <see langword="null"/>。
    /// </summary>
    public string? NormalizerName { get; }

    /// <summary>
    /// 验证失败时抛出的异常类型全限定名（含 <c>global::</c> 前缀）；未配置时为 <see langword="null"/>，
    /// 表示沿用 <c>System.ArgumentException</c>。
    /// </summary>
    public string? ExceptionTypeName { get; }

    /// <summary>
    /// <c>[StronglyTypedId]</c> 是否要求生成 <c>System.ComponentModel.TypeConverter</c>。
    /// </summary>
    public bool HasTypeConverter { get; }

    /// <summary>
    /// <c>[StronglyTypedId]</c> 是否要求 JSON 采用对象形态（<c>{ "value": … }</c>）。
    /// </summary>
    public bool IsObjectJsonShape { get; }

    /// <summary>
    /// <c>[StronglyTypedId]</c> 上 <c>Prefix</c> 指定的前缀；未指定或为空 / 纯空白时为 <see langword="null"/>。
    /// </summary>
    public string? Prefix { get; }

    /// <summary>
    /// 强类型 Id 的规范文本形是否携带前缀。
    /// </summary>
    public bool HasPrefix => Prefix is { Length: > 0 };

    /// <summary>
    /// 读回带前缀的文本时是否要求前缀必须存在（<c>PrefixReadMode.Strict</c>）。
    /// </summary>
    public bool IsStrictPrefixRead { get; }

    /// <summary>
    /// 生效的集成掩码（见 <see cref="IntegrationFlags"/>）。已剔除 <c>None</c> 标记位，
    /// 未指定时等于 <see cref="IntegrationFlags.All"/>。
    /// </summary>
    public int Integrations { get; }

    /// <summary>
    /// 验证失败消息是否隐去被拒绝的原始值。
    /// </summary>
    /// <remarks>
    /// 打开后 <c>Create</c> 的消息是 <c>The value is not a valid MyId.</c>，不再内插原始值，后者往往是用户输入。
    /// </remarks>
    public bool RedactsValidationMessage { get; }

    /// <summary>
    /// 判断给定集成是否被选中。仍须由调用方另外确认「当前编译确实引用了对应程序集」。
    /// </summary>
    public bool Supports(int integration) => (Integrations & integration) == integration;

    /// <summary>
    /// 命名实例的渲染块：每条实例的文档注释、生成器标注与 <c>public static readonly</c> 字段，
    /// 自带产物中的绝对缩进（成员级 4 空格），块首是两个换行。
    /// </summary>
    /// <remarks>
    /// 存渲染后的文本而非实例集合：本类型是 <c>readonly record struct</c>，集合的引用相等性会让增量缓存永久失效。
    /// 未声明实例或全部非法时为空串。
    /// </remarks>
    public string InstancesBlock { get; }

    /// <summary>
    /// 渲染全部合法命名实例的成员块；没有合法实例时为空串。
    /// </summary>
    /// <remarks>
    /// 非法项静默跳过：分析器会报 <c>STIA0020</c> / <c>STIA0021</c>；生成器不能抛异常，那会降级成 <c>STIA0012</c> 盖掉真正原因。
    /// </remarks>
    private static string RenderInstancesBlock(ITypeSymbol type, ITypeSymbol primitiveType, bool redactsValidationMessage)
    {
        // 非具名类型在构造函数开头就已抛异常，这里只是让签名不必依赖调用点的静态类型。
        if (type is not INamedTypeSymbol namedType)
        {
            return string.Empty;
        }

        var instances = InstanceDeclaration.Read(namedType, primitiveType);

        if (instances.IsDefaultOrEmpty)
        {
            return string.Empty;
        }

        // 自定义基元的值只能在运行期校验（生成器跑在 netstandard2.0 上，无法执行用户的 Parse），
        // 故实例改经 CreateInstance 包装，把「哪个实例、什么原文」写进异常消息。
        var isCustomPrimitive = SupportedPrimitiveTypes.IsCustomPrimitive(primitiveType);
        var builder = new System.Text.StringBuilder();

        foreach (var instance in instances)
        {
            if (instance.Issue != InstanceIssue.None)
            {
                continue;
            }

            // 块经插值洞注入模板，洞写在去缩进基准列上，故每行必须自带产物中的绝对缩进。
            builder.Append('\n').Append('\n');
            builder.Append("    /// <summary>").Append('\n');
            builder.Append("    /// 获取 <c>").Append(IdentifierEscaping.Escape(type.Name)).Append("</c> 的 <c>").Append(instance.Name).Append("</c> 实例。").Append('\n');
            builder.Append("    /// </summary>").Append('\n');
            builder.Append("    ").Append(GeneratedCode.CompilerGeneratedAttribute).Append('\n');
            builder.Append("    ").Append(GeneratedCode.GeneratedCodeAttribute(nameof(Generators.StronglyTypedIdCodeGenerator), GeneratedCode.Version)).Append('\n');
            // 字段类型是 Id 自身、字段名是实例名，两者都要转义：Id 叫 class 或实例叫 class 时，
            // 原样写出 "public static readonly class Empty" / "public static readonly MyId class" 都是语法错误。
            // 实例名转义只在落盘这一刻做，IsReservedName 的撞名判定仍用原始名，两者判据一致。
            builder.Append("    public static readonly ").Append(IdentifierEscaping.Escape(type.Name)).Append(' ').Append(IdentifierEscaping.Escape(instance.Name)).Append(" = ")
                .Append(isCustomPrimitive
                    ? $"CreateInstance({SymbolDisplay.FormatLiteral(instance.Name, quote: true)}, {SymbolDisplay.FormatLiteral(instance.Value, quote: true)})"
                    : $"Create({instance.Expression})")
                .Append(';');
        }

        if (isCustomPrimitive)
        {
            AppendCreateInstanceMethod(builder, namedType, primitiveType, redactsValidationMessage);
        }

        return builder.ToString();
    }

    /// <summary>
    /// 追加自定义基元实例专用的私有工厂：把基元 <c>Parse</c> 的失败包成带实例名与原文的
    /// <see cref="InvalidOperationException"/>。
    /// </summary>
    /// <remarks>
    /// 静态字段初始化遇第一个异常就中止，用户看到的原本是 <c>TypeInitializationException</c> 套一层基元自己的
    /// <c>FormatException</c>，消息里既没有实例名也没有 Id 类型名。固定前缀让测试可断言、APM 可按前缀分组计数。
    /// 原文是否内插沿用 <c>RedactValidationMessage</c> 的口径：打开后不泄漏用户输入。
    /// </remarks>
    private static void AppendCreateInstanceMethod(System.Text.StringBuilder builder, INamedTypeSymbol type, ITypeSymbol primitiveType, bool redactsValidationMessage)
    {
        var idName = IdentifierEscaping.Escape(type.Name);
        var primitiveName = primitiveType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        builder.Append('\n').Append('\n');
        builder.Append("    /// <summary>").Append('\n');
        builder.Append("    /// 由命名实例的文本值创建 Id；失败时抛出带实例名与原文的异常。").Append('\n');
        builder.Append("    /// </summary>").Append('\n');
        builder.Append("    ").Append(GeneratedCode.CompilerGeneratedAttribute).Append('\n');
        builder.Append("    ").Append(GeneratedCode.GeneratedCodeAttribute(nameof(Generators.StronglyTypedIdCodeGenerator), GeneratedCode.Version)).Append('\n');
        builder.Append($"    private static {idName} CreateInstance(string name, string text)").Append('\n');
        builder.Append("    {").Append('\n');
        builder.Append("        try").Append('\n');
        builder.Append("        {").Append('\n');
        builder.Append($"            return Create({primitiveName}.Parse(text, global::System.Globalization.CultureInfo.InvariantCulture));").Append('\n');
        builder.Append("        }").Append('\n');
        builder.Append("        catch (global::System.Exception exception)").Append('\n');
        builder.Append("        {").Append('\n');
        builder.Append(redactsValidationMessage
            ? $"            throw new global::System.InvalidOperationException(\"StronglyTypedId: named instance '\" + name + \"' of '{type.Name}' could not be created.\", exception);"
            : $"            throw new global::System.InvalidOperationException(\"StronglyTypedId: named instance '\" + name + \"' of '{type.Name}' could not be created from '\" + text + \"'.\", exception);").Append('\n');
        builder.Append("        }").Append('\n');
        builder.Append("    }");
    }

    /// <summary>
    /// 读取 <c>[assembly: StronglyTypedIdDefaults(Validator = …)]</c> 的默认验证器方法名；未声明时为 <see langword="null"/>。
    /// </summary>
    public static string? GetAssemblyDefaultValidator(IAssemblySymbol assembly)
        => StronglyTypedIdDefaults.Read(assembly).ValidatorName;

    /// <summary>
    /// 读取 <c>[assembly: StronglyTypedIdDefaults(Normalizer = …)]</c> 的默认归一化方法名；未声明时为 <see langword="null"/>。
    /// </summary>
    public static string? GetAssemblyDefaultNormalizer(IAssemblySymbol assembly)
        => StronglyTypedIdDefaults.Read(assembly).NormalizerName;

    /// <summary>
    /// 读取 <c>[assembly: StronglyTypedIdDefaults(ExceptionType = typeof(…))]</c> 的默认异常类型符号；未声明时为 <see langword="null"/>。
    /// </summary>
    /// <remarks>
    /// 分析器需要符号（要校验它是不是异常类型），生成器需要可直接写进产物的显示名，故两个入口并存。
    /// </remarks>
    public static ITypeSymbol? GetAssemblyDefaultExceptionTypeSymbol(IAssemblySymbol assembly)
        => StronglyTypedIdDefaults.ReadExceptionTypeSymbol(assembly);

    /// <summary>
    /// 取强类型 Id 的基元类型，即唯一一个「单参数、且参数类型不是自身」的实例构造函数所带的参数类型。
    /// </summary>
    /// <param name="type">强类型 Id 类型符号。</param>
    /// <param name="skipNullableAnnotated">
    /// 是否跳过可空引用类型标注的参数。生成器传 <see langword="true"/>（可空参数不生成任何代码），
    /// 分析器传 <see langword="false"/>（它要拿到类型才能报 <c>ParameterCannotBeNullable</c>）。
    /// </param>
    /// <remarks>
    /// 跳过参数类型等于自身的构造，是为了滤掉 record 合成的拷贝构造，也顺带覆盖了用户手写的单参数构造
    /// 排在主构造之前的情况。
    /// </remarks>
    internal static ITypeSymbol? GetSingleParameterType(INamedTypeSymbol type, bool skipNullableAnnotated = false)
    {
        foreach (var constructor in type.InstanceConstructors)
        {
            if (constructor.Parameters is [var parameter]
                && (!skipNullableAnnotated || parameter.NullableAnnotation != NullableAnnotation.Annotated)
                && !SymbolEqualityComparer.Default.Equals(parameter.Type, type))
            {
                return parameter.Type;
            }
        }

        return null;
    }

    /// <summary>
    /// 解析强类型 Id 所包装的基元类型符号。
    /// </summary>
    /// <remarks>
    /// 一个类型可能实现多个 <c>IStronglyTypedId&lt;,&gt;</c>，此时取 <c>TSelf</c> 等于自身的那个接口。
    /// 直接取 <c>FirstOrDefault</c> 会让结果取决于接口枚举顺序，与运行时
    /// <c>StronglyTypedIdExtensions.ResolvePrimitiveIdType</c> 的结论不一致。
    /// </remarks>
    private static ITypeSymbol GetPrimitiveIdType(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol namedType)
        {
            ITypeSymbol? fallback = null;

            foreach (var candidate in namedType.Interfaces)
            {
                if (!IsStronglyTypedIdInterface(candidate))
                {
                    continue;
                }

                if (SymbolEqualityComparer.Default.Equals(candidate.TypeArguments[0], namedType))
                {
                    return candidate.TypeArguments[1];
                }

                fallback ??= candidate.TypeArguments[1];
            }

            if (fallback is not null)
            {
                return fallback;
            }

            // 构造回退同样要跳过参数类型等于自身的那个：没有主构造函数的 record 会把合成的拷贝构造排在首位，
            // 取第一个会绑到 record 自身，得到「基元是自己」的假象。
            foreach (var constructor in namedType.InstanceConstructors)
            {
                if (constructor.Parameters is [var parameter]
                    && !SymbolEqualityComparer.Default.Equals(parameter.Type, namedType))
                {
                    return parameter.Type;
                }
            }
        }

        throw new InvalidOperationException($"无法从类型“{type.ToDisplayString()}”解析基元 Id 类型：它既未实现 {InterfaceName}<TSelf, TPrimitiveId>，也不具备单参数构造函数。");
    }

    /// <summary>
    /// 自内向外取出包含类型链，反转成最外层在前，并逐层渲染为可重开该类型的 partial 声明。
    /// </summary>
    private static (string Declarations, int Depth, bool IsInGenericContainer) GetContainingTypeDeclarations(ITypeSymbol type)
    {
        var levels = new List<string>();
        var isInGenericContainer = false;

        for (var container = type.ContainingType; container is not null; container = container.ContainingType)
        {
            if (container.TypeParameters.Length > 0)
            {
                isInGenericContainer = true;
            }

            levels.Add(GetContainingTypeDeclaration(container));
        }

        levels.Reverse();

        return (string.Join("\n", levels), levels.Count, isInGenericContainer);
    }

    /// <summary>
    /// 把包含类型渲染成可重开它的 partial 声明，形如 <c>public partial class Outer</c>。
    /// </summary>
    private static string GetContainingTypeDeclaration(INamedTypeSymbol type)
    {
        var keyword = type switch
        {
            { IsRecord: true, TypeKind: TypeKind.Struct } => "record struct",
            { IsRecord: true } => "record",
            { TypeKind: TypeKind.Struct } => "struct",
            { TypeKind: TypeKind.Interface } => "interface",
            _ => "class",
        };

        // 必须复现类型参数表（否则 CS0693），约束子句只需在任一 partial 上声明一次，故省略 where。
        var typeParameterList = type.TypeParameters.Length > 0
            ? "<" + string.Join(", ", type.TypeParameters.Select(parameter => IdentifierEscaping.Escape(parameter.Name))) + ">"
            : string.Empty;

        return $"{GetAccessibilityKeyword(type.DeclaredAccessibility)} partial {keyword} {IdentifierEscaping.Escape(type.Name)}{typeParameterList}";
    }

    /// <summary>
    /// 判断该 Id 是否带有（或本次生成即将产出）指定的内嵌转换器类型。
    /// </summary>
    /// <remarks>
    /// 引用程序集里的 Id 直接查嵌套类型是否存在；本次编译声明的 Id 尚未实现接口，
    /// 按「没实现接口就是声明类型，转换器会被生成」推断。
    /// </remarks>
    private static bool WillHaveNestedType(ITypeSymbol type, string nestedTypeName)
        => type is INamedTypeSymbol namedType
            && (AlreadyHasNestedType(namedType, nestedTypeName) || IsDeclaredInThisCompilation(namedType));

    /// <summary>
    /// 判定该 Id 是否只能由消费方程序集提供转换器：它不是本次编译声明的（声明程序集里已定型），
    /// 而它的声明程序集又没有生成过转换器（那份程序集没引用 Newtonsoft.Json）。
    /// </summary>
    private static bool RequiresExternalConverter(ITypeSymbol type, string nestedTypeName)
        => type is INamedTypeSymbol namedType
            && !AlreadyHasNestedType(namedType, nestedTypeName)
            && !IsDeclaredInThisCompilation(namedType);

    /// <summary>
    /// 引用程序集里的 Id 已经生成过转换器：直接查嵌套类型是否存在。
    /// </summary>
    private static bool AlreadyHasNestedType(INamedTypeSymbol type, string nestedTypeName)
        => type.GetTypeMembers(nestedTypeName).Length > 0;

    /// <summary>
    /// 本次编译声明的 Id 尚未实现 <c>IStronglyTypedId&lt;,&gt;</c>（生成器还没跑），因此转换器一定会被生成。
    /// </summary>
    private static bool IsDeclaredInThisCompilation(INamedTypeSymbol type)
        => !type.Interfaces.Any(IsStronglyTypedIdInterface);

    /// <summary>
    /// 把基元类型的显示名归一化为不含命名空间限定的短名。
    /// </summary>
    internal static string GetPrimitiveIdTypeShortName(string primitiveIdTypeName)
    {
        var separatorIndex = primitiveIdTypeName.LastIndexOf('.');

        return separatorIndex < 0 ? primitiveIdTypeName : primitiveIdTypeName[(separatorIndex + 1)..];
    }

    /// <summary>
    /// 把符号的可访问性渲染成 C# 关键字。
    /// </summary>
    private static string GetAccessibilityKeyword(Accessibility accessibility)
        => accessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            Accessibility.Protected => "protected",
            Accessibility.ProtectedOrInternal => "protected internal",
            Accessibility.ProtectedAndInternal => "private protected",
            _ => "private",
        };

    /// <summary>
    /// 判断给定类型是否实现了指定的 BCL 接口。
    /// </summary>
    private static bool ImplementsInterface(ITypeSymbol type, string interfaceName)
        => type.AllInterfaces.Any(@interface => @interface.Name == interfaceName
            && @interface.ContainingNamespace.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == SystemNamespace);

    /// <summary>
    /// 判断枚举是否标注了 <c>[System.Flags]</c>。
    /// </summary>
    internal static bool HasFlagsAttribute(ITypeSymbol type) => EnumBitMath.HasFlagsAttribute(type);

    /// <summary>
    /// 计算 <c>[Flags]</c> 枚举里「没有任何成员占用」的位，供 <c>TryParse</c> 做位校验。
    /// </summary>
    internal static ulong? GetFlagsEnumUndefinedBitsMask(ITypeSymbol enumType) => EnumBitMath.GetUndefinedBitsMask(enumType);

    /// <summary>
    /// 取整型基元类型的位宽；非整型返回 0。
    /// </summary>
    internal static int GetIntegralBitWidth(SpecialType type) => EnumBitMath.GetIntegralBitWidth(type);

    /// <summary>
    /// 取「枚举值是否落在已定义取值内」的条件表达式；非枚举基元、或无需校验时返回 <see langword="null"/>。
    /// </summary>
    /// <remarks>
    /// <paramref name="variableName"/> 是产物里持有该枚举值的局部变量名。普通枚举查 <see cref="Enum.IsDefined"/>，
    /// <c>[Flags]</c> 枚举查「不许出现未定义的位」。各条「文本/数值 → Id」的入口都要做这道校验，
    /// 因为 <c>TryCreate</c> 只跑用户配的 Validator，而 <c>Enum.TryParse</c> 与 JSON 读取路径对未定义数值一律放行。
    /// 用非泛型重载是因为泛型 <c>Enum.IsDefined&lt;T&gt;</c> 只有 .NET 5+ 才有。
    /// </remarks>
    internal string? GetEnumValidityCondition(string variableName)
    {
        if (!IsEnum)
        {
            return null;
        }

        if (!IsFlagsEnum)
        {
            return $"global::System.Enum.IsDefined(typeof({PrimitiveIdTypeName}), {variableName})";
        }

        // [Flags] 的合法取值本就是位组合，IsDefined 会误杀，改用「不许出现未定义的位」：
        // 掩码在编译期由全部成员的常量算出，运行期只剩一次按位与。
        // 必须显式 unchecked：调用方若开了 CheckForOverflowUnderflow，负数枚举值转 ulong 会抛 OverflowException。
        return FlagsEnumUndefinedBitsMask is { } undefinedBits
            ? $"(unchecked((ulong){variableName}) & {GeneratedCode.UndefinedBitsLiteral(undefinedBits)}) == 0UL"
            : null;
    }

    /// <summary>
    /// 取「把 <paramref name="variableName"/> 还原成 Id」的条件表达式，供各反序列化入口共用。
    /// </summary>
    /// <remarks>
    /// 一律走 <c>TryCreate</c> 而非 <c>Create</c>：后者在验证器拒绝时抛 <see cref="ArgumentException"/>（或
    /// <c>ExceptionType</c> 指定的类型），会穿透各序列化库的转换器契约。枚举基元另附已定义性校验，
    /// 因为 <c>TryCreate</c> 只跑用户配的 Validator，而各库对未定义数值（<c>999</c>）一律放行。
    /// </remarks>
    /// <param name="typeReference">引用该 Id 类型时用的文本（短名或全限定名）。</param>
    /// <param name="variableName">持有基元值的局部变量名。</param>
    /// <param name="resultName">接收结果的局部变量名。</param>
    internal string GetTryCreateCondition(string typeReference, string variableName = "value", string resultName = "id")
    {
        var condition = $"{typeReference}.TryCreate({variableName}, out var {resultName})";

        return GetEnumValidityCondition(variableName) is { } validity
            ? $"{condition} && {validity}"
            : condition;
    }

    /// <summary>
    /// 把枚举成员的常量值按位展开成 <see cref="ulong"/>（负数按补码展开，随后由宽度掩码裁掉高位）。
    /// </summary>
    internal static ulong ToBits(object constant) => EnumBitMath.ToBits(constant);

    /// <summary>
    /// 判断给定类型是否暴露了公开的静态 <c>TryParse(ReadOnlySpan&lt;TElement&gt;, IFormatProvider, out T)</c> 重载。
    /// </summary>
    internal static bool HasPublicStaticTryParse(ITypeSymbol type, SpecialType spanElementType)
        => type.GetMembers("TryParse").Any(member =>
            member is IMethodSymbol { IsStatic: true, DeclaredAccessibility: Accessibility.Public, Parameters.Length: 3 } method
            && method.Parameters[0].Type is INamedTypeSymbol spanType
            && spanType.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.ReadOnlySpan<T>"
            && spanType.TypeArguments.Length == 1
            && spanType.TypeArguments[0].SpecialType == spanElementType
            && method.Parameters[1].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.IFormatProvider"
            && method.Parameters[2].RefKind == RefKind.Out);

    /// <summary>
    /// 判定基元 <c>TryFormat</c> 的直呼形态，用于避免值类型基元在格式化时装箱。
    /// </summary>
    internal static TryFormatKind GetTryFormatKind(ITypeSymbol type, SpecialType spanElementType)
    {
        var hasProviderOverload = false;
        var hasPlainOverload = false;

        // 沿基类链取成员：GetMembers 只返回声明成员。当前 24 种基元都是 sealed 值类型或 string，
        // 没有继承来的 TryFormat，但这条判定是「按成员名 + 参数形状」猜的，将来若加入某个自带
        // TryFormat 的自定义结构体基元，漏看基类会静默落到 Interface 分支（每次格式化都装箱）。
        var members = new List<ISymbol>();
        for (var current = type; current is not null; current = current.BaseType)
        {
            members.AddRange(current.GetMembers("TryFormat"));
        }

        foreach (var member in members)
        {
            if (member is not IMethodSymbol { IsStatic: false, DeclaredAccessibility: Accessibility.Public, Parameters.Length: 3 or 4 } method
                || method.Parameters[0].Type is not INamedTypeSymbol spanType
                || spanType.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) != "global::System.Span<T>"
                || spanType.TypeArguments.Length != 1
                || spanType.TypeArguments[0].SpecialType != spanElementType
                || method.Parameters[1].RefKind != RefKind.Out
                || method.Parameters[2].Type is not INamedTypeSymbol formatType
                || formatType.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) != "global::System.ReadOnlySpan<T>"
                || formatType.TypeArguments.Length != 1
                || formatType.TypeArguments[0].SpecialType != SpecialType.System_Char)
            {
                continue;
            }

            if (method.Parameters.Length == 4)
            {
                hasProviderOverload = true;
            }
            else
            {
                hasPlainOverload = true;
            }
        }

        return hasProviderOverload
            ? TryFormatKind.Direct
            : hasPlainOverload
                ? TryFormatKind.DirectWithoutProvider
                : TryFormatKind.Interface;
    }

    /// <summary>
    /// 把前缀归一化：空串与纯空白一律视为「未设置」（<see langword="null"/>）。
    /// </summary>
    private static string? NormalizePrefix(string? prefix)
        => string.IsNullOrWhiteSpace(prefix) ? null : prefix;
}
