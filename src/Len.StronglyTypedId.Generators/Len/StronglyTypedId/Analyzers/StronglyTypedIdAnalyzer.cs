using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

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
        // 程序集级默认值只校验一次：若放到逐类型里，程序集默认写错会为每个 Id 各报一条。
        // 符号操作不支持 SymbolKind.Assembly（RS1003），故走整编译回调。
        context.RegisterCompilationAction(ReportAssemblyIntegrationProblems);
        context.RegisterSyntaxNodeAction(
            AnalyzeObjectCreation,
            SyntaxKind.ObjectCreationExpression,
            SyntaxKind.ImplicitObjectCreationExpression);
        context.RegisterOperationAction(AnalyzeDefaultValue, OperationKind.DefaultValue);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
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

        // 前缀校验与形状 / 验证器无关，独立于上面的早退分支。
        ValidatePrefix(context, type, records);

        // 集成开关同样独立：只校验逐类型显式写出的那一份，程序集默认值由 AnalyzeAssembly 负责。
        ValidateIntegrations(context, type, records);

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
    /// 校验 <c>[StronglyTypedId(Prefix = …)]</c> 的前缀是否为非空、非纯空白字符串（STIA0013）。
    /// </summary>
    private static void ValidatePrefix(SymbolAnalysisContext context, INamedTypeSymbol type, RecordDeclarationSyntax[] records)
    {
        var prefix = AttributeReader.GetString(
            AttributeReader.Find(type, AttributeReader.StronglyTypedIdAttributeName),
            StronglyTypedIdInfo.PrefixArgumentName);

        if (prefix is not null && string.IsNullOrWhiteSpace(prefix))
        {
            Report(context, Descriptors.InvalidPrefix, FindNamedArgumentLocation(records, StronglyTypedIdInfo.PrefixArgumentName) ?? records[0].GetLocation());
        }
    }

    /// <summary>
    /// 校验 <c>[StronglyTypedId(Integrations = …)]</c> 请求的集成在当前编译里是否真的可用（STIA0019）。
    /// </summary>
    /// <remarks>
    /// 只在逐类型显式写出该参数时校验；未写出时生效的是程序集默认值，由
    /// <see cref="ReportAssemblyIntegrationProblems"/> 统一报一次。
    /// </remarks>
    private static void ValidateIntegrations(SymbolAnalysisContext context, INamedTypeSymbol type, RecordDeclarationSyntax[] records)
    {
        var raw = AttributeReader.GetEnumValue(
            AttributeReader.Find(type, AttributeReader.StronglyTypedIdAttributeName),
            StronglyTypedIdInfo.IntegrationsArgumentName);

        if (raw is null)
        {
            return;
        }

        ReportUnavailableIntegrations(
            context.Compilation,
            IntegrationFlags.Normalize(raw)!.Value,
            type.Name,
            FindNamedArgumentLocation(records, StronglyTypedIdInfo.IntegrationsArgumentName) ?? records[0].GetLocation(),
            context.ReportDiagnostic);
    }

    /// <summary>
    /// 校验 <c>[assembly: StronglyTypedIdDefaults(Integrations = …)]</c> 请求的集成是否可用（STIA0019）。
    /// </summary>
    private static void ReportAssemblyIntegrationProblems(CompilationAnalysisContext context)
    {
        var assembly = context.Compilation.Assembly;
        var attribute = AttributeReader.Find(assembly, AttributeReader.StronglyTypedIdDefaultsAttributeName);
        var raw = AttributeReader.GetEnumValue(attribute, StronglyTypedIdInfo.IntegrationsArgumentName);

        if (raw is null)
        {
            return;
        }

        var location = attribute?.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation();

        ReportUnavailableIntegrations(
            context.Compilation,
            IntegrationFlags.Normalize(raw)!.Value,
            assembly.Name,
            location ?? Location.None,
            context.ReportDiagnostic);
    }

    /// <summary>
    /// 逐个比对掩码里的集成与当前编译的引用列表，为每个「请求了但没有引用」的集成报一条 STIA0019。
    /// </summary>
    private static void ReportUnavailableIntegrations(
        Compilation compilation,
        int mask,
        string ownerName,
        Location location,
        Action<Diagnostic> report)
    {
        foreach (var requirement in _integrationRequirements)
        {
            if ((mask & requirement.Flag) == 0 || IsIntegrationReferenced(compilation, requirement))
            {
                continue;
            }

            report(Diagnostic.Create(
                Descriptors.IntegrationUnavailable,
                location,
                ownerName,
                requirement.DisplayName,
                requirement.AssemblyName));
        }
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
    ];

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

        var location = FindNamedArgumentLocation(records, StronglyTypedIdInfo.ValidatorArgumentName) ?? records[0].GetLocation();

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

        var location = FindNamedArgumentLocation(records, StronglyTypedIdInfo.NormalizerArgumentName) ?? records[0].GetLocation();

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
        var location = FindNamedArgumentLocation(records, StronglyTypedIdInfo.ExceptionTypeArgumentName) ?? records[0].GetLocation();

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
    /// 在声明段里定位 <c>[StronglyTypedId(&lt;argumentName&gt; = …)]</c> 中指定命名实参的语法位置。
    /// </summary>
    private static Location? FindNamedArgumentLocation(RecordDeclarationSyntax[] records, string argumentName)
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
                        if (argument.NameEquals?.Name.Identifier.ValueText == argumentName)
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
        => AttributeReader.GetString(
            AttributeReader.Find(assembly, AttributeReader.StronglyTypedIdDefaultsAttributeName),
            StronglyTypedIdInfo.ValidatorArgumentName);

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
        => GetNormalizerName(type) ?? GetAssemblyDefaultNormalizer(assembly);

    /// <summary>
    /// 读取 <c>[assembly: StronglyTypedIdDefaults(Normalizer = …)]</c> 的默认归一化方法名；未声明时为 <see langword="null"/>。
    /// </summary>
    private static string? GetAssemblyDefaultNormalizer(IAssemblySymbol assembly)
        => AttributeReader.GetString(
            AttributeReader.Find(assembly, AttributeReader.StronglyTypedIdDefaultsAttributeName),
            StronglyTypedIdInfo.NormalizerArgumentName);

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
        => GetExceptionType(type) ?? GetAssemblyDefaultExceptionType(assembly);

    /// <summary>
    /// 读取 <c>[assembly: StronglyTypedIdDefaults(ExceptionType = typeof(…))]</c> 的默认异常类型；未声明时为 <see langword="null"/>。
    /// </summary>
    private static ITypeSymbol? GetAssemblyDefaultExceptionType(IAssemblySymbol assembly)
        => AttributeReader.GetType(
            AttributeReader.Find(assembly, AttributeReader.StronglyTypedIdDefaultsAttributeName),
            StronglyTypedIdInfo.ExceptionTypeArgumentName);

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
}
