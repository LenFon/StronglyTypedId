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
            Descriptors.ValidatorReferenceInvalidId);

    public override FixAllProvider? GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

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
                                _ => Task.FromResult(context.Document),
                             },
                             title);

            context.RegisterCodeFix(action, diagnostic);
        }
    }

    /// <summary>
    /// 判断该诊断的靶子是否真的适用对应修复。
    /// </summary>
    /// <remarks>
    /// 两条规则各自有两种触发原因，原因不同则可行的修复也不同 —— 不加区分就会给出「点了没反应」
    /// 甚至把人写坏的入口。
    /// </remarks>
    private static bool IsFixable(Diagnostic diagnostic, SyntaxNode root)
    {
        switch (diagnostic.Id)
        {
            // 泛型靶子有两种：Id 自己泛型（删掉类型参数表即可）与包含类型泛型（删掉容器的类型参数会改坏
            // 使用者的设计，不该提供修复）。只在靶子是 record 时给修复。
            case Descriptors.TypeCannotBeGenericId:
                return FindNode<TypeDeclarationSyntax>(root, diagnostic) is RecordDeclarationSyntax;

            // 触发原因有两种：容器缺 partial，或容器是 file 本地类型。只在缺 partial 时提供修复 ——
            // 否则会往已有的 partial 之后再插一个（file partial class → file partial partial class）。
            case Descriptors.ContainingTypeMustBePartialId:
                return FindNode<TypeDeclarationSyntax>(root, diagnostic) is { } declaration
                    && !declaration.Modifiers.Any(SyntaxKind.PartialKeyword);

            // 类型必须 record：靶子当前是 class 或 struct（record 不会触发本规则），直接转关键字即可。
            case Descriptors.TypeMustBeRecordId:
                return FindNode<TypeDeclarationSyntax>(root, diagnostic) is ClassDeclarationSyntax or StructDeclarationSyntax;

            // 类型必须在命名空间内：仅当最外层容器直接挂在编译单元下（链上没有任何命名空间）才包命名空间；
            // 嵌在类型里又不在命名空间的情形，包的是最外层那个类型，同样是这一个判定。
            case Descriptors.TypeMustHaveNamespaceId:
                return FindNode<TypeDeclarationSyntax>(root, diagnostic) is { } nsCandidate
                    && GetOutermostType(nsCandidate).Parent is CompilationUnitSyntax;

            // 必须恰好一个单参数主构造函数：无主构造且没有任何 body 构造时可安全补一个（Guid Value）；
            // 已有 body 构造时再加主构造会撞 CS0111，交给用户自己处理，不提供修复。
            // 已有主构造但参数数 != 1 时，替换为单参主构造是安全的。
            case Descriptors.TypeMustHaveSingleParameterPrimaryConstructorId:
                return FindNode<RecordDeclarationSyntax>(root, diagnostic) is { } recordDeclaration
                    && (recordDeclaration.ParameterList is null
                        ? !recordDeclaration.Members.OfType<ConstructorDeclarationSyntax>().Any()
                        : recordDeclaration.ParameterList.Parameters.Count != 1);

            // 基元类型非法：把参数类型改成受支持的 Guid 即可（名称此前已被 STIAO007 校验为 Value）。
            case Descriptors.ParameterTypeIsInvalidId:
                return true;

            // 验证器引用无效：删掉 Validator 命名实参即可，无需判别原因。
            case Descriptors.ValidatorReferenceInvalidId:
                return FindNode<AttributeArgumentSyntax>(root, diagnostic) is not null;

            default:
                return true;
        }
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
    /// 给诊断所在的包含类型补上 <c>partial</c>。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="AddPartialKeywordAsync"/> 的区别只在靶子：嵌套强类型 Id 的容器可以是 class、struct、
    /// record 或 interface，不限于 record。
    /// </remarks>
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

        if (root is null || FindNode<TypeDeclarationSyntax>(root, diagnostic) is not { } declaration)
        {
            return context.Document;
        }

        return context.Document.WithSyntaxRoot(root.ReplaceNode(declaration, replace(declaration)));
    }

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
    /// 把诊断所在的 <c>new Xxx(value)</c> 重写成 <c>Xxx.Create(value)</c>，使构造强制经过验证器。
    /// </summary>
    /// <remarks>
    /// STIAO011 仅在 Id 设了验证器时触发：该情形下生成代码里已有静态 <c>Create</c> 工厂，用它替换直接构造
    /// 既消除诊断、又保留校验语义。原 <c>new</c> 关键字的缩进（前导 trivia）交给新的调用表达式，
    /// 参数列表原样保留，因此 <c>new Xxx(Value: value)</c> 这类具名实参也会一并迁移；类型拼写（含
    /// <c>global::</c> 前缀或命名空间限定）沿用 <c>creation.Type</c>，不会丢失。
    /// </remarks>
    private static async Task<Document> ReplaceWithCreateAsync(CodeFixContext context, Diagnostic diagnostic, CancellationToken token)
    {
        var root = await context.Document.GetSyntaxRootAsync(token);

        if (root is null || FindNode<ObjectCreationExpressionSyntax>(root, diagnostic) is not { } creation)
        {
            return context.Document;
        }

        var createInvocation = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                creation.Type,
                SyntaxFactory.IdentifierName("Create")),
            creation.ArgumentList!);

        var withTrivia = createInvocation.WithLeadingTrivia(creation.GetLeadingTrivia());

        return context.Document.WithSyntaxRoot(root.ReplaceNode(creation, withTrivia));
    }

    /// <summary>
    /// 把 class / struct 关键字转成 record，使其通过 STIAO000。
    /// </summary>
    /// <remarks>
    /// class / struct 与 record 是不同形状的语法节点，不能只换一个关键字 token，因此用
    /// <c>SyntaxFactory.RecordDeclaration</c> 重建：原样搬移特性列表、修饰符、标识符、基列表、
    /// 类型参数表、约束子句与全部成员，仅把关键字换成 <c>record</c>（引用记录）。
    /// 转换后若还缺 partial / 主构造等，由各自的诊断继续提供修复（链式）。
    /// <c>record struct</c> 也属 record，STIAO000 不会对其触发，故此处无需区分，统一转引用 record。
    /// </remarks>
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
    /// 把最外层类型包进命名空间块，使其通过 STIAO004。
    /// </summary>
    /// <remarks>
    /// 诊断定位在 Id 声明上，但真正要包的是链上最外层那个直接挂在编译单元下的类型（Id 自身、或嵌它的容器）。
    /// 命名空间名无从从报错推断：优先取项目的默认命名空间（RootNamespace），取不到时退回占位
    /// <c>MyNamespace</c>，由使用者改名。STIAO004 只要求「位于命名空间内」，具体名称分析器不过问。
    /// </remarks>
    private static async Task<Document> WrapInNamespaceAsync(CodeFixContext context, Diagnostic diagnostic, CancellationToken token)
    {
        var root = await context.Document.GetSyntaxRootAsync(token);

        if (root is null || FindNode<TypeDeclarationSyntax>(root, diagnostic) is not { } found)
        {
            return context.Document;
        }

        var outermost = GetOutermostType(found);

        // 命名空间名无从从报错推断：统一用占位 MyNamespace，由使用者改名。这是结构性占位 ——
        // STIAO004 只要求「在命名空间内」，具体叫什么分析器不过问。
        var namespaceName = "MyNamespace";

        // 仅重建命名空间骨架（原样搬移最外层类型作成员），缩进 / 换行交给 Formatter 规范化，
        // 避免手工拼 trivia 时「特性与声明之间的换行」漏缩进而拼成无缩进。
        var namespaceDeclaration = SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName(namespaceName))
            .WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(outermost))
            .WithOpenBraceToken(SyntaxFactory.Token(SyntaxKind.OpenBraceToken))
            .WithCloseBraceToken(SyntaxFactory.Token(SyntaxKind.CloseBraceToken));

        var newRoot = root.ReplaceNode(outermost, namespaceDeclaration);

        var formattedRoot = Microsoft.CodeAnalysis.Formatting.Formatter.Format(
            newRoot,
            context.Document.Project.Solution.Workspace);

        return context.Document.WithSyntaxRoot(formattedRoot);
    }

    /// <summary>
    /// 补 / 改单参数主构造函数，使其通过 STIAO005。
    /// </summary>
    /// <remarks>
    /// 占位类型用 <c>Guid</c>（最常见的强类型 Id 基元），名称固定 <c>Value</c> 以免再触发 STIAO007。
    /// 无主构造时直接补；已有主构造但参数数 != 1 时整段替换为单参版本。已有 body 构造的情形由
    /// <see cref="IsFixable"/> 拦下（再加主构造会撞 CS0111），不在此处理。
    /// </remarks>
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
                    default(SyntaxList<AttributeListSyntax>),
                    default(SyntaxTokenList),
                    SyntaxFactory.ParseTypeName("Guid"),
                    SyntaxFactory.Identifier("Value"),
                    default(EqualsValueClauseSyntax))));

        return context.Document.WithSyntaxRoot(root.ReplaceNode(recordDeclaration, recordDeclaration.WithParameterList(parameterList)));
    }

    /// <summary>
    /// 把非受支持基元类型的参数改成 <c>Guid</c>，使其通过 STIAO008。
    /// </summary>
    /// <remarks>
    /// STIAO008 触发前 STIAO007 已确认参数名为 <c>Value</c>，故此处只换类型。参数上的 attribute、修饰符、
    /// 默认值与全部 trivia（含附着在旧类型上的空白 / 注释）用 <c>WithType</c> 保留，
    /// 新类型沿用旧类型的 trivia 以免拼成 <c>GuidValue</c>。
    /// </remarks>
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
    /// STIAO010 修复主入口：决定「生成验证器桩」还是「删掉 Validator 实参」。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item>指向的静态方法根本不存在时，在 Id 类型里补一个符合契约的桩方法（静态、返回 <see cref="bool"/>、
    ///     恰好一个参数且类型等于基元 Id 类型）——这是比「删实参」更贴合意图的修复，保留校验语义。</item>
    ///   <item>方法已存在却签名不符时，再生成同名方法会撞 CS0101，故退化为删掉实参（见
    ///     <see cref="RemoveValidatorArgumentAsync"/>）。</item>
    /// </list>
    /// </remarks>
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
        if (typeSymbol.GetMembers(validatorName).OfType<IMethodSymbol>().Any(member => member.IsStatic))
        {
            return await RemoveValidatorArgumentAsync(context, diagnostic, token);
        }

        return GenerateValidatorStub(context.Document, root, recordDeclaration, validatorName, primitiveType);
    }

    /// <summary>
    /// 取 <c>[StronglyTypedId(Validator = …)]</c> 上 <c>Validator</c> 命名实参的方法名；未指定时为 <see langword="null"/>。
    /// </summary>
    private static string? GetValidatorName(INamedTypeSymbol type)
        => type.GetAttributes()
            .FirstOrDefault(attribute => attribute.AttributeClass?.ToDisplayString() == "Len.StronglyTypedId.StronglyTypedIdAttribute")
            ?.NamedArguments
            .FirstOrDefault(argument => argument.Key == "Validator")
            .Value.Value as string;

    /// <summary>
    /// 取主构造函数唯一参数的类型（即基元 Id 类型），作为验证器桩方法的参数类型。
    /// </summary>
    /// <remarks>
    /// 沿 record 的主构造函数参数列表取首个参数并用语义模型解析其类型 —— 不依赖
    /// <c>IMethodSymbol.IsPrimaryConstructor</c>（该成员在 netstandard2.0 目标下的引用程序集里未必存在）。
    /// </remarks>
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
    /// 在 Id 类型里生成一个符合契约的验证器桩方法，使其通过 STIAO010。
    /// </summary>
    /// <remarks>
    /// 桩方法固定为 <c>private static bool &lt;name&gt;(&lt;基元类型&gt; value) =&gt; true;</c>：返回 <see cref="bool"/>、
    /// 恰好一个参数且类型等于基元 Id 类型（用全限定名渲染以避免与任何同名局部类型冲突）、可见性 private
    /// （生成代码与 Id 同 partial，可达）。<c>;</c> 结尾的声明会被就地转成带 body 的形式，最后交给
    /// <see cref="Microsoft.CodeAnalysis.Formatting.Formatter"/> 统一缩进。
    /// </remarks>
    private static Document GenerateValidatorStub(
        Document document,
        SyntaxNode root,
        TypeDeclarationSyntax typeDeclaration,
        string validatorName,
        ITypeSymbol primitiveType)
    {
        var typeDisplay = primitiveType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var method = SyntaxFactory.ParseMemberDeclaration($"private static bool {validatorName}({typeDisplay} value) => true;")!
            .WithLeadingTrivia(SyntaxFactory.CarriageReturnLineFeed);

        var newDeclaration = typeDeclaration.OpenBraceToken.IsKind(SyntaxKind.None)
            ? typeDeclaration
                .WithSemicolonToken(default)
                .WithOpenBraceToken(SyntaxFactory.Token(SyntaxKind.OpenBraceToken))
                .WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(method))
                .WithCloseBraceToken(SyntaxFactory.Token(SyntaxKind.CloseBraceToken)
                    .WithTrailingTrivia(typeDeclaration.SemicolonToken.TrailingTrivia))
            : typeDeclaration.AddMembers(method);

        var formattedRoot = Microsoft.CodeAnalysis.Formatting.Formatter.Format(
            root.ReplaceNode(typeDeclaration, newDeclaration),
            document.Project.Solution.Workspace);

        return document.WithSyntaxRoot(formattedRoot);
    }

    /// <summary>
    /// 从 <c>[StronglyTypedId]</c> 中删掉无效的 <c>Validator = …</c> 命名实参，使其通过 STIAO010。
    /// </summary>
    /// <remarks>
    /// 这是 STIAO010 在「方法已存在却签名不符」时的退化修复：删掉指向错误方法的实参后，验证器回退到装配级默认（或无）。
    /// 若该实参是 attribute 里唯一实参，则连同整对括号一起删掉；否则只移除它，
    /// <see cref="SeparatedSyntaxList{TNode}.Remove"/> 会自动收拾相邻的逗号分隔符。
    /// </remarks>
    private static async Task<Document> RemoveValidatorArgumentAsync(CodeFixContext context, Diagnostic diagnostic, CancellationToken token)
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
