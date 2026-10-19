namespace Len.StronglyTypedId;

/// <summary>
/// 集中定义强类型 Id 分析器的全部诊断：规则 ID、严重级别与本地化后的标题/提示。
/// </summary>
internal static class Descriptors
{
    private const string Category = "StronglyTypedIdAnalyzer";

    /// <summary>
    /// 生成器侧诊断的类别。与 <see cref="Category"/> 分开，便于使用者按类别整体调级。
    /// </summary>
    private const string GeneratorCategory = "StronglyTypedIdGenerator";

    public const string ContainingTypeMustBePartialId = "STIA0009";
    public const string ParameterCannotBeNullableId = "STIA0006";
    public const string ParameterNameMustBeValueId = "STIA0007";
    public const string ParameterTypeIsInvalidId = "STIA0008";
    public const string ValidatorReferenceInvalidId = "STIA0010";
    public const string BypassCreateId = "STIA0011";
    public const string TypeCannotBeAbstractId = "STIA0002";
    public const string TypeCannotBeGenericId = "STIA0003";
    public const string TypeMustHaveNamespaceId = "STIA0004";
    public const string TypeMustBePartialId = "STIA0001";
    public const string TypeMustBeRecordId = "STIA0000";
    public const string TypeMustHaveSingleParameterPrimaryConstructorId = "STIA0005";
    public const string GeneratorFailureId = "STIA0012";
    public const string InvalidPrefixId = "STIA0013";
    public const string ValidatorCanBeBypassedId = "STIA0014";
    public const string TypeNameShouldEndWithIdId = "STIA0015";
    public const string DuplicatePrimitiveTypeId = "STIA0016";
    public const string NormalizerReferenceInvalidId = "STIA0017";
    public const string ExceptionTypeInvalidId = "STIA0018";

    /// <summary>
    /// 强类型 Id 的包含类型不是 partial，或包含类型是 file 本地类型。
    /// </summary>
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
    /// <c>[StronglyTypedId(Validator = nameof(Foo))]</c> 指向的方法不存在、非静态、返回类型不是 <see cref="bool"/>，或形参类型不等于基元
    /// Id 类型。
    /// </summary>
    public static readonly DiagnosticDescriptor ValidatorReferenceInvalid
        = new(ValidatorReferenceInvalidId,
            new LocalizedString("ValidatorReferenceInvalidTitle"),
            new LocalizedString("ValidatorReferenceInvalidMessage"),
            Category,
            DiagnosticSeverity.Error,
            true);

    /// <summary>
    /// <c>[StronglyTypedId(Normalizer = nameof(Foo))]</c> 指向的方法不存在、非静态、返回类型不是基元 Id 类型，或形参类型不等于基元 Id 类型。
    /// </summary>
    public static readonly DiagnosticDescriptor NormalizerReferenceInvalid
        = new(NormalizerReferenceInvalidId,
            new LocalizedString("NormalizerReferenceInvalidTitle"),
            new LocalizedString("NormalizerReferenceInvalidMessage"),
            Category,
            DiagnosticSeverity.Error,
            true);

    /// <summary>
    /// <c>[StronglyTypedId(ExceptionType = typeof(…))]</c> 指向的类型不是派生自 <see cref="System.Exception"/>
    /// 的非抽象类，或不具备可访问的单字符串参数构造函数。
    /// </summary>
    public static readonly DiagnosticDescriptor ExceptionTypeInvalid
        = new(ExceptionTypeInvalidId,
            new LocalizedString("ExceptionTypeInvalidTitle"),
            new LocalizedString("ExceptionTypeInvalidMessage"),
            Category,
            DiagnosticSeverity.Error,
            true);

    /// <summary>
    /// 直接构造强类型 Id（<c>new OrderId(...)</c> / <c>new(...)</c>）或用 <c>default</c> 取零值实例，绕过了 <c>Create</c> /
    /// <c>TryParse</c> 强制的归一化与校验。
    /// </summary>
    public static readonly DiagnosticDescriptor BypassCreate
        = new(BypassCreateId,
            new LocalizedString("BypassCreateTitle"),
            new LocalizedString("BypassCreateMessage"),
            Category,
            DiagnosticSeverity.Warning,
            true);

    /// <summary>
    /// 代码生成阶段抛出了未预期的异常。
    /// </summary>
    public static readonly DiagnosticDescriptor GeneratorFailure
        = new(GeneratorFailureId,
            new LocalizedString("GeneratorFailureTitle"),
            new LocalizedString("GeneratorFailureMessage"),
            GeneratorCategory,
            DiagnosticSeverity.Error,
            true);

    /// <summary>
    /// <c>[StronglyTypedId(Prefix = …)]</c> 的前缀为空串或纯空白。
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidPrefix
        = new(InvalidPrefixId,
            new LocalizedString("InvalidPrefixTitle"),
            new LocalizedString("InvalidPrefixMessage"),
            Category,
            DiagnosticSeverity.Warning,
            true);

    /// <summary>
    /// 配了归一化或验证器的强类型 Id 被声明为 <c>public</c>，其主构造函数因而对外可见，这些约束可被绕过。
    /// </summary>
    public static readonly DiagnosticDescriptor ValidatorCanBeBypassed
        = new(ValidatorCanBeBypassedId,
            new LocalizedString("ValidatorCanBeBypassedTitle"),
            new LocalizedString("ValidatorCanBeBypassedMessage"),
            Category,
            DiagnosticSeverity.Info,
            false);

    /// <summary>
    /// 强类型 Id 的类型名不以 <c>Id</c> 结尾。
    /// </summary>
    public static readonly DiagnosticDescriptor TypeNameShouldEndWithId
        = new(TypeNameShouldEndWithIdId,
            new LocalizedString("TypeNameShouldEndWithIdTitle"),
            new LocalizedString("TypeNameShouldEndWithIdMessage"),
            Category,
            DiagnosticSeverity.Info,
            true);

    /// <summary>
    /// 同一命名空间下多个强类型 Id 共用同一基元类型。
    /// </summary>
    public static readonly DiagnosticDescriptor DuplicatePrimitiveType
        = new(DuplicatePrimitiveTypeId,
            new LocalizedString("DuplicatePrimitiveTypeTitle"),
            new LocalizedString("DuplicatePrimitiveTypeMessage"),
            Category,
            DiagnosticSeverity.Info,
            true);

    /// <summary>
    /// 全部分析器诊断描述符，顺序与规则 ID 的语义分组一致。
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
        NormalizerReferenceInvalid,
        ExceptionTypeInvalid,
        BypassCreate,
        InvalidPrefix,
        ValidatorCanBeBypassed,
        TypeNameShouldEndWithId,
        DuplicatePrimitiveType,
    ];

    /// <summary>
    /// 全部生成器诊断描述符。
    /// </summary>
    public static readonly DiagnosticDescriptor[] GeneratorDiagnostics =
    [
        GeneratorFailure,
    ];
}
