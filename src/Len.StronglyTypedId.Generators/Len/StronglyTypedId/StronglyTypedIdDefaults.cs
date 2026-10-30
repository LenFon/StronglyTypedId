namespace Len.StronglyTypedId;

/// <summary>
/// 程序集级 <c>[assembly: StronglyTypedIdDefaults(...)]</c> 的生效值，作为 <see cref="StronglyTypedIdInfo"/>
/// 构造函数的参数对象。
/// </summary>
/// <remarks>
/// 这九项总是一起从同一个程序集读出、一起传给构造函数，属于典型的 Data Clump：拆成九个可选参数会让
/// 每个调用点重复同样的读取序列，且新增一项就要改所有调用点。打包成一个值对象后，调用方只需
/// <see cref="Read"/> 一次。
/// <para>
/// 用普通 <see langword="readonly"/> 结构体而非位置记录：生成器跑在 netstandard2.0 上，没有 <c>IsExternalInit</c>。
/// </para>
/// </remarks>
internal readonly struct StronglyTypedIdDefaults(
    string? validatorName,
    string? normalizerName,
    string? exceptionTypeName,
    int? integrations,
    bool redactValidationMessage,
    int? jsonShape,
    bool typeConverter,
    int? prefixReadMode)
{
    /// <summary>
    /// 全部取默认值（未声明任何程序集级默认）。
    /// </summary>
    public static StronglyTypedIdDefaults Empty => default;

    /// <summary>
    /// 默认验证器方法名；未声明时为 <see langword="null"/>。
    /// </summary>
    public string? ValidatorName { get; } = validatorName;

    /// <summary>
    /// 默认归一化方法名；未声明时为 <see langword="null"/>。
    /// </summary>
    public string? NormalizerName { get; } = normalizerName;

    /// <summary>
    /// 默认异常类型全限定名；未声明时为 <see langword="null"/>。
    /// </summary>
    public string? ExceptionTypeName { get; } = exceptionTypeName;

    /// <summary>
    /// 默认集成掩码（已规范化）；未声明时为 <see langword="null"/>。
    /// </summary>
    public int? Integrations { get; } = integrations;

    /// <summary>
    /// 默认是否隐去验证失败消息中的原始值。
    /// </summary>
    public bool RedactValidationMessage { get; } = redactValidationMessage;

    /// <summary>
    /// 默认 JSON 形态的底层值；未声明时为 <see langword="null"/>。
    /// </summary>
    public int? JsonShape { get; } = jsonShape;

    /// <summary>
    /// 默认是否生成 <c>TypeConverter</c>。
    /// </summary>
    public bool TypeConverter { get; } = typeConverter;

    /// <summary>
    /// 默认前缀读入策略的底层值；未声明时为 <see langword="null"/>。
    /// </summary>
    public int? PrefixReadMode { get; } = prefixReadMode;

    /// <summary>
    /// 从程序集读取 <c>[assembly: StronglyTypedIdDefaults(...)]</c> 的全部生效值。
    /// </summary>
    /// <remarks>
    /// 属性只查一次：九项都要读命名实参，逐项重查会把 <c>GetAttributes</c> 与显示名格式化各做九遍。
    /// </remarks>
    public static StronglyTypedIdDefaults Read(IAssemblySymbol assembly)
    {
        var attribute = AttributeReader.Find(assembly, AttributeReader.StronglyTypedIdDefaultsAttributeName);

        return new StronglyTypedIdDefaults(
            AttributeReader.GetString(attribute, StronglyTypedIdInfo.ValidatorArgumentName),
            AttributeReader.GetString(attribute, StronglyTypedIdInfo.NormalizerArgumentName),
            AttributeReader.GetType(attribute, StronglyTypedIdInfo.ExceptionTypeArgumentName)
                ?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            IntegrationFlags.Normalize(AttributeReader.GetEnumValue(attribute, StronglyTypedIdInfo.IntegrationsArgumentName)),
            AttributeReader.GetBoolean(attribute, StronglyTypedIdInfo.RedactValidationMessageArgumentName),
            AttributeReader.GetEnumValue(attribute, StronglyTypedIdInfo.JsonShapeArgumentName),
            AttributeReader.GetBoolean(attribute, StronglyTypedIdInfo.TypeConverterArgumentName),
            AttributeReader.GetEnumValue(attribute, StronglyTypedIdInfo.PrefixReadModeArgumentName));
    }

    /// <summary>
    /// 读取默认异常类型符号；分析器要校验它是不是异常类型，故与显示名入口并存。
    /// </summary>
    public static ITypeSymbol? ReadExceptionTypeSymbol(IAssemblySymbol assembly)
        => AttributeReader.GetType(
            AttributeReader.Find(assembly, AttributeReader.StronglyTypedIdDefaultsAttributeName),
            StronglyTypedIdInfo.ExceptionTypeArgumentName);
}
