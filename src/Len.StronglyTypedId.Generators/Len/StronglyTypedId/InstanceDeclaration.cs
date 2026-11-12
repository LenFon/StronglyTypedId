namespace Len.StronglyTypedId;

/// <summary>
/// 一条命名实例声明的校验结论。
/// </summary>
internal enum InstanceIssue
{
    /// <summary>
    /// 合法，可以生成。
    /// </summary>
    None,

    /// <summary>
    /// 名字不是合法的 C# 标识符（含关键字与空串）。
    /// </summary>
    InvalidName,

    /// <summary>
    /// 同一类型上已有同名实例（先声明者胜出）。
    /// </summary>
    DuplicateName,

    /// <summary>
    /// 名字与类型自身、既有成员或即将生成的成员冲突。
    /// </summary>
    CollidingName,

    /// <summary>
    /// 文本无法解析为该基元的合法取值。
    /// </summary>
    InvalidValue,
}

/// <summary>
/// 一条 <c>[StronglyTypedIdInstance]</c> 声明：名字、原始文本、翻译出的表达式与校验结论。
/// </summary>
/// <remarks>
/// 生成器与分析器共用这一份读取逻辑，保证「报错的那条」与「跳过不生成的那条」是同一条。
/// 用普通 <see langword="readonly"/> 结构体而非位置记录：生成器跑在 netstandard2.0 上，没有 <c>IsExternalInit</c>。
/// </remarks>
internal readonly struct InstanceDeclaration
{
    /// <summary>
    /// <c>[System.CodeDom.Compiler.GeneratedCode]</c> 的全限定显示名（含 <c>global::</c> 前缀）。
    /// </summary>
    private const string GeneratedCodeAttributeName = "global::System.CodeDom.Compiler.GeneratedCodeAttribute";

    /// <summary>
    /// 生成代码里会出现的成员名。这些成员此刻还不存在（由生成器稍后产出），
    /// <c>type.GetMembers</c> 查不到，只能硬编码。
    /// </summary>
    /// <remarks>
    /// 只列<b>无条件</b>产出的成员。随基元能力 / Id 种类 / 特性开关变化的成员见
    /// <see cref="IsConditionalReservedName"/>，否则会给「本来不存在的撞名」报错。
    /// </remarks>
    private static readonly string[] _reservedNames =
    [
        "Create",
        "TryCreate",
        "Parse",
        "TryParse",
        "CompareTo",
        "Deconstruct",
        "ToString",
        "Equals",
        "GetHashCode",
        "PrintMembers",
        // 生成的关系运算符在元数据里叫这些名字，而它们都是合法 C# 标识符：
        // SyntaxFacts.IsValidIdentifier 放行，分析器运行时 GetMembers 又查不到（运算符此刻还没生成），
        // 于是实例名撞上它们会直接 CS0102。实测 op_LessThan / op_GreaterThan / op_LessThanOrEqual /
        // op_GreaterThanOrEqual 四个都会撞；op_Equality / op_Inequality 由 record 合成，一并保留以防将来生成。
        "op_LessThan",
        "op_GreaterThan",
        "op_LessThanOrEqual",
        "op_GreaterThanOrEqual",
        "op_Equality",
        "op_Inequality",
        // 弃元。`_` 是合法标识符，SyntaxFacts.IsValidIdentifier 放行，而生成代码里有 `out _`
        // （TryCreate 的两参重载委托给三参重载）。一旦 `_` 成为字段名，`out _` 就不再是弃元，
        // 而是「把静态只读字段当 out 实参」，报 CS0199。`__` 及更长的下划线串不构成弃元，不受影响。
        "_",
        // 两个 JSON 转换器名保持无条件保留：它们只在引用了对应程序集、且该集成未被关掉时才生成，
        // 但「是否引用」是编译期信息，而保留名判定拿不到集成开关的完整上下文（逐类型 + 程序集默认）。
        // 提前拦下比让产物在引用 STJ / Newtonsoft 的消费方里 CS0102 更好排查。
        "SystemTextJsonConverter",
        "NewtonsoftJsonConverter",
    ];

    /// <summary>
    /// 仅自定义基元的 Id 才会产出的成员名。
    /// </summary>
    /// <remarks>
    /// 单独成表是为了不给内置基元制造「本来不存在的撞名」：<c>CreateInstance</c> 只在自定义基元的类型上生成，
    /// 内置基元上叫这个名字的实例此前一直合法，不该因为新能力而突然报错。
    /// </remarks>
    private static readonly string[] _customPrimitiveReservedNames =
    [
        "CreateInstance",
    ];

    /// <summary>
    /// 仅引用类型 Id 才会产出的成员名。
    /// </summary>
    /// <remarks>
    /// 引用类型的比较运算符要容忍 <see langword="null"/>，故额外生成 <c>CompareValues</c>；单独成表是为了不给值类型 Id 制造「本来不存在的撞名」。
    /// </remarks>
    private static readonly string[] _referenceTypeReservedNames =
    [
        "CompareValues",
    ];

    /// <summary>
    /// 初始化一条实例声明。
    /// </summary>
    internal InstanceDeclaration(string name, string value, string? expression, InstanceIssue issue)
    {
        Name = name;
        Value = value;
        Expression = expression;
        Issue = issue;
    }

    /// <summary>
    /// 声明的名字。
    /// </summary>
    internal string Name { get; }

    /// <summary>
    /// 声明的原始文本值。
    /// </summary>
    internal string Value { get; }

    /// <summary>
    /// 翻译出的生成代码表达式；<see cref="Issue"/> 不是 <see cref="InstanceIssue.None"/> 时为 <see langword="null"/>。
    /// </summary>
    internal string? Expression { get; }

    /// <summary>
    /// 校验结论。
    /// </summary>
    internal InstanceIssue Issue { get; }

    /// <summary>
    /// 读取并逐条校验 <paramref name="type"/> 上的全部 <c>[StronglyTypedIdInstance]</c>，保持声明顺序。
    /// </summary>
    internal static ImmutableArray<InstanceDeclaration> Read(INamedTypeSymbol type, ITypeSymbol primitiveType)
    {
        var attributes = AttributeReader.FindAll(type, AttributeReader.StronglyTypedIdInstanceAttributeName);

        if (attributes.IsDefaultOrEmpty)
        {
            return [];
        }

        var builder = ImmutableArray.CreateBuilder<InstanceDeclaration>(attributes.Length);
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var attribute in attributes)
        {
            // 位置实参缺失只可能是编译错误现场（特性签名要求两个 string），按非法名字处理即可。
            var name = AttributeReader.GetPositionalString(attribute, 0) ?? string.Empty;
            var value = AttributeReader.GetPositionalString(attribute, 1) ?? string.Empty;

            if (!SyntaxFacts.IsValidIdentifier(name))
            {
                builder.Add(new InstanceDeclaration(name, value, null, InstanceIssue.InvalidName));
                continue;
            }

            if (!names.Add(name))
            {
                builder.Add(new InstanceDeclaration(name, value, null, InstanceIssue.DuplicateName));
                continue;
            }

            if (IsReservedName(type, name, primitiveType))
            {
                builder.Add(new InstanceDeclaration(name, value, null, InstanceIssue.CollidingName));
                continue;
            }

            if (!InstanceValueEmitter.TryEmit(primitiveType, value, out var expression))
            {
                builder.Add(new InstanceDeclaration(name, value, null, InstanceIssue.InvalidValue));
                continue;
            }

            builder.Add(new InstanceDeclaration(name, value, expression, InstanceIssue.None));
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// 判断名字是否会与类型自身、既有成员或即将生成的成员撞名。
    /// </summary>
    /// <remarks>
    /// 与类型同名触发 CS0542，与既有成员同名触发 CS0102；保留名清单覆盖生成器稍后才产出的成员。
    /// 必须滤掉生成器自己产出的命名实例字段，否则每条实例都会撞上自己刚生成的字段。
    /// </remarks>
    private static bool IsReservedName(INamedTypeSymbol type, string name, ITypeSymbol primitiveType)
        => name == type.Name
            || IsConditionalReservedName(type, name, primitiveType)
            || Array.IndexOf(_reservedNames, name) >= 0
            || (type.IsReferenceType && Array.IndexOf(_referenceTypeReservedNames, name) >= 0)
            || (SupportedPrimitiveTypes.IsCustomPrimitive(primitiveType) && Array.IndexOf(_customPrimitiveReservedNames, name) >= 0)
            || type.GetMembers(name).Any(member => !IsGeneratedInstanceField(member));

    /// <summary>
    /// 判断名字是否撞上「只在特定条件下才生成」的成员。
    /// </summary>
    /// <param name="type">声明 Id 的类型。</param>
    /// <param name="name">命名实例的名字。</param>
    /// <param name="primitiveType">Id 的基元类型。</param>
    /// <remarks>
    /// 这些成员不是无条件产出的，判据必须与生成器同源，否则会给「本来不存在的撞名」报 STIA0020（Error），
    /// 而用户绕过它之后实例又会被生成器静默丢弃 —— 比直接报错更难排查。
    /// <list type="bullet">
    /// <item><c>TryFormat</c>：仅基元实现 <c>ISpanFormattable</c> / <c>IUtf8SpanFormattable</c> 时生成。</item>
    /// <item><c>EqualityContract</c>：仅 <c>record class</c> 由编译器合成，<c>record struct</c> 没有。</item>
    /// <item><c>{TypeName}TypeConverter</c>：仅 <c>TypeConverter = true</c> 时生成。</item>
    /// <item><c>{TypeName}DebugView</c>：仅非泛型容器时生成（泛型容器里的 <c>[DebuggerTypeProxy]</c> 会 CS0416）。</item>
    /// </list>
    /// <para>
    /// <c>{TypeName}Surrogate</c> 刻意保持无条件：protobuf-net 的 surrogate 只能靠特性声明，而特性只能加在
    /// 本次编译声明的类型上，判据依赖「是否引用了 protobuf-net」这一编译期信息，提前拦下比让产物编译失败好。
    /// 两个 JSON 转换器名同理，见 <see cref="_reservedNames"/>。
    /// </para>
    /// </remarks>
    private static bool IsConditionalReservedName(INamedTypeSymbol type, string name, ITypeSymbol primitiveType)
    {
        if (name == type.Name + "Surrogate")
        {
            return true;
        }

        if (name == type.Name + "TypeConverter")
        {
            return HasTypeConverter(type);
        }

        if (name == type.Name + "DebugView")
        {
            return !StronglyTypedIdInfo.HasGenericContainer(type);
        }

        if (name == "TryFormat")
        {
            return ImplementsInterface(primitiveType, "ISpanFormattable")
                || ImplementsInterface(primitiveType, "IUtf8SpanFormattable");
        }

        // EqualityContract 只有 record class 才由编译器合成，record struct 没有。
        return name == "EqualityContract" && type.IsReferenceType;
    }

    /// <summary>
    /// 判断 Id 是否要求生成 <c>TypeConverter</c>：逐类型 <c>TypeConverter = true</c>，或程序集默认打开。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="StronglyTypedIdInfo.HasTypeConverter"/> 同一回退链。这里不能直接构造
    /// <see cref="StronglyTypedIdInfo"/>（那会把整份渲染工作重做一遍），故只读这一个开关。
    /// </remarks>
    private static bool HasTypeConverter(INamedTypeSymbol type)
        => AttributeReader.GetBoolean(
                AttributeReader.Find(type, AttributeReader.StronglyTypedIdAttributeName),
                StronglyTypedIdInfo.TypeConverterArgumentName)
            || StronglyTypedIdDefaults.Read(type.ContainingAssembly).TypeConverter;

    /// <summary>
    /// 判断基元类型是否实现了指定的 BCL 接口（按接口名 + <c>System</c> 命名空间匹配）。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="StronglyTypedIdInfo"/> 里同名判据同源；枚举继承的 <c>ISpanFormattable</c> 已被标记
    /// <c>[Obsolete]</c>，生成器不透出它，故这里同样排除枚举。
    /// </remarks>
    private static bool ImplementsInterface(ITypeSymbol type, string interfaceName)
        => type.TypeKind != TypeKind.Enum
            && type.AllInterfaces.Any(@interface =>
                @interface.Name == interfaceName
                && @interface.ContainingNamespace.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System");

    /// <summary>
    /// 判断成员是否是生成器为某条命名实例产出的字段（<c>public static readonly</c> + <c>[GeneratedCode]</c>）。
    /// </summary>
    /// <remarks>
    /// 只认字段：命名实例是唯一「与实例同名」的产物，其余生成成员都与实例名争同一个声明空间。
    /// </remarks>
    private static bool IsGeneratedInstanceField(ISymbol member)
        => member is IFieldSymbol { IsStatic: true, IsReadOnly: true }
            && member.GetAttributes().Any(attribute =>
                attribute.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == GeneratedCodeAttributeName);

    /// <summary>
    /// 判断名字是否符合 PascalCase，并在不符合时给出建议名。
    /// </summary>
    /// <remarks>
    /// 判定刻意保守：只看首字符是否大写、是否含下划线；全大写缩写（<c>URL</c>）不算违规。
    /// <para>
    /// 建议名只是机械推导的结果，<b>不保证可用</b>：<c>_1</c> 会得到 <c>1</c>（不是标识符），
    /// <c>create</c> 会得到 <c>Create</c>（生成成员），<c>order_id</c> 会得到 <c>OrderId</c>（类型名）。
    /// 调用方在报出建议前必须先过 <see cref="IsUsableInstanceName"/>。
    /// </para>
    /// </remarks>
    internal static bool IsPascalCase(string name, out string suggestion)
    {
        suggestion = string.Empty;

        if (name.Length == 0)
        {
            return true;
        }

        var builder = new System.Text.StringBuilder(name.Length);
        var capitalizeNext = true;

        foreach (var character in name)
        {
            if (character == '_')
            {
                capitalizeNext = true;
                continue;
            }

            builder.Append(capitalizeNext ? char.ToUpperInvariant(character) : character);
            capitalizeNext = false;
        }

        suggestion = builder.ToString();

        // 全下划线的名字（"_"、"__"）去掉分隔符后什么都不剩，给不出建议名，按合规处理以免报出空建议。
        return builder.Length == 0 || string.Equals(suggestion, name, StringComparison.Ordinal);
    }

    /// <summary>
    /// 判断一个候选名字能否作为该类型的命名实例名。
    /// </summary>
    /// <param name="type">声明 Id 的类型。</param>
    /// <param name="name">候选名字。</param>
    /// <param name="primitiveType">Id 的基元类型。</param>
    /// <param name="declarations">该类型上的全部实例声明。</param>
    /// <param name="index">候选名字所属的那条声明在 <paramref name="declarations"/> 里的下标。</param>
    /// <remarks>
    /// STIA0022 的建议名由 <see cref="IsPascalCase"/> 机械推导，<b>不保证可用</b>：<c>_1</c> 得到 <c>1</c>
    /// （不是标识符），<c>create</c> 得到 <c>Create</c>（生成成员），<c>order_id</c> 得到 <c>OrderId</c>（类型名）。
    /// 这类建议一旦被 CodeFix 应用，就把一条 Info 提示变成了 STIA0020（Error），故报出前必须先过这一关。
    /// <para>
    /// 兄弟实例之间的重名要单独判：<see cref="IsReservedName"/> 会滤掉生成器产出的实例字段
    /// （否则每条实例都会撞上自己），因此它看不到「建议名与另一条实例同名」这一档。
    /// </para>
    /// </remarks>
    internal static bool IsUsableInstanceName(
        INamedTypeSymbol type,
        string name,
        ITypeSymbol primitiveType,
        ImmutableArray<InstanceDeclaration> declarations,
        int index)
    {
        if (!SyntaxFacts.IsValidIdentifier(name) || IsReservedName(type, name, primitiveType))
        {
            return false;
        }

        for (var other = 0; other < declarations.Length; other++)
        {
            if (other != index && string.Equals(declarations[other].Name, name, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
