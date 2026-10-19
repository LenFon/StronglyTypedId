using Microsoft.CodeAnalysis.Diagnostics;

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

        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
        context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        if (context.Symbol is not INamedTypeSymbol type
            || !type.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == "Len.StronglyTypedId.StronglyTypedIdAttribute"))
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

        var constructorParameterType = type.Constructors[0].Parameters[0].Type;

        if (!SupportedPrimitiveTypes.IsSupported(constructorParameterType))
        {
            Report(context, Descriptors.ParameterTypeIsInvalid, parameter.Type!.GetLocation(), parameter.Type);
            return;
        }

        // 形状校验全部通过后基元类型才可靠，此时再校验 Validator 的方法签名。
        var validatorName = GetEffectiveValidatorName(type, context.Compilation.Assembly);

        if (validatorName is not null)
        {
            ValidateValidator(context, type, validatorName, constructorParameterType, records);
        }

        // 前缀校验与形状 / 验证器无关，独立于上面的早退分支。
        ValidatePrefix(context, type, records);

        // 验证器可绕过同样独立于形状校验，复用上面已算出的名字。
        ValidateValidatorBypass(context, type, validatorName, records);

        // 风格提示放在全部硬性校验之后，避免连 record 都不是的类型同时冒出改名建议。
        ValidateNameConvention(context, type, records);
        ValidateDuplicatePrimitiveType(context, type, constructorParameterType, records);
    }

    /// <summary>
    /// 类型名不以 <c>Id</c> 结尾时给出改名建议（STIA0015）。
    /// </summary>
    /// <remarks>定位取标识符本身：这是一条关于「这个名字」的建议。该规则没有代码修复，不受相关测试约束。</remarks>
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
    /// <remarks>
    /// 每组只报一次，落在组内名字序最小的类型上。嵌套类型不在 <c>GetTypeMembers</c> 的返回范围内，天然排除。
    /// 复杂度 O(k²)，k 为同命名空间的类型数，现实中可忽略。
    /// </remarks>
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

        var group = namespaceSymbol.GetTypeMembers()
            .Where(HasStronglyTypedIdAttribute)
            .Select(member => (Member: member, Primitive: GetSingleParameterType(member)))
            .Where(pair => pair.Primitive?.ToDisplayString() == primitiveName)
            .Select(pair => pair.Member)
            .OrderBy(member => member.Name, StringComparer.Ordinal)
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
    /// 取强类型 Id 的基元类型，即唯一一个「单参数、且参数类型不是自身」的实例构造函数所带的参数类型。
    /// </summary>
    /// <remarks>
    /// 必须排除参数类型等于自身的构造函数：<c>record class</c> 合成的拷贝构造函数也是单参数的。
    /// </remarks>
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
    /// 检测「配了验证器、但类型对外可见」的组合，提示验证器可被直接构造绕过（STIA0014）。
    /// </summary>
    /// <remarks>
    /// record 的主构造函数不能单独指定可见性（CS1514），它跟随类型本身，故类型对外可见时 <c>new R(default)</c>
    /// 就可用。该规则默认关闭，此处无条件上报，是否呈现由 <c>IsEnabledByDefault</c> / <c>.editorconfig</c> 决定。
    /// </remarks>
    private static void ValidateValidatorBypass(
        SymbolAnalysisContext context,
        INamedTypeSymbol type,
        string? validatorName,
        RecordDeclarationSyntax[] records)
    {
        if (validatorName is null || !IsExternallyVisible(type))
        {
            return;
        }

        Report(context, Descriptors.ValidatorCanBeBypassed, records[0].GetLocation(), type.Name);
    }

    /// <summary>
    /// 判断类型（含其全部包含类型）是否对外可见。
    /// </summary>
    /// <remarks>嵌套在 <c>internal</c> 容器里的 <c>public</c> 类型不可跨程序集访问，故逐层判定。</remarks>
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
    /// 校验 <c>[StronglyTypedId(Prefix = …)]</c> 的前缀是否为非空、非纯空白字符串（STIA0013）。
    /// </summary>
    /// <remarks>
    /// 生成器把空 / 纯空白前缀静默按「未设置」处理，这里提前告警。取值走符号级命名实参，故常量写法也能判定，
    /// 且不触发 RS1030。
    /// </remarks>
    private static void ValidatePrefix(SymbolAnalysisContext context, INamedTypeSymbol type, RecordDeclarationSyntax[] records)
    {
        var prefix = type.GetAttributes()
            .FirstOrDefault(attribute => attribute.AttributeClass?.ToDisplayString() == "Len.StronglyTypedId.StronglyTypedIdAttribute")
            ?.NamedArguments
            .FirstOrDefault(argument => argument.Key == "Prefix")
            .Value.Value as string;

        if (prefix is not null && string.IsNullOrWhiteSpace(prefix))
        {
            Report(context, Descriptors.InvalidPrefix, FindPrefixArgumentLocation(records) ?? records[0].GetLocation());
        }
    }

    /// <summary>
    /// 校验 <c>[StronglyTypedId(Validator = nameof(Foo))]</c> 指向的方法是否为合法的静态验证器。
    /// </summary>
    /// <remarks>
    /// 校验项：方法存在、静态、返回 <see cref="bool"/>、恰好一个参数且类型等于基元 Id 类型。可见性不约束 ——
    /// 生成代码与 Id 同 partial，private 也可达。
    /// </remarks>
    private static void ValidateValidator(
        SymbolAnalysisContext context,
        INamedTypeSymbol type,
        string validatorName,
        ITypeSymbol primitiveType,
        RecordDeclarationSyntax[] records)
    {
        var method = type.GetMembers(validatorName).OfType<IMethodSymbol>().FirstOrDefault(member => member.IsStatic);

        var location = FindValidatorArgumentLocation(records) ?? records[0].GetLocation();

        if (method is null
            || method.ReturnType.SpecialType != SpecialType.System_Boolean
            || method.Parameters.Length != 1
            || !SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, primitiveType))
        {
            Report(context, Descriptors.ValidatorReferenceInvalid, location, validatorName, primitiveType.ToDisplayString());
        }
    }

    /// <summary>
    /// 在声明段里定位 <c>[StronglyTypedId(Validator = …)]</c> 中 <c>Validator</c> 实参的语法位置。
    /// </summary>
    private static Location? FindValidatorArgumentLocation(RecordDeclarationSyntax[] records)
    {
        foreach (var record in records)
        {
            foreach (var attributeList in record.AttributeLists)
            {
                foreach (var attribute in attributeList.Attributes)
                {
                    if (!IsStronglyTypedIdAttribute(attribute))
                    {
                        continue;
                    }

                    foreach (var argument in attribute.ArgumentList?.Arguments ?? default)
                    {
                        if (argument.NameEquals?.Name.Identifier.ValueText == "Validator")
                        {
                            return argument.GetLocation();
                        }
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 在声明段里定位 <c>[StronglyTypedId(Prefix = …)]</c> 中 <c>Prefix</c> 实参的语法位置。
    /// </summary>
    private static Location? FindPrefixArgumentLocation(RecordDeclarationSyntax[] records)
    {
        foreach (var record in records)
        {
            foreach (var attributeList in record.AttributeLists)
            {
                foreach (var attribute in attributeList.Attributes)
                {
                    if (!IsStronglyTypedIdAttribute(attribute))
                    {
                        continue;
                    }

                    foreach (var argument in attribute.ArgumentList?.Arguments ?? default)
                    {
                        if (argument.NameEquals?.Name.Identifier.ValueText == "Prefix")
                        {
                            return argument.GetLocation();
                        }
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 判断 attribute 语法是否为 <c>[StronglyTypedId]</c>（含各种书写形态）。
    /// </summary>
    private static bool IsStronglyTypedIdAttribute(AttributeSyntax attribute)
    {
        var attributeName = attribute.Name.ToString();

        return attributeName is "StronglyTypedId" or "StronglyTypedIdAttribute" or "Len.StronglyTypedId.StronglyTypedIdAttribute"
            || attributeName.EndsWith("StronglyTypedIdAttribute", StringComparison.Ordinal);
    }

    /// <summary>
    /// 读取 <c>[StronglyTypedId]</c> 上 <c>Validator</c> 命名实参的方法名；未指定时为 <see langword="null"/>。
    /// </summary>
    private static string? GetValidatorName(INamedTypeSymbol type)
        => type.GetAttributes()
            .FirstOrDefault(attribute => attribute.AttributeClass?.ToDisplayString() == "Len.StronglyTypedId.StronglyTypedIdAttribute")
            ?.NamedArguments
            .FirstOrDefault(argument => argument.Key == "Validator")
            .Value.Value as string;

    /// <summary>
    /// 取生效的验证器方法名：优先逐类型 <c>Validator</c>，其次回退到装配级 <c>StronglyTypedIdDefaults.Validator</c>。
    /// </summary>
    private static string? GetEffectiveValidatorName(INamedTypeSymbol type, IAssemblySymbol assembly)
        => GetValidatorName(type) ?? GetAssemblyDefaultValidator(assembly);

    /// <summary>
    /// 读取 <c>[assembly: StronglyTypedIdDefaults(Validator = …)]</c> 的默认验证器方法名；未声明时为 <see langword="null"/>。
    /// </summary>
    private static string? GetAssemblyDefaultValidator(IAssemblySymbol assembly)
        => assembly.GetAttributes()
            .FirstOrDefault(attribute => attribute.AttributeClass?.ToDisplayString() == "Len.StronglyTypedId.StronglyTypedIdDefaultsAttribute")
            ?.NamedArguments
            .FirstOrDefault(argument => argument.Key == "Validator")
            .Value.Value as string;

    /// <summary>
    /// 判断类型是否由 <c>[StronglyTypedId]</c> 标记。
    /// </summary>
    private static bool HasStronglyTypedIdAttribute(INamedTypeSymbol type)
        => type.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == "Len.StronglyTypedId.StronglyTypedIdAttribute");

    /// <summary>
    /// 检测直接 <c>new Xxx(...)</c> 构造强类型 Id 的写法，仅在该 Id 设了 <c>Validator</c> 时提示（STIA0011）。
    /// </summary>
    /// <remarks>生成代码里的 <c>new Xxx(...)</c> 由生成代码豁免自动忽略，不会误报。</remarks>
    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not ObjectCreationExpressionSyntax creation)
        {
            return;
        }

        var method = context.SemanticModel.GetSymbolInfo(creation, context.CancellationToken).Symbol as IMethodSymbol;
        var type = method?.ContainingType;

        if (type is null || !HasStronglyTypedIdAttribute(type) || GetEffectiveValidatorName(type, context.SemanticModel.Compilation.Assembly) is null)
        {
            return;
        }

        Report(context, Descriptors.BypassCreate, creation.GetLocation(), type.Name);
    }

    private static void Report(SymbolAnalysisContext context, DiagnosticDescriptor descriptor, Location location, params object[] arguments)
        => context.ReportDiagnostic(Diagnostic.Create(descriptor, location, arguments));

    private static void Report(SyntaxNodeAnalysisContext context, DiagnosticDescriptor descriptor, Location location, params object[] arguments)
        => context.ReportDiagnostic(Diagnostic.Create(descriptor, location, arguments));
}
