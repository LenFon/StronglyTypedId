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
    public const string IntegrationUnavailableId = "STIA0019";
    public const string InvalidInstanceNameId = "STIA0020";
    public const string InvalidInstanceValueId = "STIA0021";
    public const string InstanceNameNotPascalCaseId = "STIA0022";
    public const string AmbiguousPrefixId = "STIA0023";
    public const string UnknownIntegrationId = "STIA0024";
    public const string TypeCannotBeFileLocalId = "STIA0025";
    public const string TypeNameCannotBeVarId = "STIA0026";
    public const string TypeNameCollidesWithGeneratedMemberId = "STIA0027";
    public const string GenericContainerSkipsIntegrationsId = "STIA0028";
    public const string MemberCollidesWithGeneratedMemberId = "STIA0029";

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
    /// 直接构造强类型 Id（<c>new MyId(...)</c> / <c>new(...)</c>）或用 <c>default</c> 取零值实例，绕过了 <c>Create</c> /
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
    /// 显式启用了某个集成，但当前编译没有引用其程序集，该集成不会生成任何代码。
    /// </summary>
    /// <remarks>
    /// 按集成聚合后只能等编译结束上报，故必须带 <see cref="WellKnownDiagnosticTags.CompilationEnd"/>，否则 RS1037 报错。
    /// </remarks>
    public static readonly DiagnosticDescriptor IntegrationUnavailable
        = new(IntegrationUnavailableId,
            new LocalizedString("IntegrationUnavailableTitle"),
            new LocalizedString("IntegrationUnavailableMessage"),
            Category,
            DiagnosticSeverity.Warning,
            true,
            description: null,
            helpLinkUri: null,
            WellKnownDiagnosticTags.CompilationEnd);

    /// <summary>
    /// <c>[StronglyTypedIdInstance]</c> 的名字不是合法标识符、与另一条实例重名，或与类型自身 / 既有成员 /
    /// 即将生成的成员撞名。
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidInstanceName
        = new(InvalidInstanceNameId,
            new LocalizedString("InvalidInstanceNameTitle"),
            new LocalizedString("InvalidInstanceNameMessage"),
            Category,
            DiagnosticSeverity.Error,
            true);

    /// <summary>
    /// <c>[StronglyTypedIdInstance]</c> 的值文本无法解析为该 Id 的基元类型（含越界、<c>char</c> 长度不为 1、
    /// 枚举未定义值）。
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidInstanceValue
        = new(InvalidInstanceValueId,
            new LocalizedString("InvalidInstanceValueTitle"),
            new LocalizedString("InvalidInstanceValueMessage"),
            Category,
            DiagnosticSeverity.Error,
            true);

    /// <summary>
    /// 命名实例的名字不是 PascalCase。
    /// </summary>
    public static readonly DiagnosticDescriptor InstanceNameNotPascalCase
        = new(InstanceNameNotPascalCaseId,
            new LocalizedString("InstanceNameNotPascalCaseTitle"),
            new LocalizedString("InstanceNameNotPascalCaseMessage"),
            Category,
            DiagnosticSeverity.Info,
            true);

    /// <summary>
    /// <c>[StronglyTypedId(Prefix = …)]</c> 的前缀与基元类型的文本形式重叠，读回时会被静默剥掉。
    /// </summary>
    public static readonly DiagnosticDescriptor AmbiguousPrefix
        = new(AmbiguousPrefixId,
            new LocalizedString("AmbiguousPrefixTitle"),
            new LocalizedString("AmbiguousPrefixMessage"),
            Category,
            DiagnosticSeverity.Error,
            true);

    /// <summary>
    /// <c>[StronglyTypedId(Integrations = …)]</c> 的取值含有未定义的位。
    /// </summary>
    /// <remarks>
    /// 越界位不匹配任何集成，也不会被任何生成器读到，不报就是「写了却什么都没发生」。级别取 Warning，因为代码本身合法。
    /// 带 <see cref="WellKnownDiagnosticTags.CompilationEnd"/> 是因为程序集级默认值要到编译结束才知道。
    /// </remarks>
    public static readonly DiagnosticDescriptor UnknownIntegration
        = new(UnknownIntegrationId,
            new LocalizedString("UnknownIntegrationTitle"),
            new LocalizedString("UnknownIntegrationMessage"),
            Category,
            DiagnosticSeverity.Warning,
            true,
            description: null,
            helpLinkUri: null,
            WellKnownDiagnosticTags.CompilationEnd);

    /// <summary>
    /// 强类型 Id 自身被声明为 <c>file</c> 本地类型。
    /// </summary>
    /// <remarks>
    /// 生成器要在另一个文件里重开它的 partial 声明，而 file 局部类型的各段必须都带 <c>file</c> 且仅在本文件可见，
    /// 跨文件重开在语法上不成立。放行的后果是凭空造出第二个同名类型，报 CS0535 且不带任何 STIA 诊断。
    /// </remarks>
    public static readonly DiagnosticDescriptor TypeCannotBeFileLocal
        = new(TypeCannotBeFileLocalId,
            new LocalizedString("TypeCannotBeFileLocalTitle"),
            new LocalizedString("TypeCannotBeFileLocalMessage"),
            Category,
            DiagnosticSeverity.Error,
            true);

    /// <summary>
    /// 强类型 Id 的类型名是 <c>var</c>。
    /// </summary>
    /// <remarks>
    /// 类型名 <c>var</c> 会遮蔽隐式类型关键字，生成代码里的 <c>out var id</c> 与
    /// <c>is { Value: var primitiveId }</c> 全部失效（CS1503 / CS8508）。这不是靠全限定名能绕开的：
    /// 被遮蔽的是关键字本身，而不是某个类型引用。故与 file 本地类型同样处理 —— 报错并跳过生成。
    /// </remarks>
    public static readonly DiagnosticDescriptor TypeNameCannotBeVar
        = new(TypeNameCannotBeVarId,
            new LocalizedString("TypeNameCannotBeVarTitle"),
            new LocalizedString("TypeNameCannotBeVarMessage"),
            Category,
            DiagnosticSeverity.Error,
            true);

    /// <summary>
    /// 强类型 Id 的类型名与生成器产出的成员名相同。
    /// </summary>
    /// <remarks>
    /// C# 不允许成员名与封闭类型同名（CS0542）。这些成员由生成器写进同一个 partial 类型，
    /// 用户在声明处看不到，编译器报错也只指向生成文件，故必须提前报出并跳过生成。
    /// </remarks>
    public static readonly DiagnosticDescriptor TypeNameCollidesWithGeneratedMember
        = new(TypeNameCollidesWithGeneratedMemberId,
            new LocalizedString("TypeNameCollidesWithGeneratedMemberTitle"),
            new LocalizedString("TypeNameCollidesWithGeneratedMemberMessage"),
            Category,
            DiagnosticSeverity.Error,
            true);

    /// <summary>
    /// 强类型 Id 声明在泛型包含类型内部，集成代码被静默跳过。
    /// </summary>
    /// <remarks>
    /// 泛型容器里的 Id 能编译、核心成员也生成，但 <c>DiscoveredIds</c> 会把它过滤掉：开放泛型无法作为
    /// 类型实参传给注册入口，<c>[JsonConverter]</c> 等特性也因 CS0416 无法发射。用户只有在运行时
    /// 才会发现 JSON 写成了对象形态、EF Core 说无法映射，故在声明处提示。
    /// </remarks>
    public static readonly DiagnosticDescriptor GenericContainerSkipsIntegrations
        = new(GenericContainerSkipsIntegrationsId,
            new LocalizedString("GenericContainerSkipsIntegrationsTitle"),
            new LocalizedString("GenericContainerSkipsIntegrationsMessage"),
            Category,
            DiagnosticSeverity.Warning,
            true);

    /// <summary>
    /// 用户自己声明的成员与生成器即将产出的成员同名且同参数表。
    /// </summary>
    /// <remarks>
    /// 撞名的后果是 CS0111 / CS0102，而编译器只把错误指向生成文件 —— 用户既不能编辑那个文件，
    /// 也不知道该改自己哪一行。与 STIA0027（类型名撞名）同一类问题，故同样提前报出。
    /// </remarks>
    public static readonly DiagnosticDescriptor MemberCollidesWithGeneratedMember
        = new(MemberCollidesWithGeneratedMemberId,
            new LocalizedString("MemberCollidesWithGeneratedMemberTitle"),
            new LocalizedString("MemberCollidesWithGeneratedMemberMessage"),
            Category,
            DiagnosticSeverity.Error,
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
        AmbiguousPrefix,
        UnknownIntegration,
        ValidatorCanBeBypassed,
        TypeNameShouldEndWithId,
        DuplicatePrimitiveType,
        IntegrationUnavailable,
        InvalidInstanceName,
        InvalidInstanceValue,
        InstanceNameNotPascalCase,
        TypeCannotBeFileLocal,
        TypeNameCannotBeVar,
        TypeNameCollidesWithGeneratedMember,
        GenericContainerSkipsIntegrations,
        MemberCollidesWithGeneratedMember,
    ];

    /// <summary>
    /// 全部生成器诊断描述符。
    /// </summary>
    public static readonly DiagnosticDescriptor[] GeneratorDiagnostics =
    [
        GeneratorFailure,
    ];
}
