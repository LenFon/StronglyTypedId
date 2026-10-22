using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Text;

namespace Len.StronglyTypedId.CodeRefactorings;

/// <summary>
/// 把已有 <c>record</c> 一键升级为强类型 Id 的代码重构，以及反向回退的「Inline」重构。
/// </summary>
[ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(StronglyTypedIdRefactoringProvider))]
internal class StronglyTypedIdRefactoringProvider : CodeRefactoringProvider
{
    private const string AttributeName = "StronglyTypedId";
    private const string NamespaceName = "Len.StronglyTypedId";
    private const string ValueParameterName = "Value";

    public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        if (root is null || FindRecordDeclaration(root, context.Span) is not { } record)
        {
            return;
        }

        if (HasStronglyTypedIdAttribute(record))
        {
            await RegisterInlineRefactoringAsync(context, record).ConfigureAwait(false);
            return;
        }

        if (await IsConvertibleAsync(context, record).ConfigureAwait(false))
        {
            context.RegisterRefactoring(CodeAction.Create(
                "Convert to strongly typed id",
                cancellationToken => ConvertToStronglyTypedIdAsync(context.Document, record, cancellationToken),
                equivalenceKey: "ConvertToStronglyTypedId"));
        }
    }

    /// <summary>
    /// 取光标所在的 record 声明；光标落在声明体内部（成员之间）时不提供重构。
    /// </summary>
    private static RecordDeclarationSyntax? FindRecordDeclaration(SyntaxNode root, TextSpan span)
    {
        var record = root.FindToken(span.Start)
            .Parent?
            .AncestorsAndSelf()
            .OfType<RecordDeclarationSyntax>()
            .FirstOrDefault();

        if (record is null)
        {
            return null;
        }

        var headerEnd = record.OpenBraceToken.IsKind(SyntaxKind.None)
            ? (record.SemicolonToken.IsKind(SyntaxKind.None) ? record.Span.End : record.SemicolonToken.SpanStart)
            : record.OpenBraceToken.SpanStart;

        return span.Start < headerEnd ? record : null;
    }

    /// <summary>
    /// 判断 record 是否已经是强类型 Id（已标 <c>[StronglyTypedId]</c>）。
    /// </summary>
    private static bool HasStronglyTypedIdAttribute(RecordDeclarationSyntax record)
        => record.AttributeLists
            .SelectMany(list => list.Attributes)
            .Any(attribute => IsStronglyTypedIdAttributeName(attribute.Name));

    /// <summary>
    /// 判断特性名是否**精确**指向 <c>Len.StronglyTypedId.StronglyTypedIdAttribute</c>。
    /// </summary>
    /// <remarks>
    /// 只认这几种确定写法，不做任意后缀匹配 —— 否则 Inline 重构会把第三方的同名特性物理删掉，且没有任何提示。
    /// 刻意不查语义模型：特性名被同名命名空间遮蔽时 <c>GetSymbolInfo</c> 返回 <see langword="null"/>，判定会整体失效。
    /// </remarks>
    private static bool IsStronglyTypedIdAttributeName(NameSyntax name)
        => name.ToString() is AttributeName
            or "StronglyTypedIdAttribute"
            or "Len.StronglyTypedId.StronglyTypedId"
            or "Len.StronglyTypedId.StronglyTypedIdAttribute"
            or "global::Len.StronglyTypedId.StronglyTypedId"
            or "global::Len.StronglyTypedId.StronglyTypedIdAttribute";

    /// <summary>
    /// 判断该 record 是否「加上特性、partial 与 using 后就是一个合法的强类型 Id」。
    /// </summary>
    private static async Task<bool> IsConvertibleAsync(CodeRefactoringContext context, RecordDeclarationSyntax record)
    {
        if (record.ParameterList is not { Parameters: [var parameter] }
            || parameter.Identifier.ValueText != ValueParameterName)
        {
            return false;
        }

        if (record.Modifiers.Any(SyntaxKind.AbstractKeyword) || record.TypeParameterList is not null)
        {
            return false;
        }

        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

        if (semanticModel is null || parameter.Type is null)
        {
            return false;
        }

        var parameterType = semanticModel.GetTypeInfo(parameter.Type, context.CancellationToken).Type;

        return parameterType is not null && SupportedPrimitiveTypes.IsSupported(parameterType);
    }

    /// <summary>
    /// 正向重构：补 <c>[StronglyTypedId]</c>、<c>partial</c> 与 <c>using Len.StronglyTypedId;</c>。
    /// </summary>
    private static async Task<Document> ConvertToStronglyTypedIdAsync(
        Document document,
        RecordDeclarationSyntax record,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

        if (root is null)
        {
            return document;
        }

        var endOfLine = DocumentFormatting.GetEndOfLine(root);

        // 新特性列表接管 record 原本的前导 trivia（通常是它上方那一行换行 / 空行），record 自身只留一个换行 ——
        // 否则新特性会挤在上一行末尾，或与声明之间多出一个空行。
        var attributeList = SyntaxFactory.AttributeList(
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.Attribute(SyntaxFactory.IdentifierName(AttributeName))))
            .WithLeadingTrivia(record.GetLeadingTrivia());

        var updated = record
            .WithLeadingTrivia(SyntaxFactory.EndOfLine(endOfLine))
            .WithAttributeLists(record.AttributeLists.Insert(0, attributeList))
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PartialKeyword));

        var newRoot = AddUsingIfMissing(root.ReplaceNode(record, updated), endOfLine);

        return document.WithSyntaxRoot(DocumentFormatting.Format(document, newRoot, cancellationToken));
    }

    /// <summary>
    /// 反向重构：摘掉 <c>[StronglyTypedId]</c> 与 <c>partial</c>，必要时一并摘掉 using。
    /// </summary>
    private static async Task RegisterInlineRefactoringAsync(CodeRefactoringContext context, RecordDeclarationSyntax record)
    {
        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

        if (semanticModel?.GetDeclaredSymbol(record, context.CancellationToken) is not INamedTypeSymbol symbol
            || symbol.DeclaringSyntaxReferences.Length != 1)
        {
            return;
        }

        context.RegisterRefactoring(CodeAction.Create(
            "Inline strongly typed id",
            cancellationToken => InlineToPrimitiveAsync(context.Document, record, cancellationToken),
            equivalenceKey: "InlineStronglyTypedId"));
    }

    /// <summary>
    /// 摘掉 <c>[StronglyTypedId]</c>、<c>partial</c>，并在文件里再无强类型 Id 时摘掉 using。
    /// </summary>
    private static async Task<Document> InlineToPrimitiveAsync(
        Document document,
        RecordDeclarationSyntax record,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

        if (root is null)
        {
            return document;
        }

        // 「还有没有别的 Id」必须在替换**之前**判断：替换后的树里当前 record 已经没有特性了。
        var hasOtherIds = root.DescendantNodes()
            .OfType<RecordDeclarationSyntax>()
            .Any(other => !ReferenceEquals(other, record) && HasStronglyTypedIdAttribute(other));

        var updated = record
            .WithAttributeLists(RemoveStronglyTypedIdAttributes(record.AttributeLists))
            .WithModifiers(SyntaxFactory.TokenList(
                record.Modifiers.Where(modifier => !modifier.IsKind(SyntaxKind.PartialKeyword))));

        var newRoot = root.ReplaceNode(record, updated);

        if (!hasOtherIds
            && !await UsesNamespaceAsync(document, root, record, cancellationToken).ConfigureAwait(false))
        {
            newRoot = RemoveUsingDirective(newRoot);
        }

        return document.WithSyntaxRoot(DocumentFormatting.Format(document, newRoot, cancellationToken));
    }

    /// <summary>
    /// 从特性列表中摘掉全部 <c>[StronglyTypedId]</c>；某条特性列表被摘空时连同方括号一起移除。
    /// </summary>
    private static SyntaxList<AttributeListSyntax> RemoveStronglyTypedIdAttributes(SyntaxList<AttributeListSyntax> attributeLists)
    {
        var result = new List<AttributeListSyntax>();

        foreach (var list in attributeLists)
        {
            var kept = list.Attributes
                .Where(attribute => !IsStronglyTypedIdAttributeName(attribute.Name))
                .ToArray();

            if (kept.Length == 0)
            {
                continue;
            }

            result.Add(kept.Length == list.Attributes.Count
                ? list
                : list.WithAttributes(SyntaxFactory.SeparatedList(kept)));
        }

        return SyntaxFactory.List(result);
    }

    /// <summary>
    /// 在编译单元顶部补 <c>using Len.StronglyTypedId;</c>；已有该 using 时原样返回。
    /// </summary>
    private static SyntaxNode AddUsingIfMissing(SyntaxNode root, string endOfLine)
    {
        if (root is not CompilationUnitSyntax compilationUnit || HasUsingDirective(compilationUnit))
        {
            return root;
        }

        var directive = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(NamespaceName));

        if (compilationUnit.Usings.LastOrDefault() is { } lastUsing)
        {
            var gap = lastUsing.GetTrailingTrivia();

            return compilationUnit.WithUsings(
                compilationUnit.Usings
                    .Replace(lastUsing, lastUsing.WithTrailingTrivia(SyntaxFactory.EndOfLine(endOfLine)))
                    .Add(directive.WithTrailingTrivia(gap.Count > 0 ? gap : SyntaxFactory.TriviaList(SyntaxFactory.EndOfLine(endOfLine)))));
        }

        if (compilationUnit.Members.FirstOrDefault() is not { } firstMember)
        {
            return compilationUnit.WithUsings(
                SyntaxFactory.SingletonList(directive.WithTrailingTrivia(SyntaxFactory.EndOfLine(endOfLine))));
        }

        var leading = firstMember.GetLeadingTrivia();

        // 首个成员前没有 trivia（文件直接从 namespace / 类型开头）时，新 using 后面要留一个空行，
        // 否则会出现「using 紧贴 namespace」；有 trivia（注释、#nullable 等）时把它挪到新指令上，
        // 首个成员本身只保留一个换行。
        var directiveTrailing = leading.Count > 0
            ? leading
            : SyntaxFactory.TriviaList(SyntaxFactory.EndOfLine(endOfLine), SyntaxFactory.EndOfLine(endOfLine));

        return compilationUnit
            .WithUsings(SyntaxFactory.SingletonList(directive.WithTrailingTrivia(directiveTrailing)))
            .ReplaceNode(firstMember, firstMember.WithLeadingTrivia(SyntaxFactory.EndOfLine(endOfLine)));
    }

    /// <summary>
    /// 判断文件里是否还有标识符绑定到 <c>Len.StronglyTypedId</c> 命名空间下的符号。
    /// </summary>
    /// <remarks>
    /// 该命名空间还导出 <c>IStronglyTypedId&lt;,&gt;</c>、<c>Any</c> 等，只按「还有没有带特性的 record」判断会误删仍在用的 using，产出 CS0246。
    /// 待内联的 record 自己的特性列表必须排除：它马上要被摘掉，而 <c>StronglyTypedIdAttribute</c> 本身就属于该命名空间。
    /// </remarks>
    private static async Task<bool> UsesNamespaceAsync(
        Document document,
        SyntaxNode root,
        RecordDeclarationSyntax record,
        CancellationToken cancellationToken)
    {
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

        if (semanticModel is null)
        {
            // 拿不到语义模型时保守处理：多留一行 using 无害，删错了是编译错误。
            return true;
        }

        // 按位置排除待内联 record 自己的特性列表：它马上就要被摘掉，而 StronglyTypedIdAttribute
        // 本身就属于该命名空间。用 Span 而不是节点引用，避免依赖「两次取到的语法树是同一个实例」。
        var attributeSpans = record.AttributeLists.Select(static attributeList => attributeList.Span).ToArray();

        foreach (var name in root.DescendantNodes().OfType<SimpleNameSyntax>())
        {
            if (attributeSpans.Any(span => span.Contains(name.Span)))
            {
                continue;
            }

            var info = semanticModel.GetSymbolInfo(name, cancellationToken);

            if (NeedsNamespace(info.Symbol)
                || info.CandidateSymbols.Any(static candidate => NeedsNamespace(candidate)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 判断某个标识符是否指向直接声明在 <c>Len.StronglyTypedId</c> 命名空间下的**类型**。
    /// </summary>
    /// <remarks>
    /// 只认类型、不认命名空间：<c>namespace Len.StronglyTypedId.Tests;</c> 的 <c>ContainingNamespace</c> 恰好是 <c>Len.StronglyTypedId</c>，按命名空间判会让 using 永远删不掉。
    /// 只比相等、不回溯祖先：声明在子命名空间里的类型本来就不需要这条 using。
    /// </remarks>
    private static bool NeedsNamespace(ISymbol? symbol)
        => symbol is INamedTypeSymbol { ContainingNamespace: { } containingNamespace }
            && containingNamespace.ToDisplayString() == NamespaceName;

    /// <summary>
    /// 摘掉 <c>using Len.StronglyTypedId;</c>（调用方负责先确认文件里再无强类型 Id）。
    /// </summary>
    private static SyntaxNode RemoveUsingDirective(SyntaxNode root)
    {
        if (root is not CompilationUnitSyntax compilationUnit)
        {
            return root;
        }

        var directive = compilationUnit.Usings.FirstOrDefault(usingDirective => usingDirective.Name?.ToString() == NamespaceName);

        return directive is null ? root : compilationUnit.WithUsings(compilationUnit.Usings.Remove(directive));
    }

    /// <summary>
    /// 判断编译单元里是否已有 <c>using Len.StronglyTypedId;</c>（含 <c>global using</c>）。
    /// </summary>
    private static bool HasUsingDirective(CompilationUnitSyntax compilationUnit)
        => compilationUnit.Usings.Any(directive => directive.Name?.ToString() == NamespaceName);
}
