using FluentAssertions;
using Microsoft.CodeAnalysis.Testing;

namespace Len.StronglyTypedId.Analyzers;

/// <summary>
/// <c>[JsonNumberHandling]</c> 标在强类型 Id 上的诊断（STIA0033）。
/// </summary>
/// <remarks>
/// 这条规则拦的是「代码能编译、一序列化就抛 <c>InvalidOperationException</c>」的写法：STJ 在构建
/// <c>JsonTypeInfo</c> 时按 CLR 类型判定该特性的合法性，不看有没有转换器。判据必须与 STJ 的判定面一致，
/// 否则要么漏报（用户到运行期才发现），要么误报（把合法的裸 <c>int</c> 属性判成错误）。
/// </remarks>
public class StronglyTypedIdAnalyzerJsonNumberHandlingTests
{
    #region 成员级

    /// <summary>
    /// 属性类型是 Id：STJ 会抛 <c>InvalidOperationException</c>。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReportJsonNumberHandling_WhenPropertyTypeIsId()
    {
        var code = """"
            using System;
            using System.Text.Json.Serialization;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct OrderId(int Value);

            public class Holder
            {
                [JsonNumberHandling(JsonNumberHandling.WriteAsString)]
                public OrderId Id { get; set; }
            }
            """";
        var expected = Verify.Diagnostic(Descriptors.JsonNumberHandlingOnId)
            .WithArguments("Len.StronglyTypedId.Tests.OrderId")
            .WithSpan(11, 6, 11, 58);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// <c>Nullable&lt;T&gt;</c> 要拆壳：STJ 对 <c>OrderId?</c> 的判定与裸类型一致。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReportJsonNumberHandling_WhenPropertyTypeIsNullableId()
    {
        var code = """"
            using System;
            using System.Text.Json.Serialization;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct OrderId(int Value);

            public class Holder
            {
                [JsonNumberHandling(JsonNumberHandling.WriteAsString)]
                public OrderId? Id { get; set; }
            }
            """";
        var expected = Verify.Diagnostic(Descriptors.JsonNumberHandlingOnId)
            .WithArguments("Len.StronglyTypedId.Tests.OrderId?")
            .WithSpan(11, 6, 11, 58);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// 容器内的 Id 同样报：<c>List&lt;OrderId&gt;</c> 的元素类型受同一套判定约束。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReportJsonNumberHandling_WhenPropertyTypeIsListOfIds()
    {
        var code = """"
            using System;
            using System.Collections.Generic;
            using System.Text.Json.Serialization;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct OrderId(int Value);

            public class Holder
            {
                [JsonNumberHandling(JsonNumberHandling.WriteAsString)]
                public List<OrderId> Ids { get; set; }
            }
            """";
        var expected = Verify.Diagnostic(Descriptors.JsonNumberHandlingOnId)
            .WithArguments("System.Collections.Generic.List<Len.StronglyTypedId.Tests.OrderId>")
            .WithSpan(12, 6, 12, 58);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// 字段与索引器同样在 <c>JsonNumberHandlingAttribute</c> 的 <c>AttributeUsage</c> 里。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReportJsonNumberHandling_WhenFieldTypeIsId()
    {
        var code = """"
            using System;
            using System.Text.Json.Serialization;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct OrderId(int Value);

            public class Holder
            {
                [JsonNumberHandling(JsonNumberHandling.WriteAsString)]
                public OrderId Id;
            }
            """";
        var expected = Verify.Diagnostic(Descriptors.JsonNumberHandlingOnId)
            .WithArguments("Len.StronglyTypedId.Tests.OrderId")
            .WithSpan(11, 6, 11, 58);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// 裸 <c>int</c> 属性是合法用法，不能报。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_NotReportJsonNumberHandling_WhenPropertyTypeIsPlainNumber()
    {
        var code = """"
            using System;
            using System.Text.Json.Serialization;

            namespace Len.StronglyTypedId.Tests;

            public class Holder
            {
                [JsonNumberHandling(JsonNumberHandling.WriteAsString)]
                public int Id { get; set; }
            }
            """";
        await Verify.VerifyAnalyzerAsync(code);
    }

    /// <summary>
    /// <c>Guid</c> / <c>string</c> 属性本身就会被 STJ 判为非法，但那是 STJ 的规则，不是本库的：
    /// 本规则只针对强类型 Id，避免把「与本库无关的写法」也算到本库头上。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_NotReportJsonNumberHandling_WhenPropertyTypeIsGuid()
    {
        var code = """"
            using System;
            using System.Text.Json.Serialization;

            namespace Len.StronglyTypedId.Tests;

            public class Holder
            {
                [JsonNumberHandling(JsonNumberHandling.WriteAsString)]
                public Guid Id { get; set; }
            }
            """";
        await Verify.VerifyAnalyzerAsync(code);
    }

    #endregion

    #region 类型级

    /// <summary>
    /// 标在 Id 类型自身上：实测同样抛 <c>InvalidOperationException</c>，不是「静默无效」。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReportJsonNumberHandling_WhenAppliedToIdType()
    {
        var code = """"
            using System;
            using System.Text.Json.Serialization;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            [JsonNumberHandling(JsonNumberHandling.WriteAsString)]
            public partial record struct OrderId(int Value);
            """";
        var expected = Verify.Diagnostic(Descriptors.JsonNumberHandlingOnId)
            .WithArguments("Len.StronglyTypedId.Tests.OrderId")
            .WithSpan(7, 2, 7, 54);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// 标在普通类型上不报：那与本库无关。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_NotReportJsonNumberHandling_WhenAppliedToPlainType()
    {
        var code = """"
            using System;
            using System.Text.Json.Serialization;

            namespace Len.StronglyTypedId.Tests;

            [JsonNumberHandling(JsonNumberHandling.WriteAsString)]
            public readonly record struct Plain(int Value);
            """";
        await Verify.VerifyAnalyzerAsync(code);
    }

    #endregion

    #region 其它

    /// <summary>
    /// 没有 <c>[JsonNumberHandling]</c> 时一条都不报。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_NotReportJsonNumberHandling_WhenAttributeIsAbsent()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct OrderId(int Value);

            public class Holder
            {
                public OrderId Id { get; set; }
            }
            """";
        await Verify.VerifyAnalyzerAsync(code);
    }

    /// <summary>
    /// 诊断消息必须点明「改用全局选项」，否则用户删掉特性后只会得到静默忽略。
    /// </summary>
    [Fact]
    public void JsonNumberHandlingOnIdMessage_Should_PointAtGlobalOption()
    {
        Descriptors.JsonNumberHandlingOnId.MessageFormat.ToString()
            .Should().Contain("NumberHandling")
            .And.Contain("InvalidOperationException");
    }

    #endregion
}
