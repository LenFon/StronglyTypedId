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
    /// 两个集成走文本形的理由已经不同：BSON 3.x 至今没有 <c>Int128</c> / <c>UInt128</c> / <c>BigInteger</c> 的
    /// 序列化器（写出来是空文档、读回 0，静默丢数据），那边仍然必需；MessagePack 3.x 已能原生编码三者，
    /// 本库继续用文本形只是为了与 2.x 时代的载荷保持可读。改掉会变更线上格式，故不随依赖升级而调整。
    /// </remarks>
    private static readonly ImmutableHashSet<string> _binaryTextPrimitives = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "System.Numerics.BigInteger",
        "System.Int128",
        "System.UInt128");

    /// <summary>
    /// 判断给定接口符号是否为运行时的 <c>IStronglyTypedId&lt;TSelf, TPrimitiveId&gt;</c>。
    /// </summary>
    internal static bool IsStronglyTypedIdInterface(INamedTypeSymbol @interface)
        => @interface.Name == InterfaceName
            && @interface.TypeArguments.Length == 2
            && @interface.ContainingNamespace.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == InterfaceNamespace;

    public StronglyTypedIdInfo(
        ITypeSymbol type,
        string? defaultValidatorName = null,
        string? defaultNormalizerName = null,
        string? defaultExceptionTypeName = null,
        int? defaultIntegrations = null,
        bool defaultRedactValidationMessage = false)
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
        HintName = SanitizeHintName(FullName);

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
        // {"IsPowerOfTwo":…,"Sign":1}，读回来又是 default —— 静默丢数据。故这类基元改走规范文本形。
        IsBigIntegerPrimitive = primitiveIdType.ToDisplayString() == BigIntegerTypeName;
        // Newtonsoft.Json 的 JsonWriter.WriteValue 不认识这几个类型，会抛 JsonWriterException；
        // 读侧同理。它们的转换器因此也改走规范文本形。
        IsNewtonsoftTextPrimitive = IsNewtonsoftTextPrimitiveCore(primitiveIdType);
        // MessagePack / BSON 同理：走文本形的基元见 _binaryTextPrimitives。
        IsBinaryTextPrimitive = _binaryTextPrimitives.Contains(primitiveIdType.ToDisplayString());
        // 属性只查一次：下面九项都要读命名实参，逐项重查会把 GetAttributes 与显示名格式化各做九遍。
        var attribute = AttributeReader.Find(type, AttributeReader.StronglyTypedIdAttributeName);
        ValidatorName = AttributeReader.GetString(attribute, ValidatorArgumentName) ?? defaultValidatorName;
        NormalizerName = AttributeReader.GetString(attribute, NormalizerArgumentName) ?? defaultNormalizerName;
        // 存显示名而非符号：本类型是增量缓存的键，符号跨编译不稳定，显示名才是稳定值。
        ExceptionTypeName = AttributeReader.GetType(attribute, ExceptionTypeArgumentName)
                ?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            ?? defaultExceptionTypeName;
        HasTypeConverter = AttributeReader.GetBoolean(attribute, TypeConverterArgumentName);
        // 逐类型优先，其次程序集默认。属性是 bool，未写时读到的就是 false，无法区分「没写」与「写了 false」——
        // 因此逐类型只能单向打开：程序集默认打开后，单个 Id 无法再关回去。这是有意的：脱敏是安全策略，
        // 允许逐类型放宽等于把「整库不泄露用户输入」变成一句空话。
        RedactsValidationMessage = AttributeReader.GetBoolean(attribute, RedactValidationMessageArgumentName)
            || defaultRedactValidationMessage;
        IsObjectJsonShape = AttributeReader.GetEnumFlag(attribute, JsonShapeArgumentName, ObjectJsonShapeValue);
        Prefix = NormalizePrefix(AttributeReader.GetString(attribute, PrefixArgumentName));
        IsStrictPrefixRead = AttributeReader.GetEnumFlag(attribute, PrefixReadModeArgumentName, StrictPrefixReadModeValue);
        // 逐类型优先，其次程序集默认，最后「引用了就生成」。存整数而非枚举：本类型是增量缓存的键。
        Integrations = IntegrationFlags.Normalize(AttributeReader.GetEnumValue(attribute, IntegrationsArgumentName))
            ?? defaultIntegrations
            ?? IntegrationFlags.All;
        HasSystemTextJsonConverter = WillHaveNestedType(type, SystemTextJsonConverterTypeName);
        HasNewtonsoftJsonConverter = WillHaveNestedType(type, NewtonsoftJsonConverterTypeName);
        // 命名实例在构造期一次性渲染成文本：本类型是增量缓存的键，集合成员会退化成引用比较而让缓存永久失效。
        InstancesBlock = RenderInstancesBlock(type, primitiveIdType);
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
    /// <see cref="Name"/> 取自 <see cref="ISymbol.Name"/>，而它对 <c>class</c>、<c>return</c>、<c>int</c>
    /// 这类保留关键字不加转义。把它原样写进类型声明位，编译器会把名字吃成关键字：
    /// <c>partial record struct class :</c> 的类型名变成空串，进而爆出 CS0260 / CS1520 / CS0101 /
    /// CS9348 等一串错误，成员整体掉出类型落进全局命名空间。
    /// <para>
    /// 上下文关键字（<c>var</c> / <c>record</c> / <c>nameof</c> 等）也一并转义：它们单独出现时是合法
    /// 标识符，但本生成器自己就会在产物里写出 <c>out var id</c> 这类局部变量声明，Id 叫 <c>var</c>
    /// 时那个 <c>var</c> 会被当成隐式类型声明而不是类型名。加 <c>@</c> 对任何关键字都是合法且不改变
    /// 语义的。因此普通名字下 <see cref="EscapedName"/> 与 <see cref="Name"/> 逐字节相同，
    /// 既有生成代码与快照测试不受影响。
    /// </para>
    /// <para>
    /// 拼装生成源码时一律用本属性，不要用 <see cref="Name"/>。唯一的例外是名字要作为「非代码文本」
    /// 使用的场合——例如 ASP.NET Core 路由约束的令牌（<c>options.ConstraintMap["OrderId"]</c>），
    /// 那里比较的是路由模板里的字面量，加 <c>@</c> 会改变语义。
    /// </para>
    /// </remarks>
    public string EscapedName => EscapeIdentifier(Name);

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
    /// <c>DateOnly</c> / <c>TimeOnly</c>）。
    /// </summary>
    /// <remarks>
    /// 这些类型没有对应的 <c>PrimitiveTypeCode</c>，<c>JsonWriter.WriteValue</c> 会抛异常，故改走规范文本形。
    /// </remarks>
    public bool IsNewtonsoftTextPrimitive { get; }

    /// <summary>
    /// 基元是否缺少 MessagePack / BSON 的原生表示（<c>BigInteger</c> / <c>Int128</c> / <c>UInt128</c>）。
    /// </summary>
    /// <remarks>
    /// 为 <see langword="true"/> 时两种二进制格式改走规范文本形，否则会在运行期抛「格式化器未注册」。
    /// </remarks>
    public bool IsBinaryTextPrimitive { get; }

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
    /// 类型做范围检查再交给 <c>Enum.ToObject</c> —— 后者对超范围整数静默绕回（<c>300L</c> 交给 <c>byte</c>
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
    /// 打开后 <c>Create</c> 的消息是 <c>The value is not a valid OrderId.</c>，不再内插原始值 —— 后者往往是用户输入。
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
    /// 存渲染后的文本而非实例集合：本类型是 <c>readonly record struct</c>，<see cref="ImmutableArray{T}"/> 的引用相等性会让缓存永久失效。
    /// 未声明实例或全部非法时为空串，产物与加入该能力之前逐字节一致。
    /// </remarks>
    public string InstancesBlock { get; }

    /// <summary>
    /// 渲染全部合法命名实例的成员块；没有合法实例时为空串。
    /// </summary>
    /// <remarks>
    /// 非法项静默跳过：分析器会为它们报 <c>STIA0020</c> / <c>STIA0021</c>；生成器绝不能抛异常，那会降级成 <c>STIA0012</c> 盖掉真正原因。
    /// </remarks>
    private static string RenderInstancesBlock(ITypeSymbol type, ITypeSymbol primitiveType)
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
            builder.Append("    /// Gets the <c>").Append(instance.Name).Append("</c> instance of <c>").Append(EscapeIdentifier(type.Name)).Append("</c>.").Append('\n');
            builder.Append("    /// </summary>").Append('\n');
            builder.Append("    ").Append(GeneratedCode.CompilerGeneratedAttribute).Append('\n');
            builder.Append("    ").Append(GeneratedCode.GeneratedCodeAttribute(nameof(Generators.StronglyTypedIdCodeGenerator), GeneratedCode.Version)).Append('\n');
            // 字段类型是 Id 自身、字段名是实例名，两者都要转义：Id 叫 class 或实例叫 class 时，
            // 原样写出 "public static readonly class Empty" / "public static readonly OrderId class" 都是语法错误。
            // 实例名转义只在落盘这一刻做，IsReservedName 的撞名判定仍用原始名，两者判据一致。
            builder.Append("    public static readonly ").Append(EscapeIdentifier(type.Name)).Append(' ').Append(EscapeIdentifier(instance.Name)).Append(" = Create(").Append(instance.Expression).Append(");");
        }

        return builder.ToString();
    }

    /// <summary>
    /// 读取 <c>[assembly: StronglyTypedIdDefaults(RedactValidationMessage = …)]</c>；未声明时为
    /// <see langword="false"/>。
    /// </summary>
    public static bool GetAssemblyDefaultRedactValidationMessage(IAssemblySymbol assembly)
        => AttributeReader.GetBoolean(
            AttributeReader.Find(assembly, AttributeReader.StronglyTypedIdDefaultsAttributeName),
            RedactValidationMessageArgumentName);

    /// <summary>
    /// 读取 <c>[assembly: StronglyTypedIdDefaults(Integrations = …)]</c> 并规范化；未声明时为
    /// <see langword="null"/>。
    /// </summary>
    public static int? GetAssemblyDefaultIntegrations(IAssemblySymbol assembly)
        => IntegrationFlags.Normalize(AttributeReader.GetEnumValue(
            AttributeReader.Find(assembly, AttributeReader.StronglyTypedIdDefaultsAttributeName),
            IntegrationsArgumentName));

    /// <summary>
    /// 读取 <c>[assembly: StronglyTypedIdDefaults(Validator = …)]</c> 的默认验证器方法名；未声明时为 <see langword="null"/>。
    /// </summary>
    public static string? GetAssemblyDefaultValidator(IAssemblySymbol assembly)
        => AttributeReader.GetString(
            AttributeReader.Find(assembly, AttributeReader.StronglyTypedIdDefaultsAttributeName),
            ValidatorArgumentName);

    /// <summary>
    /// 读取 <c>[assembly: StronglyTypedIdDefaults(Normalizer = …)]</c> 的默认归一化方法名；未声明时为 <see langword="null"/>。
    /// </summary>
    public static string? GetAssemblyDefaultNormalizer(IAssemblySymbol assembly)
        => AttributeReader.GetString(
            AttributeReader.Find(assembly, AttributeReader.StronglyTypedIdDefaultsAttributeName),
            NormalizerArgumentName);

    /// <summary>
    /// 读取 <c>[assembly: StronglyTypedIdDefaults(ExceptionType = typeof(…))]</c> 的默认异常类型符号；未声明时为 <see langword="null"/>。
    /// </summary>
    public static ITypeSymbol? GetAssemblyDefaultExceptionTypeSymbol(IAssemblySymbol assembly)
        => AttributeReader.GetType(
            AttributeReader.Find(assembly, AttributeReader.StronglyTypedIdDefaultsAttributeName),
            ExceptionTypeArgumentName);

    /// <summary>
    /// 读取 <c>[assembly: StronglyTypedIdDefaults(ExceptionType = typeof(…))]</c> 的默认异常类型显示名；未声明时为 <see langword="null"/>。
    /// </summary>
    /// <remarks>
    /// 生成器需要的是可直接写进产物的文本，分析器需要的是符号（要校验它是不是异常类型），故两个入口并存。
    /// </remarks>
    public static string? GetAssemblyDefaultExceptionType(IAssemblySymbol assembly)
        => GetAssemblyDefaultExceptionTypeSymbol(assembly)?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    /// <summary>
    /// 解析强类型 Id 所包装的基元类型符号。
    /// </summary>
    /// <remarks>
    /// 一个类型可以同时实现多个 <c>IStronglyTypedId&lt;,&gt;</c>（手写类型或跨版本继承）。此时必须选
    /// <c>TSelf</c> 恰好等于该类型的那个接口 —— 那才是「它自己」的契约。直接取 <c>FirstOrDefault</c>
    /// 会让结果取决于接口枚举顺序，与运行时的 <c>StronglyTypedIdExtensions.ResolvePrimitiveIdType</c>
    /// 给出不同答案（模型绑定器与 EF 转换器按后者选转换器）。
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

            // 构造回退：同样要跳过参数类型等于自身的那个 —— 没有主构造函数的 record 会把合成的拷贝构造
            // 排在 Constructors 首位，取 FirstOrDefault 会绑到 record 自身，得到一个「基元是自己」的假象。
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

        // 必须复现类型参数表（否则 CS0693），但约束子句只需在任一 partial 上声明一次，故省略 where。
        // 类型参数名同样要转义：形参叫 class 时原样写出 "<class>" 就是语法错误。
        var typeParameterList = type.TypeParameters.Length > 0
            ? "<" + string.Join(", ", type.TypeParameters.Select(parameter => EscapeIdentifier(parameter.Name))) + ">"
            : string.Empty;

        // 包含类型名同样要转义：容器叫 return 时原样写出 "internal partial class return" 就是语法错误。
        return $"{GetAccessibilityKeyword(type.DeclaredAccessibility)} partial {keyword} {EscapeIdentifier(type.Name)}{typeParameterList}";
    }

    /// <summary>
    /// 判断该 Id 是否带有（或本次生成即将产出）指定的内嵌转换器类型。
    /// </summary>
    /// <remarks>
    /// 两种情况：
    /// <list type="bullet">
    /// <item>引用程序集里的 Id —— 接口已实现，转换器也已生成，直接查嵌套类型是否存在；</item>
    /// <item>本次编译声明的 Id —— 生成器还没跑，接口尚未实现，此时按「没实现接口 ⇒ 是声明类型 ⇒ 会生成」推断。</item>
    /// </list>
    /// 两个分支拆开写是为了让第二种情况的「推断」性质显式可见，而不是藏在一个布尔表达式里。
    /// </remarks>
    private static bool WillHaveNestedType(ITypeSymbol type, string nestedTypeName)
        => type is INamedTypeSymbol namedType
            && (AlreadyHasNestedType(namedType, nestedTypeName) || IsDeclaredInThisCompilation(namedType));

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
    /// 把标识符转成可安全写入 C# 源码的形式：C# 关键字（保留关键字与上下文关键字）加 <c>@</c> 前缀，其余原样返回。
    /// </summary>
    /// <remarks>
    /// 判据走 Roslyn 自己的关键字表而非自己维护一份：表会漏，且将来新增关键字时不会有人想起回来改这里。
    /// <see cref="SyntaxFacts.GetKeywordKind"/> 查保留关键字、<see cref="SyntaxFacts.GetContextualKeywordKind"/>
    /// 查上下文关键字，两者都返回 <see cref="SyntaxKind.None"/> 表示不是关键字。
    /// <para>
    /// 为什么上下文关键字也要转义：它们单独出现时确实是合法标识符，但本生成器自己就会在产物里写出
    /// <c>out var id</c> / <c>out var val</c> 这类局部变量声明。Id 若叫 <c>var</c>，
    /// <c>out var result</c> 里的 <c>var</c> 会被当成隐式类型声明而不是类型名，
    /// 进而 CS1503 / CS8508 一串错误。加 <c>@</c> 对任何关键字都是合法且不改变语义的，故一律转义。
    /// </para>
    /// <para>
    /// <see cref="IsKeywordMissingFromRoslynTables"/> 是补丁：C# 规范把这些词列为上下文关键字
    /// （词法结构 §6.4.5 的 contextual_keyword 规则），但 Roslyn 的 <see cref="SyntaxFacts"/>
    /// 两张表都不收录它们。其中 <c>value</c> 有实证危害——生成器在 STJ 读取路径里写出
    /// <c>var value = ...Deserialize&lt;Guid&gt;(...); return value.TryCreate(...)</c>，
    /// 局部变量 <c>value</c> 会遮蔽同名类型，CS1061「Guid 不包含 TryCreate」。
    /// 其余几个（<c>nint</c> / <c>nuint</c> / <c>dynamic</c> 等）在当前发射模式下尚无实测危害，
    /// 但同属规范上下文关键字，一并转义：不依赖「恰好没有冲突位置」这种运气。
    /// 列表按 Roslyn 5.9.0 实测得出；将来 Roslyn 若收录其中某个词，这里只是变得冗余，无害。
    /// </para>
    /// </remarks>
    internal static string EscapeIdentifier(string name)
        => SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None
            || SyntaxFacts.GetContextualKeywordKind(name) != SyntaxKind.None
            || IsKeywordMissingFromRoslynTables(name)
                ? "@" + name
                : name;

    /// <summary>
    /// C# 规范列为上下文关键字、但 Roslyn 5.9 的 <see cref="SyntaxFacts"/> 两张关键字表均不收录的词。
    /// </summary>
    /// <remarks>
    /// 清单来自规范的 contextual_keyword 规则（词法结构 §6.4.5），逐词对照
    /// <see cref="SyntaxFacts.GetKeywordKind"/> / <see cref="SyntaxFacts.GetContextualKeywordKind"/>
    /// 实测得出。将来 Roslyn 若收录其中某个词，这里只是变得冗余，无害。
    /// </remarks>
    private static bool IsKeywordMissingFromRoslynTables(string name)
        => name is "Cdecl" or "Stdcall" or "Thiscall"  // 函数指针调用约定
            or "dynamic" or "nint" or "nuint"          // 预定义类型一类的伪关键字
            or "value" or "notnull";                   // 访问器 / 泛型约束位置的上下文化标识符

    /// <summary>
    /// 把类型显示名净化成合法的 hint 名：只保留 Unicode 字母、数字与 <c>_</c> <c>.</c>，其余一律剔除。
    /// </summary>
    /// <remarks>
    /// 白名单而非黑名单：<see cref="FullName"/> 来自 <c>ToDisplayString()</c>，除了尖括号、逗号、
    /// 空格，还会因为关键字转义而带上 <c>@</c>（<c>@int.@return</c>），泛型包含类型的类型参数甚至
    /// 可能带来 <c>[]</c>。Roslyn 的 <c>AddSource</c> 会把其中的 <c>@</c> 判为非法字符并抛
    /// <see cref="ArgumentException"/>，表现为 STIA0012「生成器失败」且本类型全部输出被丢弃。
    /// 黑名单只能逐个补字符，白名单一次覆盖全部非法字符。
    /// <para>
    /// 剔除会让不同全名塌陷到同一 hint 名，撞名时 <c>AddSource</c> 抛异常并降级成 STIA0012；故只要确实剔掉了字符，就追加原始全名的稳定哈希。
    /// </para>
    /// </remarks>
    private static string SanitizeHintName(string fullName)
    {
        var sanitized = string.Concat(fullName.Where(static c => char.IsLetterOrDigit(c) || c is '_' or '.'));

        // 净化只会删字符，长度不变即代表一个字符都没动，此时保持 hint 名原样。
        return sanitized.Length == fullName.Length
            ? sanitized
            : $"{sanitized}_{ComputeStableHash(fullName)}";
    }

    /// <summary>
    /// 计算与进程无关的 32 位 FNV-1a 哈希，用作 hint 名的消歧后缀。
    /// </summary>
    /// <remarks>
    /// 不能用 <see cref="string.GetHashCode()"/>：它按进程随机化，会让同一份源码产出不同的 hint 名。
    /// </remarks>
    private static string ComputeStableHash(string value)
    {
        const uint OffsetBasis = 2166136261;
        const uint Prime = 16777619;

        var hash = OffsetBasis;

        foreach (var character in value)
        {
            hash ^= character;
            hash *= Prime;
        }

        return hash.ToString("x8", System.Globalization.CultureInfo.InvariantCulture);
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
    internal static bool HasFlagsAttribute(ITypeSymbol type)
        => type.GetAttributes().Any(attribute =>
            attribute.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.FlagsAttribute");

    /// <summary>
    /// 计算 <c>[Flags]</c> 枚举里「没有任何成员占用」的位，供 <c>TryParse</c> 做位校验。
    /// </summary>
    /// <remarks>
    /// <c>Enum.TryParse</c> 接受任意数值文本，<c>[Flags]</c> 又不能用 <c>Enum.IsDefined</c> 收紧，故改为「不许出现未定义的位」。
    /// 掩码按底层类型宽度裁剪：<c>sbyte</c> 的 <c>-1</c> 在 <see cref="ulong"/> 里是满位，不裁剪会把整个掩码吃掉。
    /// </remarks>
    internal static ulong? GetFlagsEnumUndefinedBitsMask(ITypeSymbol enumType)
    {
        if (enumType is not INamedTypeSymbol { EnumUnderlyingType: { } underlying } namedEnum)
        {
            return null;
        }

        var width = GetIntegralBitWidth(underlying.SpecialType);

        if (width == 0)
        {
            return null;
        }

        var widthMask = width == 64 ? ulong.MaxValue : (1UL << width) - 1;
        var definedBits = 0UL;

        foreach (var member in namedEnum.GetMembers())
        {
            if (member is IFieldSymbol { HasConstantValue: true, ConstantValue: { } constant })
            {
                definedBits |= ToBits(constant);
            }
        }

        var undefinedBits = ~definedBits & widthMask;

        return undefinedBits == 0 ? null : undefinedBits;
    }

    /// <summary>
    /// 取整型基元类型的位宽；非整型返回 0。
    /// </summary>
    internal static int GetIntegralBitWidth(SpecialType type)
        => type switch
        {
            SpecialType.System_SByte or SpecialType.System_Byte => 8,
            SpecialType.System_Int16 or SpecialType.System_UInt16 => 16,
            SpecialType.System_Int32 or SpecialType.System_UInt32 => 32,
            SpecialType.System_Int64 or SpecialType.System_UInt64 => 64,
            _ => 0,
        };

    /// <summary>
    /// 取「枚举值是否落在已定义取值内」的条件表达式；非枚举基元、或无需校验时返回 <see langword="null"/>。
    /// </summary>
    /// <remarks>
    /// <paramref name="variableName"/> 是产物里持有该枚举值的局部变量名，条件按它渲染。
    /// 判据与 <c>TryParse</c> 的枚举分支同源：普通枚举查 <see cref="Enum.IsDefined"/>，
    /// <c>[Flags]</c> 枚举查「不许出现未定义的位」（全部位都已定义时为 <see langword="null"/>，调用方直接跳过）。
    /// <para>
    /// 存在的意义是让每条「文本/数值 → Id」的入口都做同一道校验：<c>TryCreate</c> 只跑用户配的
    /// Validator，而 <c>Enum.TryParse</c> 与各 JSON 读取路径对未定义数值一律放行 —— 不补这一道，
    /// <c>999</c> 会变成一个不对应任何成员的 Id，后续 switch 落空、序列化写出野值。
    /// 用非泛型重载：泛型 <c>Enum.IsDefined&lt;T&gt;(T)</c> 是 .NET 5+ 才有的，生成代码要照顾老 TFM 的消费者。
    /// </para>
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
    /// 把枚举成员的常量值按位展开成 <see cref="ulong"/>（负数按补码展开，随后由宽度掩码裁掉高位）。
    /// </summary>
    internal static ulong ToBits(object constant)
        => constant switch
        {
            sbyte value => unchecked((ulong)value),
            byte value => value,
            short value => unchecked((ulong)value),
            ushort value => value,
            int value => unchecked((ulong)value),
            uint value => value,
            long value => unchecked((ulong)value),
            ulong value => value,
            _ => 0UL,
        };

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
        // 没有继承来的 TryFormat，但这条判定是「按成员名 + 参数形状」猜的 —— 将来若加入某个自带
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
