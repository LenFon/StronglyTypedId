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

    /// <summary>
    /// 强类型 Id 的包含类型不是 partial，或包含类型是 file 本地类型。
    /// </summary>
    /// <remarks>
    /// 两种情况同一条规则：生成代码要在另一个文件里重开这个容器，缺 partial 做不到，<c>file</c> 本地类型更是无从重开。
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
    /// 写错会让错误跑到生成代码里变成 CS0103 / CS0117 / CS1503，甚至触发 CS8785 让整段产出被丢弃；
    /// 提前在 attribute 上报错，位置和原因都清楚。
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
    /// 仅当该 Id 设了 <c>Validator</c> 时才提示。默认 <see cref="DiagnosticSeverity.Info"/>，可在
    /// <c>.editorconfig</c> 里调级。生成代码里的 <c>new Xxx(...)</c> 由生成代码豁免自动忽略。
    /// </remarks>
    public static readonly DiagnosticDescriptor BypassCreate
        = new(BypassCreateId,
            new LocalizedString("BypassCreateTitle"),
            new LocalizedString("BypassCreateMessage"),
            Category,
            DiagnosticSeverity.Info,
            true);

    /// <summary>
    /// 代码生成阶段抛出了未预期的异常。
    /// </summary>
    /// <remarks>
    /// 生成器抛异常时 Roslyn 会丢弃本次全部产出，只留一条无位置、无原因的 CS8785。这里报成带原因的诊断：
    /// <c>{0}</c> 生成器名、<c>{1}</c> 异常类型、<c>{2}</c> 异常消息；定位取 <see cref="Location.None"/>，
    /// 因为故障可能与任何单个 Id 无关。
    /// </remarks>
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
    /// <remarks>
    /// 生成器把二者静默按「未设置」处理，这里提前告警，避免使用者误以为前缀已生效。
    /// </remarks>
    public static readonly DiagnosticDescriptor InvalidPrefix
        = new(InvalidPrefixId,
            new LocalizedString("InvalidPrefixTitle"),
            new LocalizedString("InvalidPrefixMessage"),
            Category,
            DiagnosticSeverity.Warning,
            true);

    /// <summary>
    /// 带验证器的强类型 Id 被声明为 <c>public</c>，其主构造函数因而对外可见，验证器可被绕过。
    /// </summary>
    /// <remarks>
    /// 主构造函数的可见性跟随类型（CS1514），故 <c>public</c> 类型上任何引用方都能 <c>new OrderId(default)</c>
    /// 绕过验证器。默认关闭：公开 Id 类型是正常用法，需要强保证的项目可在 <c>.editorconfig</c> 里显式开启。
    /// </remarks>
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
    /// <remarks>
    /// 纯命名风格规则，默认 <see cref="DiagnosticSeverity.Info"/>。后缀按序数比较（区分大小写），
    /// 否则 <c>Grid</c> / <c>Valid</c> 这类以 "id" 收尾的普通词会被误判为合规。
    /// </remarks>
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
    /// <remarks>
    /// 同基元的 Id 在存储与传输层面完全同形，编译器拦不住映射配置、序列化契约、手写 SQL 里接错的那一处。
    /// 每组只报一次，落在组内名字序最小的类型上。
    /// </remarks>
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
    /// <remarks>直接充当分析器的 <c>SupportedDiagnostics</c>；生成器侧规则另见 <see cref="GeneratorDiagnostics"/>。</remarks>
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
        InvalidPrefix,
        ValidatorCanBeBypassed,
        TypeNameShouldEndWithId,
        DuplicatePrimitiveType,
    ];

    /// <summary>
    /// 全部生成器诊断描述符。
    /// </summary>
    /// <remarks>生成器没有 <c>SupportedDiagnostics</c> 入口，故单独成组，不与 <see cref="All"/> 混在一起。</remarks>
    public static readonly DiagnosticDescriptor[] GeneratorDiagnostics =
    [
        GeneratorFailure,
    ];
}
