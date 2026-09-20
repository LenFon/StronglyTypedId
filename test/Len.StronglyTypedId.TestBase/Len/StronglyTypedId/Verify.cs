using Len.StronglyTypedId.Analyzers;
using Len.StronglyTypedId.CodeFixes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

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

        test.TestState.AdditionalReferences.Add(typeof(StronglyTypedIdAttribute).Assembly);
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
