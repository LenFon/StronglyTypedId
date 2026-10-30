using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Simplification;

namespace Len.StronglyTypedId.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp)]
internal class StronglyTypedIdCodeFixProvider : CodeFixProvider
{
    private const string ValueParameterName = "Value";

    private static readonly LocalizedString _primaryConstructorFixTitle
        = new("TypeMustHaveSingleParameterPrimaryConstructorFixTitle");

    private static readonly LocalizedString _fileLocalFixTitle
        = new("TypeCannotBeFileLocalFixTitle");

    private static readonly LocalizedString _integrationsArgumentFixTitle
        = new("IntegrationsArgumentFixTitle");

    private static readonly LocalizedString _instanceAttributeFixTitle
        = new("InstanceAttributeFixTitle");

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
            Descriptors.ExceptionTypeInvalidId,
            Descriptors.InvalidPrefixId,
            Descriptors.ValidatorCanBeBypassedId,
            Descriptors.TypeCannotBeFileLocalId,
            Descriptors.IntegrationUnavailableId,
            Descriptors.UnknownIntegrationId,
            Descriptors.InvalidInstanceNameId,
            Descriptors.InvalidInstanceValueId);

    public override FixAllProvider? GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken);

        foreach (var diagnostic in context.Diagnostics)
        {
            // 描述符只用来兜底标题；生成器侧的 STIA0012 不在 All 里，查不到即代表无需提供修复。
            var descriptor = Descriptors.All.FirstOrDefault(item => item.Id == diagnostic.Id);

            if (descriptor is null || root is null || !IsFixable(diagnostic, root))
            {
                continue;
            }

            var title = GetFixTitle(diagnostic.Id, descriptor);
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
                                Descriptors.ExceptionTypeInvalidId => RemoveNamedArgumentAsync(context, diagnostic, token),
                                Descriptors.InvalidPrefixId => RemoveNamedArgumentAsync(context, diagnostic, token),
                                Descriptors.ValidatorCanBeBypassedId => NarrowVisibilityToInternalAsync(context, diagnostic, token),
                                Descriptors.TypeCannotBeFileLocalId => RemoveFileKeywordAsync(context, diagnostic, token),
                                Descriptors.IntegrationUnavailableId => RemoveNamedArgumentAsync(context, diagnostic, token),
                                Descriptors.UnknownIntegrationId => RemoveNamedArgumentAsync(context, diagnostic, token),
                                Descriptors.InvalidInstanceNameId => RemoveInstanceAttributeAsync(context, diagnostic, token),
                                Descriptors.InvalidInstanceValueId => RemoveInstanceAttributeAsync(context, diagnostic, token),
                                _ => Task.FromResult(context.Document),
                             },
                             title);

            context.RegisterCodeFix(action, diagnostic);
        }
    }

    /// <summary>
    /// 取修复标题。默认沿用诊断标题；STIA0005 例外，它的修复是把参数表整体换成 <c>(Guid Value)</c>，
    /// 原有参数会被丢弃，标题必须把这一点说出来，否则用户会以为只是「补一个参数」。
    /// STIA0025 例外，诊断标题说的是「不能是文件本地类型」，照搬会变成一句读不通的修复项。
    /// </summary>
    private static string GetFixTitle(string diagnosticId, DiagnosticDescriptor descriptor)
        => diagnosticId switch
        {
            Descriptors.TypeMustHaveSingleParameterPrimaryConstructorId => _primaryConstructorFixTitle.ToString(),
            Descriptors.TypeCannotBeFileLocalId => _fileLocalFixTitle.ToString(),
            Descriptors.IntegrationUnavailableId => _integrationsArgumentFixTitle.ToString(),
            Descriptors.UnknownIntegrationId => _integrationsArgumentFixTitle.ToString(),
            Descriptors.InvalidInstanceNameId => _instanceAttributeFixTitle.ToString(),
            Descriptors.InvalidInstanceValueId => _instanceAttributeFixTitle.ToString(),
            _ => descriptor.Title.ToString(),
        };

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

            // 靶子当前是 class 或 struct，直接转关键字即可。ref struct 不给修复：它不能装箱、不能实现接口，
            // 无论转成哪种 record 都不可能成为合法的强类型 Id。
            Descriptors.TypeMustBeRecordId => FindNode<TypeDeclarationSyntax>(root, diagnostic) switch
            {
                ClassDeclarationSyntax => true,
                StructDeclarationSyntax structDeclaration => !structDeclaration.Modifiers.Any(SyntaxKind.RefKeyword),
                _ => false,
            },

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

            // 装配级默认值写不出可删的实参，此时不给修复（与 STIA0017 的装配级形态一致）。
            Descriptors.ExceptionTypeInvalidId =>
                FindNode<AttributeArgumentSyntax>(root, diagnostic) is not null,

            Descriptors.InvalidPrefixId =>
                FindNode<AttributeArgumentSyntax>(root, diagnostic) is not null,

            // 这两类「集成相关」诊断都钉在 Integrations 命名实参上；实参找不到（例如位置回退到类型声明）时不给修复。
            Descriptors.IntegrationUnavailableId =>
                FindNode<AttributeArgumentSyntax>(root, diagnostic) is not null,

            Descriptors.UnknownIntegrationId =>
                FindNode<AttributeArgumentSyntax>(root, diagnostic) is not null,

            // 命名实例诊断钉在 [StronglyTypedIdInstance] 特性本身；名字非法 / 取值无法解析只能整条删掉，找不到特性时不给修复。
            Descriptors.InvalidInstanceNameId =>
                FindNode<AttributeSyntax>(root, diagnostic) is not null,

            Descriptors.InvalidInstanceValueId =>
                FindNode<AttributeSyntax>(root, diagnostic) is not null,

            // STIA0023 刻意不给修复：唯一能机械做出的改动是「删掉 Prefix」，但那会改变规范文本形，
            // 已经按带前缀格式写出去的数据将再也读不回来。换一个安全前缀必须由人决定。

            // 只有显式写了 public 才有得收窄；省略修饰符的类型本身就是 internal。
            Descriptors.ValidatorCanBeBypassedId =>
                FindNode<TypeDeclarationSyntax>(root, diagnostic) is { } bypassCandidate
                    && bypassCandidate.Modifiers.Any(SyntaxKind.PublicKeyword),

            // 只摘本段声明上的 file：类型其余 partial 段若也带 file，修复后分析器会对那些段再报一次，
            // 逐段应用即可全部摘除；一次性跨文件改动不是单个 CodeFix 该做的事。
            Descriptors.TypeCannotBeFileLocalId =>
                FindNode<RecordDeclarationSyntax>(root, diagnostic) is { } fileLocalCandidate
                    && fileLocalCandidate.Modifiers.Any(SyntaxKind.FileKeyword),

            // 只有「带实参的构造」能机械改写成 Create(...)。default / default(T) 与无参的对象初始化器
            // 一律不给修复：前者改成 Create(default) 会把编译期提示换成运行时异常，后者没有可搬运的实参
            // （`new Xxx()` 的 ArgumentList 不是 null 而是空表，只判 null 会产出缺参数的 `Xxx.Create()`）。
            Descriptors.BypassCreateId =>
                FindNode<BaseObjectCreationExpressionSyntax>(root, diagnostic) is { ArgumentList.Arguments.Count: > 0 },

            _ => true,
        };

    private static Task<Document> AddPartialKeywordAsync(CodeFixContext context, Diagnostic diagnostic, CancellationToken token)
        => ReplaceRecordDeclarationAsync(context, diagnostic, token,
            static declaration => declaration.AddModifiers(SyntaxFactory.Token(SyntaxKind.PartialKeyword)));

    private static Task<Document> RemoveAbstractKeywordAsync(CodeFixContext context, Diagnostic diagnostic, CancellationToken token)
        => ReplaceRecordDeclarationAsync(context, diagnostic, token,
            static declaration => declaration.WithoutModifiers(SyntaxKind.AbstractKeyword));

    /// <summary>
    /// 摘掉强类型 Id 自身声明上的 <c>file</c> 修饰符，使其可被生成器在另一个文件里重开（STIA0025）。
    /// </summary>
    private static Task<Document> RemoveFileKeywordAsync(CodeFixContext context, Diagnostic diagnostic, CancellationToken token)
        => ReplaceRecordDeclarationAsync(context, diagnostic, token,
            static declaration => declaration.WithoutModifiers(SyntaxKind.FileKeyword));

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
    /// 把 class / struct 关键字转成对应的 record，使其通过 STIA0000。
    /// </summary>
    /// <remarks>
    /// struct 必须转成 <c>record struct</c>：写成 <c>record</c> 会把值类型悄悄换成引用类型，<c>readonly struct</c> 更是直接 CS8809。
    /// </remarks>
    private static async Task<Document> ConvertToRecordAsync(CodeFixContext context, Diagnostic diagnostic, CancellationToken token)
    {
        var root = await context.Document.GetSyntaxRootAsync(token);

        if (root is null || FindNode<TypeDeclarationSyntax>(root, diagnostic) is not { } declaration)
        {
            return context.Document;
        }

        // record 与 struct 之间必须有一个空格，两个关键字都是新造的 token，没有现成的 trivia 可用。
        // kind 也必须跟着换：RecordDeclaration 与 RecordStructDeclaration 是两个不同的节点种类，
        // 只改 classOrStructKeyword 会得到「长得像 record struct、实则是引用类型 record」的产物。
        var isStruct = declaration is StructDeclarationSyntax;

        var record = SyntaxFactory.RecordDeclaration(
                isStruct ? SyntaxKind.RecordStructDeclaration : SyntaxKind.RecordDeclaration,
                SyntaxFactory.Token(SyntaxKind.RecordKeyword),
                declaration.Identifier)
            .WithClassOrStructKeyword(isStruct
                ? SyntaxFactory.Token(SyntaxKind.StructKeyword)
                    .WithLeadingTrivia(SyntaxFactory.Space)
                    .WithTrailingTrivia(SyntaxFactory.Space)
                : SyntaxFactory.Token(SyntaxKind.None))
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
                    GuidTypeName(),
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

        var newType = GuidTypeName().WithTriviaFrom(parameter.Type!);
        var updated = parameter.WithType(newType);

        return context.Document.WithSyntaxRoot(root.ReplaceNode(parameter, updated));
    }

    /// <summary>
    /// 修复产物里的 <c>Guid</c> 类型名。写成 <c>global::System.Guid</c> 并挂 <see cref="Simplifier.Annotation"/>：
    /// 文件里已有 <c>using System;</c> 时由 CodeFix 后处理简化回 <c>Guid</c>，没有时停在 <c>System.Guid</c>，
    /// 否则裸 <c>Guid</c> 会留下一个新的 CS0246。
    /// </summary>
    private static TypeSyntax GuidTypeName()
        => SyntaxFactory.ParseTypeName("global::System.Guid").WithAdditionalAnnotations(Simplifier.Annotation);

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

        var validatorName = AttributeReader.GetValidatorName(typeSymbol);
        var primitiveType = GetPrimitiveParameterType(recordDeclaration, semanticModel, token);

        if (validatorName is null || primitiveType is null)
        {
            return context.Document;
        }

        // 方法已存在（但签名不符）时删掉实参；只有根本不存在时才生成桩，二者不可混用。
        // 桩体必须是 throw 而不是 `=> true`：Fix All 会把这条桩铺到几十个 Id 上，恒真的桩等于把校验整体架空，
        // 而「忘了填」要到第一次运行时才炸出来，比编译不过更难发现。
        return typeSymbol.GetMembers(validatorName).OfType<IMethodSymbol>().Any(member => member.IsStatic)
            ? await RemoveNamedArgumentAsync(context, diagnostic, token)
            : GenerateMethodStub(context.Document, root, recordDeclaration, $"private static bool {validatorName}({primitiveType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} value) => throw new global::System.NotImplementedException(\"TODO: implement the validation rule.\");", token);
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

        var normalizerName = AttributeReader.GetNormalizerName(typeSymbol);
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
    /// 摘掉诊断所在的那条 <c>[StronglyTypedIdInstance]</c> 特性，使 STIA0020 / STIA0021 消失。
    /// </summary>
    /// <remarks>
    /// 名字非法或取值无法解析的命名实例没有可机械改写的目标，只能整条删掉，把「重新挑一个合法名字 / 取值」
    /// 留给人决定。只摘诊断落点的那一条特性，类型上其余的命名实例必须原样保留。
    /// </remarks>
    private static async Task<Document> RemoveInstanceAttributeAsync(CodeFixContext context, Diagnostic diagnostic, CancellationToken token)
    {
        var root = await context.Document.GetSyntaxRootAsync(token);

        if (root is null
            || FindNode<AttributeSyntax>(root, diagnostic) is not { } attribute
            || attribute.Parent is not AttributeListSyntax attributeList
            || attributeList.Parent is not TypeDeclarationSyntax typeDeclaration)
        {
            return context.Document;
        }

        // 单特性列表摘掉后整段消失；多特性列表只删命中那一条，逗号与分隔 trivia 由 SeparatedSyntaxList 一并带走。
        var updatedType = attributeList.Attributes.Count == 1
            ? typeDeclaration.WithAttributeLists(typeDeclaration.AttributeLists.Remove(attributeList))
            : typeDeclaration.WithAttributeLists(
                typeDeclaration.AttributeLists.Replace(
                    attributeList,
                    attributeList.WithAttributes(attributeList.Attributes.Remove(attribute))));

        return context.Document.WithSyntaxRoot(root.ReplaceNode(typeDeclaration, updatedType));
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
