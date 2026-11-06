using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using FluentAssertions;
using Len.StronglyTypedId.Analyzers;
using Len.StronglyTypedId.CodeFixes;
using Len.StronglyTypedId.CodeRefactorings;
using Len.StronglyTypedId.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;

namespace Len.StronglyTypedId;

public static class Verify
{
    public static DiagnosticResult Diagnostic(string? diagnosticId = null)
        => diagnosticId is null
            ? CSharpAnalyzerVerifier<StronglyTypedIdAnalyzer, DefaultVerifier>.Diagnostic()
            : CSharpAnalyzerVerifier<StronglyTypedIdAnalyzer, DefaultVerifier>.Diagnostic(diagnosticId);

    public static DiagnosticResult Diagnostic(DiagnosticDescriptor descriptor)
        => new(descriptor);

    public static Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expected)
    {
        var test = new TestAnalyzer { TestCode = source };
        EnableStyleHintsWhenExpected(test, expected);
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    /// <summary>
    /// 与 <see cref="VerifyAnalyzerAsync"/> 相同，但无条件启用默认被测试基建屏蔽的两条命名风格提示（STIA0015 / STIA0016）。
    /// </summary>
    /// <remarks>
    /// 只在「期望零诊断、但源码结构足以触发风格提示」时才需要显式调用；期望里已写明 STIA0015 / STIA0016 的用例直接走 <see cref="VerifyAnalyzerAsync"/>。
    /// </remarks>
    public static Task VerifyAnalyzerWithStyleHintsAsync(string source, params DiagnosticResult[] expected)
    {
        var test = new TestAnalyzer { TestCode = source };
        EnableStyleHintDiagnostics(test);
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    /// <summary>
    /// 与 <see cref="VerifyAnalyzerAsync"/> 相同，但额外挂一份 <c>.editorconfig</c>，且<b>不</b>解除任何
    /// 默认被屏蔽的诊断。
    /// </summary>
    /// <remarks>
    /// <see cref="VerifyAnalyzerWithEditorConfigAsync"/> 会顺手解除 STIA0014 与两条命名风格提示的屏蔽，
    /// 那对「验证某条规则的诊断」是对的，对「验证一条诊断都不报」却是干扰。此处保持屏蔽原样，
    /// 让「零诊断」这个断言真的在断言零诊断。
    /// </remarks>
    public static Task VerifyAnalyzerWithRawEditorConfigAsync(string source, string editorConfig, params DiagnosticResult[] expected)
    {
        var test = new TestAnalyzer { TestCode = source };
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", SourceText.From(editorConfig)));
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    /// <summary>
    /// 与 <see cref="VerifyAnalyzerAsync"/> 相同，但额外挂一份 <c>.editorconfig</c>。
    /// </summary>
    public static Task VerifyAnalyzerWithEditorConfigAsync(string source, string editorConfig, params DiagnosticResult[] expected)
    {
        var test = new TestAnalyzer { TestCode = source };
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", SourceText.From(editorConfig)));
        EnableOptInDiagnostics(test);
        EnableStyleHintDiagnostics(test);
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    /// <summary>
    /// 与 <see cref="VerifyCodeFixAsync(string, DiagnosticResult, string)"/> 相同，但额外挂一份 <c>.editorconfig</c>。
    /// </summary>
    public static Task VerifyCodeFixWithEditorConfigAsync(string source, string editorConfig, DiagnosticResult expected, string fixedSource)
    {
        var test = new TestCodeFix
        {
            TestCode = source,
            FixedCode = fixedSource,
        };

        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", SourceText.From(editorConfig)));
        EnableOptInDiagnostics(test);
        EnableStyleHintsWhenExpected(test, [expected]);
        test.ExpectedDiagnostics.Add(expected);
        return test.RunAsync();
    }

    public static Task VerifyCodeFixAsync(string source, string fixedSource)
        => VerifyCodeFixAsync(source, DiagnosticResult.EmptyDiagnosticResults, fixedSource);

    public static Task VerifyCodeFixAsync(string source, DiagnosticResult expected, string fixedSource)
        => VerifyCodeFixAsync(source, [expected], fixedSource);

    public static Task VerifyCodeFixAsync(string source, DiagnosticResult[] expected, string fixedSource)
    {
        var test = new TestCodeFix
        {
            TestCode = source,
            FixedCode = fixedSource,
        };

        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    /// <summary>
    /// 验证 FixAll（<c>WellKnownFixAllProviders.BatchFixer</c>）：源码里的全部可修复诊断被一次性修复。
    /// </summary>
    public static Task VerifyCodeFixAllAsync(string source, DiagnosticResult[] expected, string fixedSource)
    {
        var test = new TestCodeFix
        {
            TestCode = source,
            FixedCode = fixedSource,
            BatchFixedCode = fixedSource,
            NumberOfIncrementalIterations = expected.Length,
            NumberOfFixAllIterations = 1,
        };

        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    /// <summary>
    /// 驱动 <see cref="StronglyTypedIdRefactoringProvider"/>，收集它在光标处注册的重构。
    /// </summary>
    public static async Task<IReadOnlyList<CodeAction>> CollectRefactoringsAsync(string source)
    {
        using var workspace = new AdhocWorkspace();
        var (document, span) = CreateRefactoringDocument(workspace, source);
        var actions = new List<CodeAction>();
        var context = new CodeRefactoringContext(document, span, actions.Add, CancellationToken.None);

        await new StronglyTypedIdRefactoringProvider().ComputeRefactoringsAsync(context);

        return actions;
    }

    /// <summary>
    /// 驱动重构提供器并应用它注册的唯一重构，返回改写后的源码文本。
    /// </summary>
    public static async Task<string> ApplyRefactoringAsync(string source)
    {
        using var workspace = new AdhocWorkspace();
        var (document, span) = CreateRefactoringDocument(workspace, source);
        var actions = new List<CodeAction>();
        var context = new CodeRefactoringContext(document, span, actions.Add, CancellationToken.None);

        await new StronglyTypedIdRefactoringProvider().ComputeRefactoringsAsync(context);

        var action = actions.Should().ContainSingle().Subject;
        var operations = await action.GetOperationsAsync(CancellationToken.None);
        var applied = operations.OfType<ApplyChangesOperation>().Single();
        var updated = applied.ChangedSolution.GetDocument(document.Id)!;

        return (await updated.GetTextAsync()).ToString();
    }

    /// <summary>
    /// 跑一次重构，然后把产物真正编译一遍，返回编译错误。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="GetCodeFixCompilationErrorsAsync"/> 同理：重构用例此前只比对文本，
    /// 「产物能否编译」从未被验证。Convert 会补特性、<c>partial</c> 与 <c>using</c>，Inline 会摘掉它们，
    /// 任何一步写错都会把「一次重构」变成「一片编译错误」，而纯文本断言看不出来。
    /// <para>
    /// 编译时同样跑源生成器：Convert 之后 <c>Create</c> / <c>TryParse</c> 等成员由生成器产出。
    /// </para>
    /// </remarks>
    /// <param name="source">重构前的源码（可用 <c>$$</c> 标记光标位置）。</param>
    /// <returns>产物的编译错误；可编译时为空。</returns>
    public static async Task<ImmutableArray<Diagnostic>> GetRefactoringCompilationErrorsAsync(string source)
    {
        var fixedText = await ApplyRefactoringAsync(source);

        var references = await _referenceAssemblies.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);

        var compilation = CSharpCompilation.Create(
            "RefactoredCode",
            [CSharpSyntaxTree.ParseText(fixedText, CreateDefaultParseOptions())],
            references.Add(_runtimeReference),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var withGenerators = (CSharpCompilation)RunGenerators(compilation);

        return [.. withGenerators.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)];
    }

    /// <summary>
    /// 断言重构产物可编译。
    /// </summary>
    /// <param name="source">重构前的源码（可用 <c>$$</c> 标记光标位置）。</param>
    public static async Task AssertRefactoringProducesCompilableCodeAsync(string source)
    {
        var errors = await GetRefactoringCompilationErrorsAsync(source);

        errors.Should().BeEmpty(
            "重构产物必须可编译，否则等于把一次重构换成一片编译错误：{0}",
            string.Join(" | ", errors.Select(static error => $"{error.Id}: {error.GetMessage()}")));
    }

    /// <summary>
    /// 直接驱动 <see cref="StronglyTypedIdCodeFixProvider"/>：把源码解析成 Document，在指定诊断（定位在 record 声明上）上调用
    /// <c>RegisterCodeFixesAsync</c>，收集它注册的 <see cref="CodeAction"/>。
    /// </summary>
    /// <remarks>
    /// 用于断言「不提供修复」这类 <c>VerifyCodeFixAsync</c> 框架本身不支持的场景（后者假定修复一定会被应用）。
    /// </remarks>
    public static Task<IReadOnlyList<CodeAction>> CountOfferedCodeFixesAsync(
        string source,
        DiagnosticDescriptor descriptor,
        string typeName)
        => CountOfferedCodeFixesAsync(
            source,
            descriptor,
            typeName,
            static root => root.DescendantNodes().OfType<RecordDeclarationSyntax>().First());

    /// <summary>
    /// 同上，但由调用方指定诊断落在哪个节点上（例如 <c>default</c> 表达式而非 record 声明）。
    /// </summary>
    public static async Task<IReadOnlyList<CodeAction>> CountOfferedCodeFixesAsync(
        string source,
        DiagnosticDescriptor descriptor,
        string typeName,
        Func<SyntaxNode, SyntaxNode> selectTarget)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
        var tree = CSharpSyntaxTree.ParseText(source, parseOptions);
        var root = tree.GetRoot();
        var target = selectTarget(root);
        var location = Location.Create(tree, target.Span);
        // 本类另有 Diagnostic(...) 工厂方法，此处必须全限定，否则解析到方法而非类型。
        var diagnostic = Microsoft.CodeAnalysis.Diagnostic.Create(descriptor, location, typeName);

        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var project = workspace.CurrentSolution
            .AddProject(projectId, "TestProject", "TestProject", LanguageNames.CSharp)
            .GetProject(projectId)!
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithParseOptions(parseOptions);
        var document = project.AddDocument("Test.cs", tree.GetRoot());

        var actions = new List<CodeAction>();
        var context = new CodeFixContext(
            document,
            diagnostic,
            (action, _) => actions.Add(action),
            CancellationToken.None);

        var provider = new StronglyTypedIdCodeFixProvider();
        await provider.RegisterCodeFixesAsync(context);

        return actions;
    }

    /// <summary>
    /// 直接驱动 <see cref="StronglyTypedIdCodeFixProvider"/> 并应用它注册的唯一修复，返回改写后的源码文本。
    /// </summary>
    /// <remarks>
    /// 绕开 <see cref="Microsoft.CodeAnalysis.Testing"/> 的 <c>CodeFixTest</c> 限制：后者拒绝为 <c>CompilationEnd</c>
    /// 诊断（如 STIA0019 / STIA0024）提供修复。本方法在 AdhocWorkspace 里手工跑修复并套用。
    /// </remarks>
    public static async Task<string> ApplyCodeFixAsync(
        string source,
        DiagnosticDescriptor descriptor,
        Func<SyntaxNode, SyntaxNode> selectTarget)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
        var tree = CSharpSyntaxTree.ParseText(source, parseOptions);
        var root = tree.GetRoot();
        var target = selectTarget(root);
        var location = Location.Create(tree, target.Span);
        var diagnostic = Microsoft.CodeAnalysis.Diagnostic.Create(descriptor, location);

        // 必须挂上引用：STIA0010 / STIA0017 的修复要读语义模型（判断验证器方法是否存在、签名是否匹配），
        // 没有引用时 GetSemanticModelAsync 拿不到任何符号，修复会静默退化成「什么也不做」。
        // 与 ApplyDefaults 用同一套 net8.0 引用程序集，结论与真实消费方一致。
        var references = await _referenceAssemblies.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);

        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var project = workspace.CurrentSolution
            .AddProject(projectId, "TestProject", "TestProject", LanguageNames.CSharp)
            .GetProject(projectId)!
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable))
            .WithParseOptions(parseOptions)
            .AddMetadataReferences(references.Add(_runtimeReference));
        var document = project.AddDocument("Test.cs", root);

        var actions = new List<CodeAction>();
        var context = new CodeFixContext(
            document,
            diagnostic,
            (action, _) => actions.Add(action),
            CancellationToken.None);

        var provider = new StronglyTypedIdCodeFixProvider();
        await provider.RegisterCodeFixesAsync(context);

        var action = actions.Should().ContainSingle().Subject;
        var operations = await action.GetOperationsAsync(CancellationToken.None);
        var applied = operations.OfType<ApplyChangesOperation>().Single();
        var updated = applied.ChangedSolution.GetDocument(document.Id)!;

        return (await updated.GetTextAsync()).ToString();
    }

    /// <summary>
    /// 跑一次 CodeFix，然后把修复产物真正编译一遍，返回编译错误。
    /// </summary>
    /// <remarks>
    /// 其余 CodeFix 用例只比对修复后的文本，而 <see cref="ApplyDefaults"/> 把
    /// <see cref="AnalyzerTest{TVerifier}.CompilerDiagnostics"/> 设成了 <c>None</c>，于是「修复产物能否编译」
    /// 从来没人验证过。一个把一条诊断换成一片编译错误的修复（例如漏掉 <c>using</c>、产出非法语法）
    /// 在纯文本断言下完全看不出来。本方法补上这一层：返回空数组即表示修复产物可编译。
    /// <para>
    /// 编译时必须同时跑源生成器：修复产物里的 <c>Create</c> / <c>TryParse</c> 等成员由生成器产出，
    /// 不跑生成器会把「成员尚未生成」误报成编译错误（CS0117）。这与真实消费方的编译过程一致。
    /// </para>
    /// <para>
    /// 引用集与 <see cref="ApplyDefaults"/> 一致（net8.0 引用程序集 + 被测运行时程序集），
    /// 因此结论与真实消费方一致。
    /// </para>
    /// </remarks>
    /// <param name="source">修复前的源码。</param>
    /// <param name="descriptor">要修复的诊断。</param>
    /// <param name="selectTarget">诊断落在哪个节点上。</param>
    /// <returns>修复产物的编译错误；可编译时为空。</returns>
    public static async Task<ImmutableArray<Diagnostic>> GetCodeFixCompilationErrorsAsync(
        string source,
        DiagnosticDescriptor descriptor,
        Func<SyntaxNode, SyntaxNode> selectTarget)
    {
        var fixedText = await ApplyCodeFixAsync(source, descriptor, selectTarget);

        var references = await _referenceAssemblies.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);

        var compilation = CSharpCompilation.Create(
            "FixedCode",
            [CSharpSyntaxTree.ParseText(fixedText, CreateDefaultParseOptions())],
            references.Add(_runtimeReference),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        // 先跑生成器补齐 Create / TryParse 等成员，再取编译诊断。
        var withGenerators = (CSharpCompilation)RunGenerators(compilation);

        return [.. withGenerators.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)];
    }

    /// <summary>
    /// 对 <paramref name="compilation"/> 跑一遍强类型 Id 源生成器，把产物加进编译。
    /// </summary>
    /// <remarks>
    /// 走 <see cref="GeneratorDriver"/> 的常规路径，与真实构建一致。生成器抛异常时返回原编译，
    /// 让编译错误如实反映修复产物本身的问题，而不是被生成器故障掩盖。
    /// </remarks>
    private static Compilation RunGenerators(Compilation compilation)
    {
        try
        {
            GeneratorDriver driver = CSharpGeneratorDriver.Create(new StronglyTypedIdGenerator());
            driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);

            return output;
        }
        catch (Exception)
        {
            return compilation;
        }
    }

    /// <summary>
    /// 断言 CodeFix 的修复产物可编译。
    /// </summary>
    /// <param name="source">修复前的源码。</param>
    /// <param name="descriptor">要修复的诊断。</param>
    /// <param name="selectTarget">诊断落在哪个节点上。</param>
    public static async Task AssertCodeFixProducesCompilableCodeAsync(
        string source,
        DiagnosticDescriptor descriptor,
        Func<SyntaxNode, SyntaxNode> selectTarget)
    {
        var errors = await GetCodeFixCompilationErrorsAsync(source, descriptor, selectTarget);

        errors.Should().BeEmpty(
            "修复产物必须可编译，否则等于把一条诊断换成一片编译错误：{0}",
            string.Join(" | ", errors.Select(static error => $"{error.Id}: {error.GetMessage()}")));
    }

    /// <summary>
    /// 把源码解析成「可跑重构的 Document」，并算出光标位置。
    /// </summary>
    private static (Document Document, TextSpan Span) CreateRefactoringDocument(AdhocWorkspace workspace, string source)
    {
        const string Marker = "$$";
        var caret = source.IndexOf(Marker, StringComparison.Ordinal);
        var text = caret < 0 ? source : source.Remove(caret, Marker.Length);

        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
        var root = CSharpSyntaxTree.ParseText(text, parseOptions).GetRoot();

        var position = caret >= 0
            ? caret
            : root.DescendantNodes().OfType<RecordDeclarationSyntax>().First().Identifier.SpanStart;

        var projectId = ProjectId.CreateNewId();
        var project = workspace.CurrentSolution
            .AddProject(projectId, "TestProject", "TestProject", LanguageNames.CSharp)
            .GetProject(projectId)!
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithParseOptions(parseOptions)
            .AddMetadataReference(AssemblyReferenceCache.FromAssembly(typeof(object).Assembly))
            .AddMetadataReference(AssemblyReferenceCache.FromAssembly(typeof(StronglyTypedIdAttribute).Assembly));

        return (project.AddDocument("Test.cs", root), new TextSpan(position, 0));
    }

    /// <summary>
    /// 分析器 / CodeFix 用例共用的 net8.0 引用程序集集。
    /// </summary>
    /// <remarks>
    /// 必须复用同一个实例：<see cref="ReferenceAssemblies"/> 把解析结果缓存在实例字段里，每次新建都会把
    /// 约 160 个引用程序集重新打开、解析元数据并建 <see cref="MetadataReference"/> —— 单次约 80ms，
    /// 而本工程有 170 多个分析器 / CodeFix 用例，重复解析占了它们近一半墙钟。
    /// 该类型是不可变的（配置走 <c>With*</c> 返回新实例），跨用例共享安全。
    /// </remarks>
    private static readonly ReferenceAssemblies _referenceAssemblies = new(
        "net8.0",
        new PackageIdentity("Microsoft.NETCore.App.Ref", "8.0.0"),
        Path.Combine("ref", "net8.0"));

    /// <summary>
    /// 被测运行时程序集的 net8.0 构建产物引用，同样只解析一次。
    /// </summary>
    private static readonly MetadataReference _runtimeReference = ResolveRuntimeReference();

    /// <summary>
    /// 分析器测试与 CodeFix 测试共用的默认配置：关闭编译器诊断、固定 net8.0 引用程序集集、附加被测程序集。
    /// </summary>
    private static void ApplyDefaults(AnalyzerTest<DefaultVerifier> test)
    {
        test.CompilerDiagnostics = CompilerDiagnostics.None;
        test.ReferenceAssemblies = _referenceAssemblies;
        test.TestState.AdditionalReferences.Add(_runtimeReference);

        // STIA0014 默认关闭，但 Roslyn 测试框架默认仍会报告被抑制的诊断，不显式登记会让「public + Validator」的既有用例凭空多出一条。
        // 需要验证该规则的用例走 VerifyAnalyzerWithEditorConfigAsync，那里会用 EnableOptInDiagnostics 把它从禁用清单里摘掉。
        test.DisabledDiagnostics.Add(Descriptors.ValidatorCanBeBypassedId);

        // STIA0015 / STIA0016 是命名风格提示（Info 级、无代码修复），代码修复用例里经常同时出现多个同基元 Id，修复后提示仍在，会让「修复后应无诊断」的断言失败。
        // 需要验证这两条规则的用例走 VerifyAnalyzerWithStyleHintsAsync。
        test.DisabledDiagnostics.Add(Descriptors.TypeNameShouldEndWithIdId);
        test.DisabledDiagnostics.Add(Descriptors.DuplicatePrimitiveTypeId);
    }

    /// <summary>
    /// 解析被测运行时程序集的 net8.0 构建产物引用。
    /// </summary>
    /// <remarks>
    /// 引用程序集固定在 net8.0，故附加的运行时程序集也必须取 net8.0 构建的那一份。
    /// 在 net10.0 宿主里取 <c>typeof(...).Assembly</c> 会指向 net10.0 产物，版本错位使命名实参无法绑定
    /// （被静默丢弃），验证器相关诊断在 net10.0 下零产出。
    /// </remarks>
    private static MetadataReference ResolveRuntimeReference()
    {
        var runtimeAssembly = typeof(StronglyTypedIdAttribute).Assembly;
        var net8Location = runtimeAssembly.Location.Replace(
            $"{Path.DirectorySeparatorChar}net10.0{Path.DirectorySeparatorChar}",
            $"{Path.DirectorySeparatorChar}net8.0{Path.DirectorySeparatorChar}");

        return File.Exists(net8Location)
            ? AssemblyReferenceCache.FromFile(net8Location)
            : AssemblyReferenceCache.FromAssembly(runtimeAssembly);
    }

    /// <summary>
    /// 解除「默认关闭」的登记，让 <c>.editorconfig</c> 里显式调级的规则真正产出诊断。
    /// </summary>
    private static void EnableOptInDiagnostics(AnalyzerTest<DefaultVerifier> test)
        => test.DisabledDiagnostics.Remove(Descriptors.ValidatorCanBeBypassedId);

    /// <summary>
    /// 解除两条命名风格提示的屏蔽，让专门验证 STIA0015 / STIA0016 的用例能看到它们。
    /// </summary>
    private static void EnableStyleHintDiagnostics(AnalyzerTest<DefaultVerifier> test)
    {
        test.DisabledDiagnostics.Remove(Descriptors.TypeNameShouldEndWithIdId);
        test.DisabledDiagnostics.Remove(Descriptors.DuplicatePrimitiveTypeId);
    }

    /// <summary>
    /// 期望的诊断里只要出现 STIA0015 / STIA0016，就自动解除它们的屏蔽。
    /// </summary>
    /// <remarks>
    /// 这两条规则默认被 <see cref="ApplyDefaults"/> 关掉；按期望内容自动判定后，手工改用 <see cref="VerifyAnalyzerWithStyleHintsAsync"/> 与直接写期望两种写法等价。
    /// </remarks>
    private static void EnableStyleHintsWhenExpected(AnalyzerTest<DefaultVerifier> test, DiagnosticResult[] expected)
    {
        foreach (var diagnostic in expected)
        {
            if (diagnostic.Id == Descriptors.TypeNameShouldEndWithIdId ||
                diagnostic.Id == Descriptors.DuplicatePrimitiveTypeId)
            {
                EnableStyleHintDiagnostics(test);
                return;
            }
        }
    }

    private static CSharpParseOptions CreateDefaultParseOptions()
        => new(LanguageVersion.Latest, DocumentationMode.Diagnose);

    private class TestAnalyzer : CSharpAnalyzerTest<StronglyTypedIdAnalyzer, DefaultVerifier>
    {
        public TestAnalyzer() => ApplyDefaults(this);

        protected override ParseOptions CreateParseOptions() => CreateDefaultParseOptions();
    }

    private class TestCodeFix : CSharpCodeFixTest<StronglyTypedIdAnalyzer, StronglyTypedIdCodeFixProvider, DefaultVerifier>
    {
        public TestCodeFix() => ApplyDefaults(this);

        protected override ParseOptions CreateParseOptions() => CreateDefaultParseOptions();
    }
}
