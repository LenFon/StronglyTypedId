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
    /// 通过 <c>dotnet_diagnostic.STIA0011.severity</c> 调整为 warning / error / none，避免噪声。
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
    /// 代码生成阶段抛出了未预期的异常。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 生成器内部一旦抛异常，Roslyn 会丢弃该生成器本次的<b>全部</b>产出，使用者只看到一条 CS8785 警告 ——
    /// 没有位置、没有原因、没有堆栈，表现为「生成的代码凭空消失」。生成器侧的注释反复提到这条风险链
    /// （<c>StronglyTypedIdInfo</c> 的基元类型解析与 hint name 去重），却一直没有兜底。
    /// </para>
    /// <para>
    /// 这里把异常就地兜住并报成一条带原因的诊断：<c>{0}</c> 为生成器名，<c>{1}</c> 为异常类型，
    /// <c>{2}</c> 为异常消息。定位取 <see cref="Location.None"/> —— 故障可能发生在与任何单个 Id 无关的
    /// 阶段（例如模块枚举），强行挂到某个 Id 上反而误导。
    /// </para>
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
    /// 空前缀没有任何语义，纯空白前缀几乎必然是笔误。生成器侧把二者静默按「未设置」处理
    /// （见 <c>StronglyTypedIdInfo.NormalizePrefix</c>），这里提前告警，避免使用者误以为前缀已生效。
    /// 严重级别为 <see cref="DiagnosticSeverity.Warning"/>，各项目可在 <c>.editorconfig</c> 里调整。
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
    /// <para>
    /// 强类型 Id 的价值一半来自「构造即校验」：<c>Create</c> / <c>TryParse</c> 会调用 <c>Validator</c>。
    /// 但只要类型是 <c>public</c>，主构造函数的可见性就跟随类型（record 的主构造函数<b>不能</b>单独指定
    /// 可见性，<c>public record R private (int X)</c> 是 CS1514），于是任何引用方都能写
    /// <c>new OrderId(default)</c> 直接造出一个未经校验的实例。
    /// </para>
    /// <para>
    /// 这条规则默认<b>关闭</b>（<c>IsEnabledByDefault = false</c>）：公开 Id 类型是完全正常的用法
    /// （API 契约、DTO、跨程序集传递都需要它），默认开启只会在绝大多数项目上制造噪声。
    /// 需要「验证器不可绕过」这一强保证的项目可在 <c>.editorconfig</c> 里显式开启：
    /// <c>dotnet_diagnostic.STIA0014.severity = suggestion</c>（或 warning / error）。
    /// </para>
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
    /// <para>
    /// 纯命名风格规则，默认 <see cref="DiagnosticSeverity.Info"/>，可在 <c>.editorconfig</c> 里
    /// 调级或关闭（<c>dotnet_diagnostic.STIA0015.severity = none</c>）。
    /// </para>
    /// <para>
    /// 后缀按<b>序数</b>比较（区分大小写）：<c>OrderID</c> 会被提示，而 <c>Grid</c> / <c>Solid</c> /
    /// <c>Valid</c> 这些以 "id" 收尾的普通词不会被误判为合规 —— 不区分大小写地比 "id" 会把它们全部放过。
    /// </para>
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
    /// <para>
    /// 同基元的 Id 在<b>存储与传输层面完全同形</b>：EF 列类型、Dapper 参数、JSON 值、路由片段都一样。
    /// 编译器能拦住把 <c>OrderId</c> 赋给 <c>InvoiceId</c> 的直接误用，却拦不住映射配置、序列化契约、
    /// 手写 SQL 里接错的那一处 —— 这条规则就是把「它们其实是同一种东西」提前说出来。
    /// </para>
    /// <para>
    /// 每组只报一次，落在组内名字序最小的类型上：n 个同基元 Id 各报一次就是 n 条重复提示，
    /// 而这条规则本身只是「回头看一眼」的提醒。默认 <see cref="DiagnosticSeverity.Info"/>，
    /// 可在 <c>.editorconfig</c> 里调级或关闭。
    /// </para>
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
    /// <remarks>
    /// 这里只收分析器（<see cref="Analyzers.StronglyTypedIdAnalyzer"/>）会报出的规则，并直接充当其
    /// <c>SupportedDiagnostics</c>；生成器侧规则另见 <see cref="GeneratorDiagnostics"/>。
    /// </remarks>
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
    /// <remarks>
    /// 生成器没有 <c>SupportedDiagnostics</c> 这样的声明入口，规则是否「存在」只由是否被报出体现，
    /// 因此这里单独成组，不与 <see cref="All"/> 混在一起 —— 后者是分析器的对外承诺清单。
    /// </remarks>
    public static readonly DiagnosticDescriptor[] GeneratorDiagnostics =
    [
        GeneratorFailure,
    ];
}
