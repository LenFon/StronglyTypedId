using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace Len.StronglyTypedId.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp)]
internal class StronglyTypedIdCodeFixProvider : CodeFixProvider
{
    private const string ValueParameterName = "Value";

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
            // 只有可修复的诊断才在 Descriptors.All 中登记，找不到即代表无需提供修复。
            var descriptor = Descriptors.All.FirstOrDefault(item => item.Id == diagnostic.Id);

            if (descriptor is null)
            {
                continue;
            }

            var title = descriptor.Title.ToString();
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

    private static Task<Document> AddPartialKeywordAsync(CodeFixContext context, Diagnostic diagnostic, CancellationToken token)
        => ReplaceRecordDeclarationAsync(context, diagnostic, token,
            static declaration => declaration.AddModifiers(SyntaxFactory.Token(SyntaxKind.PartialKeyword)));

    private static Task<Document> RemoveAbstractKeywordAsync(CodeFixContext context, Diagnostic diagnostic, CancellationToken token)
        => ReplaceRecordDeclarationAsync(context, diagnostic, token,
            static declaration => declaration.WithoutModifiers(SyntaxKind.AbstractKeyword));

    private static Task<Document> RemoveGenericAsync(CodeFixContext context, Diagnostic diagnostic, CancellationToken token)
        => ReplaceRecordDeclarationAsync(context, diagnostic, token,
            static declaration => declaration.WithTypeParameterList(null));

    private static Task<Document> UpdateParameterNameToValueAsync(CodeFixContext context, Diagnostic diagnostic, CancellationToken token)
        => ReplaceParameterAsync(context, diagnostic, token, stripNullableSuffix: false);

    private static Task<Document> RemoveNullableAsync(CodeFixContext context, Diagnostic diagnostic, CancellationToken token)
        => ReplaceParameterAsync(context, diagnostic, token, stripNullableSuffix: true);

    /// <summary>
    /// 用 <paramref name="replace"/> 的结果替换诊断所在的 record 声明。
    /// </summary>
    private static async Task<Document> ReplaceRecordDeclarationAsync(
        CodeFixContext context,
        Diagnostic diagnostic,
        CancellationToken token,
        Func<RecordDeclarationSyntax, RecordDeclarationSyntax> replace)
    {
        var root = await context.Document.GetSyntaxRootAsync(token);

        if (root is null || FindNode<RecordDeclarationSyntax>(root, diagnostic) is not { } declaration)
        {
            return context.Document;
        }

        return context.Document.WithSyntaxRoot(root.ReplaceNode(declaration, replace(declaration)));
    }

    /// <summary>
    /// 把诊断所在的构造参数改名为 <c>Value</c>，并按需去掉可空后缀。
    /// </summary>
    private static async Task<Document> ReplaceParameterAsync(
        CodeFixContext context,
        Diagnostic diagnostic,
        CancellationToken token,
        bool stripNullableSuffix)
    {
        var root = await context.Document.GetSyntaxRootAsync(token);

        if (root is null || FindNode<ParameterSyntax>(root, diagnostic) is not { } parameter)
        {
            return context.Document;
        }

        var typeText = parameter.Type!.ToString();
        var newParameter = SyntaxFactory.Parameter(SyntaxFactory.Identifier(ValueParameterName))
            .WithType(SyntaxFactory.ParseTypeName(stripNullableSuffix ? typeText.TrimEnd('?') : typeText));

        return context.Document.WithSyntaxRoot(root.ReplaceNode(parameter, newParameter));
    }

    /// <summary>
    /// 沿诊断所在位置向上查找最近的指定类型语法节点。
    /// </summary>
    private static TNode? FindNode<TNode>(SyntaxNode root, Diagnostic diagnostic)
        where TNode : SyntaxNode
        => root.FindToken(diagnostic.Location.SourceSpan.Start)
            .Parent?.AncestorsAndSelf()
            .OfType<TNode>()
            .FirstOrDefault();
}

internal static class RecordDeclarationSyntaxExtensions
{
    /// <summary>
    /// 返回移除了指定修饰符的 record 声明副本（沿用 Roslyn 的 <c>Without…</c> 命名惯例）。
    /// </summary>
    public static RecordDeclarationSyntax WithoutModifiers(this RecordDeclarationSyntax declaration, params SyntaxKind[] syntaxKinds)
    {
        return declaration.WithModifiers([.. declaration.Modifiers.Where(modifier => !syntaxKinds.Contains(modifier.Kind()))]);
    }
}
