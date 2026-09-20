namespace Len.StronglyTypedId;

/// <summary>
/// 集中定义强类型 Id 分析器的全部诊断：规则 ID、严重级别与本地化后的标题/提示。
/// </summary>
internal static class Descriptors
{
    private const string Category = "StronglyTypedIdAnalyzer";

    public const string ContainingTypeMustBePartialId = "STIAO009";
    public const string ParameterCannotBeNullableId = "STIAO006";
    public const string ParameterNameMustBeValueId = "STIAO007";
    public const string ParameterTypeIsInvalidId = "STIAO008";
    public const string ValidatorReferenceInvalidId = "STIAO010";
    public const string BypassCreateId = "STIAO011";
    public const string TypeCannotBeAbstractId = "STIAO002";
    public const string TypeCannotBeGenericId = "STIAO003";
    public const string TypeMustHaveNamespaceId = "STIAO004";
    public const string TypeMustBePartialId = "STIAO001";
    public const string TypeMustBeRecordId = "STIAO000";
    public const string TypeMustHaveSingleParameterPrimaryConstructorId = "STIAO005";

    /// <summary>
    /// 强类型 Id 的包含类型不是 partial，或包含类型是 file 本地类型。
    /// </summary>
    /// <remarks>
    /// 两种情况同一条规则，因为要求同出一源：生成代码要新增的是嵌套 Id 自身的成员，而 partial 的各段
    /// 必须处于同一容器内，所以只能把这个容器在生成的文件里重新打开一次。缺 partial 的容器做不到；
    /// <c>file</c> 本地类型则连「在另一个文件里重开」这条路本身都不存在。
    /// </remarks>
    public static readonly DiagnosticDescriptor ContainingTypeMustBePartial
        = new(ContainingTypeMustBePartialId,
            new LocalizedString("ContainingTypeMustBePartialTitle"),
            new LocalizedString("ContainingTypeMustBePartialMessage"),
            Category,
            DiagnosticSeverity.Error,
            true);

    public static readonly DiagnosticDescriptor ParameterCannotBeNullable
        = new(ParameterCannotBeNullableId,
            new LocalizedString("ParameterCannotBeNullableTitle"),
            new LocalizedString("ParameterCannotBeNullableMessage"),
            Category,
            DiagnosticSeverity.Error,
            true);

    public static readonly DiagnosticDescriptor ParameterNameMustBeValue
        = new(ParameterNameMustBeValueId,
            new LocalizedString("ParameterNameMustBeValueTitle"),
            new LocalizedString("ParameterNameMustBeValueMessage"),
            Category,
            DiagnosticSeverity.Error,
            true);

    public static readonly DiagnosticDescriptor ParameterTypeIsInvalid
        = new(ParameterTypeIsInvalidId,
            new LocalizedString("ParameterTypeIsInvalidTitle"),
            new LocalizedString("ParameterTypeIsInvalidMessage"),
            Category,
            DiagnosticSeverity.Error,
            true);

    public static readonly DiagnosticDescriptor TypeCannotBeAbstract
        = new(TypeCannotBeAbstractId,
            new LocalizedString("TypeCannotBeAbstractTitle"),
            new LocalizedString("TypeCannotBeAbstractMessage"),
            Category,
            DiagnosticSeverity.Error,
            true);

    public static readonly DiagnosticDescriptor TypeCannotBeGeneric
        = new(TypeCannotBeGenericId,
            new LocalizedString("TypeCannotBeGenericTitle"),
            new LocalizedString("TypeCannotBeGenericMessage"),
            Category,
            DiagnosticSeverity.Error,
            true);

    public static readonly DiagnosticDescriptor TypeMustHaveNamespace
        = new(TypeMustHaveNamespaceId,
            new LocalizedString("TypeMustHaveNamespaceTitle"),
            new LocalizedString("TypeMustHaveNamespaceMessage"),
            Category,
            DiagnosticSeverity.Error,
            true);

    public static readonly DiagnosticDescriptor TypeMustBePartial
        = new(TypeMustBePartialId,
            new LocalizedString("TypeMustBePartialTitle"),
            new LocalizedString("TypeMustBePartialMessage"),
            Category,
            DiagnosticSeverity.Error,
            true);

    public static readonly DiagnosticDescriptor TypeMustBeRecord
       = new(TypeMustBeRecordId,
            new LocalizedString("TypeMustBeRecordTitle"),
            new LocalizedString("TypeMustBeRecordMessage"),
            Category,
            DiagnosticSeverity.Error,
            true);

    public static readonly DiagnosticDescriptor TypeMustHaveSingleParameterPrimaryConstructor
        = new(TypeMustHaveSingleParameterPrimaryConstructorId,
            new LocalizedString("TypeMustHaveSingleParameterPrimaryConstructorTitle"),
            new LocalizedString("TypeMustHaveSingleParameterPrimaryConstructorMessage"),
            Category,
            DiagnosticSeverity.Error,
            true);

    /// <summary>
    /// <c>[StronglyTypedId(Validator = nameof(Foo))]</c> 指向的方法不存在、非静态、返回类型不是 <see cref="bool"/>，
    /// 或形参类型不等于基元 Id 类型。
    /// </summary>
    /// <remarks>
    /// 这是结构性缺口：一旦写错，错误会跑到生成代码里变成 CS0103 / CS0117 / CS1503，位置和原因都极难理解，
    /// 且踩在 CS8785（生成器整段产出被丢弃、全项目生成归零）的风险链上。在分析器里提前校验，
    /// 错误直接落在 attribute 上，属于纯新增诊断、不改任何生成逻辑，风险最低。
    /// </remarks>
    public static readonly DiagnosticDescriptor ValidatorReferenceInvalid
        = new(ValidatorReferenceInvalidId,
            new LocalizedString("ValidatorReferenceInvalidTitle"),
            new LocalizedString("ValidatorReferenceInvalidMessage"),
            Category,
            DiagnosticSeverity.Error,
            true);

    /// <summary>
    /// 直接 <c>new OrderId(...)</c> 构造强类型 Id，绕过了 <c>Create</c> / <c>TryParse</c> 强制的验证器。
    /// </summary>
    /// <remarks>
    /// 仅当该 Id 设了 <c>Validator</c> 时才提示：无验证器时主构造函数本就可无校验地构造，不构成问题。
    /// 严重级别默认 <see cref="DiagnosticSeverity.Info"/>（建议性），且各项目可在 <c>.editorconfig</c> 里
    /// 通过 <c>dotnet_diagnostic.STIAO011.severity</c> 调整为 warning / error / none，避免噪声。
    /// 生成代码里的 <c>new Xxx(...)</c>（位于生成的 <c>Create</c> / <c>TryParse</c> 内）由分析器对生成代码的
    /// 豁免配置自动忽略，不会误报。
    /// </remarks>
    public static readonly DiagnosticDescriptor BypassCreate
        = new(BypassCreateId,
            new LocalizedString("BypassCreateTitle"),
            new LocalizedString("BypassCreateMessage"),
            Category,
            DiagnosticSeverity.Info,
            true);

    /// <summary>
    /// 全部诊断描述符，顺序与规则 ID 的语义分组一致。
    /// </summary>
    public static readonly DiagnosticDescriptor[] All =
    [
        TypeMustBeRecord,
        TypeMustBePartial,
        TypeCannotBeAbstract,
        TypeCannotBeGeneric,
        TypeMustHaveNamespace,
        TypeMustHaveSingleParameterPrimaryConstructor,
        ParameterCannotBeNullable,
        ParameterNameMustBeValue,
        ParameterTypeIsInvalid,
        ContainingTypeMustBePartial,
        ValidatorReferenceInvalid,
        BypassCreate,
    ];
}
