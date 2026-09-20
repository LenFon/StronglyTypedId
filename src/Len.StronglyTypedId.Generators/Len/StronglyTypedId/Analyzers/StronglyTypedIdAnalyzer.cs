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
        // 若按段逐条校验，只追加成员的后续段会因缺少 ParameterList 被误判为 STIAO005（类型必须有且
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
        // 原样重开 —— 要求 partial、不能是泛型、不能是 file 本地类型。这里逐层向上判定，报错落在
        // 最内层那个不合规的容器上，即使用者真正要改的那一处；若容器都不合规而整体又不在命名空间内，
        // 报在 Id 自己身上。泛型容器复用「不能是泛型」这条既有规则，{0} 取容器名，措辞依旧自洽。
        // 定位取整个类型声明（与其它类型级规则一致），而不是只标标识符：指向子片段的位置会让
        // 代码修复测试框架判定为「非本地的分析器诊断」而拒绝走修复流程。
        var container = records[0].Parent;

        while (container is TypeDeclarationSyntax declaration)
        {
            if (declaration.TypeParameterList is not null)
            {
                Report(context, Descriptors.TypeCannotBeGeneric, declaration.GetLocation(), declaration.Identifier.ValueText);
                return;
            }

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
        }
    }

    private static void Report(SymbolAnalysisContext context, DiagnosticDescriptor descriptor, Location location, object argument)
        => context.ReportDiagnostic(Diagnostic.Create(descriptor, location, argument));
}
