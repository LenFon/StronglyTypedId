using FluentAssertions;
using Len.StronglyTypedId.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace Len.StronglyTypedId.Analyzers;

/// <summary>
/// 「分析器一键静音」的行为约定。
/// </summary>
/// <remarks>
/// 开关走 MSBuild → 分析器通道：消费方设 <c>&lt;StronglyTypedIdEnableAnalyzers&gt;false&lt;/…&gt;</c>，
/// SDK 把它写进 <c>build_property.StronglyTypedIdEnableAnalyzers</c>，分析器读到「否」就不注册任何诊断动作。
/// 只消费 <c>IStronglyTypedId&lt;,&gt;</c> 而不声明 Id 的项目要背 26 条规则的噪音，逐条 NoWarn 很笨拙，故提供这个总开关。
/// </remarks>
public class StronglyTypedIdAnalyzerMutingTests
{
    /// <summary>
    /// 故意违反两条 Error 级规则：参数可空（STIA0006）、参数名不是 <c>Value</c>（STIA0007）。
    /// </summary>
    /// <remarks>
    /// 选 Error 级而不是 Info 级的规则，是为了不受「命名风格提示默认被屏蔽」的影响。
    /// 静音要证明的是一条都不报，用会被屏蔽的规则去证明等于什么都没证明。
    /// </remarks>
    private const string SourceWithViolations = """
        using System;

        namespace Len.StronglyTypedId.Tests;

        [StronglyTypedId]
        public partial record struct OrderId(Guid? Value);

        [StronglyTypedId]
        public partial record struct InvoiceId(Guid Value1);
        """;

    /// <summary>
    /// 模拟 SDK 注入的 analyzer config：<c>build_property.*</c> 恒在 <c>is_global</c> 段里。
    /// </summary>
    private static string EditorConfig(string value)
        => $"""
            is_global = true
            build_property.StronglyTypedIdEnableAnalyzers = {value}
            """;

    /// <summary>
    /// 源码里那两条违例的期望：STIA0006（参数可空）与 STIA0007（参数名不是 <c>Value</c>）。
    /// </summary>
    private static DiagnosticResult[] ExpectedViolations()
        =>
        [
            Verify.Diagnostic(Descriptors.ParameterCannotBeNullable)
                .WithSpan(6, 38, 6, 43).WithArguments("Guid?"),
            Verify.Diagnostic(Descriptors.ParameterNameMustBeValue)
                .WithSpan(9, 45, 9, 51).WithArguments("Value1"),
        ];

    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostics_WhenMutingPropertyIsAbsent()
    {
        // 基线：属性缺失 → 行为不变（默认开启）。
        var expected = ExpectedViolations();

        await Verify.VerifyAnalyzerAsync(SourceWithViolations, expected);
    }

    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostics_WhenMutingPropertyIsTrue()
    {
        // 属性显式为「是」→ 与缺失时同一条诊断照报。
        var expected = ExpectedViolations();

        await Verify.VerifyAnalyzerWithRawEditorConfigAsync(SourceWithViolations, EditorConfig("true"), expected);
    }

    [Fact]
    public async Task AnalyzingCode_Should_ReturnNoDiagnostics_WhenMutingPropertyIsFalse()
    {
        // 属性为「否」→ 一条都不报。
        await Verify.VerifyAnalyzerWithRawEditorConfigAsync(SourceWithViolations, EditorConfig("false"));
    }

    [Theory]
    [InlineData("FALSE")]
    [InlineData("False")]
    [InlineData("false")]
    public async Task AnalyzingCode_Should_ReturnNoDiagnostics_WhenMutingPropertyIsFalse_RegardlessOfCasing(string value)
    {
        await Verify.VerifyAnalyzerWithRawEditorConfigAsync(SourceWithViolations, EditorConfig(value));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("no")]
    [InlineData("off")]
    public async Task AnalyzingCode_Should_ReturnNoDiagnostics_WhenMutingPropertyIsFalsy(string value)
    {
        await Verify.VerifyAnalyzerWithRawEditorConfigAsync(SourceWithViolations, EditorConfig(value));
    }

    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostics_WhenMutingPropertyHasUnrelatedValue()
    {
        // 只认「否」那一个取值；其它值一律按「开启」处理，避免用户写错一个词就静默丢掉全部诊断。
        var expected = ExpectedViolations();

        await Verify.VerifyAnalyzerWithRawEditorConfigAsync(SourceWithViolations, EditorConfig("yes"), expected);
    }

    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostics_WhenMutingPropertyIsEmpty()
    {
        var expected = ExpectedViolations();

        await Verify.VerifyAnalyzerWithRawEditorConfigAsync(SourceWithViolations, EditorConfig(""), expected);
    }
}
