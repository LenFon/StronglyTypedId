using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using System.Globalization;

namespace Len.StronglyTypedId.Analyzers;

public class StronglyTypedIdAnalyzerIntegrationTests
{
    #region 请求了未引用的集成（STIA0019）

    /// <summary>
    /// 显式请求 Dapper 集成，但编译里没有引用 Dapper：该集成不会产出任何代码，必须可见地提示。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenRequestedIntegrationIsNotReferenced()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Integrations = StronglyTypedIdIntegrations.Dapper)]
            public partial record struct OrderId(Guid Value);
            """";
        var expected = Verify.Diagnostic(Descriptors.IntegrationUnavailable)
            .WithArguments("OrderId", "Dapper", "Dapper")
            .WithSpan(5, 18, 5, 67);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// 多个 Id 请求同一个未引用的集成时只报一条：逐 Id 各报一条会在 N 个 Id 时炸出成百条同义诊断。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnSingleDiagnostic_WhenSeveralIdsRequestTheSameUnavailableIntegration()
    {
        // 四个 Id 刻意用四种基元：同命名空间同基元会额外触发 STIA0016，干扰「只报一条」的判定。
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Integrations = StronglyTypedIdIntegrations.Dapper)]
            public partial record struct OrderId(Guid Value);

            [StronglyTypedId(Integrations = StronglyTypedIdIntegrations.Dapper)]
            public partial record struct InvoiceId(int Value);

            [StronglyTypedId(Integrations = StronglyTypedIdIntegrations.Dapper)]
            public partial record struct ProductId(string Value);

            [StronglyTypedId(Integrations = StronglyTypedIdIntegrations.Dapper)]
            public partial record struct ReceiptId(long Value);
            """";
        // 请求方超过三个时截断并附总数，避免一条消息里堆几十个类型名。
        var expected = Verify.Diagnostic(Descriptors.IntegrationUnavailable)
            .WithArguments("OrderId, InvoiceId, ProductId … (4)", "Dapper", "Dapper")
            .WithSpan(5, 18, 5, 67);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// 请求了确实已引用的集成（<c>System.Text.Json</c> 属于基础共享框架）时不报告。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenRequestedIntegrationIsReferenced()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Integrations = StronglyTypedIdIntegrations.SystemTextJson)]
            public partial record struct OrderId(Guid Value);
            """";
        await Verify.VerifyAnalyzerAsync(code);
    }

    /// <summary>
    /// <c>None</c> 只关不请求，掩码归一后为空，不应报告任何集成缺失。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenIntegrationsIsNone()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Integrations = StronglyTypedIdIntegrations.None)]
            public partial record struct OrderId(Guid Value);
            """";
        await Verify.VerifyAnalyzerAsync(code);
    }

    /// <summary>
    /// 程序集级默认值只报一次：若放进逐类型回调，两个 Id 会各刷一条同样的诊断。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnSingleDiagnostic_WhenAssemblyDefaultRequestsUnavailableIntegration()
    {
        var code = """"
            using System;

            [assembly: Len.StronglyTypedId.StronglyTypedIdDefaults(Integrations = Len.StronglyTypedId.StronglyTypedIdIntegrations.Dapper)]

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct OrderId(Guid Value);

            [StronglyTypedId]
            public partial record struct InvoiceId(int Value);
            """";
        // 程序集名取测试基建的默认值 TestProject；「只报一条」才是本用例的判别点
        // （若放进逐类型回调，两个 Id 会各报一条，此处立刻失败）。
        var expected = Verify.Diagnostic(Descriptors.IntegrationUnavailable)
            .WithArguments("TestProject", "Dapper", "Dapper")
            .WithSpan(3, 12, 3, 126);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    #endregion

    #region 集成掩码含未定义的位（STIA0024）

    /// <summary>
    /// 掩码里超出已知标志的位既不匹配任何集成、也不会被生成器读到 —— 不报就是「写了却什么都没发生」。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenIntegrationsHasUndefinedBits()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Integrations = (StronglyTypedIdIntegrations)2048)]
            public partial record struct OrderId(Guid Value);
            """";
        var expected = Verify.Diagnostic(Descriptors.UnknownIntegration)
            .WithArguments("OrderId", 2048, 1022)
            .WithSpan(5, 18, 5, 66);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// 程序集级默认值走的是编译结束回调，是另一条上报路径，同样要能报出来。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenAssemblyDefaultHasUndefinedBits()
    {
        var code = """"
            using System;

            [assembly: Len.StronglyTypedId.StronglyTypedIdDefaults(Integrations = (Len.StronglyTypedId.StronglyTypedIdIntegrations)2048)]

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct OrderId(Guid Value);
            """";
        var expected = Verify.Diagnostic(Descriptors.UnknownIntegration)
            .WithArguments("TestProject", 2048, 1022)
            .WithSpan(3, 12, 3, 125);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }


    #endregion
}
