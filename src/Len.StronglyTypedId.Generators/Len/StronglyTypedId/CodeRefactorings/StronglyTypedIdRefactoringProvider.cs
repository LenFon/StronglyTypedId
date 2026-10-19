using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Text;

namespace Len.StronglyTypedId.CodeRefactorings;

/// <summary>
/// 把已有 <c>record</c> 一键升级为强类型 Id 的代码重构，以及反向回退的「Inline」重构。
/// </summary>
/// <remarks>
/// 与代码修复的分工：修复针对「已标特性但写错」的诊断，本重构针对「形状已合适、尚未标特性」的普通 record，
/// 一次补齐特性、<c>partial</c> 与 using。只做这三件事 —— 其余形状问题各有专门诊断；参数名不是 <c>Value</c>
/// 时也不提供（重命名会连带改掉生成的属性名）。
/// </remarks>
[ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(StronglyTypedIdRefactoringProvider))]
internal class StronglyTypedIdRefactoringProvider : CodeRefactoringProvider
{
    private const string AttributeName = "StronglyTypedId";
    private const string AttributeFullName = "Len.StronglyTypedId.StronglyTypedIdAttribute";
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
    /// <remarks>只认声明头上的光标（标识符、修饰符、参数表、基列表都在 <c>{</c> / <c>;</c> 之前），否则停在成员体里也会弹重构。</remarks>
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
    /// 判断特性名是否指向 <c>Len.StronglyTypedId.StronglyTypedIdAttribute</c>。
    /// </summary>
    /// <remarks>覆盖 <c>[StronglyTypedId]</c>、<c>[StronglyTypedIdAttribute]</c>、全限定名，以及任意以 <c>.StronglyTypedIdAttribute</c> 结尾的写法。</remarks>
    private static bool IsStronglyTypedIdAttributeName(NameSyntax name)
    {
        var text = name.ToString();

        return text is AttributeName or "StronglyTypedIdAttribute" or AttributeFullName
            || text.EndsWith(".StronglyTypedIdAttribute", StringComparison.Ordinal);
    }

    /// <summary>
    /// 判断该 record 是否「加上特性、partial 与 using 后就是一个合法的强类型 Id」。
    /// </summary>
    /// <remarks>只校验重构不去修的那几项：单参数、参数名 <c>Value</c>、非抽象、非泛型、基元受支持。</remarks>
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
    /// <remarks>换行取被编辑文档自身的行尾（见 <see cref="DocumentFormatting"/>），写死 CRLF 会在 LF 文件里留下混排。</remarks>
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
    /// <remarks>
    /// 仅在 Id 只有一个声明段时注册：多段 partial 里摘掉任一段的 <c>partial</c> 都会 CS0260。摘掉特性后
    /// <c>Create</c> / <c>Parse</c> 的调用点会失去目标，故标题用「Inline」而非「Remove」。
    /// </remarks>
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
    /// <remarks>格式化同样按文档自身的行尾进行（见 <see cref="DocumentFormatting"/>）。</remarks>
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

        var updated = record
            .WithAttributeLists(RemoveStronglyTypedIdAttributes(record.AttributeLists))
            .WithModifiers(SyntaxFactory.TokenList(
                record.Modifiers.Where(modifier => !modifier.IsKind(SyntaxKind.PartialKeyword))));

        var newRoot = RemoveUsingIfUnused(root.ReplaceNode(record, updated));

        return document.WithSyntaxRoot(DocumentFormatting.Format(document, newRoot, cancellationToken));
    }

    /// <summary>
    /// 从特性列表中摘掉全部 <c>[StronglyTypedId]</c>；某条特性列表被摘空时连同方括号一起移除。
    /// </summary>
    /// <remarks>一条列表里可并列多个特性（<c>[StronglyTypedId, Obsolete]</c>），故按特性粒度摘，摘空时连方括号一起移除。</remarks>
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
    /// <remarks>
    /// 插入点：已有 using 时插在最后一个之后，否则插在首个成员之前；插入点原有的 trivia 挪到新指令上，
    /// 再把插入点规整为一个换行，避免多出空行或 using 紧贴 namespace。
    /// </remarks>
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
    /// 在文件里再无强类型 Id 时摘掉 <c>using Len.StronglyTypedId;</c>。
    /// </summary>
    private static SyntaxNode RemoveUsingIfUnused(SyntaxNode root)
    {
        if (root is not CompilationUnitSyntax compilationUnit
            || compilationUnit.DescendantNodes().OfType<RecordDeclarationSyntax>().Any(HasStronglyTypedIdAttribute))
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
