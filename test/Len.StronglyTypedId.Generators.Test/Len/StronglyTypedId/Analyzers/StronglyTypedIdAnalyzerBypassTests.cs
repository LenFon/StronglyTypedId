using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using System.Globalization;

namespace Len.StronglyTypedId.Analyzers;

public class StronglyTypedIdAnalyzerBypassTests
{
    #region 验证器可被绕过（STIA0014）

    /// <summary>
    /// 规则默认关闭：即便「public + Validator」这种可被绕过的组合，未在 <c>.editorconfig</c> 调级时也不上报。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenValidatorBypassRuleIsNotEnabled()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Validator = "Validate")]
            public partial record struct OrderId(Guid Value)
            {
                private static bool Validate(Guid value) => value != Guid.Empty;
            }
            """";
        await Verify.VerifyAnalyzerAsync(code);
    }

    /// <summary>
    /// 经 <c>.editorconfig</c> 启用后，对外可见且配了验证器的 Id 上报 STIA0014。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenValidatorBypassRuleIsEnabled()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Validator = "Validate")]
            public partial record struct OrderId(Guid Value)
            {
                private static bool Validate(Guid value) => value != Guid.Empty;
            }
            """";
        var expected = Verify.Diagnostic(Descriptors.ValidatorCanBeBypassed)
            .WithArguments("OrderId")
            .WithSpan(5, 1, 9, 2);
        await Verify.VerifyAnalyzerWithEditorConfigAsync(code, EnableValidatorBypassRule, expected);
    }

    /// <summary>
    /// 类型不可跨程序集访问时，主构造函数同样不可用，验证器无法被绕过，不上报。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenValidatorOwnerIsInternal()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Validator = "Validate")]
            internal partial record struct OrderId(Guid Value)
            {
                private static bool Validate(Guid value) => value != Guid.Empty;
            }
            """";
        await Verify.VerifyAnalyzerWithEditorConfigAsync(code, EnableValidatorBypassRule);
    }

    /// <summary>
    /// 未配置验证器时不存在「绕过」问题，即便类型对外可见也不上报。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenNoValidatorIsConfigured()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct OrderId(Guid Value);
            """";
        await Verify.VerifyAnalyzerWithEditorConfigAsync(code, EnableValidatorBypassRule);
    }

    /// <summary>
    /// 嵌套在 <c>internal</c> 容器里的 <c>public</c> 类型实际不可跨程序集访问，不应上报。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenValidatorOwnerIsNestedInInternalContainer()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            internal static partial class Container
            {
                [StronglyTypedId(Validator = "Validate")]
                public partial record struct OrderId(Guid Value)
                {
                    private static bool Validate(Guid value) => value != Guid.Empty;
                }
            }
            """";
        await Verify.VerifyAnalyzerWithEditorConfigAsync(code, EnableValidatorBypassRule);
    }

    /// <summary>
    /// 启用 STIA0014 所用的 <c>.editorconfig</c> 片段。
    /// </summary>
    private const string EnableValidatorBypassRule = """
        root = true

        [*.cs]
        dotnet_diagnostic.STIA0014.severity = suggestion
        """;

    #endregion

    #region 装配级默认验证器（STIA0010 的装配级形态）

    /// <summary>
    /// 装配级 <c>[assembly: StronglyTypedIdDefaults(Validator = "Validate")]</c> 作用于全体 Id，当某个 Id
    /// 自带匹配的静态验证器方法时不报告任何诊断。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenAssemblyDefaultValidatorMatches()
    {
        var code = """"
            using System;

            [assembly: Len.StronglyTypedId.StronglyTypedIdDefaults(Validator = "Validate")]

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct OrderId(Guid Value)
            {
                private static bool Validate(Guid value) => value != Guid.Empty;
            }
            """";
        await Verify.VerifyAnalyzerAsync(code);
    }

    /// <summary>
    /// 装配级默认验证器要求名为 <c>Validate</c> 的方法，但本程序集的 Id 没有该方法时应报告 STIA0010。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenAssemblyDefaultValidatorHasNoMatchingMethod()
    {
        var code = """"
            using System;

            [assembly: Len.StronglyTypedId.StronglyTypedIdDefaults(Validator = "Validate")]

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct OrderId(Guid Value);
            """";
        var expected = Verify.Diagnostic(Descriptors.ValidatorReferenceInvalid)
            .WithArguments("Validate", "System.Guid")
            .WithSpan(7, 1, 8, 50);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    #endregion
}
