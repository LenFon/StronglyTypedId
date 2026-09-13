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

        foreach (var declaringSyntaxReference in type.DeclaringSyntaxReferences)
        {
            var syntax = declaringSyntaxReference.GetSyntax(context.CancellationToken);

            if (syntax is not RecordDeclarationSyntax declaration)
            {
                Report(context, Descriptors.TypeMustBeRecord, syntax.GetLocation(), type.Name);
                continue;
            }

            if (declaration.Modifiers.Any(SyntaxKind.AbstractKeyword))
            {
                Report(context, Descriptors.TypeCannotBeAbstract, declaration.GetLocation(), type.Name);
                continue;
            }

            if (!declaration.Modifiers.Any(SyntaxKind.PartialKeyword))
            {
                Report(context, Descriptors.TypeMustBePartial, declaration.GetLocation(), type.Name);
                continue;
            }

            if (declaration.TypeParameterList is not null)
            {
                Report(context, Descriptors.TypeCannotBeGeneric, declaration.GetLocation(), type.Name);
                continue;
            }

            if (declaration.Parent is not BaseNamespaceDeclarationSyntax)
            {
                Report(context, Descriptors.TypeCannotBeNestedAndMustHaveNamespace, declaration.GetLocation(), type.Name);
                continue;
            }

            if (declaration.ParameterList is not { Parameters: [var parameter] })
            {
                Report(context, Descriptors.TypeMustHaveSingleParameterPrimaryConstructor, declaration.GetLocation(), type.Name);
                continue;
            }

            if (parameter.Type is NullableTypeSyntax)
            {
                Report(context, Descriptors.ParameterCannotBeNullable, parameter.Type.GetLocation(), parameter.Type);
                continue;
            }

            if (parameter.Identifier.ValueText != "Value")
            {
                Report(context, Descriptors.ParameterNameMustBeValue, parameter.Identifier.GetLocation(), parameter.Identifier.ValueText);
                continue;
            }

            var constructorParameterType = type.Constructors[0].Parameters[0].Type;

            if (!SupportedPrimitiveTypes.IsSupported(constructorParameterType.Name))
            {
                Report(context, Descriptors.ParameterTypeIsInvalid, parameter.Type!.GetLocation(), parameter.Type);
            }
        }
    }

    private static void Report(SymbolAnalysisContext context, DiagnosticDescriptor descriptor, Location location, object argument)
        => context.ReportDiagnostic(Diagnostic.Create(descriptor, location, argument));
}
