namespace Len.StronglyTypedId;

/// <summary>
/// 生成器会写进 Id 类型体内的成员名，以及「类型名是否撞上它们」的判据。
/// </summary>
/// <remarks>
/// 类型名与成员名相同会触发 CS0542（成员名不能与它们的封闭类型相同）。这些成员由生成器产出，
/// 用户在声明处看不到，编译器报错也只指向生成文件，因此必须由分析器提前报出、生成器同步跳过。
/// <para>
/// 与 <see cref="InstanceDeclaration"/> 的保留名清单同源：那里判「命名实例叫这个名字会不会撞」，
/// 这里判「类型叫这个名字会不会撞」。两处判据都从这张表派生，避免新增成员时只改一处。
/// </para>
/// </remarks>
internal static class GeneratedMemberNames
{
    /// <summary>
    /// 核心生成器无条件产出的成员名（元数据名）。
    /// </summary>
    /// <remarks>
    /// 运算符在元数据里叫 <c>op_LessThan</c> 这类名字，而它们都是合法 C# 标识符，
    /// <c>SyntaxFacts.IsValidIdentifier</c> 会放行，故必须显式列出。
    /// </remarks>
    internal static readonly ImmutableHashSet<string> Core = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "Create",
        "TryCreate",
        "Parse",
        "TryParse",
        "CompareTo",
        "op_LessThan",
        "op_GreaterThan",
        "op_LessThanOrEqual",
        "op_GreaterThanOrEqual");

    /// <summary>
    /// record 自身合成的成员名。
    /// </summary>
    /// <remarks>
    /// 这些名字作为类型名时，用户自己的声明就已经 CS0542（<c>record Equals</c> 与合成的 <c>Equals</c> 撞名），
    /// 与本库无关。纳入判据会报出一条指向错误原因的诊断，故只在此记录、不参与碰撞判定。
    /// <para>
    /// <c>ToString</c> 与 <c>Deconstruct</c> 也在其中：生成器虽然会重写 / 追加它们，但 record 本身已合成同名成员，
    /// 用户写 <c>record ToString</c> 时即便没有本库也编译不过。
    /// </para>
    /// </remarks>
    internal static readonly ImmutableHashSet<string> RecordSynthesized = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "Equals",
        "GetHashCode",
        "PrintMembers",
        "EqualityContract",
        "ToString",
        "Deconstruct",
        "op_Equality",
        "op_Inequality",
        "Value");

    /// <summary>
    /// 引用类型 Id 才会产出的 null 安全比较辅助方法名。
    /// </summary>
    internal const string CompareValues = "CompareValues";

    /// <summary>
    /// 自定义基元且声明了合法命名实例时才会产出的私有工厂方法名。
    /// </summary>
    internal const string CreateInstance = "CreateInstance";

    /// <summary>
    /// 基元可 span 格式化时才会产出的成员名。
    /// </summary>
    internal const string TryFormat = "TryFormat";

    /// <summary>
    /// System.Text.Json 转换器的嵌套类型名。
    /// </summary>
    internal const string SystemTextJsonConverter = "SystemTextJsonConverter";

    /// <summary>
    /// Newtonsoft.Json 转换器的嵌套类型名。
    /// </summary>
    internal const string NewtonsoftJsonConverter = "NewtonsoftJsonConverter";

    /// <summary>
    /// 本次编译会产出哪些依赖「引用了什么」的嵌套类型。
    /// </summary>
    /// <remarks>
    /// 生成哪些成员取决于「引用了什么」以及 Id 自身的开关，而分析器与生成器都要判同一件事。
    /// 收成一个值对象而不是逐个布尔参数：判据已有三个维度，再加就会变成难以读懂的布尔参数列。
    /// <para>
    /// <see cref="EmitsProtobufSurrogate"/> 是「本次会产出 <c>{TypeName}Surrogate</c>」的最终结论，
    /// 由调用方算好：分析器跑在生成后的编译上，此时 <c>[ProtoContract]</c> 可能来自生成器自己，
    /// 不能直接用 <see cref="StronglyTypedIdInfo.HasProtoContractAttribute"/> 判断。
    /// </para>
    /// </remarks>
    internal readonly record struct IntegrationReferences(
        bool SystemTextJson,
        bool NewtonsoftJson,
        bool EmitsProtobufSurrogate);

    /// <summary>
    /// 廉价前置判定：该名字是否可能撞上任何生成成员。
    /// </summary>
    /// <remarks>
    /// 绝大多数 Id 的名字不在此列，先用字符串比较挡掉，避免为每个 Id 构造 <see cref="StronglyTypedIdInfo"/>。
    /// </remarks>
    internal static bool MayCollide(string typeName)
        => Core.Contains(typeName)
            || typeName is CompareValues or CreateInstance or TryFormat
                or SystemTextJsonConverter or NewtonsoftJsonConverter;

    /// <summary>
    /// 判断该 Id 的类型名是否与生成器产出的成员撞名。
    /// </summary>
    /// <param name="idInfo">当前 Id 的描述信息。</param>
    /// <param name="hasSystemTextJsonConverter">本次编译是否引用了 System.Text.Json。</param>
    /// <param name="hasNewtonsoftJsonConverter">本次编译是否引用了 Newtonsoft.Json ≥ 13。</param>
    /// <remarks>
    /// 两个转换器的名字取决于是否引用了对应程序集，故由调用方传入；其余成员只由 Id 自身属性决定。
    /// </remarks>
    internal static bool CollidesWithGeneratedMember(
        StronglyTypedIdInfo idInfo,
        bool hasSystemTextJsonConverter,
        bool hasNewtonsoftJsonConverter)
        => Core.Contains(idInfo.Name)
            || (idInfo.TypeKindSuffix is null && idInfo.Name == CompareValues)
            || (idInfo.IsCustomPrimitive && idInfo.InstancesBlock.Length > 0 && idInfo.Name == CreateInstance)
            || ((idInfo.IsSpanFormattable || idInfo.IsUtf8SpanFormattable) && idInfo.Name == TryFormat)
            || (hasSystemTextJsonConverter && idInfo.Name == SystemTextJsonConverter)
            || (hasNewtonsoftJsonConverter && idInfo.Name == NewtonsoftJsonConverter);

    /// <summary>
    /// 判断给定名字是否就是生成器本次会写进该 Id 的某个成员名（不比较签名）。
    /// </summary>
    /// <param name="name">待判定的名字（元数据名，运算符即 <c>op_LessThan</c> 形态）。</param>
    /// <param name="idInfo">当前 Id 的描述信息。</param>
    /// <param name="references">本次编译引用了哪些集成程序集。</param>
    /// <remarks>
    /// 供字段 / 属性 / 事件 / 嵌套类型使用：这些成员只要同名就撞 CS0102，与参数表无关，
    /// 故判据只到名字这一层。方法走 <see cref="FindCollidingGeneratedMember"/>，那里还要比签名 ——
    /// 同名不同参数表是合法重载。
    /// <para>
    /// 只覆盖生成器自己产出的名字。record 合成的 <c>Equals</c> / <c>GetHashCode</c> / <c>PrintMembers</c> /
    /// <c>ToString</c> / <c>Deconstruct</c> / <c>op_Equality</c> / <c>op_Inequality</c> / <c>Value</c> 不在此列：
    /// 用户写这些名字即便没有本库也编译不过，报出来只会把错误原因指错。
    /// </para>
    /// </remarks>
    internal static bool IsGeneratedMemberName(
        string name,
        StronglyTypedIdInfo idInfo,
        IntegrationReferences references)
        => Core.Contains(name)
            || (idInfo.TypeKindSuffix is null && name == CompareValues)
            || (idInfo.IsCustomPrimitive && idInfo.InstancesBlock.Length > 0 && name == CreateInstance)
            || ((idInfo.IsSpanFormattable || idInfo.IsUtf8SpanFormattable) && name == TryFormat)
            || IsGeneratedNestedTypeName(name, idInfo, references);

    /// <summary>
    /// 判断给定名字是否是生成器本次会写进该 Id 的嵌套类型名。
    /// </summary>
    /// <param name="name">待判定的名字。</param>
    /// <param name="idInfo">当前 Id 的描述信息。</param>
    /// <param name="references">本次编译引用了哪些集成程序集。</param>
    /// <remarks>
    /// 方法用这些名字同样撞 CS0102（成员名与嵌套类型名共用一个声明空间），故方法也要单独判这一档。
    /// <para>
    /// <c>{TypeName}Surrogate</c> 必须按生成器的真实条件判定，不能无条件保留：protobuf-net 未引用、
    /// Id 在泛型容器里、或用户自带 <c>[ProtoContract]</c> 时生成器都不产出它，此时用户的同名嵌套类型
    /// 完全合法，无条件保留会把能编译的代码报成 Error。这三个条件由调用方算进
    /// <see cref="IntegrationReferences.EmitsProtobufSurrogate"/>。
    /// </para>
    /// </remarks>
    internal static bool IsGeneratedNestedTypeName(
        string name,
        StronglyTypedIdInfo idInfo,
        IntegrationReferences references)
        => (references.SystemTextJson && idInfo.Supports(IntegrationFlags.SystemTextJson) && name == SystemTextJsonConverter)
            || (references.NewtonsoftJson && idInfo.Supports(IntegrationFlags.NewtonsoftJson) && name == NewtonsoftJsonConverter)
            || (idInfo.HasTypeConverter && name == idInfo.Name + "TypeConverter")
            || (!idInfo.IsInGenericContainer && name == idInfo.Name + "DebugView")
            || (references.EmitsProtobufSurrogate && name == idInfo.Name + "Surrogate");

    /// <summary>
    /// 判断该成员是否是用户自己写在源文件里的声明。
    /// </summary>
    /// <remarks>
    /// 生成器与分析器都要跳过 record 合成的成员（<c>Equals</c> / <c>GetHashCode</c> / <c>PrintMembers</c> …）：
    /// 用户重写它们不构成本库的问题。生成器跑在生成前的编译上，此时 <c>GetMembers</c> 里还没有生成成员；
    /// 分析器跑在生成后的编译上，生成成员的位置落在生成文件里，由 Roslyn 的
    /// <c>GeneratedCodeAnalysisFlags.None</c> 统一滤掉。
    /// </remarks>
    internal static bool IsUserDeclaredMember(ISymbol member)
        => !member.IsImplicitlyDeclared
            && member.Locations.Length > 0
            && member.Locations[0].IsInSource;

    /// <summary>
    /// 判断单个用户成员是否与生成器即将产出的成员撞名，撞名时返回该生成成员的名字。
    /// </summary>
    /// <param name="member">用户在 Id 类型上声明的成员。</param>
    /// <param name="idInfo">当前 Id 的描述信息。</param>
    /// <param name="references">本次编译引用了哪些集成程序集。</param>
    /// <remarks>
    /// 生成器与分析器共用这一份判据：分析器据此报 <c>STIA0029</c>，生成器据此跳过产出。
    /// 两边必须同源 —— 生成器若产出而分析器不报，用户会看到一条指向生成文件的 CS0111；
    /// 生成器若不产出而分析器不报，用户会一个产物都没有且没有任何提示。
    /// </remarks>
    internal static string? FindCollision(
        ISymbol member,
        StronglyTypedIdInfo idInfo,
        IntegrationReferences references)
        => member is IMethodSymbol method
            ? FindCollidingGeneratedMember(method, idInfo, references)
            : IsGeneratedMemberName(member.Name, idInfo, references)
                ? member.Name
                : null;

    /// <summary>
    /// 判断该 Id 上是否有用户成员会与生成器即将产出的成员撞名。
    /// </summary>
    /// <param name="type">声明 Id 的类型。</param>
    /// <param name="idInfo">当前 Id 的描述信息。</param>
    /// <param name="references">本次编译引用了哪些集成程序集。</param>
    /// <remarks>
    /// 供生成器在产出前判定：撞名时产物必然 CS0111 / CS0102，而这类<b>声明级</b>编译错误会让编译器
    /// 跳过全部分析器诊断 —— 包括本该指出问题所在的 <c>STIA0029</c>。用户于是只看到一条指向生成文件的错误，
    /// 既不能编辑那个文件，也不知道该改自己哪一行。生成器不产出后，<c>STIA0029</c> 成为唯一可见的错误。
    /// <para>
    /// 判据必须与分析器同源（<see cref="FindCollision"/>），否则会出现「生成器跳过但分析器不报」——
    /// 用户一个产物都没有，且没有任何提示。
    /// </para>
    /// </remarks>
    internal static bool HasMemberCollision(
        INamedTypeSymbol type,
        StronglyTypedIdInfo idInfo,
        IntegrationReferences references)
    {
        foreach (var member in type.GetMembers())
        {
            if (IsUserDeclaredMember(member) && FindCollision(member, idInfo, references) is not null)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 判断用户自己声明的成员是否会与生成器即将产出的成员撞名（同名且同参数表）。
    /// </summary>
    /// <param name="member">用户在 Id 类型上声明的成员。</param>
    /// <param name="idInfo">当前 Id 的描述信息。</param>
    /// <param name="references">本次编译引用了哪些集成程序集。</param>
    /// <returns>会撞名时返回生成成员的名字（用于诊断文案），否则返回 <see langword="null"/>。</returns>
    /// <remarks>
    /// 撞名的后果是 CS0111（方法）或 CS0102（字段 / 嵌套类型），而错误只指向生成文件 —— 用户既不能编辑那个文件，
    /// 也不知道该改自己哪一行。类型名撞名已有 STIA0027，成员名撞名同样需要一条落在用户声明上的诊断。
    /// <para>
    /// 判据必须与生成器同源且<b>按条件</b>判定：<c>TryFormat</c> 只在基元可 span 格式化时生成、
    /// <c>ToString(string, IFormatProvider)</c> 只在基元实现 <c>IFormattable</c> 时生成、
    /// <c>CompareValues</c> 只在引用类型 Id 上生成。无条件判定会把「本来不存在的撞名」误报成 Error。
    /// </para>
    /// <para>
    /// 只比「名字 + 参数个数 + 每个参数的类型与修饰符」，不比返回类型：C# 不允许仅按返回类型重载，
    /// 故同名同参数表必然撞名，与返回类型无关。
    /// </para>
    /// </remarks>
    internal static string? FindCollidingGeneratedMember(
        IMethodSymbol member,
        StronglyTypedIdInfo idInfo,
        IntegrationReferences references)
        => MatchesGeneratedSignature(member, idInfo)
            || IsGeneratedNestedTypeName(member.Name, idInfo, references)
                ? member.Name
                : null;

    /// <summary>
    /// 判断用户方法是否与生成器产出的某个方法签名完全相同。
    /// </summary>
    /// <remarks>
    /// 运算符在元数据里叫 <c>op_LessThan</c> 这类名字，<see cref="ISymbol.Name"/> 正是这个形态，
    /// 故与 <see cref="Core"/> 里的保留名一一对应。
    /// <para>
    /// 泛型方法一律不算撞名：生成器产出的方法都不带类型参数，而 C# 允许按元数重载，
    /// <c>Create&lt;T&gt;(Guid)</c> 与 <c>Create(Guid)</c> 可以并存。
    /// </para>
    /// </remarks>
    private static bool MatchesGeneratedSignature(IMethodSymbol member, StronglyTypedIdInfo idInfo)
    {
        if (member.TypeParameters.Length > 0)
        {
            return false;
        }

        var self = idInfo.FullyQualifiedName;
        var primitive = idInfo.PrimitiveIdTypeName;
        var validationResult = StronglyTypedIdInfo.ValidationResultTypeName;
        var provider = "global::System.IFormatProvider";
        var spanChars = "global::System.ReadOnlySpan<char>";
        var spanBytes = "global::System.ReadOnlySpan<byte>";
        var writableChars = "global::System.Span<char>";
        var writableBytes = "global::System.Span<byte>";

        return member.Name switch
        {
            // 无条件产出。
            "Create" => HasParameters(member, ByValue(primitive)),
            "Deconstruct" => HasParameters(member, Out(primitive)),
            "CompareTo" => HasParameters(member, ByValue(self)),
            "ToString" => HasParameters(member)
                || (idInfo.IsFormattable && HasParameters(member, ByValue("string"), ByValue(provider))),
            // 三个 Parse 重载：string 与 char-span 无条件，byte-span 仅基元实现 IUtf8SpanParsable 时。
            "Parse" => HasParameters(member, ByValue("string"), ByValue(provider))
                || HasParameters(member, ByValue(spanChars), ByValue(provider))
                || (idInfo.IsUtf8SpanParsable && HasParameters(member, ByValue(spanBytes), ByValue(provider))),
            "TryParse" => HasParameters(member, ByValue("string"), ByValue(provider), Out(self))
                || HasParameters(member, ByValue(spanChars), ByValue(provider), Out(self))
                || (idInfo.IsUtf8SpanParsable && HasParameters(member, ByValue(spanBytes), ByValue(provider), Out(self))),
            "TryCreate" => HasParameters(member, ByValue(primitive), Out(self))
                || HasParameters(member, ByValue(primitive), Out(self), Out(validationResult)),
            // 自定义基元且声明了命名实例时，生成器产出私有工厂 CreateInstance(string, string)。
            "CreateInstance" => idInfo.IsCustomPrimitive
                && idInfo.InstancesBlock.Length > 0
                && HasParameters(member, ByValue("string"), ByValue("string")),
            "TryFormat" => (idInfo.IsSpanFormattable
                    && HasParameters(member, ByValue(writableChars), Out("int"), ByValue(spanChars), ByValue(provider)))
                || (idInfo.IsUtf8SpanFormattable
                    && HasParameters(member, ByValue(writableBytes), Out("int"), ByValue(spanChars), ByValue(provider))),
            "CompareValues" => idInfo.TypeKindSuffix is null && HasParameters(member, ByValue(self), ByValue(self)),
            "op_LessThan" or "op_GreaterThan" or "op_LessThanOrEqual" or "op_GreaterThanOrEqual" =>
                HasParameters(member, ByValue(self), ByValue(self)),
            _ => false,
        };
    }

    /// <summary>
    /// 判断方法的参数表是否与给定的「修饰符 + 类型」序列完全一致。
    /// </summary>
    /// <remarks>
    /// 类型比较走 <see cref="Describe"/>：引用类型的 <c>?</c> 只是可空标注，不参与重载决议，故剥掉；
    /// 值类型的 <c>?</c> 是 <see cref="System.Nullable{T}"/>，按值传递时是类型本身的一部分，必须保留 ——
    /// <c>record struct</c> 上的 <c>CompareTo(OrderId?)</c> 与生成的 <c>CompareTo(OrderId)</c> 是合法重载。
    /// </remarks>
    private static bool HasParameters(IMethodSymbol member, params (RefKind RefKind, string Type)[] parameters)
    {
        if (member.Parameters.Length != parameters.Length)
        {
            return false;
        }

        for (var index = 0; index < parameters.Length; index++)
        {
            var actual = member.Parameters[index];

            if (actual.RefKind != parameters[index].RefKind
                || Describe(actual.Type, actual.RefKind) != parameters[index].Type)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 把参数类型渲染成用于比较的文本。
    /// </summary>
    /// <param name="type">参数类型。</param>
    /// <param name="refKind">参数的传递方式。</param>
    /// <remarks>
    /// 引用类型的可空标注（<c>string?</c>）不影响重载决议，剥掉末尾 <c>?</c> 后与 <c>string</c> 等价。
    /// <para>
    /// 值类型的 <c>?</c> 是 <see cref="System.Nullable{T}"/>，按值传递时与 <c>T</c> 是不同类型，属合法重载，
    /// 故保留原样。但 <c>out</c> / <c>ref</c> 位置要剥掉：生成代码用 <c>out var</c> 接收，两个候选都能推断，
    /// 编译器报 CS0121 二义性 —— 与撞名同样阻断编译。
    /// </para>
    /// </remarks>
    private static string Describe(ITypeSymbol type, RefKind refKind)
    {
        var text = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        return type.IsValueType && refKind is not (RefKind.Out or RefKind.Ref)
            ? text
            : Normalize(text);
    }

    /// <summary>
    /// 构造一个按值传递的参数描述。
    /// </summary>
    private static (RefKind RefKind, string Type) ByValue(string type) => (RefKind.None, type);

    /// <summary>
    /// 构造一个 <c>out</c> 参数描述。
    /// </summary>
    private static (RefKind RefKind, string Type) Out(string type) => (RefKind.Out, type);

    /// <summary>
    /// 剥掉类型显示名末尾的可空标注。
    /// </summary>
    /// <remarks>
    /// 用 <c>EndsWith(string, StringComparison)</c> 而非 <c>EndsWith(char)</c>：生成器跑在 netstandard2.0 上，
    /// 那里没有 char 重载。
    /// </remarks>
    private static string Normalize(string typeName)
        => typeName.EndsWith("?", StringComparison.Ordinal) ? typeName[..^1] : typeName;
}
