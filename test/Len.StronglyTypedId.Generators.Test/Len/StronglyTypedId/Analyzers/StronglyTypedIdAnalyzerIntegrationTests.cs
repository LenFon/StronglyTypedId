using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using System.Globalization;
using System.Reflection;

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
    /// MessagePack 集成同样受门控：没引用 <c>MessagePack</c> 时不产出格式化器，必须报 STIA0019。
    /// </summary>
    /// <remarks>
    /// 这条用例锁的是一个曾经漏掉的集成：对照表原先只登记了七个，MessagePack 与 MongoDb 虽在
    /// <c>Integrations = All</c> 里却无人校验，请求它们时既不生成代码也不报警，与文档承诺相反。
    /// </remarks>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenMessagePackIsNotReferenced()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Integrations = StronglyTypedIdIntegrations.MessagePack)]
            public partial record struct OrderId(Guid Value);
            """";
        var expected = Verify.Diagnostic(Descriptors.IntegrationUnavailable)
            .WithArguments("OrderId", "MessagePack", "MessagePack")
            .WithSpan(5, 18, 5, 72);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// MongoDB 集成同理：没引用 <c>MongoDB.Bson</c> 时序列化器不会生成，必须报 STIA0019。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenMongoDbIsNotReferenced()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Integrations = StronglyTypedIdIntegrations.MongoDb)]
            public partial record struct OrderId(Guid Value);
            """";
        var expected = Verify.Diagnostic(Descriptors.IntegrationUnavailable)
            .WithArguments("OrderId", "MongoDb", "MongoDB.Bson")
            .WithSpan(5, 18, 5, 68);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// 对照表必须覆盖 <see cref="IntegrationFlags.All"/> 的每一位：漏登记的集成会静默失效，
    /// 而逐条行为用例只能守住当时写下的那几个，新增集成时仍会重演同一个缺口。
    /// </summary>
    [Fact]
    public void IntegrationRequirements_Should_CoverEveryFlagInAll()
    {
        var requirements = (IEnumerable<(int Flag, string AssemblyName, string DisplayName)>)typeof(StronglyTypedIdAnalyzer)
            .GetField("_integrationRequirements", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;

        var covered = requirements.Aggregate(0, (mask, requirement) => mask | requirement.Flag);

        covered.Should().Be(IntegrationFlags.All);
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
