using Microsoft.CodeAnalysis;
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
            Descriptors.TypeCannotBeGenericId,
            Descriptors.ContainingTypeMustBePartialId,
            Descriptors.BypassCreateId,
            Descriptors.TypeMustBeRecordId,
            Descriptors.TypeMustHaveNamespaceId,
            Descriptors.TypeMustHaveSingleParameterPrimaryConstructorId,
            Descriptors.ParameterTypeIsInvalidId,
            Descriptors.ValidatorReferenceInvalidId,
            Descriptors.NormalizerReferenceInvalidId,
            Descriptors.InvalidPrefixId,
            Descriptors.ValidatorCanBeBypassedId);

    public override FixAllProvider? GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken);

        foreach (var diagnostic in context.Diagnostics)
        {
            // 只有可修复的诊断才在 Descriptors.All 中登记，找不到即代表无需提供修复。
            var descriptor = Descriptors.All.FirstOrDefault(item => item.Id == diagnostic.Id);

            if (descriptor is null || root is null || !IsFixable(diagnostic, root))
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
                                 Descriptors.ContainingTypeMustBePartialId => AddPartialToContainingTypeAsync(context, diagnostic, token),
                                Descriptors.BypassCreateId => ReplaceWithCreateAsync(context, diagnostic, token),
                                Descriptors.TypeMustBeRecordId => ConvertToRecordAsync(context, diagnostic, token),
                                Descriptors.TypeMustHaveNamespaceId => WrapInNamespaceAsync(context, diagnostic, token),
                                Descriptors.TypeMustHaveSingleParameterPrimaryConstructorId => EnsurePrimaryConstructorAsync(context, diagnostic, token),
                                Descriptors.ParameterTypeIsInvalidId => ChangeParameterTypeToGuidAsync(context, diagnostic, token),
                                Descriptors.ValidatorReferenceInvalidId => FixValidatorReferenceAsync(context, diagnostic, token),
                                Descriptors.NormalizerReferenceInvalidId => FixNormalizerReferenceAsync(context, diagnostic, token),
                                Descriptors.InvalidPrefixId => RemoveNamedArgumentAsync(context, diagnostic, token),
                                Descriptors.ValidatorCanBeBypassedId => NarrowVisibilityToInternalAsync(context, diagnostic, token),
                                _ => Task.FromResult(context.Document),
                             },
                             title);

            context.RegisterCodeFix(action, diagnostic);
        }
    }

    /// <summary>
    /// 判断该诊断的靶子是否真的适用对应修复。
    /// </summary>
    private static bool IsFixable(Diagnostic diagnostic, SyntaxNode root)
        => diagnostic.Id switch
        {
            // 只在靶子是 record（Id 自己泛型）时给修复；包含类型泛型删掉类型参数会改坏使用者的设计。
            Descriptors.TypeCannotBeGenericId =>
                FindNode<TypeDeclarationSyntax>(root, diagnostic) is RecordDeclarationSyntax,

            // 只在缺 partial 时提供修复，否则会插出 file partial partial class。
            Descriptors.ContainingTypeMustBePartialId =>
                FindNode<TypeDeclarationSyntax>(root, diagnostic) is { } declaration
                    && !declaration.Modifiers.Any(SyntaxKind.PartialKeyword),

            // 靶子当前是 class 或 struct，直接转关键字即可。
            Descriptors.TypeMustBeRecordId =>
                FindNode<TypeDeclarationSyntax>(root, diagnostic) is ClassDeclarationSyntax or StructDeclarationSyntax,

            // 仅当最外层容器直接挂在编译单元下（链上没有任何命名空间）才包命名空间。
            Descriptors.TypeMustHaveNamespaceId =>
                FindNode<TypeDeclarationSyntax>(root, diagnostic) is { } nsCandidate
                    && GetOutermostType(nsCandidate).Parent is CompilationUnitSyntax,

            // 无主构造且无 body 构造时可安全补一个；已有 body 构造时再加主构造会撞 CS0111，不提供修复。
            Descriptors.TypeMustHaveSingleParameterPrimaryConstructorId =>
                FindNode<RecordDeclarationSyntax>(root, diagnostic) is { } recordDeclaration
                    && (recordDeclaration.ParameterList is null
                        ? !recordDeclaration.Members.OfType<ConstructorDeclarationSyntax>().Any()
                        : recordDeclaration.ParameterList.Parameters.Count != 1),

            // 把参数类型改成受支持的 Guid 即可（名称此前已被 STIA0007 校验为 Value）。
            Descriptors.ParameterTypeIsInvalidId => true,

            Descriptors.ValidatorReferenceInvalidId =>
                FindNode<AttributeArgumentSyntax>(root, diagnostic) is not null,

            Descriptors.NormalizerReferenceInvalidId =>
                FindNode<AttributeArgumentSyntax>(root, diagnostic) is not null,

            Descriptors.InvalidPrefixId =>
                FindNode<AttributeArgumentSyntax>(root, diagnostic) is not null,

            // 只有显式写了 public 才有得收窄；省略修饰符的类型本身就是 internal。
            Descriptors.ValidatorCanBeBypassedId =>
                FindNode<TypeDeclarationSyntax>(root, diagnostic) is { } bypassCandidate
                    && bypassCandidate.Modifiers.Any(SyntaxKind.PublicKeyword),

            // 只有「带参数列表的构造」能机械改写成 Create(...)。default / default(T) 与无参的对象初始化器
            // 一律不给修复：前者改成 Create(default) 会把编译期提示换成运行时异常，后者没有可搬运的实参。
            Descriptors.BypassCreateId =>
                FindNode<BaseObjectCreationExpressionSyntax>(root, diagnostic) is { ArgumentList: not null },

            _ => true,
        };

    private static Task<Document> AddPartialKeywordAsync(CodeFixContext context, Diagnostic diagnostic, CancellationToken token)
        => ReplaceRecordDeclarationAsync(context, diagnostic, token,
            static declaration => declaration.AddModifiers(SyntaxFactory.Token(SyntaxKind.PartialKeyword)));

    private static Task<Document> RemoveAbstractKeywordAsync(CodeFixContext context, Diagnostic diagnostic, CancellationToken token)
        => ReplaceRecordDeclarationAsync(context, diagnostic, token,
            static declaration => declaration.WithoutModifiers(SyntaxKind.AbstractKeyword));

    private static Task<Document> RemoveGenericAsync(CodeFixContext context, Diagnostic diagnostic, CancellationToken token)
        => ReplaceRecordDeclarationAsync(context, diagnostic, token, RemoveTypeParametersAndConstraints);

    /// <summary>
    /// 给诊断所在的包含类型补上 <c>partial</c>。
    /// </summary>
    private static Task<Document> AddPartialToContainingTypeAsync(CodeFixContext context, Diagnostic diagnostic, CancellationToken token)
        => ReplaceTypeDeclarationAsync(context, diagnostic, token,
            static declaration => declaration.AddModifiers(SyntaxFactory.Token(SyntaxKind.PartialKeyword)));

    /// <summary>
    /// 用 <paramref name="replace"/> 的结果替换诊断所在的类型声明（record 之外的种类也适用）。
    /// </summary>
    private static async Task<Document> ReplaceTypeDeclarationAsync(
        CodeFixContext context,
        Diagnostic diagnostic,
        CancellationToken token,
        Func<TypeDeclarationSyntax, TypeDeclarationSyntax> replace)
    {
        var root = await context.Document.GetSyntaxRootAsync(token);

        return root is null || FindNode<TypeDeclarationSyntax>(root, diagnostic) is not { } declaration
            ? context.Document
            : context.Document.WithSyntaxRoot(root.ReplaceNode(declaration, replace(declaration)));
    }

    /// <summary>
    /// 把诊断所在类型的 <c>public</c> 修饰符换成 <c>internal</c>，使验证器无法被外部直接构造绕过（STIA0014）。
    /// </summary>
    private static Task<Document> NarrowVisibilityToInternalAsync(CodeFixContext context, Diagnostic diagnostic, CancellationToken token)
        => ReplaceTypeDeclarationAsync(context, diagnostic, token, NarrowVisibilityToInternal);

    private static TypeDeclarationSyntax NarrowVisibilityToInternal(TypeDeclarationSyntax declaration)
    {
        var modifiers = declaration.Modifiers;
        var index = modifiers.IndexOf(SyntaxKind.PublicKeyword);

        return index < 0
            ? declaration
            : declaration.WithModifiers(modifiers.Replace(
                modifiers[index],
                SyntaxFactory.Token(SyntaxKind.InternalKeyword).WithTriviaFrom(modifiers[index])));
    }

    /// <summary>
    /// 移除 record 的类型参数表，并把紧随其后的约束子句一并移除。
    /// </summary>
    private static RecordDeclarationSyntax RemoveTypeParametersAndConstraints(RecordDeclarationSyntax declaration)
    {
        return declaration.ConstraintClauses.Count == 0
            ? declaration.WithTypeParameterList(null)
            : declaration
                .WithTypeParameterList(null)
                .WithConstraintClauses([])
                .WithParameterList(declaration.ParameterList?.WithoutTrailingTrivia());
    }

    private static Task<Document> UpdateParameterNameToValueAsync(CodeFixContext context, Diagnostic diagnostic, CancellationToken token)
        => ReplaceParameterAsync(context, diagnostic, token, stripNullableSuffix: false);

    private static Task<Document> RemoveNullableAsync(CodeFixContext context, Diagnostic diagnostic, CancellationToken token)
        => ReplaceParameterAsync(context, diagnostic, token, stripNullableSuffix: true);

    /// <summary>
    /// 把诊断所在的 <c>new Xxx(value)</c> / <c>new(value)</c> 重写成 <c>Xxx.Create(value)</c>，使构造强制经过验证器。
    /// </summary>
    private static async Task<Document> ReplaceWithCreateAsync(CodeFixContext context, Diagnostic diagnostic, CancellationToken token)
    {
        var root = await context.Document.GetSyntaxRootAsync(token);

        if (root is null || FindNode<BaseObjectCreationExpressionSyntax>(root, diagnostic) is not { ArgumentList: { } argumentList } creation)
        {
            return context.Document;
        }

        var typeSyntax = creation is ObjectCreationExpressionSyntax explicitCreation
            ? explicitCreation.Type
            : await GetCreatedTypeSyntaxAsync(context, creation, token);

        if (typeSyntax is null)
        {
            return context.Document;
        }

        var createInvocation = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                typeSyntax,
                SyntaxFactory.IdentifierName("Create")),
            argumentList);

        var withTrivia = createInvocation.WithLeadingTrivia(creation.GetLeadingTrivia());

        return context.Document.WithSyntaxRoot(root.ReplaceNode(creation, withTrivia));
    }

    /// <summary>
    /// 取目标类型化 <c>new(...)</c> 的目标类型语法。
    /// </summary>
    private static async Task<TypeSyntax?> GetCreatedTypeSyntaxAsync(
        CodeFixContext context,
        BaseObjectCreationExpressionSyntax creation,
        CancellationToken token)
    {
        var semanticModel = await context.Document.GetSemanticModelAsync(token);
        var type = semanticModel?.GetTypeInfo(creation, token).Type;

        return type is null || semanticModel is null
            ? null
            : SyntaxFactory.ParseTypeName(type.ToMinimalDisplayString(semanticModel, creation.SpanStart));
    }

    /// <summary>
    /// 把 class / struct 关键字转成 record，使其通过 STIA0000。
    /// </summary>
    private static async Task<Document> ConvertToRecordAsync(CodeFixContext context, Diagnostic diagnostic, CancellationToken token)
    {
        var root = await context.Document.GetSyntaxRootAsync(token);

        if (root is null || FindNode<TypeDeclarationSyntax>(root, diagnostic) is not { } declaration)
        {
            return context.Document;
        }

        var record = SyntaxFactory.RecordDeclaration(
                SyntaxFactory.Token(SyntaxKind.RecordKeyword),
                declaration.Identifier)
            .WithAttributeLists(declaration.AttributeLists)
            .WithModifiers(declaration.Modifiers)
            .WithTypeParameterList(declaration.TypeParameterList)
            .WithParameterList(declaration.ParameterList)
            .WithBaseList(declaration.BaseList)
            .WithConstraintClauses(declaration.ConstraintClauses)
            .WithOpenBraceToken(declaration.OpenBraceToken)
            .WithMembers(declaration.Members)
            .WithCloseBraceToken(declaration.CloseBraceToken)
            .WithSemicolonToken(declaration.SemicolonToken)
            .WithLeadingTrivia(declaration.GetLeadingTrivia())
            .WithTrailingTrivia(declaration.GetTrailingTrivia());

        return context.Document.WithSyntaxRoot(root.ReplaceNode(declaration, record));
    }

    /// <summary>
    /// 把最外层类型包进命名空间块，使其通过 STIA0004。
    /// </summary>
    private static async Task<Document> WrapInNamespaceAsync(CodeFixContext context, Diagnostic diagnostic, CancellationToken token)
    {
        var root = await context.Document.GetSyntaxRootAsync(token);

        if (root is null || FindNode<TypeDeclarationSyntax>(root, diagnostic) is not { } found)
        {
            return context.Document;
        }

        var outermost = GetOutermostType(found);

        // 命名空间名无从推断，统一用占位 MyNamespace，由使用者改名。
        var namespaceName = "MyNamespace";

        // 仅重建命名空间骨架，缩进 / 换行交给 DocumentFormatting 规范化。
        var namespaceDeclaration = SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName(namespaceName))
            .WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(outermost))
            .WithOpenBraceToken(SyntaxFactory.Token(SyntaxKind.OpenBraceToken))
            .WithCloseBraceToken(SyntaxFactory.Token(SyntaxKind.CloseBraceToken));

        var newRoot = root.ReplaceNode(outermost, namespaceDeclaration);

        return context.Document.WithSyntaxRoot(
            DocumentFormatting.Format(context.Document, newRoot, token));
    }

    /// <summary>
    /// 补 / 改单参数主构造函数，使其通过 STIA0005。
    /// </summary>
    private static async Task<Document> EnsurePrimaryConstructorAsync(CodeFixContext context, Diagnostic diagnostic, CancellationToken token)
    {
        var root = await context.Document.GetSyntaxRootAsync(token);

        if (root is null || FindNode<RecordDeclarationSyntax>(root, diagnostic) is not { } recordDeclaration)
        {
            return context.Document;
        }

        var parameterList = SyntaxFactory.ParameterList(
            SyntaxFactory.SingletonSeparatedList(
                SyntaxFactory.Parameter(
                    default,
                    default,
                    SyntaxFactory.ParseTypeName("Guid"),
                    SyntaxFactory.Identifier("Value"),
                    default)));

        return context.Document.WithSyntaxRoot(root.ReplaceNode(recordDeclaration, recordDeclaration.WithParameterList(parameterList)));
    }

    /// <summary>
    /// 把非受支持基元类型的参数改成 <c>Guid</c>，使其通过 STIA0008。
    /// </summary>
    private static async Task<Document> ChangeParameterTypeToGuidAsync(CodeFixContext context, Diagnostic diagnostic, CancellationToken token)
    {
        var root = await context.Document.GetSyntaxRootAsync(token);

        if (root is null || FindNode<ParameterSyntax>(root, diagnostic) is not { } parameter)
        {
            return context.Document;
        }

        var newType = SyntaxFactory.ParseTypeName("Guid").WithTriviaFrom(parameter.Type!);
        var updated = parameter.WithType(newType);

        return context.Document.WithSyntaxRoot(root.ReplaceNode(parameter, updated));
    }

    /// <summary>
    /// STIA0010 修复主入口：决定「生成验证器桩」还是「删掉 Validator 实参」。
    /// </summary>
    private static async Task<Document> FixValidatorReferenceAsync(CodeFixContext context, Diagnostic diagnostic, CancellationToken token)
    {
        var root = await context.Document.GetSyntaxRootAsync(token);

        if (root is null || FindNode<RecordDeclarationSyntax>(root, diagnostic) is not { } recordDeclaration)
        {
            return context.Document;
        }

        var semanticModel = await context.Document.GetSemanticModelAsync(token);

        if (semanticModel is null
            || semanticModel.GetDeclaredSymbol(recordDeclaration, token) is not INamedTypeSymbol typeSymbol)
        {
            return context.Document;
        }

        var validatorName = GetValidatorName(typeSymbol);
        var primitiveType = GetPrimitiveParameterType(recordDeclaration, semanticModel, token);

        if (validatorName is null || primitiveType is null)
        {
            return context.Document;
        }

        // 方法已存在（但签名不符）时删掉实参；只有根本不存在时才生成桩，二者不可混用。
        return typeSymbol.GetMembers(validatorName).OfType<IMethodSymbol>().Any(member => member.IsStatic)
            ? await RemoveNamedArgumentAsync(context, diagnostic, token)
            : GenerateMethodStub(context.Document, root, recordDeclaration, $"private static bool {validatorName}({primitiveType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} value) => true;", token);
    }

    /// <summary>
    /// STIA0017 修复主入口：决定「生成归一化桩」还是「删掉 Normalizer 实参」。
    /// </summary>
    private static async Task<Document> FixNormalizerReferenceAsync(CodeFixContext context, Diagnostic diagnostic, CancellationToken token)
    {
        var root = await context.Document.GetSyntaxRootAsync(token);

        if (root is null || FindNode<RecordDeclarationSyntax>(root, diagnostic) is not { } recordDeclaration)
        {
            return context.Document;
        }

        var semanticModel = await context.Document.GetSemanticModelAsync(token);

        if (semanticModel is null
            || semanticModel.GetDeclaredSymbol(recordDeclaration, token) is not INamedTypeSymbol typeSymbol)
        {
            return context.Document;
        }

        var normalizerName = GetNormalizerName(typeSymbol);
        var primitiveType = GetPrimitiveParameterType(recordDeclaration, semanticModel, token);

        if (normalizerName is null || primitiveType is null)
        {
            return context.Document;
        }

        var typeDisplay = primitiveType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        return typeSymbol.GetMembers(normalizerName).OfType<IMethodSymbol>().Any(member => member.IsStatic)
            ? await RemoveNamedArgumentAsync(context, diagnostic, token)
            : GenerateMethodStub(context.Document, root, recordDeclaration, $"private static {typeDisplay} {normalizerName}({typeDisplay} value) => value;", token);
    }

    /// <summary>
    /// 取 <c>[StronglyTypedId(Validator = …)]</c> 上 <c>Validator</c> 命名实参的方法名；未指定时为 <see langword="null"/>。
    /// </summary>
    private static string? GetValidatorName(INamedTypeSymbol type)
        => AttributeReader.GetString(
            AttributeReader.Find(type, AttributeReader.StronglyTypedIdAttributeName),
            StronglyTypedIdInfo.ValidatorArgumentName);

    /// <summary>
    /// 取 <c>[StronglyTypedId(Normalizer = …)]</c> 上 <c>Normalizer</c> 命名实参的方法名；未指定时为 <see langword="null"/>。
    /// </summary>
    private static string? GetNormalizerName(INamedTypeSymbol type)
        => AttributeReader.GetString(
            AttributeReader.Find(type, AttributeReader.StronglyTypedIdAttributeName),
            StronglyTypedIdInfo.NormalizerArgumentName);

    /// <summary>
    /// 取主构造函数唯一参数的类型（即基元 Id 类型），作为验证器桩方法的参数类型。
    /// </summary>
    private static ITypeSymbol? GetPrimitiveParameterType(
        RecordDeclarationSyntax recordDeclaration,
        SemanticModel semanticModel,
        CancellationToken token)
    {
        var parameter = recordDeclaration.ParameterList?.Parameters.FirstOrDefault();

        return parameter?.Type is null
            ? null
            : semanticModel.GetTypeInfo(parameter.Type, token).Type;
    }

    /// <summary>
    /// 在 Id 类型里生成一个桩方法，使诊断消失。
    /// </summary>
    private static Document GenerateMethodStub(
        Document document,
        SyntaxNode root,
        TypeDeclarationSyntax typeDeclaration,
        string signature,
        CancellationToken token)
    {
        var method = SyntaxFactory.ParseMemberDeclaration(signature)!
            .WithLeadingTrivia(SyntaxFactory.EndOfLine(DocumentFormatting.GetEndOfLine(root)));

        var newDeclaration = typeDeclaration.OpenBraceToken.IsKind(SyntaxKind.None)
            ? typeDeclaration
                .WithSemicolonToken(default)
                .WithOpenBraceToken(SyntaxFactory.Token(SyntaxKind.OpenBraceToken))
                .WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(method))
                .WithCloseBraceToken(SyntaxFactory.Token(SyntaxKind.CloseBraceToken)
                    .WithTrailingTrivia(typeDeclaration.SemicolonToken.TrailingTrivia))
            : typeDeclaration.AddMembers(method);

        return document.WithSyntaxRoot(
            DocumentFormatting.Format(document, root.ReplaceNode(typeDeclaration, newDeclaration), token));
    }

    /// <summary>
    /// 从 <c>[StronglyTypedId]</c> 中删掉诊断所在的命名实参，使其通过对应规则。
    /// </summary>
    private static async Task<Document> RemoveNamedArgumentAsync(CodeFixContext context, Diagnostic diagnostic, CancellationToken token)
    {
        var root = await context.Document.GetSyntaxRootAsync(token);

        if (root is null || FindNode<AttributeArgumentSyntax>(root, diagnostic) is not { } argument
            || argument.Parent is not AttributeArgumentListSyntax argumentList
            || argumentList.Parent is not AttributeSyntax attribute)
        {
            return context.Document;
        }

        var updatedAttribute = argumentList.Arguments.Count == 1
            ? attribute.WithArgumentList(null)
            : attribute.WithArgumentList(argumentList.WithArguments(argumentList.Arguments.Remove(argument)));

        return context.Document.WithSyntaxRoot(root.ReplaceNode(attribute, updatedAttribute));
    }

    /// <summary>
    /// 沿父链向上取到直接挂在编译单元下的最外层类型声明（穿透嵌套容器）。
    /// </summary>
    private static TypeDeclarationSyntax GetOutermostType(TypeDeclarationSyntax node)
    {
        while (node.Parent is TypeDeclarationSyntax parent)
        {
            node = parent;
        }

        return node;
    }

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

        return root is null || FindNode<RecordDeclarationSyntax>(root, diagnostic) is not { } declaration
            ? context.Document
            : context.Document.WithSyntaxRoot(root.ReplaceNode(declaration, replace(declaration)));
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

        // 就地改写而非重建：重建出的参数只带「类型 + 名字」，attribute、修饰符、默认值与 trivia 都会丢。
        var newParameter = parameter.WithIdentifier(
            SyntaxFactory.Identifier(parameter.Identifier.LeadingTrivia, ValueParameterName, parameter.Identifier.TrailingTrivia));

        if (stripNullableSuffix && parameter.Type is NullableTypeSyntax nullableType)
        {
            // 用 ElementType 顶掉 `?` 并继承 trivia，否则会拼成 `GuidValue`。
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
        => declaration.WithModifiers([.. declaration.Modifiers.Where(modifier => !syntaxKinds.Contains(modifier.Kind()))]);
}
