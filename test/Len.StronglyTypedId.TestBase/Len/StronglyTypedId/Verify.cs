using Len.StronglyTypedId.Analyzers;
using Len.StronglyTypedId.CodeFixes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace Len.StronglyTypedId;

public static class Verify
{
    public static DiagnosticResult Diagnostic(string? diagnosticId = null)
     => diagnosticId == null ?
         CSharpAnalyzerVerifier<StronglyTypedIdAnalyzer, XUnitVerifier>.Diagnostic() :
         CSharpAnalyzerVerifier<StronglyTypedIdAnalyzer, XUnitVerifier>.Diagnostic(diagnosticId);

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
        => VerifyCodeFixAsync(source, new[] { expected }, fixedSource);

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

    private class TestAnalyzer : CSharpAnalyzerTest<StronglyTypedIdAnalyzer, XUnitVerifier>
    {
        public TestAnalyzer()
        {
            CompilerDiagnostics = CompilerDiagnostics.None;
            ReferenceAssemblies = new ReferenceAssemblies(
                        "net7.0",
                        new PackageIdentity(
                            "Microsoft.NETCore.App.Ref",
                            "7.0.0"),
                        Path.Combine("ref", "net7.0"));

            TestState.AdditionalReferences.Add(typeof(StronglyTypedIdAttribute).Assembly);
        }

        protected override ParseOptions CreateParseOptions()
        {
            return new CSharpParseOptions(LanguageVersion.Latest, DocumentationMode.Diagnose);
        }
    }

    private class TestCodeFix : CSharpCodeFixTest<StronglyTypedIdAnalyzer, StronglyTypedIdCodeFixProvider, XUnitVerifier>
    {
        public TestCodeFix()
        {
            CompilerDiagnostics = CompilerDiagnostics.None;
            ReferenceAssemblies = new ReferenceAssemblies(
                        "net7.0",
                        new PackageIdentity(
                            "Microsoft.NETCore.App.Ref",
                            "7.0.0"),
                        Path.Combine("ref", "net7.0"));

            TestState.AdditionalReferences.Add(typeof(StronglyTypedIdAttribute).Assembly);
        }

        protected override ParseOptions CreateParseOptions()
        {
            return new CSharpParseOptions(LanguageVersion.Latest, DocumentationMode.Diagnose);
        }
    }
}