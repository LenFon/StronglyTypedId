using Microsoft.CodeAnalysis.Diagnostics;

namespace Len.StronglyTypedId.Analyzers;

// 本程序集与 CodeFixProvider 合并后必须引用 Microsoft.CodeAnalysis.Workspaces，因而触发 RS1038。
// 命令行编译时 Roslyn 会逐个跳过因缺少 Workspaces 而无法解析的扩展点，生成器与分析器仍正常加载
// （消费者项目实测：源码生成正常、诊断正常、无 CS8032），故在此处局部豁免该规则而非全项目 NoWarn。
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

        // 强类型 Id 可以跨多个 partial 声明分段声明（例如后续段追加成员），各段共享同一个类型符号。
        // 类型级约束（必须 record / 不能 abstract / 必须 partial / 不能泛型 / 必须在命名空间内 /
        // 包含类型必须能被生成代码重开）
        // 描述的是整个类型，因此跨全部声明段判定且最多报告一次；而主构造函数及其参数只存在于声明主
        // 构造函数的那一段，故参数级约束只在该段上判定。
        // 若按段逐条校验，只追加成员的后续段会因缺少 ParameterList 被误判为 STIA0005（类型必须有且
        // 仅有一个单参数主构造函数），把合法的分段声明标记为错误。
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

        // 嵌套类型是被支持的：partial 的各段必须位于同一容器内，因此判定首个声明段即可。
        // 生成代码要补的是 Id 自身的成员，只能把声明逐层嵌回原本的包含类型里，因此链上每一层都必须能被
        // 原样重开 —— 要求 partial、不能是 file 本地类型。这里逐层向上判定，报错落在
        // 最内层那个不合规的容器上，即使用者真正要改的那一处；若容器都不合规而整体又不在命名空间内，
        // 报在 Id 自己身上。泛型容器已放开（重开的 partial 只需复现类型参数表、约束可省略），不再拦截。
        // 定位取整个类型声明（与其它类型级规则一致），而不是只标标识符：指向子片段的位置会让
        // 代码修复测试框架判定为「非本地的分析器诊断」而拒绝走修复流程。
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

        // 主构造函数最多出现在一段声明中，以该段为准；整体缺失时定位到首个声明段。
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

        // 形状校验全部通过后，基元类型已确定且合法，此时再校验 Validator 指向的方法签名。
        // 放在最后而非与形状校验并行，是因为它需要「主构造函数参数类型」作为比对基准，
        // 而该类型只有在通过了上面的单参数 / 非可空 / 受支持基元等校验后才可靠。
        var validatorName = GetEffectiveValidatorName(type, context.Compilation.Assembly);

        if (validatorName is not null)
        {
            ValidateValidator(context, type, validatorName, constructorParameterType, records);
        }

        // 前缀校验与形状 / 验证器无关，独立于上面的早退分支，放在最后统一判定。
        ValidatePrefix(context, type, records);

        // 验证器可绕过同样独立于形状校验，但它需要「是否真的配了验证器」这一结论，故复用上面已算出的名字。
        ValidateValidatorBypass(context, type, validatorName, records);

        // 以下两条是「形状合法之后」的风格提示：放在全部硬性校验之后，避免一个连 record 都不是的类型
        // 同时冒出改名建议。命名约定与基元类型无关；重复基元则需要上面已确定的基元类型。
        ValidateNameConvention(context, type, records);
        ValidateDuplicatePrimitiveType(context, type, constructorParameterType, records);
    }

    /// <summary>
    /// 类型名不以 <c>Id</c> 结尾时给出改名建议（STIA0015）。
    /// </summary>
    /// <remarks>
    /// 定位取标识符本身而非整个声明：这是一条关于「这个名字」的建议，指向名字才说得通。
    /// 该规则没有代码修复，因此不受「诊断须落在整个声明上」那条代码修复测试约束的限制。
    /// </remarks>
    private static void ValidateNameConvention(SymbolAnalysisContext context, INamedTypeSymbol type, RecordDeclarationSyntax[] records)
    {
        if (type.Name.EndsWith("Id", StringComparison.Ordinal))
        {
            return;
        }

        // 以 "ID" 收尾时把这两个字符替换成 "Id"，而不是直接追加 —— 否则建议名会变成 "OrderIDId"。
        var suggestion = type.Name.EndsWith("ID", StringComparison.Ordinal)
            ? type.Name[..^2] + "Id"
            : type.Name + "Id";

        Report(context, Descriptors.TypeNameShouldEndWithId, records[0].Identifier.GetLocation(), type.Name, suggestion);
    }

    /// <summary>
    /// 同一命名空间下多个强类型 Id 共用同一基元类型时提示（STIA0016）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 每组只报一次，落在组内名字序最小的类型上 —— n 个同基元 Id 各报一次就是 n 条重复提示。
    /// 嵌套类型不在 <c>INamespaceSymbol.GetTypeMembers</c> 的返回范围内，因而天然排除：
    /// 它们的作用域由包含类型界定，不属于命名空间级的重复。
    /// </para>
    /// <para>
    /// 复杂度是「每个 Id 扫一遍所在命名空间的类型」，即 O(k²)。k 是同一命名空间里的类型总数，
    /// 现实中是几十的量级，相对一次编译的整体开销可以忽略；换成编译级聚合（
    /// <c>RegisterCompilationStartAction</c> + 结束时统一分组）能降到 O(k)，但会把现有的
    /// 「一个符号动作搞定全部校验」结构拆散，收益不抵复杂度。
    /// </para>
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

        // 分组键必须与兄弟类型的取值方式一致，否则当前类型可能落不进自己的组里（见 GetSingleParameterType）。
        var primitiveName = (GetSingleParameterType(type) ?? primitiveType).ToDisplayString();

        var group = namespaceSymbol.GetTypeMembers()
            .Where(HasStronglyTypedIdAttribute)
            .Select(member => (Member: member, Primitive: GetSingleParameterType(member)))
            .Where(pair => pair.Primitive?.ToDisplayString() == primitiveName)
            .Select(pair => pair.Member)
            .OrderBy(member => member.Name, StringComparer.Ordinal)
            .ToArray();

        // 组内第一个成员负责上报，且必须确认「第一个」就是当前类型，否则同一组会被每个成员各报一次。
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
    /// 必须排除「参数类型等于自身」的那个构造函数：<c>record class</c> 会合成拷贝构造函数
    /// <c>X(X original)</c>，它同样是单参数的，不排除就会把基元类型误判成 Id 自身。
    /// 形状不合法的类型（无单参数构造函数）返回 <see langword="null"/>，由调用方跳过。
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
    /// <para>
    /// record 的主构造函数<b>不能</b>单独指定可见性（<c>public record R private (int X)</c> 是 CS1514），
    /// 它始终跟随类型本身。因此只要类型对外可见，<c>new R(default)</c> 就对外可用，<c>Validator</c> 形同虚设。
    /// </para>
    /// <para>
    /// 该规则默认关闭（见 <see cref="Descriptors.ValidatorCanBeBypassed"/>），此处无条件上报即可 ——
    /// 是否呈现由 <c>IsEnabledByDefault</c> / <c>.editorconfig</c> 决定，分析器不必自行判断开关。
    /// 定位取首个声明段（与其它类型级规则一致），便于代码修复框架接管。
    /// </para>
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
    /// <remarks>
    /// 嵌套在 <c>internal</c> 容器里的 <c>public</c> 类型实际不可跨程序集访问，不应触发 STIA0014，
    /// 故逐层向上判定，任一层不是 <c>public</c> 即视为不可见。
    /// </remarks>
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
    /// 生成器侧把空 / 纯空白前缀静默按「未设置」处理（见 <c>StronglyTypedIdInfo.NormalizePrefix</c>），
    /// 这里提前告警，避免使用者误以为前缀已生效。取值走<b>符号级</b>的命名实参
    /// （<see cref="AttributeData.NamedArguments"/>），因此 <c>Prefix = "ord_"</c> 与 <c>Prefix = SomeConst</c>
    /// 两种写法都能判定，且不触发 RS1030（诊断分析器不得调用 <c>Compilation.GetSemanticModel</c>）。
    /// 定位仍在 attribute 的 <c>Prefix</c> 实参上（找不到时退回首个声明段）。
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
    /// 校验项：方法存在、是静态成员、返回 <see cref="bool"/>、恰好一个参数且参数类型等于基元 Id 类型。
    /// 可见性不约束为 public —— 生成代码与 Id 类型处于同一 partial 内，private 也可达；约束可见性反而会
    /// 把本可工作的写法误判为非法。任一条件不满足即报 <see cref="Descriptors.ValidatorReferenceInvalid"/>，
    /// 定位在 attribute 的 <c>Validator</c> 实参上（找不到时退回首个声明段）。
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
    /// <remarks>
    /// 生成代码里的 <c>new Xxx(...)</c>（位于 <c>Create</c> / <c>TryParse</c> 内）由
    /// <see cref="AnalysisContext.ConfigureGeneratedCodeAnalysis"/> 对生成代码的豁免而自动忽略，不会误报。
    /// </remarks>
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
