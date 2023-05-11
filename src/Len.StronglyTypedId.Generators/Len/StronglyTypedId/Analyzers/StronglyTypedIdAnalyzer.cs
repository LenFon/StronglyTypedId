using Microsoft.CodeAnalysis.Diagnostics;

namespace Len.StronglyTypedId.Analyzers;

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

    private void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        if (!context.Symbol.GetAttributes()
            .Any(w => w.AttributeClass?.ToDisplayString() == "Len.StronglyTypedId.StronglyTypedIdAttribute"))
        {
            return;
        }

        var type = (INamedTypeSymbol)context.Symbol;
        var supportedTypeNames = new string[]
        {
            nameof(Guid),
            nameof(String),
            nameof(Byte),
            nameof(SByte),
            nameof(Int16),
            nameof(Int32),
            nameof(Int64),
            nameof(UInt16),
            nameof(UInt32),
            nameof(UInt64)
        };

        foreach (var declaringSyntaxReference in type.DeclaringSyntaxReferences)
        {
            var syntax = declaringSyntaxReference.GetSyntax(context.CancellationToken);

            if (syntax is not RecordDeclarationSyntax declaration)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Descriptors.TypeMustBeRecord,
                    syntax.GetLocation(),
                    type.Name));
                continue;
            }

            if (declaration.Modifiers.Any(SyntaxKind.AbstractKeyword))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Descriptors.TypeCannotBeAbstract,
                    declaration.GetLocation(),
                    type.Name));
                continue;
            }

            if (!declaration.Modifiers.Any(SyntaxKind.PartialKeyword))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Descriptors.TypeMustBePartial,
                    declaration.GetLocation(),
                    type.Name));
                continue;
            }

            if (declaration.TypeParameterList is not null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                   Descriptors.TypeCannotBeGeneric,
                   declaration.GetLocation(),
                   type.Name));
                continue;
            }

            if (declaration.Parent is not BaseNamespaceDeclarationSyntax)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                  Descriptors.TypeCannotBeNestedAndMustHaveNamespace,
                  declaration.GetLocation(),
                  type.Name));
                continue;
            }

            if (declaration.ParameterList is not { Parameters: [var parameter] })
            {
                context.ReportDiagnostic(Diagnostic.Create(
                  Descriptors.TypeMustHavePrimaryConstructorWithExactlyHaveOneParameter,
                  declaration.GetLocation(),
                  type.Name));
                continue;
            }

            if (parameter.Type is NullableTypeSyntax)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                  Descriptors.ParameterCannotBeNullable,
                  parameter.Type.GetLocation(),
                  parameter.Type));
                continue;
            }

            if (parameter.Identifier.ValueText != "Value")
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Descriptors.ParameterNameMustBeValue,
                    parameter.Identifier.GetLocation(),
                    parameter.Identifier.ValueText));
                continue;
            }

            var ctorArgType = type.Constructors.First().Parameters.First().Type;
            if (!supportedTypeNames.Contains(ctorArgType.Name))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Descriptors.ParameterTypeIsInvalid,
                    parameter.Type!.GetLocation(),
                    parameter.Type));

                continue;
            }
        }
    }
}