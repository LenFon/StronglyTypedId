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

    /// <summary>
    /// 静态构造函数也在 <c>INamedTypeSymbol.Constructors</c> 里，且没有参数。若按 <c>Constructors[0].Parameters[0]</c>
    /// 取基元类型，这里会抛 IndexOutOfRangeException 并被 Roslyn 包成 AD0001，把该类型的全部 STIA 诊断一起吞掉。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenStaticConstructorDeclared()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct OrderId(Guid Value)
            {
                static OrderId() { }
            }
            """";

        await Verify.VerifyAnalyzerAsync(code);
    }

    /// <summary>
    /// 用户额外写的无参实例构造排在主构造之前时，同样不能让 <c>Constructors[0].Parameters[0]</c> 越界。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenExtraParameterlessConstructorDeclared()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct OrderId(Guid Value)
            {
                public OrderId() : this(Guid.Empty) { }
            }
            """";

        await Verify.VerifyAnalyzerAsync(code);
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
        // 不会在后续声明段上再叠加一条 STIA0005。
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
    /// 回归用例：使用者自定义的 <c>X.Guid</c> 并不是 BCL 的 <see cref="System.Guid"/>，必须报 STIA0008。
    /// </summary>
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
    public async Task AnalyzingCode_Should_NotReturnDiagnostic_WhenContainingTypeIsGeneric()
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

        // 泛型容器现已放开：重开的 partial 只需复现类型参数表（约束子句可省略），hint 名经净化去掉尖括号，
        // 因此不再报 STIA0003。这里验证分析器对泛型容器不再产生任何诊断。
        await Verify.VerifyAnalyzerAsync(code);
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

    #region 验证器引用校验（STIA0010）

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

    #region 归一化引用校验（STIA0017）

    /// <summary>
    /// <c>[StronglyTypedId(Normalizer = nameof(Normalize))]</c> 指向的方法不存在。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenNormalizerMethodMissing()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Normalizer = nameof(Normalize))]
            public partial record struct OrderId(Guid Value);
            """";
        var expected = Verify.Diagnostic(Descriptors.NormalizerReferenceInvalid)
            .WithArguments("Normalize", "System.Guid")
            .WithSpan(5, 18, 5, 48);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// 归一化方法返回 <c>void</c>：生成代码会拿 <c>void</c> 去构造 Id（CS1503），必须在 attribute 上拦住。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenNormalizerMethodReturnsVoid()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Normalizer = nameof(Normalize))]
            public partial record struct OrderId(Guid Value)
            {
                private static void Normalize(Guid value) { }
            }
            """";
        var expected = Verify.Diagnostic(Descriptors.NormalizerReferenceInvalid)
            .WithArguments("Normalize", "System.Guid")
            .WithSpan(5, 18, 5, 48);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// 归一化方法的参数类型与基元 Id 类型不一致。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenNormalizerMethodHasWrongParameterType()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Normalizer = nameof(Normalize))]
            public partial record struct OrderId(Guid Value)
            {
                private static Guid Normalize(int value) => Guid.Empty;
            }
            """";
        var expected = Verify.Diagnostic(Descriptors.NormalizerReferenceInvalid)
            .WithArguments("Normalize", "System.Guid")
            .WithSpan(5, 18, 5, 48);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// 归一化方法不是静态成员，生成代码（处于同一 partial 内）无法可达。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenNormalizerMethodIsNotStatic()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Normalizer = nameof(Normalize))]
            public partial record struct OrderId(Guid Value)
            {
                private Guid Normalize(Guid value) => value;
            }
            """";
        var expected = Verify.Diagnostic(Descriptors.NormalizerReferenceInvalid)
            .WithArguments("Normalize", "System.Guid")
            .WithSpan(5, 18, 5, 48);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// 签名完全合规时不报告任何诊断。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenNormalizerMatches()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Normalizer = nameof(Normalize))]
            public partial record struct OrderId(Guid Value)
            {
                private static Guid Normalize(Guid value) => value;
            }
            """";
        await Verify.VerifyAnalyzerAsync(code);
    }

    /// <summary>
    /// 装配级默认归一化要求名为 <c>Normalize</c> 的方法，但本程序集的 Id 没有该方法时应报告 STIA0017。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenAssemblyDefaultNormalizerHasNoMatchingMethod()
    {
        var code = """"
            using System;

            [assembly: Len.StronglyTypedId.StronglyTypedIdDefaults(Normalizer = "Normalize")]

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct OrderId(Guid Value);
            """";
        var expected = Verify.Diagnostic(Descriptors.NormalizerReferenceInvalid)
            .WithArguments("Normalize", "System.Guid")
            .WithSpan(7, 1, 8, 50);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    #endregion

    #region 验证失败异常类型校验（STIA0018）

    /// <summary>
    /// 派生自 <see cref="Exception"/>、非抽象、且有可访问单字符串构造函数的类型是合法配置。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenExceptionTypeIsUsable()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Validator = nameof(Validate), ExceptionType = typeof(DomainException))]
            public partial record struct OrderId(Guid Value)
            {
                private static bool Validate(Guid value) => value != Guid.Empty;
            }

            public sealed class DomainException(string message) : Exception(message);
            """";
        await Verify.VerifyAnalyzerAsync(code);
    }

    /// <summary>
    /// 异常类型未派生自 <see cref="Exception"/>：生成代码 <c>throw new T(message)</c> 会编译失败。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenExceptionTypeDoesNotDeriveFromException()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(ExceptionType = typeof(NotAnException))]
            public partial record struct OrderId(Guid Value);

            public sealed class NotAnException(string message);
            """";
        var expected = Verify.Diagnostic(Descriptors.ExceptionTypeInvalid)
            .WithArguments("Len.StronglyTypedId.Tests.NotAnException")
            .WithSpan(5, 18, 5, 56);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// 抽象异常类型无法实例化。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenExceptionTypeIsAbstract()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(ExceptionType = typeof(AbstractDomainException))]
            public partial record struct OrderId(Guid Value);

            public abstract class AbstractDomainException(string message) : Exception(message);
            """";
        var expected = Verify.Diagnostic(Descriptors.ExceptionTypeInvalid)
            .WithArguments("Len.StronglyTypedId.Tests.AbstractDomainException")
            .WithSpan(5, 18, 5, 65);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// 缺少接收单个字符串的构造函数：生成代码只能传消息，无法构造该异常。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenExceptionTypeHasNoStringConstructor()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(ExceptionType = typeof(ParameterlessException))]
            public partial record struct OrderId(Guid Value);

            public sealed class ParameterlessException : Exception
            {
                public ParameterlessException() { }
            }
            """";
        var expected = Verify.Diagnostic(Descriptors.ExceptionTypeInvalid)
            .WithArguments("Len.StronglyTypedId.Tests.ParameterlessException")
            .WithSpan(5, 18, 5, 64);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// 开放泛型异常类型无法直接构造（<c>typeof(Foo&lt;&gt;)</c>）。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenExceptionTypeIsOpenGeneric()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(ExceptionType = typeof(GenericException<>))]
            public partial record struct OrderId(Guid Value);

            public sealed class GenericException<T>(string message) : Exception(message);
            """";
        var expected = Verify.Diagnostic(Descriptors.ExceptionTypeInvalid)
            .WithArguments("Len.StronglyTypedId.Tests.GenericException<>")
            .WithSpan(5, 18, 5, 60);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// 闭合泛型异常（<c>typeof(Foo&lt;int&gt;)</c>）可以照常 <c>new</c>，不报诊断。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenExceptionTypeIsClosedGeneric()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(ExceptionType = typeof(GenericException<int>))]
            public partial record struct OrderId(Guid Value);

            public sealed class GenericException<T>(string message) : Exception(message);
            """";
        await Verify.VerifyAnalyzerAsync(code);
    }

    /// <summary>
    /// 装配级默认异常类型同样生效：合法配置不产生诊断。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenAssemblyDefaultExceptionTypeIsUsable()
    {
        var code = """"
            using System;

            [assembly: Len.StronglyTypedId.StronglyTypedIdDefaults(ExceptionType = typeof(Len.StronglyTypedId.Tests.DomainException))]

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Validator = nameof(Validate))]
            public partial record struct OrderId(Guid Value)
            {
                private static bool Validate(Guid value) => value != Guid.Empty;
            }

            public sealed class DomainException(string message) : Exception(message);
            """";
        await Verify.VerifyAnalyzerAsync(code);
    }

    /// <summary>
    /// 装配级默认异常类型不合法时，落在 Id 声明上报错（逐类型特性未覆盖它）。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenAssemblyDefaultExceptionTypeIsInvalid()
    {
        var code = """"
            using System;

            [assembly: Len.StronglyTypedId.StronglyTypedIdDefaults(ExceptionType = typeof(Len.StronglyTypedId.Tests.NotAnException))]

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Validator = nameof(Validate))]
            public partial record struct OrderId(Guid Value)
            {
                private static bool Validate(Guid value) => value != Guid.Empty;
            }

            public sealed class NotAnException(string message);
            """";
        var expected = Verify.Diagnostic(Descriptors.ExceptionTypeInvalid)
            .WithArguments("Len.StronglyTypedId.Tests.NotAnException")
            .WithSpan(7, 1, 11, 2);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    #endregion

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
    /// 未写该参数时按「引用了就生成」处理，不存在「请求了却没有」的问题。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenIntegrationsIsNotSpecified()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
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

    #region 绕过 Create 直接构造（STIA0011）

    /// <summary>
    /// 设了验证器的 Id 通过 <c>new</c> 直接构造时，应提示 STIA0011（绕过校验）。
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
    /// 只配了归一化、没配验证器时，直接构造同样要提示 STIA0011：绕过归一化得到的 Id 不是规范形，相等性比较与字典键都会失效。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenConstructedDirectlyWithNormalizer()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Normalizer = nameof(Normalize))]
            public partial record struct OrderId(Guid Value)
            {
                private static Guid Normalize(Guid value) => value;
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
    /// 无验证器的 Id 直接构造不构成问题，不应报告 STIA0011。
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

    /// <summary>
    /// 目标类型化的 <c>new(...)</c> 与显式 <c>new Xxx(...)</c> 等价，同样绕过验证器，应报告 STIA0011。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenConstructedWithTargetTypedNew()
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
                public OrderId Build() => new(Guid.NewGuid());
            }
            """";
        var expected = Verify.Diagnostic(Descriptors.BypassCreate).WithArguments("OrderId").WithSpan(13, 31, 13, 50);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// <c>default</c> 字面量产生零值实例，绕过验证器，应报告 STIA0011。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenInitializedWithDefaultLiteral()
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
                public OrderId Build() => default;
            }
            """";
        var expected = Verify.Diagnostic(Descriptors.BypassCreate).WithArguments("OrderId").WithSpan(13, 31, 13, 38);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// <c>default(T)</c> 与 <c>default</c> 字面量等价，应同样报告 STIA0011。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenInitializedWithDefaultExpression()
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
                public OrderId Build() => default(OrderId);
            }
            """";
        var expected = Verify.Diagnostic(Descriptors.BypassCreate).WithArguments("OrderId").WithSpan(13, 31, 13, 47);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// 与 <c>default</c> 做相等比较是合法的判空写法，不应报告 STIA0011。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenDefaultIsComparisonOperand()
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
                public bool IsEmpty(OrderId id) => id == default;

                public bool IsNotEmpty(OrderId id) => default != id;

                public bool IsEmptyByEquals(OrderId id) => id.Equals(default);
            }
            """";
        await Verify.VerifyAnalyzerAsync(code);
    }

    /// <summary>
    /// 引用类型 Id 的 <c>default</c> 就是 <see langword="null"/>，没有「构造出无效实例」，不应报告 STIA0011。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenDefaultUsedOnReferenceTypeId()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Validator = nameof(Validate))]
            public partial record OrderId(Guid Value)
            {
                private static bool Validate(Guid value) => value != Guid.Empty;
            }

            public class Usage
            {
                public OrderId Build() => default;
            }
            """";
        await Verify.VerifyAnalyzerAsync(code);
    }

    /// <summary>
    /// 无验证器的 Id 用 <c>default</c> 初始化不构成「绕过校验」，不应报告 STIA0011。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenDefaultUsedWithoutValidator()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct OrderId(Guid Value);

            public class Usage
            {
                public OrderId Build() => default;
            }
            """";
        await Verify.VerifyAnalyzerAsync(code);
    }

    /// <summary>
    /// 验证器来自装配级 <c>[assembly: StronglyTypedIdDefaults]</c> 时，直接构造同样是绕过，应报告 STIA0011。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenAssemblyDefaultValidatorBypassed()
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

            public class Usage
            {
                public OrderId Build() => new OrderId(Guid.NewGuid());
            }
            """";
        var expected = Verify.Diagnostic(Descriptors.BypassCreate).WithArguments("OrderId").WithSpan(15, 31, 15, 58);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    #endregion

    #region 空前缀（STIA0013）

    /// <summary>
    /// <c>Prefix = ""</c> 不报告 STIA0013：生成器侧 <c>NormalizePrefix</c> 把它等同于「未设置」，
    /// 产物与不写这个实参逐字符相同。报了只会让人以为必须删掉，实际删不删都一样。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenPrefixIsEmpty()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Prefix = "")]
            public partial record struct OrderId(Guid Value);
            """";
        await Verify.VerifyAnalyzerAsync(code);
    }

    /// <summary>
    /// <c>Prefix = "   "</c>（纯空白）几乎必然是笔误，应报告 STIA0013。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenPrefixIsWhitespace()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Prefix = "   ")]
            public partial record struct OrderId(Guid Value);
            """";
        var expected = Verify.Diagnostic(Descriptors.InvalidPrefix)
            .WithSpan(5, 18, 5, 32);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// 第三方命名空间下的同名特性不能被当成自家的：诊断位置必须落在真正的 <c>[StronglyTypedId]</c> 上，
    /// 而不是 <c>Acme.StronglyTypedIdAttribute</c> 的同名实参上（按名字后缀匹配就会指错地方）。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_IgnoreForeignAttribute_WhenLocatingNamedArgument()
    {
        var code = """"
            using System;

            namespace Acme
            {
                [AttributeUsage(AttributeTargets.All)]
                public sealed class StronglyTypedIdAttribute : Attribute
                {
                    public string? Prefix { get; set; }
                }
            }

            namespace Len.StronglyTypedId.Tests
            {
                [Acme.StronglyTypedIdAttribute(Prefix = "x")]
                [StronglyTypedId(Prefix = "   ")]
                public partial record struct OrderId(Guid Value);
            }
            """";

        var expected = Verify.Diagnostic(Descriptors.InvalidPrefix)
            .WithSpan(15, 22, 15, 36);

        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// 常量引用的空串同样不报：取值走符号级命名实参，判定口径必须与字面量一致。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenPrefixConstantIsEmpty()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Prefix = Empty)]
            public partial record struct OrderId(Guid Value)
            {
                private const string Empty = "";
            }
            """";
        await Verify.VerifyAnalyzerAsync(code);
    }

    /// <summary>
    /// 非空前缀不报告 STIA0013。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenPrefixIsValid()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Prefix = "ord_")]
            public partial record struct OrderId(Guid Value);
            """";
        await Verify.VerifyAnalyzerAsync(code);
    }

    /// <summary>
    /// 未设置前缀不报告 STIA0013。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenPrefixIsNotSet()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct OrderId(Guid Value);
            """";
        await Verify.VerifyAnalyzerAsync(code);
    }

    #endregion

    #region 前缀与基元文本重叠（STIA0023）

    /// <summary>
    /// <c>Prefix = "-"</c> + <c>int</c>：<c>Parse("-5")</c> 会剥掉前缀得到 5，无前缀的负数被静默读成相反数。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenNumericPrefixIsSign()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Prefix = "-")]
            public partial record struct OrderId(int Value);
            """";
        var expected = Verify.Diagnostic(Descriptors.AmbiguousPrefix)
            .WithArguments("-", "int")
            .WithSpan(5, 18, 5, 30);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// <c>Prefix = "1"</c> + <c>int</c>：无前缀的 123 与「值 23 带前缀」的文本完全相同。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenNumericPrefixIsDigit()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Prefix = "1")]
            public partial record struct OrderId(int Value);
            """";
        var expected = Verify.Diagnostic(Descriptors.AmbiguousPrefix)
            .WithArguments("1", "int")
            .WithSpan(5, 18, 5, 30);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// <c>Prefix = "NaN"</c> + <c>double</c>：浮点的特殊值文本同样是合法取值。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenNumericPrefixIsNaNAndTypeIsDouble()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Prefix = "NaN")]
            public partial record struct RatioId(double Value);
            """";
        var expected = Verify.Diagnostic(Descriptors.AmbiguousPrefix)
            .WithArguments("NaN", "double")
            .WithSpan(5, 18, 5, 32);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// <c>Prefix = "2"</c> + <c>DateTime</c>：不变文化下日期以数字开头，前缀会啃掉年份首位。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenTemporalPrefixIsDigit()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Prefix = "2")]
            public partial record struct BookingId(DateTime Value);
            """";
        var expected = Verify.Diagnostic(Descriptors.AmbiguousPrefix)
            .WithArguments("2", "System.DateTime")
            .WithSpan(5, 18, 5, 30);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// <c>Prefix = "a"</c> + <c>Guid</c>：十六进制字符能开启一个 Guid 文本。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenGuidPrefixIsHexDigit()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Prefix = "a")]
            public partial record struct OrderId(Guid Value);
            """";
        var expected = Verify.Diagnostic(Descriptors.AmbiguousPrefix)
            .WithArguments("a", "System.Guid")
            .WithSpan(5, 18, 5, 30);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// <c>Prefix = "A"</c> + <c>char</c>：单字符前缀本身就是一个合法取值。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenCharPrefixIsSingleCharacter()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Prefix = "A")]
            public partial record struct GradeId(char Value);
            """";
        var expected = Verify.Diagnostic(Descriptors.AmbiguousPrefix)
            .WithArguments("A", "char")
            .WithSpan(5, 18, 5, 30);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// 字母开头且不是 <c>NaN</c> / <c>Infinity</c> 的前缀不误伤：<c>"INV-"</c> 不可能开启一个数值文本。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenNumericPrefixStartsLetter()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Prefix = "INV-")]
            public partial record struct InvoiceId(int Value);
            """";
        await Verify.VerifyAnalyzerAsync(code);
    }

    /// <summary>
    /// 日期基元的字母前缀同样安全：<c>"DT-"</c> 不会与任何日期文本重叠。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenTemporalPrefixStartsLetter()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Prefix = "DT-")]
            public partial record struct BookingId(DateTime Value);
            """";
        await Verify.VerifyAnalyzerAsync(code);
    }

    /// <summary>
    /// <c>string</c> 基元不判定：取值可以是任意文本，任何前缀都可能与之重叠，没有可枚举的文法。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenStringPrefixIsDigit()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Prefix = "1")]
            public partial record struct AccountId(string Value);
            """";
        await Verify.VerifyAnalyzerAsync(code);
    }

    /// <summary>
    /// 枚举基元不判定：成员名由用户定义，分析器无从得知其文本集合。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenEnumPrefixIsDigit()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            public enum Level
            {
                Low,
                High,
            }

            [StronglyTypedId(Prefix = "1")]
            public partial record struct LevelId(Level Value);
            """";
        await Verify.VerifyAnalyzerAsync(code);
    }

    #endregion

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
            .WithArguments("Order", "OrderId")
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
            .WithArguments("OrderID", "OrderId")
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

    #region 命名实例（STIA0020 / STIA0021 / STIA0022）

    /// <summary>
    /// 合法实例不报告任何诊断。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenInstanceIsValid()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            [StronglyTypedIdInstance("Unspecified", "-1")]
            [StronglyTypedIdInstance("Anonymous", "0")]
            public partial record struct OrderId(int Value);
            """";
        await Verify.VerifyAnalyzerAsync(code);
    }

    /// <summary>
    /// 名字不是合法标识符时报告 STIA0020。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenInstanceNameIsNotIdentifier()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            [StronglyTypedIdInstance("1Bad", "0")]
            public partial record struct OrderId(int Value);
            """";
        var expected = Verify.Diagnostic(Descriptors.InvalidInstanceName)
            .WithArguments("1Bad", "OrderId")
            .WithSpan(6, 2, 6, 38);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// 同一类型上重名时，后声明的那条报告 STIA0020（先声明者胜出）。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenInstanceNameIsDuplicated()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            [StronglyTypedIdInstance("Same", "1")]
            [StronglyTypedIdInstance("Same", "2")]
            public partial record struct OrderId(int Value);
            """";
        var expected = Verify.Diagnostic(Descriptors.InvalidInstanceName)
            .WithArguments("Same", "OrderId")
            .WithSpan(7, 2, 7, 38);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// 名字与用户自己写的成员撞名（会触发 CS0102）时报告 STIA0020。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenInstanceNameCollidesWithExistingMember()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            [StronglyTypedIdInstance("Empty", "0")]
            public partial record struct OrderId(int Value)
            {
                public bool Empty => Value == 0;
            }
            """";
        var expected = Verify.Diagnostic(Descriptors.InvalidInstanceName)
            .WithArguments("Empty", "OrderId")
            .WithSpan(6, 2, 6, 39);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// 名字与生成器稍后产出的成员撞名（<c>Create</c> 此刻还不存在，只能靠保留名清单判定）。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenInstanceNameCollidesWithGeneratedMember()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            [StronglyTypedIdInstance("Create", "0")]
            public partial record struct OrderId(int Value);
            """";
        var expected = Verify.Diagnostic(Descriptors.InvalidInstanceName)
            .WithArguments("Create", "OrderId")
            .WithSpan(6, 2, 6, 40);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// 名字与类型自身同名会触发 CS0542，同样报告 STIA0020。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenInstanceNameEqualsTypeName()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            [StronglyTypedIdInstance("OrderId", "0")]
            public partial record struct OrderId(int Value);
            """";
        var expected = Verify.Diagnostic(Descriptors.InvalidInstanceName)
            .WithArguments("OrderId", "OrderId")
            .WithSpan(6, 2, 6, 41);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// 引用类型 Id 会额外生成私有的 <c>CompareValues</c> 比较辅助方法，实例取这个名字会撞 CS0102，
    /// 必须报告 STIA0020。
    /// </summary>
    /// <remarks>
    /// 守着一次具体回归：撞名判定原先靠「滤掉带 <c>[GeneratedCode]</c> 的成员」，把生成器产出的方法也一并放过，于是 <c>CompareValues</c> 被当成不撞名，直到生成代码里冒出 CS0102。
    /// </remarks>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenInstanceNameCollidesWithGeneratedComparisonHelper()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            [StronglyTypedIdInstance("CompareValues", "0")]
            public partial record OrderId(int Value);
            """";
        var expected = Verify.Diagnostic(Descriptors.InvalidInstanceName)
            .WithArguments("CompareValues", "OrderId")
            .WithSpan(6, 2, 6, 47);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// <c>CompareValues</c> 只属于引用类型 Id：值类型直连基元比较，不生成该辅助方法，
    /// 因此值类型上的同名实例合法，不应报告任何诊断。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenValueTypeInstanceNameIsCompareValues()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            [StronglyTypedIdInstance("CompareValues", "0")]
            public partial record struct OrderId(int Value);
            """";
        await Verify.VerifyAnalyzerAsync(code);
    }

    /// <summary>
    /// 值文本无法解析成基元时报告 STIA0021。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenInstanceValueIsNotParseable()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            [StronglyTypedIdInstance("Bad", "abc")]
            public partial record struct OrderId(int Value);
            """";
        var expected = Verify.Diagnostic(Descriptors.InvalidInstanceValue)
            .WithArguments("abc", "int", "Bad")
            .WithSpan(6, 2, 6, 39);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// 数值越界与「解析不了」同属 STIA0021：<c>256</c> 对 <c>byte</c> 越界。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenInstanceValueOverflows()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            [StronglyTypedIdInstance("TooBig", "256")]
            public partial record struct OrderId(byte Value);
            """";
        var expected = Verify.Diagnostic(Descriptors.InvalidInstanceValue)
            .WithArguments("256", "byte", "TooBig")
            .WithSpan(6, 2, 6, 42);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// <c>char</c> 的合法文本长度恰为 1。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenInstanceValueIsNotSingleCharacter()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            [StronglyTypedIdInstance("TooLong", "AB")]
            public partial record struct OrderId(char Value);
            """";
        var expected = Verify.Diagnostic(Descriptors.InvalidInstanceValue)
            .WithArguments("AB", "char", "TooLong")
            .WithSpan(6, 2, 6, 42);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// 非 <c>[Flags]</c> 枚举的未定义数值与 <c>Enum.IsDefined</c> 同源地被拒。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenInstanceValueIsUndefinedEnumMember()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            public enum Status
            {
                Active,
                Suspended,
            }

            [StronglyTypedId]
            [StronglyTypedIdInstance("Weird", "3")]
            public partial record struct OrderId(Status Value);
            """";
        var expected = Verify.Diagnostic(Descriptors.InvalidInstanceValue)
            .WithArguments("3", "Len.StronglyTypedId.Tests.Status", "Weird")
            .WithSpan(12, 2, 12, 39);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// 名字不是 PascalCase 时给出改名建议（STIA0022，Info 级）。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenInstanceNameIsNotPascalCase()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            [StronglyTypedIdInstance("unspecified", "-1")]
            public partial record struct OrderId(int Value);
            """";
        var expected = Verify.Diagnostic(Descriptors.InstanceNameNotPascalCase)
            .WithArguments("unspecified", "Unspecified")
            .WithSpan(6, 2, 6, 46);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// 下划线分隔的名字按「分段首字母大写」给出建议名。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WithSuggestedName_WhenInstanceNameHasUnderscores()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            [StronglyTypedIdInstance("not_set", "-1")]
            public partial record struct OrderId(int Value);
            """";
        var expected = Verify.Diagnostic(Descriptors.InstanceNameNotPascalCase)
            .WithArguments("not_set", "NotSet")
            .WithSpan(6, 2, 6, 42);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// 全大写缩写（<c>URL</c>）已是 PascalCase 的常见写法，不报告 STIA0022。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenInstanceNameIsAcronym()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            [StronglyTypedIdInstance("URL", "0")]
            public partial record struct OrderId(int Value);
            """";
        await Verify.VerifyAnalyzerAsync(code);
    }

    #endregion

    /// <summary>
    /// STIA0007 / STIA0008 的规则 ID 与对应的实参样例。
    /// </summary>
    public static TheoryData<string, string> ParameterDiagnostics => new()
    {
        { Descriptors.ParameterNameMustBeValueId, "Value1" },
        { Descriptors.ParameterTypeIsInvalidId, "bool" },
    };

    /// <summary>
    /// 回归用例：STIA0007 / STIA0008 的文案必须保留 <c>{0}</c> 占位符。
    /// </summary>
    [Theory]
    [MemberData(nameof(ParameterDiagnostics))]
    public void Diagnostic_MessageFormat_Should_ContainArgument(string diagnosticId, string argument)
    {
        var descriptor = Descriptors.All.Single(candidate => candidate.Id == diagnosticId);

        var message = string.Format(
            CultureInfo.InvariantCulture,
            descriptor.MessageFormat.ToString(),
            argument);

        message.Should().Contain(argument);
    }
}
