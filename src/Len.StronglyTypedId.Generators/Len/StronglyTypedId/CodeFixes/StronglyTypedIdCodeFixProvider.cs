using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace Len.StronglyTypedId.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp)]
internal class StronglyTypedIdCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        ImmutableArray.Create(
            Descriptors.TypeMustBePartialId,
            Descriptors.TypeCannotBeAbstractId,
            Descriptors.ParameterNameMustBeValueId,
            Descriptors.ParameterCannotBeNullableId,
            Descriptors.TypeCannotBeGenericId);

    public override FixAllProvider? GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        foreach (var diagnostic in context.Diagnostics)
        {
            if (!FixableDiagnosticIds.Contains(diagnostic.Id))
                continue;

            var title = Descriptors.All.Single(w => w.Id == diagnostic.Id).Title.ToString();
            var action = CodeAction.Create(title,
                             token => diagnostic.Id switch
                             {
                                 Descriptors.TypeMustBePartialId => AddPartialKeywordAsync(context, diagnostic, token),
                                 Descriptors.TypeCannotBeAbstractId => RemoveAbstractKeywordAsync(context, diagnostic, token),
                                 Descriptors.ParameterNameMustBeValueId => UpdateParameterNameToValueAsync(context, diagnostic, token),
                                 Descriptors.ParameterCannotBeNullableId => RemoveNullableAsync(context, diagnostic, token),
                                 Descriptors.TypeCannotBeGenericId => RemoveGenericAsync(context, diagnostic, token),
                                 _ => Task.FromResult(context.Document),
                             },
                             title);

            context.RegisterCodeFix(action, diagnostic);
        }

        return Task.CompletedTask;
    }

    private static async Task<Document> AddPartialKeywordAsync(CodeFixContext context, Diagnostic diagnostic, CancellationToken token)
    {
        var root = await context.Document.GetSyntaxRootAsync(token);

        if (root is null)
            return context.Document;

        var declaration = root.FindToken(diagnostic.Location.SourceSpan.Start)
          .Parent?.AncestorsAndSelf()
          .OfType<RecordDeclarationSyntax>()
          .First()!;

        return context.Document.WithSyntaxRoot(
            root.ReplaceNode(declaration, declaration.AddModifiers(
                SyntaxFactory.Token(SyntaxKind.PartialKeyword))));
    }

    private static async Task<Document> RemoveAbstractKeywordAsync(CodeFixContext context, Diagnostic diagnostic, CancellationToken token)
    {
        var root = await context.Document.GetSyntaxRootAsync(token);

        if (root is null)
            return context.Document;

        var declaration = root.FindToken(diagnostic.Location.SourceSpan.Start)
         .Parent?.AncestorsAndSelf()
         .OfType<RecordDeclarationSyntax>()
         .First()!;

        return context.Document.WithSyntaxRoot(
           root.ReplaceNode(declaration, declaration.RemoveModifiers(
                SyntaxKind.AbstractKeyword)));
    }

    private async Task<Document> RemoveGenericAsync(CodeFixContext context, Diagnostic diagnostic, CancellationToken token)
    {
        var root = await context.Document.GetSyntaxRootAsync(token);

        if (root is null)
            return context.Document;

        var declaration = root.FindToken(diagnostic.Location.SourceSpan.Start)
         .Parent?.AncestorsAndSelf()
         .OfType<RecordDeclarationSyntax>()
         .First()!;

        return context.Document.WithSyntaxRoot(
           root.ReplaceNode(declaration, declaration.WithTypeParameterList(null)));
    }

    private async Task<Document> RemoveNullableAsync(CodeFixContext context, Diagnostic diagnostic, CancellationToken token)
    {
        var root = await context.Document.GetSyntaxRootAsync(token);

        if (root is null)
            return context.Document;

        var parameterSyntax = root.FindToken(diagnostic.Location.SourceSpan.Start)
            .Parent?.AncestorsAndSelf()
            .OfType<ParameterSyntax>()
            .First()!;

        var newParameterSyntax = SyntaxFactory.Parameter(parameterSyntax.Identifier)
            .WithType(SyntaxFactory.ParseTypeName(parameterSyntax.Type!.ToString().TrimEnd('?')))
            .WithIdentifier(SyntaxFactory.Identifier("Value"));

        return context.Document
            .WithSyntaxRoot(root.ReplaceNode(parameterSyntax, newParameterSyntax));
    }

    private async Task<Document> UpdateParameterNameToValueAsync(CodeFixContext context, Diagnostic diagnostic, CancellationToken token)
    {
        var root = await context.Document.GetSyntaxRootAsync(token);

        if (root is null)
            return context.Document;

        var parameterSyntax = root.FindToken(diagnostic.Location.SourceSpan.Start)
            .Parent?.AncestorsAndSelf()
            .OfType<ParameterSyntax>()
            .First()!;

        var newParameterSyntax = SyntaxFactory.Parameter(parameterSyntax.Identifier)
            .WithType(SyntaxFactory.ParseTypeName(parameterSyntax.Type!.ToString()))
            .WithIdentifier(SyntaxFactory.Identifier("Value"));

        return context.Document
            .WithSyntaxRoot(root.ReplaceNode(parameterSyntax, newParameterSyntax));
    }
}

internal static class RecordDeclarationSyntaxExtensions
{
    public static RecordDeclarationSyntax RemoveModifiers(this RecordDeclarationSyntax syntax, params SyntaxKind[] items)
    {
        return syntax.WithModifiers(new SyntaxTokenList(syntax.Modifiers.Where(w => !items.Select(s => (int)s).Contains(w.RawKind))));
    }
}