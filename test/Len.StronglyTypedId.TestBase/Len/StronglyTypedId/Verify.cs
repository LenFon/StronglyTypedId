using System.IO;
using System.Reflection;
using FluentAssertions;
using Len.StronglyTypedId.Analyzers;
using Len.StronglyTypedId.CodeFixes;
using Len.StronglyTypedId.CodeRefactorings;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
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
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    /// <summary>
    /// 与 <see cref="VerifyAnalyzerAsync"/> 相同，但会启用默认被测试基建屏蔽的两条命名风格提示
    /// （STIA0015 / STIA0016）。
    /// </summary>
    /// <remarks>
    /// 这两条是 Info 级、无代码修复的风格提示，与「声明是否合法」无关（见 <see cref="ApplyDefaults"/>）。
    /// 专门验证它们的用例必须走这里，否则诊断会被基建过滤掉，断言「应当有诊断」永远失败。
    /// </remarks>
    public static Task VerifyAnalyzerWithStyleHintsAsync(string source, params DiagnosticResult[] expected)
    {
        var test = new TestAnalyzer { TestCode = source };
        EnableStyleHintDiagnostics(test);
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    /// <summary>
    /// 与 <see cref="VerifyAnalyzerAsync"/> 相同，但额外挂一份 <c>.editorconfig</c>。
    /// </summary>
    /// <remarks>
    /// 用于验证默认关闭（<c>IsEnabledByDefault = false</c>）的规则：Roslyn 测试框架会按
    /// <c>IsEnabledByDefault</c> 过滤诊断，只有 <c>.editorconfig</c> 显式调级后才会产出。
    /// </remarks>
    public static Task VerifyAnalyzerWithEditorConfigAsync(string source, string editorConfig, params DiagnosticResult[] expected)
    {
        var test = new TestAnalyzer { TestCode = source };
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", SourceText.From(editorConfig)));
        EnableOptInDiagnostics(test);
        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    /// <summary>
    /// 与 <see cref="VerifyCodeFixAsync(string, DiagnosticResult, string)"/> 相同，但额外挂一份 <c>.editorconfig</c>。
    /// </summary>
    /// <remarks>默认关闭的规则必须先经 <c>.editorconfig</c> 启用，代码修复才有诊断可修。</remarks>
    public static Task VerifyCodeFixWithEditorConfigAsync(string source, string editorConfig, DiagnosticResult expected, string fixedSource)
    {
        var test = new TestCodeFix
        {
            TestCode = source,
            FixedCode = fixedSource,
        };

        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", SourceText.From(editorConfig)));
        EnableOptInDiagnostics(test);
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
    /// <param name="source">原始源码。</param>
    /// <param name="expected">期望的分析器诊断，顺序需与 Roslyn 的报告顺序一致。</param>
    /// <param name="fixedSource">批处理修复后的源码。</param>
    /// <remarks>
    /// <para>
    /// 逐条修复的期望（<c>FixedCode</c>）与批处理的期望（<c>BatchFixedCode</c>）都设为
    /// <paramref name="fixedSource"/>：当 <c>FixedCode</c> 缺省时，测试框架会把它当作「与源码相同」，
    /// 于是逐条修复阶段必然失败。
    /// </para>
    /// <para>
    /// 迭代次数显式给出，不去依赖框架的自动推断：逐条修复每轮只处理一条诊断，因而轮数等于诊断条数；
    /// 批处理一轮即可收敛。
    /// </para>
    /// </remarks>
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
    /// <param name="source">
    /// 源码。可用 <c>$$</c> 标记光标位置；缺省时光标落在第一个 record 声明的标识符上。
    /// </param>
    /// <remarks>
    /// 代码重构没有像诊断那样的「期望清单」可供测试框架比对，故直接驱动 provider：解析源码 → 在光标处调用
    /// <c>ComputeRefactoringsAsync</c> → 收集它注册的 <see cref="CodeAction"/>。断言「不提供重构」这类场景
    /// 也只能这样表达。
    /// </remarks>
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
    /// <param name="source">源码，语义同 <see cref="CollectRefactoringsAsync"/>。</param>
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
    /// 把源码解析成「可跑重构的 Document」，并算出光标位置。
    /// </summary>
    /// <remarks>
    /// 引用只给 <c>System.Private.CoreLib</c> 与被测程序集：重构的门禁只需要解析出基元类型符号
    /// （<c>Guid</c> / <c>int</c> / <c>string</c> 都在 CoreLib），不需要完整的引用程序集集。
    /// </remarks>
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
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(StronglyTypedIdAttribute).Assembly.Location));

        return (project.AddDocument("Test.cs", root), new TextSpan(position, 0));
    }

    /// <summary>
    /// 分析器测试与 CodeFix 测试共用的默认配置：
    /// 关闭编译器诊断、固定 net8.0 引用程序集集、附加被测程序集。
    /// </summary>
    /// <remarks>
    /// 这里必须保持 <see cref="CompilerDiagnostics.None"/>：分析器用例的源码片段普遍不含
    /// <c>using System;</c>（<c>Guid</c> 是错误类型符号）；而 CodeFix 用例在 net10.0 目标下还会因为
    /// 被测程序集是 net10 构建、引用程序集却固定在 net8.0 而报 CS1705。修复产物的正确性由
    /// <c>FixedCode</c> 的精确文本比对把守。
    /// </remarks>
    private static void ApplyDefaults(AnalyzerTest<DefaultVerifier> test)
    {
        test.CompilerDiagnostics = CompilerDiagnostics.None;
        test.ReferenceAssemblies = new ReferenceAssemblies(
            "net8.0",
            new PackageIdentity("Microsoft.NETCore.App.Ref", "8.0.0"),
            Path.Combine("ref", "net8.0"));

        // 引用程序集固定在 net8.0，故附加的运行时程序集必须同样取 net8.0 构建的那一份。
        // 在 net10.0 测试宿主里 typeof(StronglyTypedIdAttribute).Assembly 会指向 net10.0 构建的
        // Len.StronglyTypedId.dll，与 net8.0 引用程序集产生版本错位，使
        // [StronglyTypedId(Validator = ...)] 的命名实参无法绑定（被静默丢弃），验证器相关诊断
        // （STIA0010 / STIA0011）在 net10.0 下零产出。改取同目录下的 net8.0 产物即可对齐。
        var runtimeAssembly = typeof(StronglyTypedIdAttribute).Assembly;
        var net8Location = runtimeAssembly.Location.Replace(
            $"{Path.DirectorySeparatorChar}net10.0{Path.DirectorySeparatorChar}",
            $"{Path.DirectorySeparatorChar}net8.0{Path.DirectorySeparatorChar}");

        if (File.Exists(net8Location))
        {
            test.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(net8Location));
        }
        else
        {
            test.TestState.AdditionalReferences.Add(runtimeAssembly);
        }

        // STIA0014 是「默认关闭」的建议性规则（IsEnabledByDefault = false）：公开 Id 是正常用法，
        // 默认开启只会在绝大多数项目上制造噪声。但 Roslyn 测试框架默认仍会报告被抑制的诊断，
        // 若不显式登记，凡是「public + Validator」的既有用例都会凭空多出一条诊断。
        // 需要验证该规则的用例走 VerifyAnalyzerWithEditorConfigAsync / VerifyCodeFixWithEditorConfigAsync，
        // 那里会用 EnableOptInDiagnostics 把它从禁用清单里摘掉。
        test.DisabledDiagnostics.Add(Descriptors.ValidatorCanBeBypassedId);

        // STIA0015 / STIA0016 是命名风格提示（Info 级、无代码修复），与「声明是否合法」无关。
        // 代码修复用例的源码里经常出现多个同基元 Id（例如同时声明 OrderId 与 InvoiceId），
        // 修复完成后这些提示依然存在，会让「修复后应无诊断」的断言失败 —— 与修复本身是否正确无关。
        // 需要验证这两条规则的用例走 VerifyAnalyzerWithStyleHintsAsync。
        test.DisabledDiagnostics.Add(Descriptors.TypeNameShouldEndWithIdId);
        test.DisabledDiagnostics.Add(Descriptors.DuplicatePrimitiveTypeId);
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
