using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Text;

namespace Len.StronglyTypedId.CodeRefactorings;

/// <summary>
/// 把已有 <c>record</c> 一键升级为强类型 Id 的代码重构，以及反向回退的「Inline」重构。
/// </summary>
/// <remarks>
/// <para>
/// 与 <see cref="CodeFixes.StronglyTypedIdCodeFixProvider"/> 的分工：代码修复针对「已经标了
/// <c>[StronglyTypedId]</c>、但写得不对」的诊断；本重构针对「还没标、形状却已经合适」的普通 record。
/// 使用者的典型路径是先写下 <c>public record OrderId(Guid Value);</c>，后来才知道有这个库，此时需要手动补
/// 特性、<c>partial</c> 与 using —— 本重构把这三步一次做完，把存量代码的迁移成本降到最低。
/// </para>
/// <para>
/// 只做这三件事，不越界：类型不在命名空间内、包含类型非 partial、主构造函数多参等形状问题各有专门的诊断与
/// 代码修复，重构不去替使用者猜意图。参数名不是 <c>Value</c> 时也不提供 —— 重命名位置参数会连带改掉生成的
/// 属性名（并可能打断 record 体内对它的引用），属于超出「转换」语义的破坏性变更。
/// </para>
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
    /// <remarks>
    /// 只认「声明头」上的光标：标识符、修饰符、参数表、基列表、约束子句都排在 <c>{</c> / <c>;</c> 之前。
    /// 若只看「光标是否落在整个 record 的范围内」，光标停在某个成员方法体里也会弹出重构，过于宽泛。
    /// </remarks>
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
    /// <remarks>
    /// 按写法逐一匹配：<c>[StronglyTypedId]</c>、<c>[StronglyTypedIdAttribute]</c>、全限定的
    /// <c>[Len.StronglyTypedId.StronglyTypedIdAttribute]</c>，以及任何以 <c>.StronglyTypedIdAttribute</c> 结尾的
    /// 别名限定写法。与 <c>StronglyTypedIdAnalyzer</c> 里定位 <c>Validator</c> 实参时的判据保持同一取向。
    /// </remarks>
    private static bool IsStronglyTypedIdAttributeName(NameSyntax name)
    {
        var text = name.ToString();

        return text is AttributeName or "StronglyTypedIdAttribute" or AttributeFullName
            || text.EndsWith(".StronglyTypedIdAttribute", StringComparison.Ordinal);
    }

    /// <summary>
    /// 判断该 record 是否「加上特性、partial 与 using 后就是一个合法的强类型 Id」。
    /// </summary>
    /// <remarks>
    /// 只校验重构不去修的那几项：单参数主构造函数、参数名 <c>Value</c>、非抽象、非泛型、基元类型受支持。
    /// 命名空间 / 包含类型 partial 等形状问题不在门禁内 —— 它们在存量代码里少见，且各有专门的诊断与代码修复。
    /// </remarks>
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
    /// <remarks>
    /// 插入的换行一律取自被编辑文档自身的行尾（见 <see cref="DocumentFormatting"/>）：
    /// 写死 CRLF 会在 LF 检出的文件里留下混排行尾。
    /// </remarks>
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
    /// 仅在「该 Id 只有一个声明段」时注册：多段 partial 声明里摘掉任一段的 <c>partial</c> 都会 CS0260。
    /// 摘掉特性后生成器不再为该类型产出成员，调用 <c>Create</c> / <c>Parse</c> 的代码会失去目标 ——
    /// 这是回退重构的固有代价，故标题用「Inline」而非「Remove」。
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
    /// <remarks>
    /// 与正向重构同理，格式化按被编辑文档自身的行尾进行（见 <see cref="DocumentFormatting"/>）。
    /// </remarks>
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
    /// <remarks>
    /// 一条特性列表里可以并列多个特性（<c>[StronglyTypedId, Obsolete]</c>），因此要按特性粒度摘，而不是整条列表。
    /// </remarks>
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
    /// 插入点有两处：文件里已有 using 时插在最后一个 using 之后，一个都没有时插在第一个成员之前。
    /// 两种情形都要把「插入点原本的换行 / 空行 trivia」挪到新指令上，再把插入点规整成一个换行 ——
    /// 否则要么两条 using 之间多出一个空行，要么 namespace 紧贴 using。
    /// 新造的换行取 <paramref name="endOfLine"/>（被编辑文档自身的行尾），不写死 CRLF。
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
