using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using System.Globalization;

namespace Len.StronglyTypedId.Analyzers;

public class StronglyTypedIdAnalyzerValidatorTests
{
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
}
