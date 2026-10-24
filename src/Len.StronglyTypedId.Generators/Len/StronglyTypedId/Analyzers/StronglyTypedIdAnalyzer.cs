using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Concurrent;

namespace Len.StronglyTypedId.Analyzers;

// 与 CodeFixProvider 合并后必须引用 Workspaces，因而触发 RS1038。命令行编译时 Roslyn 会跳过无法解析的
// 扩展点，生成器与分析器仍正常加载（实测无 CS8032），故局部豁免而非全项目 NoWarn。
#pragma warning disable RS1038
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal class StronglyTypedIdAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(Descriptors.All);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // 集成请求（STIA0019）先攒起来，到编译结束再按「集成」聚合上报。逐类型各报一条时，
        // 「N 个 Id 都写了 Integrations = All」会炸出 7N 条同义诊断，把真正缺失的那几个包淹掉。
        context.RegisterCompilationStartAction(startContext =>
        {
            var requests = new ConcurrentBag<IntegrationRequest>();

            startContext.RegisterSymbolAction(context => AnalyzeNamedType(context, requests), SymbolKind.NamedType);

            // 程序集级默认值也走同一条聚合路径：符号操作不支持 SymbolKind.Assembly（RS1003），
            // 只能在整编译回调里读。
            startContext.RegisterCompilationEndAction(context => ReportIntegrationProblems(context, requests));
        });
        context.RegisterSyntaxNodeAction(
            AnalyzeObjectCreation,
            SyntaxKind.ObjectCreationExpression,
            SyntaxKind.ImplicitObjectCreationExpression);
        context.RegisterOperationAction(AnalyzeDefaultValue, OperationKind.DefaultValue);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context, ConcurrentBag<IntegrationRequest> integrationRequests)
    {
        if (context.Symbol is not INamedTypeSymbol type || !HasStronglyTypedIdAttribute(type))
        {
            return;
        }

        // 类型级约束描述整个类型，故跨全部声明段判定且最多报一次；参数级约束只在声明主构造函数的那一段判定
        // —— 按段逐条校验会把只追加成员的后续段误判为 STIA0005。
        var declarations = type.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax(context.CancellationToken))
            .ToArray();

        if (declarations.Length == 0)
        {
            return;
        }

        var nonRecord = declarations.FirstOrDefault(syntax => syntax is not RecordDeclarationSyntax);

        if (nonRecord is not null)
        {
            Report(context, Descriptors.TypeMustBeRecord, nonRecord.GetLocation(), type.Name);
            return;
        }

        var records = declarations.Cast<RecordDeclarationSyntax>().ToArray();

        var abstracted = records.FirstOrDefault(record => record.Modifiers.Any(SyntaxKind.AbstractKeyword));

        if (abstracted is not null)
        {
            Report(context, Descriptors.TypeCannotBeAbstract, abstracted.GetLocation(), type.Name);
            return;
        }

        var nonPartial = records.FirstOrDefault(record => !record.Modifiers.Any(SyntaxKind.PartialKeyword));

        if (nonPartial is not null)
        {
            Report(context, Descriptors.TypeMustBePartial, nonPartial.GetLocation(), type.Name);
            return;
        }

        // file 局部类型只查容器不够：record 自身带 file 时生成器同样无法在别的文件里重开它。
        // 不拦的后果是产出一条不带 file 的冲突 partial 声明 —— 编译器报 CS0535 且不带任何 STIA 诊断，
        // 用户既看不出错在生成代码，也看不出 Id 不能是 file 局部类型；严重时整个编译单元的分析器诊断还会被 csc 一并丢弃。
        var fileLocal = records.FirstOrDefault(record => record.Modifiers.Any(SyntaxKind.FileKeyword));

        if (fileLocal is not null)
        {
            Report(context, Descriptors.TypeCannotBeFileLocal, fileLocal.GetLocation(), type.Name);
            return;
        }

        var generic = records.FirstOrDefault(record => record.TypeParameterList is not null);

        if (generic is not null)
        {
            Report(context, Descriptors.TypeCannotBeGeneric, generic.GetLocation(), type.Name);
            return;
        }

        // 逐层向上判定容器能否被原样重开（要求 partial、不能是 file 本地类型），报错落在最内层不合规的容器上。
        // 定位取整个类型声明：指向子片段会让代码修复测试框架判定为「非本地的分析器诊断」。
        var container = records[0].Parent;

        while (container is TypeDeclarationSyntax declaration)
        {
            if (!declaration.Modifiers.Any(SyntaxKind.PartialKeyword) || declaration.Modifiers.Any(SyntaxKind.FileKeyword))
            {
                Report(context, Descriptors.ContainingTypeMustBePartial, declaration.GetLocation(), declaration.Identifier.ValueText);
                return;
            }

            container = declaration.Parent;
        }

        if (container is not BaseNamespaceDeclarationSyntax)
        {
            Report(context, Descriptors.TypeMustHaveNamespace, records[0].GetLocation(), type.Name);
            return;
        }

        // 主构造函数最多出现在一段声明中，以该段为准。
        var primaryConstructor = records.FirstOrDefault(record => record.ParameterList is not null) ?? records[0];

        if (primaryConstructor.ParameterList is not { Parameters: [var parameter] })
        {
            Report(context, Descriptors.TypeMustHaveSingleParameterPrimaryConstructor, primaryConstructor.GetLocation(), type.Name);
            return;
        }

        if (parameter.Type is NullableTypeSyntax)
        {
            Report(context, Descriptors.ParameterCannotBeNullable, parameter.Type.GetLocation(), parameter.Type);
            return;
        }

        if (parameter.Identifier.ValueText != "Value")
        {
            Report(context, Descriptors.ParameterNameMustBeValue, parameter.Identifier.GetLocation(), parameter.Identifier.ValueText);
            return;
        }

        // 不能用 type.Constructors[0]：顺序不保证主构造在前，取 [0].Parameters[0] 会越界抛 AD0001，把该类型的全部 STIA 诊断一起吞掉。
        // GetSingleParameterType 遍历 InstanceConstructors 并按「单参数且类型不是自身」过滤，顺带跳过 record 合成的拷贝构造。
        var constructorParameterType = GetSingleParameterType(type);

        if (constructorParameterType is null || !SupportedPrimitiveTypes.IsSupported(constructorParameterType))
        {
            Report(context, Descriptors.ParameterTypeIsInvalid, parameter.Type!.GetLocation(), parameter.Type);
            return;
        }

        // 形状校验全部通过后基元类型才可靠，此时再校验 Validator / Normalizer 的方法签名。
        var validatorName = GetEffectiveValidatorName(type, context.Compilation.Assembly);
        var normalizerName = GetEffectiveNormalizerName(type, context.Compilation.Assembly);

        if (validatorName is not null)
        {
            ValidateValidator(context, type, validatorName, constructorParameterType, records);
        }

        if (normalizerName is not null)
        {
            ValidateNormalizer(context, type, normalizerName, constructorParameterType, records);
        }

        // 异常类型只在配了验证器时才会被抛出，但仍独立校验：配错了就该在声明处报错，而不是等到运行时。
        var exceptionType = GetEffectiveExceptionType(type, context.Compilation.Assembly);

        if (exceptionType is not null)
        {
            ValidateExceptionType(context, type, exceptionType, records);
        }

        // 前缀校验与形状 / 验证器无关，独立于上面的早退分支；但要判定「前缀是否与基元文本重叠」，
        // 必须知道基元类型，故把已算出的 constructorParameterType 一并传入。
        ValidatePrefix(context, type, constructorParameterType, records);

        // 集成开关同样独立：只登记逐类型显式写出的那一份，程序集默认值由 ReportIntegrationProblems 负责。
        ValidateIntegrations(context, type, records, integrationRequests);

        // 命名实例的名字与取值：与生成器共用同一份读取逻辑，保证「报错的那条」就是「跳过不生成的那条」。
        ValidateInstances(context, type, constructorParameterType, records);

        // 「可被绕过」同样独立于形状校验，复用上面已算出的名字。
        ValidateGuardBypass(context, type, validatorName, normalizerName, records);

        // 风格提示放在全部硬性校验之后，避免连 record 都不是的类型同时冒出改名建议。
        ValidateNameConvention(context, type, records);
        ValidateDuplicatePrimitiveType(context, type, constructorParameterType, records);
    }

    /// <summary>
    /// 类型名不以 <c>Id</c> 结尾时给出改名建议（STIA0015）。
    /// </summary>
    private static void ValidateNameConvention(SymbolAnalysisContext context, INamedTypeSymbol type, RecordDeclarationSyntax[] records)
    {
        if (type.Name.EndsWith("Id", StringComparison.Ordinal))
        {
            return;
        }

        // 以 "ID" 收尾时替换而非追加，否则建议名会变成 "OrderIDId"。
        var suggestion = type.Name.EndsWith("ID", StringComparison.Ordinal)
            ? type.Name[..^2] + "Id"
            : type.Name + "Id";

        Report(context, Descriptors.TypeNameShouldEndWithId, records[0].Identifier.GetLocation(), type.Name, suggestion);
    }

    /// <summary>
    /// 同一命名空间下多个强类型 Id 共用同一基元类型时提示（STIA0016）。
    /// </summary>
    private static void ValidateDuplicatePrimitiveType(
        SymbolAnalysisContext context,
        INamedTypeSymbol type,
        ITypeSymbol primitiveType,
        RecordDeclarationSyntax[] records)
    {
        var namespaceSymbol = type.ContainingNamespace;

        if (namespaceSymbol is null || namespaceSymbol.IsGlobalNamespace)
        {
            return;
        }

        // 分组键必须与兄弟类型的取值方式一致，否则当前类型落不进自己的组（见 GetSingleParameterType）。
        var primitiveName = (GetSingleParameterType(type) ?? primitiveType).ToDisplayString();

        // 范围是「命名空间下的全部类型」，含嵌套类型：`GetTypeMembers()` 只返回顶层类型，
        // 漏掉嵌套会让 `class Orders { record OrderId(Guid Value); }` 与同命名空间的
        // `CustomerId(Guid Value)` 撞车却不报警 —— 而两者的规范文本形确实无法区分。
        var group = GetAllTypes(namespaceSymbol)
            .Where(HasStronglyTypedIdAttribute)
            .Select(member => (Member: member, Primitive: GetSingleParameterType(member)))
            .Where(pair => pair.Primitive?.ToDisplayString() == primitiveName)
            .Select(pair => pair.Member)
            // 按完整名排序：嵌套类型与顶层类型可能同名，只比 Name 得不到确定的「第一个成员」。
            .OrderBy(member => member.ToDisplayString(), StringComparer.Ordinal)
            .ToArray();

        // 组内第一个成员负责上报，否则同一组会被每个成员各报一次。
        if (group.Length < 2 || !SymbolEqualityComparer.Default.Equals(group[0], type))
        {
            return;
        }

        Report(
            context,
            Descriptors.DuplicatePrimitiveType,
            records[0].GetLocation(),
            type.Name,
            primitiveName,
            group.Length - 1,
            namespaceSymbol.ToDisplayString());
    }

    /// <summary>
    /// 枚举容器（命名空间或类型）下的全部类型，含逐层嵌套的类型。
    /// </summary>
    private static IEnumerable<INamedTypeSymbol> GetAllTypes(INamespaceOrTypeSymbol container)
    {
        foreach (var member in container.GetTypeMembers())
        {
            yield return member;

            foreach (var nested in GetAllTypes(member))
            {
                yield return nested;
            }
        }
    }

    /// <summary>
    /// 取强类型 Id 的基元类型，即唯一一个「单参数、且参数类型不是自身」的实例构造函数所带的参数类型。
    /// </summary>
    private static ITypeSymbol? GetSingleParameterType(INamedTypeSymbol type)
    {
        foreach (var constructor in type.InstanceConstructors)
        {
            if (constructor.Parameters is [var parameter]
                && !SymbolEqualityComparer.Default.Equals(parameter.Type, type))
            {
                return parameter.Type;
            }
        }

        return null;
    }

    /// <summary>
    /// 检测「配了归一化或验证器、但类型对外可见」的组合，提示约束可被直接构造绕过（STIA0014）。
    /// </summary>
    private static void ValidateGuardBypass(
        SymbolAnalysisContext context,
        INamedTypeSymbol type,
        string? validatorName,
        string? normalizerName,
        RecordDeclarationSyntax[] records)
    {
        if ((validatorName is null && normalizerName is null) || !IsExternallyVisible(type))
        {
            return;
        }

        Report(context, Descriptors.ValidatorCanBeBypassed, records[0].GetLocation(), type.Name);
    }

    /// <summary>
    /// 判断类型（含其全部包含类型）是否对外可见。
    /// </summary>
    private static bool IsExternallyVisible(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.ContainingType)
        {
            if (current.DeclaredAccessibility != Accessibility.Public)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 校验 <c>[StronglyTypedId(Prefix = …)]</c>：纯空白前缀报 STIA0013，与基元文本重叠的前缀报 STIA0023。
    /// </summary>
    /// <remarks>
    /// 空串<b>不</b>报：生成器侧把空串与纯空白都视为「未设置」，删掉实参与不删完全等价；纯空白才是笔误，仍然报。
    /// 前缀直接参与 <c>ToString()</c> 拼接与 <c>Parse</c> 剥离且无分隔符，若前缀本身可能是基元文本的开头，会被静默剥掉一段（<c>Prefix = "-"</c> 时 <c>Parse("-5")</c> 得 5）。
    /// </remarks>
    private static void ValidatePrefix(
        SymbolAnalysisContext context,
        INamedTypeSymbol type,
        ITypeSymbol primitiveType,
        RecordDeclarationSyntax[] records)
    {
        var prefix = AttributeReader.GetString(
            AttributeReader.Find(type, AttributeReader.StronglyTypedIdAttributeName),
            StronglyTypedIdInfo.PrefixArgumentName);

        if (prefix is null || prefix.Length == 0)
        {
            return;
        }

        var location = FindNamedArgumentLocation(type, StronglyTypedIdInfo.PrefixArgumentName) ?? records[0].GetLocation();

        if (string.IsNullOrWhiteSpace(prefix))
        {
            Report(context, Descriptors.InvalidPrefix, location);
            return;
        }

        if (CanBeginPrimitiveText(prefix, primitiveType))
        {
            Report(context, Descriptors.AmbiguousPrefix, location, prefix, primitiveType.ToDisplayString());
        }
    }

    /// <summary>
    /// 判断是否存在某个基元取值，其文本形式以该前缀开头 —— 即前缀与基元文本不可区分。
    /// </summary>
    /// <remarks>
    /// 只看首字符就够了：多字符前缀只有在首字符可作开头时才需进一步考虑（<c>NaN</c> / <c>Infinity</c> 单独处理），故 <c>"DT-"</c> / <c>"INV-"</c> 不会被误伤。
    /// </remarks>
    private static bool CanBeginPrimitiveText(string prefix, ITypeSymbol primitiveType)
    {
        var first = prefix[0];

        return GetPrimitiveTextKind(primitiveType) switch
        {
            // 数字、正负号、小数点都能开启一个数值文本；"NaN" / "Infinity" 按前缀逐字符比对，
            // 而不是「首字母是 N 或 I 就拒」—— 后者会误伤 "INV-" 这类完全安全的前缀。
            PrimitiveTextKind.Numeric => IsAsciiDigit(first)
                || first is '+' or '-' or '.'
                || "NaN".StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                || "Infinity".StartsWith(prefix, StringComparison.OrdinalIgnoreCase),

            // 不变文化下的日期时间与时间跨度都以数字开头；TimeSpan 的负值以 '-' 开头。
            PrimitiveTextKind.Temporal => IsAsciiDigit(first) || first == '-',

            // Guid.ToString() 恒小写十六进制，但 Guid.Parse 大小写都收，故两种都要算重叠。
            PrimitiveTextKind.Guid => IsHexDigit(first),

            // char 的文本形式就是单个字符，任何单字符前缀都是一个合法取值。
            PrimitiveTextKind.Char => prefix.Length == 1,

            // string 与 enum 不判定：字符串取值可以是任意文本，枚举文本是用户自定义的成员名，
            // 两者都没有可枚举的文本文法，任何前缀都可能与之重叠。这是「前缀与基元文本之间没有
            // 分隔符」这一设计本身的固有歧义，只能靠文档说明。
            _ => false,
        };
    }

    /// <summary>
    /// 按基元类型的文本文法归类，供 <see cref="CanBeginPrimitiveText"/> 判定。
    /// </summary>
    private static PrimitiveTextKind GetPrimitiveTextKind(ITypeSymbol primitiveType)
    {
        if (primitiveType.TypeKind == TypeKind.Enum)
        {
            return PrimitiveTextKind.Freeform;
        }

        // 其余受支持基元没有 SpecialType，按元数据名归类。调用方已保证类型来自
        // SupportedPrimitiveTypes（System / System.Numerics），不存在同名自定义类型的干扰。
        return primitiveType.SpecialType switch
        {
            SpecialType.System_String => PrimitiveTextKind.Freeform,
            SpecialType.System_Char => PrimitiveTextKind.Char,
            SpecialType.System_Byte
                or SpecialType.System_SByte
                or SpecialType.System_Int16
                or SpecialType.System_UInt16
                or SpecialType.System_Int32
                or SpecialType.System_UInt32
                or SpecialType.System_Int64
                or SpecialType.System_UInt64
                or SpecialType.System_Decimal
                or SpecialType.System_Single
                or SpecialType.System_Double => PrimitiveTextKind.Numeric,
            _ => primitiveType.Name switch
            {
                "Int128" or "UInt128" or "Half" or "BigInteger" => PrimitiveTextKind.Numeric,
                "DateTime" or "DateTimeOffset" or "DateOnly" or "TimeOnly" or "TimeSpan" => PrimitiveTextKind.Temporal,
                "Guid" => PrimitiveTextKind.Guid,
                _ => PrimitiveTextKind.Freeform,
            },
        };
    }

    /// <summary>
    /// 是否为 ASCII 数字。<c>char.IsDigit</c> 会接受阿拉伯-印度数字等 Unicode 数字，而基元文本只发 ASCII。
    /// </summary>
    private static bool IsAsciiDigit(char value) => value is >= '0' and <= '9';

    /// <summary>
    /// 是否为十六进制数字（大小写皆算）。
    /// </summary>
    private static bool IsHexDigit(char value)
        => IsAsciiDigit(value) || value is >= 'a' and <= 'f' or >= 'A' and <= 'F';

    /// <summary>
    /// 校验 <c>[StronglyTypedIdInstance]</c> 的名字与取值（STIA0020 / STIA0021），并对非 PascalCase 的名字给出
    /// 改名建议（STIA0022）。
    /// </summary>
    /// <remarks>
    /// 逐条按属性顺序上报，位置落在对应的那条 attribute 上 —— 报在类型声明处等于让人自己猜是哪一条写错了。
    /// </remarks>
    private static void ValidateInstances(
        SymbolAnalysisContext context,
        INamedTypeSymbol type,
        ITypeSymbol primitiveType,
        RecordDeclarationSyntax[] records)
    {
        var attributes = AttributeReader.FindAll(type, AttributeReader.StronglyTypedIdInstanceAttributeName);

        if (attributes.IsDefaultOrEmpty)
        {
            return;
        }

        var declarations = InstanceDeclaration.Read(type, primitiveType);

        for (var index = 0; index < declarations.Length; index++)
        {
            var declaration = declarations[index];

            var location = attributes[index].ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation()
                ?? records[0].GetLocation();

            switch (declaration.Issue)
            {
                case InstanceIssue.InvalidName:
                case InstanceIssue.DuplicateName:
                case InstanceIssue.CollidingName:
                    Report(context, Descriptors.InvalidInstanceName, location, declaration.Name, type.Name);
                    break;

                case InstanceIssue.InvalidValue:
                    Report(
                        context,
                        Descriptors.InvalidInstanceValue,
                        location,
                        declaration.Value,
                        primitiveType.ToDisplayString(),
                        declaration.Name);
                    break;

                case InstanceIssue.None:
                    // 风格提示排在硬性校验之后：已经报错的实例不再顺带建议改名。
                    if (!InstanceDeclaration.IsPascalCase(declaration.Name, out var suggestion))
                    {
                        Report(context, Descriptors.InstanceNameNotPascalCase, location, declaration.Name, suggestion);
                    }

                    break;
            }
        }
    }

    /// <summary>
    /// 登记 <c>[StronglyTypedId(Integrations = …)]</c> 请求的集成（STIA0019）。
    /// </summary>
    /// <remarks>
    /// 只在逐类型显式写出该参数时登记；未写出时生效的是程序集默认值，由 <see cref="ReportIntegrationProblems"/> 统一登记。
    /// </remarks>
    private static void ValidateIntegrations(
        SymbolAnalysisContext context,
        INamedTypeSymbol type,
        RecordDeclarationSyntax[] records,
        ConcurrentBag<IntegrationRequest> requests)
    {
        var raw = AttributeReader.GetEnumValue(
            AttributeReader.Find(type, AttributeReader.StronglyTypedIdAttributeName),
            StronglyTypedIdInfo.IntegrationsArgumentName);

        if (raw is null)
        {
            return;
        }

        var location = FindNamedArgumentLocation(type, StronglyTypedIdInfo.IntegrationsArgumentName) ?? records[0].GetLocation();
        var mask = IntegrationFlags.Normalize(raw)!.Value;

        ReportUnknownIntegration(context, type.Name, mask, location);

        requests.Add(new IntegrationRequest(type.Name, mask, location));
    }

    /// <summary>
    /// 掩码里超出 <see cref="IntegrationFlags.All"/> 的位永远不会匹配任何集成，也就永远不会生成代码
    /// （STIA0024）。不报的话，使用者会以为自己打开了某个集成。
    /// </summary>
    private static void ReportUnknownIntegration(SymbolAnalysisContext context, string owner, int mask, Location location)
    {
        var unknown = mask & ~IntegrationFlags.All;

        if (unknown != 0)
        {
            Report(context, Descriptors.UnknownIntegration, location, owner, unknown, IntegrationFlags.All);
        }
    }

    /// <summary>
    /// 汇总整个编译的集成请求，为每个「请求了但没有引用」的集成报一条 STIA0019。
    /// </summary>
    /// <remarks>
    /// 逐类型逐集成上报时，50 个 Id 配 <c>Integrations = All</c> 会得到 350 条同义诊断；按集成去重后上限就是集成总数。
    /// </remarks>
    private static void ReportIntegrationProblems(CompilationAnalysisContext context, ConcurrentBag<IntegrationRequest> requests)
    {
        var assembly = context.Compilation.Assembly;
        var attribute = AttributeReader.Find(assembly, AttributeReader.StronglyTypedIdDefaultsAttributeName);
        var raw = AttributeReader.GetEnumValue(attribute, StronglyTypedIdInfo.IntegrationsArgumentName);

        if (raw is not null)
        {
            var location = attribute?.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation() ?? Location.None;
            var mask = IntegrationFlags.Normalize(raw)!.Value;
            var unknown = mask & ~IntegrationFlags.All;

            // 程序集默认值走的是编译结束回调，这里没有 SymbolAnalysisContext，只能直接上报。
            if (unknown != 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Descriptors.UnknownIntegration, location, assembly.Name, unknown, IntegrationFlags.All));
            }

            requests.Add(new IntegrationRequest(assembly.Name, mask, location));
        }

        if (requests.IsEmpty)
        {
            return;
        }

        // 并发回调的入袋顺序不确定，先按「文件 → 位置 → 所有者」定序：诊断的落点与参数不能随线程调度抖动。
        var ordered = requests
            .OrderBy(request => request.Location.SourceTree?.FilePath ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(request => request.Location.SourceSpan.Start)
            .ThenBy(request => request.Owner, StringComparer.Ordinal)
            .ToArray();

        foreach (var requirement in _integrationRequirements)
        {
            if (IsIntegrationReferenced(context.Compilation, requirement))
            {
                continue;
            }

            var requesters = ordered
                .Where(request => (request.Mask & requirement.Flag) != 0)
                .ToArray();

            if (requesters.Length == 0)
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                Descriptors.IntegrationUnavailable,
                requesters[0].Location,
                SummarizeOwners(requesters.Select(request => request.Owner)),
                requirement.DisplayName,
                requirement.AssemblyName));
        }
    }

    /// <summary>
    /// 把请求方名字压成一行：超过 <see cref="MaxReportedOwners"/> 个时截断并附总数。
    /// </summary>
    /// <remarks>
    /// 只用逗号与括号计数，不写 "and N more" 之类英文措辞 —— 参数会嵌进中英两套文案里。
    /// </remarks>
    private static string SummarizeOwners(IEnumerable<string> owners)
    {
        var distinct = owners.Distinct(StringComparer.Ordinal).ToArray();

        return distinct.Length <= MaxReportedOwners
            ? string.Join(", ", distinct)
            : string.Join(", ", distinct.Take(MaxReportedOwners)) + " … (" + distinct.Length + ")";
    }

    /// <summary>
    /// 判断编译是否引用了某个集成所需的程序集；MVC 的门面随 .NET 版本分散在三个程序集里，任一存在即可。
    /// </summary>
    private static bool IsIntegrationReferenced(Compilation compilation, (int Flag, string AssemblyName, string DisplayName) requirement)
        => requirement.Flag == IntegrationFlags.AspNetCoreMvc
            ? ReferencesAssembly(compilation, requirement.AssemblyName)
                || ReferencesAssembly(compilation, "Microsoft.AspNetCore.Mvc.Core")
                || ReferencesAssembly(compilation, "Microsoft.AspNetCore.Mvc.Abstractions")
            : ReferencesAssembly(compilation, requirement.AssemblyName);

    private static bool ReferencesAssembly(Compilation compilation, string simpleName)
        => compilation.ReferencedAssemblyNames.Any(identity => string.Equals(identity.Name, simpleName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// 集成标志与其所需程序集的对照表。只按程序集名判定：版本不足属于另一类问题，不在这里报。
    /// </summary>
    private static readonly (int Flag, string AssemblyName, string DisplayName)[] _integrationRequirements =
    [
        (IntegrationFlags.EntityFrameworkCore, "Microsoft.EntityFrameworkCore", "EntityFrameworkCore"),
        (IntegrationFlags.Dapper, "Dapper", "Dapper"),
        (IntegrationFlags.SystemTextJson, "System.Text.Json", "SystemTextJson"),
        (IntegrationFlags.NewtonsoftJson, "Newtonsoft.Json", "NewtonsoftJson"),
        (IntegrationFlags.Swagger, "Swashbuckle.AspNetCore.SwaggerGen", "Swagger"),
        (IntegrationFlags.AspNetCoreOpenApi, "Microsoft.AspNetCore.OpenApi", "AspNetCoreOpenApi"),
        (IntegrationFlags.AspNetCoreMvc, "Microsoft.AspNetCore.Mvc", "AspNetCoreMvc"),
        (IntegrationFlags.MessagePack, "MessagePack", "MessagePack"),
        (IntegrationFlags.MongoDb, "MongoDB.Bson", "MongoDb"),
    ];

    /// <summary>
    /// STIA0019 的消息里最多列出几个请求方，其余折成总数。
    /// </summary>
    private const int MaxReportedOwners = 3;

    /// <summary>
    /// 一条「请求了某些集成」的记录，攒到编译结束再按集成聚合上报（STIA0019）。
    /// </summary>
    private readonly struct IntegrationRequest(string owner, int mask, Location location)
    {
        /// <summary>
        /// 请求方：Id 类型名，或程序集级默认值的程序集名。
        /// </summary>
        public string Owner { get; } = owner;

        /// <summary>
        /// 归一化后的集成掩码。
        /// </summary>
        public int Mask { get; } = mask;

        /// <summary>
        /// 诊断落点：逐类型取具名实参，程序集级取特性应用处。
        /// </summary>
        public Location Location { get; } = location;
    }

    /// <summary>
    /// 校验 <c>[StronglyTypedId(Validator = nameof(Foo))]</c> 指向的方法是否为合法的静态验证器。
    /// </summary>
    private static void ValidateValidator(
        SymbolAnalysisContext context,
        INamedTypeSymbol type,
        string validatorName,
        ITypeSymbol primitiveType,
        RecordDeclarationSyntax[] records)
    {
        var method = type.GetMembers(validatorName).OfType<IMethodSymbol>().FirstOrDefault(member => member.IsStatic);

        var location = FindNamedArgumentLocation(type, StronglyTypedIdInfo.ValidatorArgumentName) ?? records[0].GetLocation();

        if (method is null
            || method.ReturnType.SpecialType != SpecialType.System_Boolean
            || method.Parameters.Length != 1
            || !SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, primitiveType))
        {
            Report(context, Descriptors.ValidatorReferenceInvalid, location, validatorName, primitiveType.ToDisplayString());
        }
    }

    /// <summary>
    /// 校验 <c>[StronglyTypedId(Normalizer = nameof(Foo))]</c> 指向的方法是否为合法的静态归一化方法。
    /// </summary>
    private static void ValidateNormalizer(
        SymbolAnalysisContext context,
        INamedTypeSymbol type,
        string normalizerName,
        ITypeSymbol primitiveType,
        RecordDeclarationSyntax[] records)
    {
        var method = type.GetMembers(normalizerName).OfType<IMethodSymbol>().FirstOrDefault(member => member.IsStatic);

        var location = FindNamedArgumentLocation(type, StronglyTypedIdInfo.NormalizerArgumentName) ?? records[0].GetLocation();

        if (method is null
            || !SymbolEqualityComparer.Default.Equals(method.ReturnType, primitiveType)
            || method.Parameters.Length != 1
            || !SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, primitiveType))
        {
            Report(context, Descriptors.NormalizerReferenceInvalid, location, normalizerName, primitiveType.ToDisplayString());
        }
    }

    /// <summary>
    /// 校验 <c>[StronglyTypedId(ExceptionType = typeof(Foo))]</c> 指向的类型能否被生成代码 <c>new Foo(message)</c> 构造（STIA0018）。
    /// </summary>
    private static void ValidateExceptionType(
        SymbolAnalysisContext context,
        INamedTypeSymbol type,
        ITypeSymbol exceptionType,
        RecordDeclarationSyntax[] records)
    {
        var location = FindNamedArgumentLocation(type, StronglyTypedIdInfo.ExceptionTypeArgumentName) ?? records[0].GetLocation();

        if (!IsUsableExceptionType(exceptionType, type, context.Compilation))
        {
            Report(context, Descriptors.ExceptionTypeInvalid, location, exceptionType.ToDisplayString());
        }
    }

    /// <summary>
    /// 判断异常类型是否满足生成代码的构造要求：非抽象、非开放泛型的 <see cref="System.Exception"/> 派生类，
    /// 且从 Id 类型所在位置可访问到一个接收单个 <see cref="string"/> 的实例构造函数。
    /// </summary>
    private static bool IsUsableExceptionType(ITypeSymbol exceptionType, INamedTypeSymbol idType, Compilation compilation)
    {
        // 闭合泛型（Foo&lt;int&gt;）可以照常 new，只有开放泛型（typeof(Foo&lt;&gt;)）不行。
        if (exceptionType is not INamedTypeSymbol { TypeKind: TypeKind.Class, IsAbstract: false } named
            || named.IsUnboundGenericType)
        {
            return false;
        }

        var exceptionSymbol = compilation.GetTypeByMetadataName("System.Exception");

        if (exceptionSymbol is null)
        {
            return false;
        }

        // 从自身起沿基类链上溯：typeof(Exception) 自身也算「异常类型」。
        var derivesFromException = SymbolEqualityComparer.Default.Equals(named, exceptionSymbol);

        for (var baseType = named.BaseType; !derivesFromException && baseType is not null; baseType = baseType.BaseType)
        {
            derivesFromException = SymbolEqualityComparer.Default.Equals(baseType, exceptionSymbol);
        }

        return derivesFromException
            && named.InstanceConstructors.Any(constructor =>
                constructor.Parameters is [{ Type.SpecialType: SpecialType.System_String }]
                && compilation.IsSymbolAccessibleWithin(constructor, idType));
    }

    /// <summary>
    /// 在 <c>[StronglyTypedId(&lt;argumentName&gt; = …)]</c> 里定位指定命名实参的语法位置。
    /// </summary>
    /// <remarks>
    /// 特性归属由符号层的 <see cref="AttributeData.AttributeClass"/> 判定，语法层只负责找实参；按名字后缀匹配会把第三方同名特性也算进来。
    /// </remarks>
    private static Location? FindNamedArgumentLocation(INamedTypeSymbol type, string argumentName)
    {
        foreach (var attribute in type.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) != AttributeReader.StronglyTypedIdAttributeName
                || attribute.ApplicationSyntaxReference?.GetSyntax() is not AttributeSyntax syntax)
            {
                continue;
            }

            foreach (var argument in syntax.ArgumentList?.Arguments ?? default)
            {
                if (argument.NameEquals?.Name.Identifier.ValueText == argumentName)
                {
                    return argument.GetLocation();
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 读取 <c>[StronglyTypedId]</c> 上 <c>Validator</c> 命名实参的方法名；未指定时为 <see langword="null"/>。
    /// </summary>
    private static string? GetValidatorName(INamedTypeSymbol type)
        => AttributeReader.GetString(
            AttributeReader.Find(type, AttributeReader.StronglyTypedIdAttributeName),
            StronglyTypedIdInfo.ValidatorArgumentName);

    /// <summary>
    /// 取生效的验证器方法名：优先逐类型 <c>Validator</c>，其次回退到装配级 <c>StronglyTypedIdDefaults.Validator</c>。
    /// </summary>
    private static string? GetEffectiveValidatorName(INamedTypeSymbol type, IAssemblySymbol assembly)
        => GetValidatorName(type) ?? GetAssemblyDefaultValidator(assembly);

    /// <summary>
    /// 读取 <c>[assembly: StronglyTypedIdDefaults(Validator = …)]</c> 的默认验证器方法名；未声明时为 <see langword="null"/>。
    /// </summary>
    private static string? GetAssemblyDefaultValidator(IAssemblySymbol assembly)
        => StronglyTypedIdInfo.GetAssemblyDefaultValidator(assembly);

    /// <summary>
    /// 读取 <c>[StronglyTypedId]</c> 上 <c>Normalizer</c> 命名实参的方法名；未指定时为 <see langword="null"/>。
    /// </summary>
    private static string? GetNormalizerName(INamedTypeSymbol type)
        => AttributeReader.GetString(
            AttributeReader.Find(type, AttributeReader.StronglyTypedIdAttributeName),
            StronglyTypedIdInfo.NormalizerArgumentName);

    /// <summary>
    /// 取生效的归一化方法名：优先逐类型 <c>Normalizer</c>，其次回退到装配级 <c>StronglyTypedIdDefaults.Normalizer</c>。
    /// </summary>
    private static string? GetEffectiveNormalizerName(INamedTypeSymbol type, IAssemblySymbol assembly)
        => GetNormalizerName(type) ?? StronglyTypedIdInfo.GetAssemblyDefaultNormalizer(assembly);

    /// <summary>
    /// 读取 <c>[StronglyTypedId]</c> 上 <c>ExceptionType</c> 命名实参的类型；未指定时为 <see langword="null"/>。
    /// </summary>
    private static ITypeSymbol? GetExceptionType(INamedTypeSymbol type)
        => AttributeReader.GetType(
            AttributeReader.Find(type, AttributeReader.StronglyTypedIdAttributeName),
            StronglyTypedIdInfo.ExceptionTypeArgumentName);

    /// <summary>
    /// 取生效的异常类型：优先逐类型 <c>ExceptionType</c>，其次回退到装配级 <c>StronglyTypedIdDefaults.ExceptionType</c>。
    /// </summary>
    private static ITypeSymbol? GetEffectiveExceptionType(INamedTypeSymbol type, IAssemblySymbol assembly)
        => GetExceptionType(type) ?? StronglyTypedIdInfo.GetAssemblyDefaultExceptionTypeSymbol(assembly);

    /// <summary>
    /// 判断类型是否由 <c>[StronglyTypedId]</c> 标记。
    /// </summary>
    private static bool HasStronglyTypedIdAttribute(INamedTypeSymbol type)
        => AttributeReader.Find(type, AttributeReader.StronglyTypedIdAttributeName) is not null;

    /// <summary>
    /// 检测直接构造强类型 Id 的写法（STIA0011）：<c>new Xxx(...)</c> 与目标类型化的 <c>new(...)</c>。
    /// </summary>
    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not BaseObjectCreationExpressionSyntax creation)
        {
            return;
        }

        var method = context.SemanticModel.GetSymbolInfo(creation, context.CancellationToken).Symbol as IMethodSymbol;
        var type = method?.ContainingType;

        if (type is null || !BypassesGuards(type, context.SemanticModel.Compilation))
        {
            return;
        }

        Report(context, Descriptors.BypassCreate, creation.GetLocation(), type.Name);
    }

    /// <summary>
    /// 检测 <c>default</c> / <c>default(T)</c> 产生的零值实例（STIA0011）。
    /// </summary>
    private static void AnalyzeDefaultValue(OperationAnalysisContext context)
    {
        if (context.Operation is not IDefaultValueOperation defaultValue
            || IsComparisonOperand(defaultValue)
            || defaultValue.Type is not INamedTypeSymbol { TypeKind: TypeKind.Struct } type
            || !BypassesGuards(type, context.Compilation))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Descriptors.BypassCreate, defaultValue.Syntax.GetLocation(), type.Name));
    }

    /// <summary>
    /// 判断 <c>default</c> 是否处在「与空值比较」的语境里（<c>x == default</c>、<c>x.Equals(default)</c>）。
    /// </summary>
    private static bool IsComparisonOperand(IDefaultValueOperation operation)
    {
        var current = operation.Parent;

        while (current is IConversionOperation conversion)
        {
            current = conversion.Parent;
        }

        return current switch
        {
            IBinaryOperation { OperatorKind: BinaryOperatorKind.Equals or BinaryOperatorKind.NotEquals } => true,
            IArgumentOperation { Parent: IInvocationOperation invocation } => invocation.TargetMethod.Name == "Equals",
            _ => false,
        };
    }

    /// <summary>
    /// 判断该类型是否「配了归一化或验证器、因而绕过 Create 就等于绕过这些约束」——STIA0011 各形态的统一前提。
    /// </summary>
    private static bool BypassesGuards(INamedTypeSymbol type, Compilation compilation)
        => HasStronglyTypedIdAttribute(type)
            && (GetEffectiveValidatorName(type, compilation.Assembly) is not null
                || GetEffectiveNormalizerName(type, compilation.Assembly) is not null);

    private static void Report(SymbolAnalysisContext context, DiagnosticDescriptor descriptor, Location location, params object[] arguments)
        => context.ReportDiagnostic(Diagnostic.Create(descriptor, location, arguments));

    private static void Report(SyntaxNodeAnalysisContext context, DiagnosticDescriptor descriptor, Location location, params object[] arguments)
        => context.ReportDiagnostic(Diagnostic.Create(descriptor, location, arguments));

    /// <summary>
    /// 基元类型文本形式的文法类别，决定「哪些字符能开启一个文本」。
    /// </summary>
    private enum PrimitiveTextKind
    {
        /// <summary>
        /// 数字、正负号、小数点，或 <c>NaN</c> / <c>Infinity</c>。
        /// </summary>
        Numeric,

        /// <summary>
        /// 数字开头（日期时间、时间跨度），负的时间跨度以 <c>-</c> 开头。
        /// </summary>
        Temporal,

        /// <summary>
        /// 十六进制数字（<c>Guid</c>）。
        /// </summary>
        Guid,

        /// <summary>
        /// 单个字符（<c>char</c>）。
        /// </summary>
        Char,

        /// <summary>
        /// 文本文法不可枚举（<c>string</c> 与枚举），无法判定重叠。
        /// </summary>
        Freeform,
    }
}
