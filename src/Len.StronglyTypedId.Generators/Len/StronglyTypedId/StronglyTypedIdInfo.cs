namespace Len.StronglyTypedId;

/// <summary>
/// 描述一个由源码生成器发现的强类型 Id 类型，供各代码生成器拼装文本模板使用。
/// </summary>
internal readonly record struct StronglyTypedIdInfo
{
    private const string GlobalPrefix = "global::";

    /// <summary>
    /// 运行时程序集中 <c>IStronglyTypedId&lt;TSelf, TPrimitiveId&gt;</c> 的简单名。
    /// </summary>
    /// <remarks>
    /// 生成器项目不引用运行时程序集，因此只能以字符串字面量匹配符号名。
    /// </remarks>
    internal const string InterfaceName = "IStronglyTypedId";

    /// <summary>
    /// 运行时程序集中 <c>Len.StronglyTypedId</c> 命名空间的显示名（含 <c>global::</c> 前缀）。
    /// </summary>
    internal const string InterfaceNamespace = "global::Len.StronglyTypedId";

    /// <summary>
    /// 运行时程序集中 <c>StronglyTypedIdAttribute</c> 的显示名（含 <c>global::</c> 前缀）。
    /// </summary>
    internal const string AttributeName = "global::Len.StronglyTypedId.StronglyTypedIdAttribute";

    /// <summary>
    /// System.Text.Json 转换器的内嵌类型名，由 <c>SystemTextJsonCodeGenerator</c> 产出。
    /// </summary>
    private const string SystemTextJsonConverterTypeName = "SystemTextJsonConverter";

    /// <summary>
    /// Newtonsoft.Json 转换器的内嵌类型名，由 <c>NewtonsoftJsonCodeGenerator</c> 产出。
    /// </summary>
    private const string NewtonsoftJsonConverterTypeName = "NewtonsoftJsonConverter";

    /// <summary>
    /// attribute 中用于指定验证器方法的命名实参名。
    /// </summary>
    internal const string ValidatorArgumentName = "Validator";

    /// <summary>
    /// attribute 中用于开启 <c>System.ComponentModel.TypeConverter</c> 生成的命名实参名。
    /// </summary>
    internal const string TypeConverterArgumentName = "TypeConverter";

    /// <summary>
    /// attribute 中用于指定 JSON 序列化形态的命名实参名。
    /// </summary>
    internal const string JsonShapeArgumentName = "JsonShape";

    /// <summary>
    /// attribute 中用于指定规范文本形前缀的命名实参名。
    /// </summary>
    internal const string PrefixArgumentName = "Prefix";

    /// <summary>
    /// attribute 中用于指定前缀读入策略的命名实参名。
    /// </summary>
    internal const string PrefixReadModeArgumentName = "PrefixReadMode";

    /// <summary>
    /// <c>Len.StronglyTypedId.PrefixReadMode.Strict</c> 的底层常量值。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="ObjectJsonShapeValue"/> 同理：命名实参在元数据里保存的是枚举的<b>底层值</b>而非成员名，
    /// 故此处以常量镜像它；与运行时 <c>PrefixReadMode</c> 的取值必须保持一致。
    /// </remarks>
    private const int StrictPrefixReadModeValue = 1;

    /// <summary>
    /// <c>Len.StronglyTypedId.JsonShape.Object</c> 的底层常量值。
    /// </summary>
    /// <remarks>
    /// 生成器项目不引用运行时程序集，无法直接书写 <c>JsonShape.Object</c>。命名实参在元数据里保存的是
    /// 枚举的<b>底层值</b>而非成员名，故此处以常量镜像它；与运行时 <c>JsonShape</c> 的取值必须保持一致。
    /// </remarks>
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
    /// 判断给定接口符号是否为运行时的 <c>IStronglyTypedId&lt;TSelf, TPrimitiveId&gt;</c>。
    /// </summary>
    /// <remarks>
    /// 简单名、命名空间与类型实参个数三者必须同时校验。只比简单名会把使用者或第三方程序集里恰好
    /// 同名（例如只带一个类型实参）的接口一并纳入，随后解析基元类型失败并抛出异常，令整个生成器的
    /// 产出被丢弃 —— 而编译器只报一条 CS8785 警告，使用者看到的是「生成代码全部消失」。
    /// </remarks>
    internal static bool IsStronglyTypedIdInterface(INamedTypeSymbol @interface)
        => @interface.Name == InterfaceName
            && @interface.TypeArguments.Length == 2
            && @interface.ContainingNamespace.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == InterfaceNamespace;

    public StronglyTypedIdInfo(ITypeSymbol type, string? defaultValidatorName = null)
    {
        // 非具名类型（数组、指针、类型参数等）没有 ContainingNamespace，直接解引用会抛
        // NullReferenceException。这里与「基元类型解析失败」保持同一异常语义，便于定位问题。
        var containingNamespace = type.ContainingNamespace
            ?? throw new InvalidOperationException($"无法从类型“{type.ToDisplayString()}”解析强类型 Id 信息：该类型不是具名类型。");

        FullyQualifiedNamespace = containingNamespace.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        Namespace = FullyQualifiedNamespace[GlobalPrefix.Length..];
        Name = type.Name;
        FullyQualifiedName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        FullName = type.ToDisplayString();
        // readonly 修饰符只作用在 struct 形态的强类型 Id 上（引用类型 record 无 readonly 概念），
        // 且必须作为「出现在 record 关键字之前的修饰符」透出（写作 partial readonly record struct），
        // 否则 C# 报错「readonly 必须位于成员类型和名称之前」。各 partial 段对「是否 readonly struct」一致，
        // 漏写会让使用者原本的 readonly record struct 在合并生成段后编译失败。
        TypeKindSuffix = type.TypeKind == TypeKind.Struct ? " struct" : null;
        ReadOnlyRecordModifier = type.TypeKind == TypeKind.Struct && type.IsReadOnly ? "readonly " : string.Empty;

        var containingTypes = GetContainingTypeDeclarations(type);
        ContainingTypeDeclarations = containingTypes.Declarations;
        NestingDepth = containingTypes.Depth;
        IsInGenericContainer = containingTypes.IsInGenericContainer;
        HintName = SanitizeHintName(FullName);

        var primitiveIdType = GetPrimitiveIdType(type);

        PrimitiveIdTypeName = primitiveIdType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        // 各项能力都由基元类型的符号判定，而非按类型名硬编码分支：与 SupportedPrimitiveTypes 保持同一取向
        // —— 判据落在符号上，类型名只用于展示。
        IsStringPrimitive = primitiveIdType.SpecialType == SpecialType.System_String;
        IsEnum = primitiveIdType.TypeKind == TypeKind.Enum;
        // 枚举虽从 System.Enum 继承了 IFormattable，但其 ToString(string, IFormatProvider) 已被标记
        // [Obsolete]（provider 参数被忽略），生成该成员会触发 CS0618 且毫无收益，故枚举不走 IFormattable 分支。
        // 枚举虽从 System.Enum 继承了 IFormattable / ISpanFormattable（后者派生于前者），但其
        // ToString(string, IFormatProvider) 已被标记 [Obsolete]（provider 参数被忽略）。若仍透出这两个接口，
        // 生成 IFormattable.ToString 会触发 CS0618，且不生成又会因 ISpanFormattable 派生于 IFormattable 而
        // 招来 CS0535。故枚举两者都不走 —— 枚举 Id 仍保留 IComparable / IParsable / ISpanParsable 等能力。
        IsFormattable = ImplementsInterface(primitiveIdType, FormattableName) && !IsEnum;
        IsSpanFormattable = ImplementsInterface(primitiveIdType, SpanFormattableName) && !IsEnum;
        // UTF-8 解析比 UTF-16 多一道门槛：除了基元实现 IUtf8SpanParsable<TSelf>，还必须暴露<b>公开</b>的
        // TryParse(ReadOnlySpan<byte>, IFormatProvider, out T)。BigInteger 实现了该接口，却把它实现为显式接口
        // 实现，生成代码按具体类型名调用会 CS1501（静态抽象接口成员又不能经接口名直接调用，CS8926），故对它
        // 不生成 UTF-8 解析、也不透出该接口 —— 与「基元本就具备才透出」的取向一致。
        IsUtf8SpanParsable = ImplementsInterface(primitiveIdType, Utf8SpanParsableName)
            && HasPublicStaticTryParse(primitiveIdType, SpecialType.System_Byte);
        IsUtf8SpanFormattable = ImplementsInterface(primitiveIdType, Utf8SpanFormattableName);
        CharTryFormatKind = GetTryFormatKind(primitiveIdType, SpecialType.System_Char);
        Utf8TryFormatKind = GetTryFormatKind(primitiveIdType, SpecialType.System_Byte);
        ValidatorName = GetStringArgument(type, ValidatorArgumentName) ?? defaultValidatorName;
        HasTypeConverter = GetBooleanArgument(type, TypeConverterArgumentName);
        IsObjectJsonShape = GetJsonShapeIsObject(type);
        Prefix = NormalizePrefix(GetStringArgument(type, PrefixArgumentName));
        IsStrictPrefixRead = GetPrefixReadModeIsStrict(type);
        HasSystemTextJsonConverter = WillHaveNestedType(type, SystemTextJsonConverterTypeName);
        HasNewtonsoftJsonConverter = WillHaveNestedType(type, NewtonsoftJsonConverterTypeName);
    }

    /// <summary>
    /// The namespace of a strongly typed id, without the <c>global::</c> prefix.
    /// </summary>
    /// <remarks>
    /// This is the form emitted right after the generated <c>namespace</c> keyword.
    /// </remarks>
    public string Namespace { get; }

    /// <summary>
    /// The namespace of a strongly typed id, prefixed with <c>global::</c>.
    /// </summary>
    public string FullyQualifiedNamespace { get; }

    /// <summary>
    /// The name of a strongly typed id.
    /// </summary>
    /// <remarks>
    /// This is a short strongly typed id type name that does not include a namespace.
    /// </remarks>
    public string Name { get; }

    /// <summary>
    /// The fully qualified name of a strongly typed id, prefixed with <c>global::</c>.
    /// </summary>
    /// <remarks>
    /// This is the form used when emitting type references into generated code, where the
    /// <c>global::</c> prefix shields the name from any namespace that happens to be in scope.
    /// </remarks>
    public string FullyQualifiedName { get; }

    /// <summary>
    /// The display name of a strongly typed id, also used as the generated hint name.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="FullyQualifiedName"/>, this form carries no <c>global::</c> prefix,
    /// which makes it a legal hint name but unsuitable for use inside generated code.
    /// </remarks>
    public string FullName { get; }

    /// <summary>
    /// 包含类型链的重开声明，最外层在前、每层一行；顶层类型为空串。
    /// </summary>
    /// <remarks>
    /// 形如 <c>public partial class Outer</c>。生成代码要补的是嵌套类型自身的成员，而 partial 的各段
    /// 必须处于同一容器内，故只能把声明逐层放回原本的包含类型中 —— 每一层都得被原样重开一次。
    /// </remarks>
    public string ContainingTypeDeclarations { get; }

    /// <summary>
    /// 包含类型链的层数；顶层类型为 0。
    /// </summary>
    public int NestingDepth { get; }

    /// <summary>
    /// 可作为生成源 hint 名的类型显示名：派生自 <see cref="FullName"/>，但剔除尖括号、逗号与空格，
    /// 使泛型包含类型（例如 <c>Container&lt;T&gt;.OrderId</c>）也能得到合法且稳定的 hint 名
    /// （<c>ContainerT.OrderId</c>）。非泛型类型与 <see cref="FullName"/> 完全相同。
    /// </summary>
    /// <remarks>
    /// hint 名里出现 <c>&lt;&gt;</c> 既非法又会让 <c>AddSource</c> 去重失效；而同一编译单元内至多只存在
    /// 一个开放泛型的包含类型声明，故剥离类型参数不会造成命名冲突。
    /// </remarks>
    public string HintName { get; }

    /// <summary>
    /// 强类型 Id 是否声明在泛型包含类型内部。
    /// </summary>
    /// <remarks>
    /// 泛型包含类型无法被生成代码从外部按全名引用（开放类型不能用作类型实参），
    /// 因此 EF Core / Swagger / Dapper 等需要外部引用 Id 类型的集成生成器会跳过此类 Id，
    /// 仅核心生成器仍把它们逐层嵌回包含类型。
    /// </remarks>
    public bool IsInGenericContainer { get; }

    /// <summary>
    /// 该 Id 是否带有（或本次生成即将产出）内嵌的 <c>SystemTextJsonConverter</c>。
    /// </summary>
    /// <remarks>
    /// 注册入口按 <c>External</c> 取全集，其中可能混入「声明它的程序集并未引用 System.Text.Json」的 Id
    /// （例如面向 netstandard2.0 的领域库）。那种 Id 没有内嵌转换器，生成
    /// <c>new X.SystemTextJsonConverter()</c> 会 CS0426，让消费者的编译直接失败 ——
    /// 而漏注册并无损失：<c>[JsonConverter]</c> 特性路径仍然生效。
    /// </remarks>
    public bool HasSystemTextJsonConverter { get; }

    /// <summary>
    /// 该 Id 是否带有（或本次生成即将产出）内嵌的 <c>NewtonsoftJsonConverter</c>。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="HasSystemTextJsonConverter"/> 同理：领域库不引用 Newtonsoft.Json 时其 Id 没有内嵌转换器，
    /// 注册入口必须跳过，否则 <c>settings.AddStronglyTypedId()</c> 所在编译报 CS0426。
    /// </remarks>
    public bool HasNewtonsoftJsonConverter { get; }

    /// <summary>
    /// The type kind suffix appended right after the <c>record</c> keyword, e.g. <c>" struct"</c>.
    /// </summary>
    /// <remarks>
    /// A <see langword="null"/> value means the strongly typed id is declared as a reference type record.
    /// </remarks>
    public string? TypeKindSuffix { get; }

    /// <summary>
    /// 当强类型 Id 是 <c>readonly record struct</c> 时，在 <c>partial</c> 与 <c>record</c> 之间插入的
    /// <c> readonly</c> 修饰符；否则为空串。引用类型 record 无 readonly 概念，恒为空串。
    /// </summary>
    public string ReadOnlyRecordModifier { get; }

    /// <summary>
    /// The fully qualified name of the primitive type wrapped by the strongly typed id.
    /// </summary>
    public string PrimitiveIdTypeName { get; }

    /// <summary>
    /// 基元类型是否为 <c>string</c>。
    /// </summary>
    /// <remarks>
    /// 仅用于选择生成模板的形态（<c>string</c> 基元的解析、格式化与字典键路径都与其它基元不同）。
    /// 判据取自符号的 <see cref="ITypeSymbol.SpecialType"/>，不比较 <see cref="PrimitiveIdTypeName"/> 的字面量。
    /// </remarks>
    public bool IsStringPrimitive { get; }

    /// <summary>
    /// 基元类型是否为枚举。
    /// </summary>
    /// <remarks>
    /// 枚举没有 <c>TryParse(string, IFormatProvider, out T)</c> 这样的静态解析入口，必须改走
    /// <c>System.Enum.TryParse&lt;TEnum&gt;</c>；其余能力（<c>IComparable</c> / <c>IFormattable</c>）由
    /// <see cref="System.Enum"/> 基类提供，故解析分支外的生成逻辑对其与普通基元一致。
    /// </remarks>
    public bool IsEnum { get; }

    /// <summary>
    /// 基元类型是否实现 <c>System.IFormattable</c>。
    /// </summary>
    /// <remarks>
    /// 只有为 <see langword="true"/> 时生成的强类型 Id 才会一并实现 <c>IFormattable</c>：
    /// 该接口要求 <c>ToString(string?, IFormatProvider?)</c>，而受支持基元里的 <c>string</c> 并不具备它。
    /// </remarks>
    public bool IsFormattable { get; }

    /// <summary>
    /// 基元类型是否实现 <c>System.ISpanFormattable</c>。
    /// </summary>
    /// <remarks>
    /// 除 <c>string</c> 外全部受支持基元都实现它（含 <c>float</c> / <c>double</c> / <c>Half</c>）；
    /// <c>string</c> 连 <c>IFormattable</c> 都不具备，故其强类型 Id 不会实现 <c>ISpanFormattable</c>、
    /// 也不生成 <c>TryFormat</c> 重载。
    /// </remarks>
    public bool IsSpanFormattable { get; }

    /// <summary>
    /// 基元类型是否实现 <c>System.IUtf8SpanParsable&lt;TSelf&gt;</c>，且暴露了公开的
    /// <c>TryParse(ReadOnlySpan&lt;byte&gt;, IFormatProvider, out T)</c>。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 与 <see cref="IsFormattable"/> 同样是「基元本就具备才透出」。这条判据必须在符号上做，
    /// 不能按类型名硬编码：<c>Guid</c> 自 <b>.NET 10</b> 起才实现该接口，在 net8.0 消费者下生成的
    /// <c>Parse(ReadOnlySpan&lt;byte&gt;…)</c> 会因为 <c>IUtf8SpanParsable&lt;Guid&gt;</c> 不存在而编译失败。
    /// 因此同一份源码在 net8.0 与 net10.0 消费者下的产物本就<b>允许不同</b>，
    /// 这与模块闸门（按引用程序集决定生成什么）是同一种设计。
    /// </para>
    /// <para>
    /// 第二道门槛（公开 <c>TryParse</c>）排除 <c>BigInteger</c>：它实现了 <c>IUtf8SpanParsable</c>，
    /// 却把该重载实现为显式接口实现，生成代码按具体类型名调用会 CS1501。
    /// </para>
    /// </remarks>
    public bool IsUtf8SpanParsable { get; }

    /// <summary>
    /// 基元类型是否实现 <c>System.IUtf8SpanFormattable</c>。
    /// </summary>
    /// <remarks>
    /// net8.0 与 net10.0 下 <c>Guid</c> 与全部数值基元均为 <see langword="true"/>，<c>string</c> 为
    /// <see langword="false"/>（<c>string</c> 连 <c>ISpanFormattable</c> 都不具备）。
    /// </remarks>
    public bool IsUtf8SpanFormattable { get; }

    /// <summary>
    /// 基元 UTF-16 <c>TryFormat</c> 的直呼形态，决定生成代码是否经接口调用（即是否装箱）。
    /// </summary>
    /// <remarks>
    /// 仅在 <see cref="IsSpanFormattable"/> 为 <see langword="true"/> 时有意义。
    /// </remarks>
    public TryFormatKind CharTryFormatKind { get; }

    /// <summary>
    /// 基元 UTF-8 <c>TryFormat</c> 的直呼形态，决定生成代码是否经接口调用（即是否装箱）。
    /// </summary>
    /// <remarks>
    /// 仅在 <see cref="IsUtf8SpanFormattable"/> 为 <see langword="true"/> 时有意义。
    /// </remarks>
    public TryFormatKind Utf8TryFormatKind { get; }

    /// <summary>
    /// <c>[StronglyTypedId]</c> 上 <c>Validator</c> 指定的验证器方法名；未指定时为 <see langword="null"/>。
    /// </summary>
    /// <remarks>
    /// 验证器是强类型 Id 类型自身的静态方法，签名形如 <c>static bool Validate(TPrimitiveId value)</c>，
    /// 返回 <see langword="true"/> 表示取值合法。生成代码只在 <c>Create</c> 与两个 <c>TryParse</c> 重载里调用它，
    /// 因此「直接 new 主构造函数」绕不过验证 —— 该限制由生成器无法改写使用者声明的主构造函数所致。
    /// </remarks>
    public string? ValidatorName { get; }

    /// <summary>
    /// <c>[StronglyTypedId]</c> 是否要求为该类型生成 <c>System.ComponentModel.TypeConverter</c>。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 这是一项<b>逐类型显式开启</b>的能力，不随引用程序集自动打开。理由：<c>[TypeConverter]</c> 会改变类型的
    /// 全局语义（<c>TypeDescriptor</c> 是 <c>XmlSerializer</c>、<c>ConfigurationBinder</c>、XAML / DataGrid
    /// 等老 API 的公共入口），而使用者完全可能已经自标了一个同名特性 —— 自动生成会直接与之相撞。
    /// 这与 <c>Validator</c> 一样，属于「不侵占使用者声明的语义」。
    /// </para>
    /// <para>
    /// 打开后还可一并补上 Newtonsoft.Json 字典键的<b>读</b>路径：Newtonsoft 还原键文本走的是
    /// <c>TypeDescriptor</c>，<c>JsonConverter.ReadJson</c> 接管不到，只有类型自带转换器时才通。
    /// </para>
    /// </remarks>
    public bool HasTypeConverter { get; }

    /// <summary>
    /// <c>[StronglyTypedId]</c> 是否要求把 JSON 形态设为对象（<c>{ "value": … }</c>）而非基元值本身。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 默认（<see langword="false"/>）下 Id 与基元值在 JSON 里同形，引入 Id 不改变既有契约；置为
    /// <see langword="true"/> 后 STJ 与 Newtonsoft 两个转换器都改写成对象形态，供「已对外承诺对象形态」
    /// 的既有契约平滑迁移。
    /// </para>
    /// <para>
    /// 该开关不作用于字典键：JSON 属性名本就是字符串，键在任何形态下都携带基元文本。
    /// </para>
    /// </remarks>
    public bool IsObjectJsonShape { get; }

    /// <summary>
    /// <c>[StronglyTypedId]</c> 上 <c>Prefix</c> 指定的规范文本形前缀；未指定或为空 / 纯空白时为
    /// <see langword="null"/>。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 前缀属于强类型 Id 的<b>规范文本形</b>：<c>ToString()</c> 前置它、<c>Parse</c> / <c>TryParse</c> 剥离它。
    /// 因此所有以文本为媒介的路径（System.Text.Json / Newtonsoft.Json / MVC 模型绑定 / 路由约束 /
    /// 生成的 <c>TypeConverter</c>）都自动带上前缀，而以<b>基元值</b>为媒介的路径（EF Core / Dapper）不受影响。
    /// </para>
    /// <para>
    /// 空串与纯空白一律归一为 <see langword="null"/>（等同「未设置」）：空前缀没有意义，纯空白前缀几乎必然是笔误，
    /// 静默忽略比生成一段诡异文本更安全。分析器另报 <c>STIA0013</c> 提示该写法。
    /// </para>
    /// </remarks>
    public string? Prefix { get; }

    /// <summary>
    /// 强类型 Id 的规范文本形是否携带前缀。
    /// </summary>
    public bool HasPrefix => Prefix is { Length: > 0 };

    /// <summary>
    /// 读回带前缀的文本时是否要求前缀必须存在（<c>PrefixReadMode.Strict</c>）。
    /// </summary>
    /// <remarks>
    /// 仅在 <see cref="HasPrefix"/> 为 <see langword="true"/> 时有意义：无前缀时读入策略无从谈起。
    /// 默认（<see langword="false"/>）为宽松读入 —— 前缀可选，带前缀则剥离、不带则按原样解析。
    /// </remarks>
    public bool IsStrictPrefixRead { get; }

    /// <summary>
    /// 解析强类型 Id 所包装的基元类型符号。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 优先读取 <c>IStronglyTypedId&lt;TSelf, TPrimitiveId&gt;</c> 的第二个类型实参：它是基元类型的权威来源，
    /// 既不需要类型具备任何特定形状的构造函数，也不会被其它构造函数干扰。引用程序集中已生成的强类型 Id
    /// 走这条路径。
    /// </para>
    /// <para>
    /// 本次编译新声明的强类型 Id 尚未实现该接口（实现由生成代码补充），因此回退到按单参数构造函数推断。
    /// 判断顺序不能颠倒：所有具名类型都满足 <see cref="INamedTypeSymbol"/>，若让构造函数分支先行，
    /// 会同时引入两类缺陷——类型没有单参数构造函数时取首个元素直接抛异常；引用类型 <c>record</c> 会额外
    /// 生成 <c>Foo(Foo original)</c> 复制构造函数，主构造函数参数多于一个时会把该记录自身误判为基元类型。
    /// </para>
    /// </remarks>
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
    /// <remarks>
    /// 这里刻意返回「拼接好的字符串 + 层数」而不是集合：本类型是 <see langword="readonly"/> record struct，
    /// 描述符靠成员级相等性去重（<see cref="StronglyTypedIdDiscovery.Discover"/> 的 <c>Distinct</c>），
    /// 而集合类型（如 <c>ImmutableArray&lt;T&gt;</c>）只按底层数组引用比较 —— 一旦相等性失效，同一个
    /// hint name 会被 <c>AddSource</c> 两次，生成器整个产出被丢弃，而编译器只报一条 CS8785 警告。
    /// </remarks>
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

        // 向上取到的是最内层在前，而生成代码要从最外层开始逐层包进去。
        levels.Reverse();

        return (string.Join("\n", levels), levels.Count, isInGenericContainer);
    }

    /// <summary>
    /// 把包含类型渲染成可重开它的 partial 声明，形如 <c>public partial class Outer</c>。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 只写可访问性与类型种类关键字，其余修饰符（<c>static</c> / <c>abstract</c> / <c>sealed</c> /
    /// <c>readonly</c> / <c>ref</c> / <c>new</c>）一律不写 —— 实测 partial 各段只要「没说」就不冲突
    /// （逐项省略均不报错），而「说错」才是错误：<c>abstract</c> 不能加在接口上、<c>sealed</c> 不能加在
    /// 值类型上，两者都是 CS0106。写全套修饰符反而会在接口容器与结构容器上直接生成非法代码。
    /// </para>
    /// <para>
    /// 可访问性必须照抄：嵌套类型省略可访问性时隐式为 <c>private</c>，与使用者写下的 <c>public</c> /
    /// <c>internal</c> 相冲会 CS0262，而显式写出真实的可访问性则必然一致。
    /// </para>
    /// <para>
    /// 种类关键字不能只看 <see cref="ITypeSymbol.TypeKind"/>：<c>record</c> 与 <c>class</c>
    /// 的 <see cref="TypeKind"/> 同为 <see cref="TypeKind.Class"/>，必须结合
    /// <see cref="ITypeSymbol.IsRecord"/> 才能区分，否则 <c>record Outer</c> 会被重开成
    /// <c>partial class Outer</c>，种类不一致报错。
    /// </para>
    /// </remarks>
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

        // 重开的 partial 必须重复类型参数表（否则 CS0693），但约束子句只需在任一 partial 上声明一次，
        // 故此处只写类型参数名、省略 where 子句 —— 既避开「逐类型复刻约束」的复杂度，
        // 也保证生成的重开段必然合法（约束在用户声明段上已经写明）。
        var typeParameterList = type.TypeParameters.Length > 0
            ? "<" + string.Join(", ", type.TypeParameters.Select(parameter => parameter.Name)) + ">"
            : string.Empty;

        return $"{GetAccessibilityKeyword(type.DeclaredAccessibility)} partial {keyword} {type.Name}{typeParameterList}";
    }

    /// <summary>
    /// 判断该 Id 是否带有（或本次生成即将产出）指定的内嵌转换器类型。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 判据分两支：<b>已实现 <c>IStronglyTypedId&lt;TSelf, TPrimitiveId&gt;</c></b> 的类型来自已编译的程序集，
    /// 其代码早已生成完毕 —— 内嵌类型存在就存在，不存在就永远不会出现；<b>未实现</b>该接口的类型是本次编译
    /// 新声明的，转换器由本轮生成器补上，故一律视为「将有」。
    /// </para>
    /// <para>
    /// 不能改用 <see cref="ISymbol.DeclaringSyntaxReferences"/> 判断来源：项目引用（<c>CompilationReference</c>）
    /// 与测试里内存编译出来的程序集，其符号同样带语法引用，判据会失效。而「是否实现接口」正是
    /// <see cref="GetPrimitiveIdType"/> 已经在用的同一条分界线。
    /// </para>
    /// </remarks>
    private static bool WillHaveNestedType(ITypeSymbol type, string nestedTypeName)
        => type is INamedTypeSymbol namedType
            && (namedType.GetTypeMembers(nestedTypeName).Length > 0
                || !namedType.Interfaces.Any(IsStronglyTypedIdInterface));

    /// <summary>
    /// 把基元类型的显示名归一化为不含命名空间限定的短名。
    /// </summary>
    /// <remarks>
    /// <see cref="PrimitiveIdTypeName"/> 由 <c>SymbolDisplayFormat.FullyQualifiedFormat</c> 产出，其
    /// <c>UseSpecialTypes</c> 只把 C# 关键字渲染成短名（<c>System.Int32</c> → <c>int</c>、
    /// <c>System.String</c> → <c>string</c>），普通 BCL 类型则一律带前缀（<c>System.Guid</c> →
    /// <c>global::System.Guid</c>）。若直接按字面量分支匹配，<c>"Guid"</c> 分支永不命中，Guid 强类型 Id
    /// 在 OpenAPI 文档里会丢掉 <c>format: uuid</c>。关键字本身不含 <c>.</c>，取末段后原样保留，
    /// 因此两种形态可统一处理。Swagger 与 AspNetCoreOpenApi 生成器都依赖此归一化，故集中于此。
    /// </remarks>
    internal static string GetPrimitiveIdTypeShortName(string primitiveIdTypeName)
    {
        var separatorIndex = primitiveIdTypeName.LastIndexOf('.');

        return separatorIndex < 0 ? primitiveIdTypeName : primitiveIdTypeName[(separatorIndex + 1)..];
    }

    /// <summary>
    /// 把类型显示名净化成合法的 hint 名：剔除尖括号、逗号与空格。
    /// </summary>
    private static string SanitizeHintName(string fullName)
        => fullName
            .Replace("<", string.Empty)
            .Replace(">", string.Empty)
            .Replace(",", string.Empty)
            .Replace(" ", string.Empty);

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
    /// <remarks>
    /// 判据是「简单名 + 所属命名空间为 <c>System</c>」，不能改回比较完整显示名：
    /// <c>IUtf8SpanParsable&lt;TSelf&gt;</c> 是泛型接口，实现侧带类型实参
    /// （<c>global::System.IUtf8SpanParsable&lt;global::System.Guid&gt;</c>），与字面量永远不相等。
    /// 命名空间必须一并校验，否则使用者自定义的同名接口会被误判为具备该能力，
    /// 进而在生成代码里调用并不存在的成员。
    /// </remarks>
    private static bool ImplementsInterface(ITypeSymbol type, string interfaceName)
        => type.AllInterfaces.Any(@interface => @interface.Name == interfaceName
            && @interface.ContainingNamespace.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == SystemNamespace);

    /// <summary>
    /// 判断给定类型是否暴露了公开的静态
    /// <c>TryParse(ReadOnlySpan&lt;TElement&gt;, IFormatProvider, out T)</c> 重载。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 生成代码按具体类型名调用该重载（<c>PrimitiveIdTypeName.TryParse(value, provider ?? InvariantCulture, out var val)</c>）。
    /// 若基元把它实现为<b>显式接口实现</b>（如 <c>BigInteger</c> 的 UTF-8 重载），按类型名调用会 CS1501；
    /// 而静态抽象接口成员又不能经接口名直接调用（CS8926），故只能在生成前就把这类基元筛掉。
    /// </para>
    /// <para>
    /// 判据落在符号上：遍历名为 <c>TryParse</c> 的公开静态方法，要求形参恰为
    /// <c>(ReadOnlySpan&lt;TElement&gt;, IFormatProvider, out T)</c>。第三个形参按引用类型比对即可，
    /// 无需校验它正是 <paramref name="type"/> —— 同名同形的重载在同一类型上不可能有两个。
    /// </para>
    /// </remarks>
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
    /// <remarks>
    /// <para>
    /// 优先认带 <c>IFormatProvider</c> 的 4 参重载；没有则退到 3 参重载。<c>Guid</c> 属于后者：它把带 provider
    /// 的重载实现为<b>显式接口实现</b>，按具体类型名调用 4 参重载会 CS1501，只能调公开的 3 参重载。
    /// <c>Guid</c> 的格式化本就与文化无关，省略 provider 语义不变。
    /// </para>
    /// <para>
    /// 两种重载都不存在时返回 <see cref="TryFormatKind.Interface"/>，生成代码仍走接口调用 ——
    /// 宁可保留一次装箱，也不能漏掉基元本就具备的能力。
    /// </para>
    /// <para>
    /// 判据与 <see cref="HasPublicStaticTryParse"/> 同构：遍历名为 <c>TryFormat</c> 的公开实例方法，
    /// 要求形参形如 <c>(Span&lt;TElement&gt;, out int, ReadOnlySpan&lt;char&gt;[, IFormatProvider])</c>。
    /// </para>
    /// </remarks>
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
    /// 读取 <c>[StronglyTypedId]</c> 上指定命名实参的布尔值，未指定时返回 <see langword="false"/>。
    /// </summary>
    private static bool GetBooleanArgument(ITypeSymbol type, string argumentName)
        => type.GetAttributes()
            .FirstOrDefault(attribute => attribute.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == AttributeName)
            ?.NamedArguments
            .FirstOrDefault(argument => argument.Key == argumentName)
            .Value.Value is true;

    /// <summary>
    /// 读取 <c>[StronglyTypedId]</c> 上 <c>JsonShape</c> 命名实参是否指向对象形态。
    /// </summary>
    /// <remarks>
    /// 命名实参在元数据里保存的是枚举的底层值，故这里与 <see cref="ObjectJsonShapeValue"/> 比对；
    /// 未指定时该实参根本不存在，返回 <see langword="false"/>（即默认的基元形态）。
    /// </remarks>
    private static bool GetJsonShapeIsObject(ITypeSymbol type)
        => type.GetAttributes()
            .FirstOrDefault(attribute => attribute.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == AttributeName)
            ?.NamedArguments
            .FirstOrDefault(argument => argument.Key == JsonShapeArgumentName)
            .Value.Value is ObjectJsonShapeValue;

    /// <summary>
    /// 读取 <c>[StronglyTypedId]</c> 上 <c>PrefixReadMode</c> 命名实参是否指向严格读入。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="GetJsonShapeIsObject"/> 同构：命名实参在元数据里保存的是枚举的底层值，故这里与
    /// <see cref="StrictPrefixReadModeValue"/> 比对；未指定时该实参根本不存在，返回 <see langword="false"/>
    /// （即默认的宽松读入）。
    /// </remarks>
    private static bool GetPrefixReadModeIsStrict(ITypeSymbol type)
        => type.GetAttributes()
            .FirstOrDefault(attribute => attribute.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == AttributeName)
            ?.NamedArguments
            .FirstOrDefault(argument => argument.Key == PrefixReadModeArgumentName)
            .Value.Value is StrictPrefixReadModeValue;

    /// <summary>
    /// 读取 <c>[StronglyTypedId]</c> 上指定命名实参的字符串值；未指定时返回 <see langword="null"/>。
    /// </summary>
    /// <remarks>
    /// 命名实参会被写入程序集元数据，因此引用程序集里的强类型 Id 与本次编译新声明的走同一条读取路径，
    /// 无需在生成器入口处另行传递。<c>Validator</c> 与 <c>Prefix</c> 共用此读取逻辑。
    /// </remarks>
    private static string? GetStringArgument(ITypeSymbol type, string argumentName)
        => type.GetAttributes()
            .FirstOrDefault(attribute => attribute.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == AttributeName)
            ?.NamedArguments
            .FirstOrDefault(argument => argument.Key == argumentName)
            .Value.Value as string;

    /// <summary>
    /// 把前缀归一化：空串与纯空白一律视为「未设置」（<see langword="null"/>）。
    /// </summary>
    /// <remarks>
    /// 空前缀没有任何语义；纯空白前缀几乎必然是笔误。二者都静默按「未设置」处理，避免生成一段诡异文本，
    /// 分析器另报 <c>STIA0013</c> 提示该写法。
    /// </remarks>
    private static string? NormalizePrefix(string? prefix)
        => string.IsNullOrWhiteSpace(prefix) ? null : prefix;
}
