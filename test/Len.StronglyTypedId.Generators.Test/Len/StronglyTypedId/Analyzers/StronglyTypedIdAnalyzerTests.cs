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

        var expected = Verify.Diagnostic(Descriptors.TypeMustHaveNamespace)
            .WithSpan(4, 1, 5, 50).WithArguments("OrderId");

        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenContainingTypeCannotBePartial()
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

        // 容器缺 partial：生成代码要补的成员只能嵌回容器里，而没写 partial 的容器重开不了。
        // 诊断落在整个容器声明上（要改的是容器，不是 Id），故 {0} 是容器名。
        var expected = Verify.Diagnostic(Descriptors.ContainingTypeMustBePartial)
            .WithSpan(5, 1, 9, 2).WithArguments("Test");

        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    [Theory]
    [InlineData("public partial class Container")]
    [InlineData("public partial struct Container")]
    [InlineData("public partial record Container")]
    [InlineData("public partial interface Container")]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenNestedInPartialContainer(string container)
    {
        var code = $$""""
            using System;

            namespace Len.StronglyTypedId.Tests;

            {{container}}
            {
                [StronglyTypedId]
                public partial record struct OrderId(Guid Value);
            }
            """";

        await Verify.VerifyAnalyzerAsync(code);
    }

    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenContainingTypeIsGeneric()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            public partial class Container<T>
            {
                [StronglyTypedId]
                public partial record struct OrderId(Guid Value);
            }
            """";

        // 泛型容器同样属于「重开不了」：重开它必须复现类型参数表与约束，而且容器一旦带上类型参数，
        // Id 的全名就成了 Container<T>.OrderId（含 <>，连 hint name 都不合法）。故不支持，
        // 复用「不能是泛型」这条既有规则的措辞，{0} 取容器名，读起来依旧自洽。
        var expected = Verify.Diagnostic(Descriptors.TypeCannotBeGeneric)
            .WithSpan(5, 1, 9, 2).WithArguments("Container");

        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenContainingTypeIsFileLocal()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            file partial class Container
            {
                [StronglyTypedId]
                public partial record struct OrderId(Guid Value);
            }
            """";

        // file 本地类型即使写了 partial 也没用：它的各段只能待在同一个文件里，而生成代码在另一个文件中。
        // 与「容器缺 partial」共用一条规则，因为要求同出一源 —— 容器必须能被生成代码重开一次。
        var expected = Verify.Diagnostic(Descriptors.ContainingTypeMustBePartial)
            .WithSpan(5, 1, 9, 2).WithArguments("Container");

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

    #region 验证器引用校验（STIAO010）

    /// <summary>
    /// <c>[StronglyTypedId(Validator = nameof(Validate))]</c> 指向的方法不存在。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenValidatorMethodMissing()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Validator = nameof(Validate))]
            public partial record struct OrderId(Guid Value);
            """";
        var expected = Verify.Diagnostic(Descriptors.ValidatorReferenceInvalid)
            .WithArguments("Validate", "System.Guid")
            .WithSpan(5, 18, 5, 46);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// 验证器方法返回类型不是 <see cref="bool"/>。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenValidatorMethodHasWrongReturnType()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Validator = nameof(Validate))]
            public partial record struct OrderId(Guid Value)
            {
                private static Guid Validate(Guid value) => value;
            }
            """";
        var expected = Verify.Diagnostic(Descriptors.ValidatorReferenceInvalid)
            .WithArguments("Validate", "System.Guid")
            .WithSpan(5, 18, 5, 46);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// 验证器方法的参数类型与基元 Id 类型不一致。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenValidatorMethodHasWrongParameterType()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Validator = nameof(Validate))]
            public partial record struct OrderId(Guid Value)
            {
                private static bool Validate(int value) => value > 0;
            }
            """";
        var expected = Verify.Diagnostic(Descriptors.ValidatorReferenceInvalid)
            .WithArguments("Validate", "System.Guid")
            .WithSpan(5, 18, 5, 46);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// 验证器方法不是静态成员，生成代码（处于同一 partial 内）无法可达。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenValidatorMethodIsNotStatic()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Validator = nameof(Validate))]
            public partial record struct OrderId(Guid Value)
            {
                private bool Validate(Guid value) => value != Guid.Empty;
            }
            """";
        var expected = Verify.Diagnostic(Descriptors.ValidatorReferenceInvalid)
            .WithArguments("Validate", "System.Guid")
            .WithSpan(5, 18, 5, 46);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    #endregion

    #region 绕过 Create 直接构造（STIAO011）

    /// <summary>
    /// 设了验证器的 Id 通过 <c>new</c> 直接构造时，应提示 STIAO011（绕过校验）。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenConstructedDirectlyWithValidator()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Validator = nameof(Validate))]
            public partial record struct OrderId(Guid Value)
            {
                private static bool Validate(Guid value) => value != Guid.Empty;
            }

            public class Usage
            {
                public OrderId Build() => new OrderId(Guid.NewGuid());
            }
            """";
        var expected = Verify.Diagnostic(Descriptors.BypassCreate).WithArguments("OrderId").WithSpan(13, 31, 13, 58);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// 无验证器的 Id 直接构造不构成问题，不应报告 STIAO011。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenConstructedDirectlyWithoutValidator()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct OrderId(Guid Value);

            public class Usage
            {
                public OrderId Build() => new OrderId(Guid.NewGuid());
            }
            """";
        await Verify.VerifyAnalyzerAsync(code);
    }

    #endregion

    #region 装配级默认验证器（STIAO010 的装配级形态）

    /// <summary>
    /// 装配级 <c>[assembly: StronglyTypedIdDefaults(Validator = "Validate")]</c> 作用于全体 Id，
    /// 当某个 Id 自带匹配的静态验证器方法时不报告任何诊断。
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
    /// 装配级默认验证器要求名为 <c>Validate</c> 的方法，但本程序集的 Id 没有该方法时应报告 STIAO010。
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
