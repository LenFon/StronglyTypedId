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
/// <para>
/// 生成器与分析器共用这一份读取逻辑，保证「报错的那条」与「跳过不生成的那条」永远是同一条。
/// 它只活在单次分析/生成过程中，不进 <see cref="StronglyTypedIdInfo"/> —— 后者是增量缓存的键，
/// 只能持有稳定值（见 <see cref="StronglyTypedIdInfo.InstancesBlock"/>）。
/// </para>
/// <para>
/// 刻意用普通 <see langword="readonly"/> 结构体而非位置记录：生成器跑在 netstandard2.0 上，
/// 位置记录会为每个属性合成 <c>init</c> 访问器，进而要求 <c>IsExternalInit</c> —— 那个类型在
/// netstandard2.0 里并不存在。
/// </para>
/// </remarks>
internal readonly struct InstanceDeclaration
{
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
    /// 初始化一条实例声明。
    /// </summary>
    /// <param name="name">声明的名字。</param>
    /// <param name="value">声明的原始文本值。</param>
    /// <param name="expression">翻译出的生成代码表达式；非法时为 <see langword="null"/>。</param>
    /// <param name="issue">校验结论。</param>
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
    /// <param name="type">强类型 Id 类型。</param>
    /// <param name="primitiveType">该 Id 包装的基元类型。</param>
    /// <returns>与属性一一对应的结论；没有声明时为空数组。</returns>
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

            if (IsReservedName(type, name))
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
    /// 与类型同名会触发 CS0542（成员名不能与包含类型同名），与既有成员同名会触发 CS0102。
    /// <c>type.GetMembers</c> 覆盖用户自己写的成员与 record 合成成员（<c>Value</c> / <c>Equals</c> /
    /// <c>&lt;Clone&gt;$</c> 等），保留名清单覆盖生成器稍后才产出的那些。
    /// </remarks>
    private static bool IsReservedName(INamedTypeSymbol type, string name)
        => name == type.Name
            || name == type.Name + "TypeConverter"
            || Array.IndexOf(_reservedNames, name) >= 0
            || type.GetMembers(name).Length > 0;

    /// <summary>
    /// 判断名字是否符合 PascalCase，并在不符合时给出建议名。
    /// </summary>
    /// <remarks>
    /// 判定刻意保守：只看首字符是否大写、以及是否含下划线。全大写缩写（<c>URL</c>）不算违规 ——
    /// 它已经是 PascalCase 的一种常见写法，强行要求改成 <c>Url</c> 只会制造噪音。
    /// </remarks>
    /// <param name="name">实例名。</param>
    /// <param name="suggestion">不符合时给出的 PascalCase 写法；符合时为空串。</param>
    /// <returns>符合 PascalCase 时为 <see langword="true"/>。</returns>
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
