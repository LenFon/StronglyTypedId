using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using System.Globalization;

namespace Len.StronglyTypedId.Analyzers;

public class StronglyTypedIdAnalyzerTests
{
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;
            
            [StronglyTypedId]
            public partial record struct OrderId(Guid Value);
            """";

        await Verify.VerifyAnalyzerAsync(code);
    }

    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenSegmentedPartialDeclaration()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct OrderId(Guid Value);

            public partial record struct OrderId
            {
                public bool IsEmpty => Value == Guid.Empty;
            }
            """";

        await Verify.VerifyAnalyzerAsync(code);
    }

    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenMoreThanTwoPartialSegments()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct OrderId(Guid Value);

            public partial record struct OrderId
            {
                public bool IsEmpty => Value == Guid.Empty;
            }

            public partial record struct OrderId
            {
                public Guid Unwrap() => Value;
            }
            """";

        await Verify.VerifyAnalyzerAsync(code);
    }

    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenAbstract()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;
            
            [StronglyTypedId]
            public abstract partial record struct OrderId(Guid Value);
            """";

        var expected = Verify.Diagnostic(Descriptors.TypeCannotBeAbstract)
            .WithSpan(5, 1, 6, 59).WithArguments("OrderId");

        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenParameterNullable()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;
            
            [StronglyTypedId]
            public partial record struct OrderId(Guid? Value);
            """";

        var expected = Verify.Diagnostic(Descriptors.ParameterCannotBeNullable)
            .WithSpan(6, 38, 6, 43).WithArguments("Guid?");

        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenParameterNameCannotBeValue()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;
            
            [StronglyTypedId]
            public partial record struct OrderId(Guid Value1);
            """";

        var expected = Verify.Diagnostic(Descriptors.ParameterNameMustBeValue)
            .WithSpan(6, 43, 6, 49).WithArguments("Value1");

        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenNotPrimaryConstructor()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;
            
            [StronglyTypedId]
            public partial record struct OrderId;
            """";

        var expected = Verify.Diagnostic(Descriptors.TypeMustHaveSingleParameterPrimaryConstructor)
            .WithSpan(5, 1, 6, 38).WithArguments("OrderId");

        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenMultipleParameter()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;
            
            [StronglyTypedId]
            public partial record struct OrderId(Guid Value, Guid Value2);
            """";

        var expected = Verify.Diagnostic(Descriptors.TypeMustHaveSingleParameterPrimaryConstructor)
            .WithSpan(5, 1, 6, 63).WithArguments("OrderId");

        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenSegmentedPartialDeclarationHasNoPrimaryConstructor()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct OrderId;

            public partial record struct OrderId
            {
                public bool IsEmpty => false;
            }
            """";

        // 主构造函数整体缺失，报告一次并定位到首个声明段；而非每个声明段各报一次。
        var expected = Verify.Diagnostic(Descriptors.TypeMustHaveSingleParameterPrimaryConstructor)
            .WithSpan(5, 1, 6, 38).WithArguments("OrderId");

        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    [Fact]
    public async Task AnalyzingCode_Should_ReturnSingleDiagnostic_WhenSegmentedPartialDeclarationHasInvalidParameter()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct OrderId(Guid Value1);

            public partial record struct OrderId
            {
                public bool IsEmpty => false;
            }
            """";

        // 只有含主构造函数的那一段参与参数校验，因此非法参数只报告一次，
        // 不会在后续声明段上再叠加一条 STIAO005。
        var expected = Verify.Diagnostic(Descriptors.ParameterNameMustBeValue)
            .WithSpan(6, 43, 6, 49).WithArguments("Value1");

        await Verify.VerifyAnalyzerAsync(code, expected);
    }


    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenGeneric()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;
            
            [StronglyTypedId]
            public partial record struct OrderId<T>(Guid Value);
            """";

        var expected = Verify.Diagnostic(Descriptors.TypeCannotBeGeneric)
            .WithSpan(5, 1, 6, 53).WithArguments("OrderId");

        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenParameterTypeIsInvalid()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;
            
            [StronglyTypedId]
            public partial record struct OrderId(bool Value);
            """";

        var expected = Verify.Diagnostic(Descriptors.ParameterTypeIsInvalid)
            .WithSpan(6, 38, 6, 42).WithArguments("bool");

        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// 回归用例：使用者自定义的 <c>X.Guid</c> 并不是 BCL 的 <see cref="System.Guid"/>，必须报 STIAO008。
    /// </summary>
    /// <remarks>
    /// 基元类型判据曾只比简单名：这种同名类型会被当作合法基元放行，生成器随后把它嵌进生成代码，
    /// 使用者最终看到的是若干条报在<b>生成文件</b>里的 CS0315（类型不满足泛型约束），
    /// 而不是一条指向自己源码的 STIAO008。
    /// </remarks>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenParameterTypeSharesSimpleNameWithBclType()
    {
        var code = """"
            namespace Probe.Fake
            {
                public struct Guid
                {
                }
            }

            namespace Len.StronglyTypedId.Tests
            {
                [StronglyTypedId]
                public partial record struct OrderId(Probe.Fake.Guid Value);
            }
            """";

        var expected = Verify.Diagnostic(Descriptors.ParameterTypeIsInvalid)
            .WithSpan(11, 42, 11, 57).WithArguments("Probe.Fake.Guid");

        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenCannotPartial()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;
            
            [StronglyTypedId]
            public record struct OrderId(Guid Value);
            """";

        var expected = Verify.Diagnostic(Descriptors.TypeMustBePartial)
            .WithSpan(5, 1, 6, 42).WithArguments("OrderId");

        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenCannotNamespace()
    {
        var code = """"
            using System;
            using Len.StronglyTypedId;

            [StronglyTypedId]
            public partial record struct OrderId(Guid Value);
            """";

        var expected = Verify.Diagnostic(Descriptors.TypeCannotBeNestedAndMustHaveNamespace)
            .WithSpan(4, 1, 5, 50).WithArguments("OrderId");

        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenNested()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;
            
            public class Test
            {
                [StronglyTypedId]
                public partial record struct OrderId(Guid Value);
            }
            """";

        var expected = Verify.Diagnostic(Descriptors.TypeCannotBeNestedAndMustHaveNamespace)
            .WithSpan(7, 5, 8, 54).WithArguments("OrderId");

        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenCannotBeRecord()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;
            
            [StronglyTypedId]
            public class Test{}
            """";

        var expected = Verify.Diagnostic(Descriptors.TypeMustBeRecord)
            .WithSpan(5, 1, 6, 20).WithArguments("Test");

        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    [Fact]
    public async Task AnalyzingCode_Should_Skip()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;
            
            public class Test{}
            """";

        await Verify.VerifyAnalyzerAsync(code);
    }

    public static TheoryData<DiagnosticDescriptor, string> ParameterDiagnostics => new()
    {
        { Descriptors.ParameterNameMustBeValue, "Value1" },
        { Descriptors.ParameterTypeIsInvalid, "bool" },
    };

    /// <summary>
    /// 回归用例：STIAO007 / STIAO008 的文案必须保留 <c>{0}</c> 占位符。
    /// </summary>
    /// <remarks>
    /// 分析器实现传入了实参（参数名 / 参数类型），文案缺少占位符时它们被静默丢弃，
    /// 诊断里看不到究竟是哪个名字或哪个类型。断言「渲染文本包含实参」与当前区域性无关：
    /// 不显式指定区域性时 <see cref="LocalizedString"/> 回退到内嵌的 en 资源。
    /// </remarks>
    [Theory]
    [MemberData(nameof(ParameterDiagnostics))]
    public void Diagnostic_MessageFormat_Should_ContainArgument(DiagnosticDescriptor descriptor, string argument)
    {
        var message = string.Format(
            CultureInfo.InvariantCulture,
            descriptor.MessageFormat.ToString(),
            argument);

        message.Should().Contain(argument);
    }
}
