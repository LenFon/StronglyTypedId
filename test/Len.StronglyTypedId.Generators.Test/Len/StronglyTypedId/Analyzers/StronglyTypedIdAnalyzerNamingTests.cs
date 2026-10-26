using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using System.Globalization;

namespace Len.StronglyTypedId.Analyzers;

public class StronglyTypedIdAnalyzerNamingTests
{
    #region 命名约定（STIA0015）

    /// <summary>
    /// 类型名不以 <c>Id</c> 结尾时提示改名，并给出建议名。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenNameDoesNotEndWithId()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct Order(Guid Value);
            """";
        var expected = Verify.Diagnostic(Descriptors.TypeNameShouldEndWithId)
            .WithArguments("Order", "OrderId", "Id")
            .WithSpan(6, 30, 6, 35);
        await Verify.VerifyAnalyzerWithStyleHintsAsync(code, expected);
    }

    /// <summary>
    /// 后缀按序数比较：<c>OrderID</c> 不合规（.NET 命名约定是 <c>Id</c>），建议名须替换而非追加。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenNameEndsWithUppercaseId()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct OrderID(Guid Value);
            """";
        var expected = Verify.Diagnostic(Descriptors.TypeNameShouldEndWithId)
            .WithArguments("OrderID", "OrderId", "Id")
            .WithSpan(6, 30, 6, 37);
        await Verify.VerifyAnalyzerWithStyleHintsAsync(code, expected);
    }

    /// <summary>
    /// 名字本身就是 <c>Id</c> 时合规，不报告。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenNameIsExactlyId()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct Id(Guid Value);
            """";
        await Verify.VerifyAnalyzerWithStyleHintsAsync(code);
    }

    /// <summary>
    /// 后缀可由 <c>.editorconfig</c> 的 <c>dotnet_diagnostic.STIA0015.suffix</c> 改成任意串；改完后提示文字与建议名都跟随新后缀。
    /// 这里把后缀改成 <c>Key</c>：<c>Order</c> 不再以 <c>Key</c> 结尾，建议名须为 <c>OrderKey</c>。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_RespectConfigurableSuffix_WhenSetInEditorConfig()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct Order(Guid Value);
            """";
        const string editorConfig = """
            [*.cs]
            dotnet_diagnostic.STIA0015.suffix = Key
            """;
        var expected = Verify.Diagnostic(Descriptors.TypeNameShouldEndWithId)
            .WithArguments("Order", "OrderKey", "Key")
            .WithSpan(6, 30, 6, 35);
        await Verify.VerifyAnalyzerWithEditorConfigAsync(code, editorConfig, expected);
    }

    /// <summary>
    /// 后缀改为 <c>Key</c> 后，以 <c>Key</c> 结尾的名字（如 <c>OrderKey</c>）应判为合规、不再报告
    /// 证明后缀确实改变了判定口径，而非只在提示文字里换个词。注意：<c>OrderKey</c> 在默认 <c>Id</c> 后缀下反而是不合规的。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_TreatSuffixCompliantNameAsClean_WhenSuffixConfigured()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct OrderKey(Guid Value);
            """";
        const string editorConfig = """
            [*.cs]
            dotnet_diagnostic.STIA0015.suffix = Key
            """;
        await Verify.VerifyAnalyzerWithEditorConfigAsync(code, editorConfig);
    }

    #endregion

    #region 同命名空间共用基元（STIA0016）

    /// <summary>
    /// 同一命名空间下两个 Id 共用同一基元时，只在组内名字序最小的那个上报告一次。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnSingleDiagnostic_WhenTwoIdsSharePrimitive()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct InvoiceId(Guid Value);

            [StronglyTypedId]
            public partial record struct OrderId(Guid Value);
            """";
        var expected = Verify.Diagnostic(Descriptors.DuplicatePrimitiveType)
            .WithArguments("InvoiceId", "System.Guid", 1, "Len.StronglyTypedId.Tests")
            .WithSpan(5, 1, 6, 52);
        await Verify.VerifyAnalyzerWithStyleHintsAsync(code, expected);
    }

    /// <summary>
    /// 三个同基元 Id 仍然只报一条，实参给出「另外还有几个」。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnSingleDiagnostic_WhenThreeIdsSharePrimitive()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct AlphaId(Guid Value);

            [StronglyTypedId]
            public partial record struct BetaId(Guid Value);

            [StronglyTypedId]
            public partial record struct GammaId(Guid Value);
            """";
        var expected = Verify.Diagnostic(Descriptors.DuplicatePrimitiveType)
            .WithArguments("AlphaId", "System.Guid", 2, "Len.StronglyTypedId.Tests")
            .WithSpan(5, 1, 6, 50);
        await Verify.VerifyAnalyzerWithStyleHintsAsync(code, expected);
    }

    /// <summary>
    /// 嵌套类型与顶层类型同处一个命名空间，规范文本形同样无法区分，因此一并参与分组。
    /// （此前按「嵌套 Id 的作用域由包含类型界定」排除嵌套类型，但那只是声明位置的差别，
    /// 落到存储与传输层面两者仍然同形。）
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnSingleDiagnostic_WhenNestedIdSharesPrimitiveWithTopLevelId()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct CustomerId(Guid Value);

            public partial class Orders
            {
                [StronglyTypedId]
                public partial record struct OrderId(Guid Value);
            }
            """";
        var expected = Verify.Diagnostic(Descriptors.DuplicatePrimitiveType)
            .WithArguments("CustomerId", "System.Guid", 1, "Len.StronglyTypedId.Tests")
            .WithSpan(5, 1, 6, 53);
        await Verify.VerifyAnalyzerWithStyleHintsAsync(code, expected);
    }

    /// <summary>
    /// 两层嵌套也要被扫到：只递归一层同样会漏。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnSingleDiagnostic_WhenDeeplyNestedIdSharesPrimitive()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct CustomerId(Guid Value);

            public partial class Outer
            {
                public partial class Inner
                {
                    [StronglyTypedId]
                    public partial record struct OrderId(Guid Value);
                }
            }
            """";
        var expected = Verify.Diagnostic(Descriptors.DuplicatePrimitiveType)
            .WithArguments("CustomerId", "System.Guid", 1, "Len.StronglyTypedId.Tests")
            .WithSpan(5, 1, 6, 53);
        await Verify.VerifyAnalyzerWithStyleHintsAsync(code, expected);
    }

    /// <summary>
    /// 基元类型不同时不存在「同形」问题，不报告。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenIdsUseDifferentPrimitives()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct OrderId(Guid Value);

            [StronglyTypedId]
            public partial record struct CustomerId(int Value);
            """";
        await Verify.VerifyAnalyzerWithStyleHintsAsync(code);
    }

    /// <summary>
    /// 分处不同命名空间时各自独立，不报告。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenSamePrimitiveLivesInDifferentNamespaces()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests.Sales
            {
                [StronglyTypedId]
                public partial record struct OrderId(Guid Value);
            }

            namespace Len.StronglyTypedId.Tests.Billing
            {
                [StronglyTypedId]
                public partial record struct InvoiceId(Guid Value);
            }
            """";
        await Verify.VerifyAnalyzerWithStyleHintsAsync(code);
    }

    #endregion
}
