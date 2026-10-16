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
        // （STIAO010 / STIAO011）在 net10.0 下零产出。改取同目录下的 net8.0 产物即可对齐。
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
    }

    private static ParseOptions CreateDefaultParseOptions()
        => new CSharpParseOptions(LanguageVersion.Latest, DocumentationMode.Diagnose);

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
