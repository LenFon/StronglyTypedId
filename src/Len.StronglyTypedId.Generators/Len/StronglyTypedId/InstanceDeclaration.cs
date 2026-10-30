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
    private static readonly string[] _reservedNames =
    [
        "Create",
        "TryCreate",
        "Parse",
        "TryParse",
        "CompareTo",
        "Deconstruct",
        "ToString",
        "TryFormat",
        "Equals",
        "GetHashCode",
        "PrintMembers",
        "EqualityContract",
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
            || name == type.Name + "TypeConverter"
            || name == type.Name + "DebugView"
            || Array.IndexOf(_reservedNames, name) >= 0
            || (type.IsReferenceType && Array.IndexOf(_referenceTypeReservedNames, name) >= 0)
            || (SupportedPrimitiveTypes.IsCustomPrimitive(primitiveType) && Array.IndexOf(_customPrimitiveReservedNames, name) >= 0)
            || type.GetMembers(name).Any(member => !IsGeneratedInstanceField(member));

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
}
