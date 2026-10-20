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
        int? defaultIntegrations = null)
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
        // 属性只查一次：下面五项都要读命名实参，逐项重查会把 GetAttributes 与显示名格式化各做五遍。
        var attribute = AttributeReader.Find(type, AttributeReader.StronglyTypedIdAttributeName);
        ValidatorName = AttributeReader.GetString(attribute, ValidatorArgumentName) ?? defaultValidatorName;
        NormalizerName = AttributeReader.GetString(attribute, NormalizerArgumentName) ?? defaultNormalizerName;
        // 存显示名而非符号：本类型是增量缓存的键，符号跨编译不稳定，显示名才是稳定值。
        ExceptionTypeName = AttributeReader.GetType(attribute, ExceptionTypeArgumentName)
                ?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            ?? defaultExceptionTypeName;
        HasTypeConverter = AttributeReader.GetBoolean(attribute, TypeConverterArgumentName);
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
    /// 可作为生成源 hint 名的类型显示名：剔除尖括号、逗号与空格。
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
    /// System.Text.Json 没有 BigInteger 的内置转换器，<c>JsonSerializer</c> 会退化成按公开属性序列化
    /// （<c>{"IsPowerOfTwo":false,…,"Sign":1}</c>），读回来又是 <c>default</c>。故它的转换器改走规范文本形。
    /// </remarks>
    public bool IsBigIntegerPrimitive { get; }

    /// <summary>
    /// 基元是否为 Newtonsoft.Json 无法按值读写的类型（<c>Int128</c> / <c>UInt128</c> / <c>Half</c> /
    /// <c>DateOnly</c> / <c>TimeOnly</c>）。
    /// </summary>
    /// <remarks>
    /// 这些类型没有对应的 <c>PrimitiveTypeCode</c>，<c>JsonWriter.WriteValue</c> 会抛
    /// <c>JsonWriterException</c>。它们的转换器改走规范文本形，读写都经 <c>Id.TryParse</c>。
    /// </remarks>
    public bool IsNewtonsoftTextPrimitive { get; }

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
    /// 全部位都已定义时（典型是 <c>All = -1</c>）为 <see langword="null"/>，调用方据此跳过校验、产物与加入该能力之前一致。
    /// </remarks>
    public ulong? FlagsEnumUndefinedBitsMask { get; }

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
    /// 判断给定集成是否被选中。仍须由调用方另外确认「当前编译确实引用了对应程序集」。
    /// </summary>
    public bool Supports(int integration) => (Integrations & integration) == integration;

    /// <summary>
    /// 命名实例的渲染块：每条实例的文档注释、生成器标注与 <c>public static readonly</c> 字段，
    /// 自带产物中的绝对缩进（成员级 4 空格），块首是两个换行。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 存渲染后的文本而非实例集合，是因为本类型是 <c>readonly record struct</c>，成员级相等性就是增量缓存的键：
    /// <see cref="ImmutableArray{T}"/> 的相等性是引用比较，放进来会让该 Id 的缓存永久失效。
    /// </para>
    /// <para>
    /// 未声明任何实例、或声明的全部非法时为空串，此时生成产物与加入该能力之前逐字节一致。
    /// </para>
    /// </remarks>
    public string InstancesBlock { get; }

    /// <summary>
    /// 渲染全部合法命名实例的成员块；没有合法实例时为空串。
    /// </summary>
    /// <remarks>
    /// 非法项静默跳过：分析器会为它们报 <c>STIA0020</c> / <c>STIA0021</c>（Error，构建必然失败）。
    /// 生成器绝不能为此抛异常 —— 那会降级成 <c>STIA0012</c>，把真正的原因盖掉。
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
            builder.Append("    /// Gets the <c>").Append(instance.Name).Append("</c> instance of <c>").Append(type.Name).Append("</c>.").Append('\n');
            builder.Append("    /// </summary>").Append('\n');
            builder.Append("    ").Append(GeneratedCode.CompilerGeneratedAttribute).Append('\n');
            builder.Append("    ").Append(GeneratedCode.GeneratedCodeAttribute(nameof(Generators.StronglyTypedIdCodeGenerator), GeneratedCode.Version)).Append('\n');
            builder.Append("    public static readonly ").Append(type.Name).Append(' ').Append(instance.Name).Append(" = Create(").Append(instance.Expression).Append(");");
        }

        return builder.ToString();
    }

    /// <summary>
    /// 读取 <c>[assembly: StronglyTypedIdDefaults(Integrations = …)]</c> 并规范化；未声明时为
    /// <see langword="null"/>。
    /// </summary>
    public static int? GetAssemblyDefaultIntegrations(IAssemblySymbol assembly)
        => IntegrationFlags.Normalize(AttributeReader.GetEnumValue(
            AttributeReader.Find(assembly, AttributeReader.StronglyTypedIdDefaultsAttributeName),
            IntegrationsArgumentName));

    /// <summary>
    /// 解析强类型 Id 所包装的基元类型符号。
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// 该类型既未实现 <c>IStronglyTypedId&lt;TSelf, TPrimitiveId&gt;</c>，也不具备单参数构造函数。
    /// </exception>
    private static ITypeSymbol GetPrimitiveIdType(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol namedType)
        {
            var stronglyTypedIdInterface = namedType.Interfaces.FirstOrDefault(IsStronglyTypedIdInterface);

            if (stronglyTypedIdInterface is not null)
            {
                return stronglyTypedIdInterface.TypeArguments[1];
            }

            var constructor = namedType.Constructors.FirstOrDefault(candidate => candidate.Parameters.Length == 1);

            if (constructor is not null)
            {
                return constructor.Parameters[0].Type;
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
        var typeParameterList = type.TypeParameters.Length > 0
            ? "<" + string.Join(", ", type.TypeParameters.Select(parameter => parameter.Name)) + ">"
            : string.Empty;

        return $"{GetAccessibilityKeyword(type.DeclaredAccessibility)} partial {keyword} {type.Name}{typeParameterList}";
    }

    /// <summary>
    /// 判断该 Id 是否带有（或本次生成即将产出）指定的内嵌转换器类型。
    /// </summary>
    private static bool WillHaveNestedType(ITypeSymbol type, string nestedTypeName)
        => type is INamedTypeSymbol namedType
            && (namedType.GetTypeMembers(nestedTypeName).Length > 0
                || !namedType.Interfaces.Any(IsStronglyTypedIdInterface));

    /// <summary>
    /// 把基元类型的显示名归一化为不含命名空间限定的短名。
    /// </summary>
    internal static string GetPrimitiveIdTypeShortName(string primitiveIdTypeName)
    {
        var separatorIndex = primitiveIdTypeName.LastIndexOf('.');

        return separatorIndex < 0 ? primitiveIdTypeName : primitiveIdTypeName[(separatorIndex + 1)..];
    }

    /// <summary>
    /// 把类型显示名净化成合法的 hint 名：剔除尖括号、逗号与空格。
    /// </summary>
    /// <remarks>
    /// 剔除会让不同的全名塌陷到同一个 hint 名（<c>Ns.Outer&lt;T&gt;.Inner</c> 与 <c>Ns.OuterT.Inner</c> 都会变成
    /// <c>Ns.OuterT.Inner</c>），撞名时 <c>AddSource</c> 抛重复 hint 名，最终降级成 STIA0012。因此只要确实剔掉了字符，
    /// 就在末尾追加原始全名的稳定哈希，把塌陷的两个名字重新分开。
    /// </remarks>
    private static string SanitizeHintName(string fullName)
    {
        var sanitized = fullName
            .Replace("<", string.Empty)
            .Replace(">", string.Empty)
            .Replace(",", string.Empty)
            .Replace(" ", string.Empty);

        // 净化只会删字符，长度不变即代表一个字符都没动，此时保持 hint 名原样。
        return sanitized.Length == fullName.Length
            ? sanitized
            : $"{sanitized}_{ComputeStableHash(fullName)}";
    }

    /// <summary>
    /// 计算与进程无关的 32 位 FNV-1a 哈希，用作 hint 名的消歧后缀。
    /// </summary>
    /// <remarks>
    /// 不能用 <see cref="string.GetHashCode()"/>：它按进程随机化，会让同一份源码在不同构建之间产出不同的 hint 名。
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
    /// <para>
    /// <c>Enum.TryParse</c> 对任意数值文本都返回 <see langword="true"/>，<c>[Flags]</c> 枚举又不能用
    /// <c>Enum.IsDefined</c> 收紧（位组合本身就是合法取值），于是改成「不许出现未定义的位」：
    /// 掩码由全部成员的常量按位或取反得出，编译期算好，运行期只剩一次按位与。
    /// </para>
    /// <para>
    /// 掩码按底层类型的宽度裁剪。<c>sbyte</c> 的 <c>-1</c> 在 <see cref="ulong"/> 里是满位，不裁剪就会把整个掩码
    /// 吃掉；裁剪后 <c>All = -1</c> 这类「全位已定义」的枚举得到 0，调用方据此跳过校验。
    /// </para>
    /// </remarks>
    /// <returns>未定义位的掩码；全部位都已定义（或无法确定底层类型）时为 <see langword="null"/>。</returns>
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
    /// <param name="type">基元类型。</param>
    /// <param name="spanElementType">目标缓冲区元素类型：UTF-16 为 <c>char</c>，UTF-8 为 <c>byte</c>。</param>
    internal static TryFormatKind GetTryFormatKind(ITypeSymbol type, SpecialType spanElementType)
    {
        var hasProviderOverload = false;
        var hasPlainOverload = false;

        foreach (var member in type.GetMembers("TryFormat"))
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
