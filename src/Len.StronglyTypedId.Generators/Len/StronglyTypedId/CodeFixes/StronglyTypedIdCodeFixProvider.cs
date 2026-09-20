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
        => ReplaceRecordDeclarationAsync(context, diagnostic, token, RemoveTypeParametersAndConstraints);

    /// <summary>
    /// 移除 record 的类型参数表，并把紧随其后的约束子句一并移除。
    /// </summary>
    /// <remarks>
    /// 只删类型参数表会留下 <c>where T : class</c>，而约束不允许出现在非泛型声明上（CS0080），
    /// 修复产物依旧是非法代码。此外，分隔参数表与约束子句的空白是参数表末位的 trailing trivia，
    /// 删掉约束子句后它会变成 <c>OrderId(Guid Value) ;</c>（多行写法下则是悬空换行），故一并清掉；
    /// 没有约束子句时说明该处的 trivia 另有归属，保持原样。
    /// </remarks>
    private static RecordDeclarationSyntax RemoveTypeParametersAndConstraints(RecordDeclarationSyntax declaration)
    {
        if (declaration.ConstraintClauses.Count == 0)
        {
            return declaration.WithTypeParameterList(null);
        }

        return declaration
            .WithTypeParameterList(null)
            .WithConstraintClauses([])
            .WithParameterList(declaration.ParameterList?.WithoutTrailingTrivia());
    }

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

        // 就地改写既有节点，而不是用 SyntaxFactory 重建：重建出的参数只带「类型 + 名字」，
        // 参数上的 attribute、修饰符、默认值以及全部 trivia（注释、空白）都会被丢掉。
        var newParameter = parameter.WithIdentifier(
            SyntaxFactory.Identifier(parameter.Identifier.LeadingTrivia, ValueParameterName, parameter.Identifier.TrailingTrivia));

        if (stripNullableSuffix && parameter.Type is NullableTypeSyntax nullableType)
        {
            // 用 ElementType 顶掉 `?`，并把整个可空类型节点的前后 trivia 交给它，否则
            // `Guid? Value` 中附着在 `?` 上的空白（或注释）会丢失，拼成 `GuidValue`。
            newParameter = newParameter.WithType(nullableType.ElementType.WithTriviaFrom(nullableType));
        }

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
