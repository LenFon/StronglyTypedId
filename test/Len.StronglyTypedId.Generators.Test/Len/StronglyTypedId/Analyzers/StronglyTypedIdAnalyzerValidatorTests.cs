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
    /// 验证器返回 <c>StronglyTypedIdValidationResult</c> 是合法签名，不报 STIA0010。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenValidatorReturnsValidationResult()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Validator = nameof(Validate))]
            public partial record struct OrderId(Guid Value)
            {
                private static StronglyTypedIdValidationResult Validate(Guid value)
                    => StronglyTypedIdValidationResult.Success;
            }
            """";

        await Verify.VerifyAnalyzerAsync(code);
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
    /// 归一化方法重载时按实参类型挑重载，而不是「第一个静态同名方法」。
    /// </summary>
    /// <remarks>
    /// 缺陷回归：判据曾取 <c>GetMembers(name).FirstOrDefault(IsStatic)</c>，重载顺序不同就会挑到签名不符的那个，
    /// 报出 STIA0017（Error 级，直接阻断编译），而生成代码按实参类型绑定、产物完全可编译。
    /// </remarks>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenNormalizerIsOverloaded()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Normalizer = nameof(Normalize))]
            public partial record struct OrderId(Guid Value)
            {
                private static int Normalize(int value) => value;

                private static Guid Normalize(Guid value) => value;
            }
            """";
        await Verify.VerifyAnalyzerAsync(code);
    }

    /// <summary>
    /// 继承来的归一化方法同样被识别。
    /// </summary>
    /// <remarks>
    /// 缺陷回归：<c>GetMembers</c> 只返回声明成员，继承来的归一化方法会被判成「不存在」并报 STIA0017，
    /// 而生成代码在派生类型里能正常调用它。
    /// </remarks>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenNormalizerIsInherited()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            public abstract partial record BaseId
            {
                protected static Guid Normalize(Guid value) => value;
            }

            [StronglyTypedId(Normalizer = nameof(Normalize))]
            public partial record OrderId(Guid Value) : BaseId;
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

    #region 验证器 / 归一化方法的绑定形态（STIA0010 / STIA0017）

    /// <summary>
    /// 验证器带 <c>ref</c> 参数时报告 STIA0010。
    /// </summary>
    /// <remarks>
    /// 缺陷回归：判据只看「静态 + 参数个数 + 参数类型」，<c>ref</c> / <c>out</c> 被放行，而生成代码写的是
    /// 裸实参调用 <c>Validate(value)</c>，产物在 5 处报 CS1620（阻断编译）且没有任何 STIA 诊断。
    /// </remarks>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenValidatorTakesRefParameter()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Validator = nameof(Validate))]
            public partial record struct OrderId(Guid Value)
            {
                private static bool Validate(ref Guid value) => value != Guid.Empty;
            }
            """";
        var expected = Verify.Diagnostic(Descriptors.ValidatorReferenceInvalid)
            .WithArguments("Validate", "System.Guid")
            .WithSpan(5, 18, 5, 46);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// 验证器带 <c>out</c> 参数时报告 STIA0010（同 <c>ref</c>，调用点必须写修饰符）。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenValidatorTakesOutParameter()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Validator = nameof(Validate))]
            public partial record struct OrderId(Guid Value)
            {
                private static bool Validate(out Guid value)
                {
                    value = Guid.Empty;
                    return true;
                }
            }
            """";
        var expected = Verify.Diagnostic(Descriptors.ValidatorReferenceInvalid)
            .WithArguments("Validate", "System.Guid")
            .WithSpan(5, 18, 5, 46);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// 验证器的参数类型就是它自己的类型参数时，实参能推断出 <c>T</c>，不报 STIA0010。
    /// </summary>
    /// <remarks>
    /// 缺陷回归：判据曾要求「参数个数恰为 1 且不带类型参数」，把 <c>Validate&lt;T&gt;(T value)</c> 误报成 Error，
    /// 而生成代码 <c>Validate(value)</c> 由编译器正常推断 <c>T := Guid</c>，产物完全可编译。
    /// </remarks>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenValidatorIsGenericOverItsParameter()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Validator = nameof(Validate))]
            public partial record struct OrderId(Guid Value)
            {
                private static bool Validate<T>(T value) => value is not null;
            }
            """";
        await Verify.VerifyAnalyzerAsync(code);
    }

    /// <summary>
    /// 验证器的类型参数无法从唯一那个实参推断时报告 STIA0010。
    /// </summary>
    /// <remarks>
    /// <c>Validate&lt;T&gt;(Guid value)</c> 的 <c>T</c> 不出现在参数表里，生成代码 <c>Validate(value)</c> 会 CS0411。
    /// </remarks>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenValidatorTypeParameterCannotBeInferred()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Validator = nameof(Validate))]
            public partial record struct OrderId(Guid Value)
            {
                private static bool Validate<T>(Guid value) => true;
            }
            """";
        var expected = Verify.Diagnostic(Descriptors.ValidatorReferenceInvalid)
            .WithArguments("Validate", "System.Guid")
            .WithSpan(5, 18, 5, 46);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// 验证器第一个参数之后全是可选参数时，实参个数足够，不报 STIA0010。
    /// </summary>
    /// <remarks>
    /// 缺陷回归：判据曾要求「参数个数恰为 1」，把 <c>Validate(Guid value, bool strict = true)</c> 误报成 Error，
    /// 而生成代码 <c>Validate(value)</c> 用默认值补足，产物完全可编译。
    /// </remarks>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenValidatorHasOptionalParameters()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Validator = nameof(Validate))]
            public partial record struct OrderId(Guid Value)
            {
                private static bool Validate(Guid value, bool strict = true) => value != Guid.Empty;
            }
            """";
        await Verify.VerifyAnalyzerAsync(code);
    }

    /// <summary>
    /// 验证器第一个参数之后是 <c>params</c> 时，实参可以一个都不给，不报 STIA0010。
    /// </summary>
    /// <remarks>
    /// 缺陷回归：判据曾只放行 <c>IsOptional</c>，把 <c>params</c> 当成必填参数误报成 Error，
    /// 而生成代码 <c>Validate(value)</c> 传空数组，产物完全可编译。
    /// </remarks>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenValidatorHasParamsParameter()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Validator = nameof(Validate))]
            public partial record struct OrderId(Guid Value)
            {
                private static bool Validate(Guid value, params int[] extra) => value != Guid.Empty;
            }
            """";
        await Verify.VerifyAnalyzerAsync(code);
    }

    /// <summary>
    /// 归一化方法第一个参数之后是 <c>params</c> 时同样不报 STIA0017。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenNormalizerHasParamsParameter()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Normalizer = nameof(Normalize))]
            public partial record struct OrderId(Guid Value)
            {
                private static Guid Normalize(Guid value, params int[] extra) => value;
            }
            """";
        await Verify.VerifyAnalyzerAsync(code);
    }

    /// <summary>
    /// 验证器第一个参数之后还有必填参数时报告 STIA0010（实参不足，CS7036）。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenValidatorHasRequiredTrailingParameter()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Validator = nameof(Validate))]
            public partial record struct OrderId(Guid Value)
            {
                private static bool Validate(Guid value, bool strict) => value != Guid.Empty;
            }
            """";
        var expected = Verify.Diagnostic(Descriptors.ValidatorReferenceInvalid)
            .WithArguments("Validate", "System.Guid")
            .WithSpan(5, 18, 5, 46);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// 归一化方法带 <c>ref</c> 参数时报告 STIA0017。
    /// </summary>
    /// <remarks>
    /// 缺陷回归：与验证器同源，判据漏了 <c>RefKind</c>，生成代码在 5 处报 CS1620 且无 STIA 诊断。
    /// </remarks>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenNormalizerTakesRefParameter()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Normalizer = nameof(Normalize))]
            public partial record struct OrderId(Guid Value)
            {
                private static Guid Normalize(ref Guid value) => value;
            }
            """";
        var expected = Verify.Diagnostic(Descriptors.NormalizerReferenceInvalid)
            .WithArguments("Normalize", "System.Guid")
            .WithSpan(5, 18, 5, 48);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// 归一化方法带 <c>in</c> 参数时不报 STIA0017：<c>in</c> 可以省略修饰符，裸实参调用照常绑定。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenNormalizerTakesInParameter()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Normalizer = nameof(Normalize))]
            public partial record struct OrderId(Guid Value)
            {
                private static Guid Normalize(in Guid value) => value;
            }
            """";
        await Verify.VerifyAnalyzerAsync(code);
    }

    /// <summary>
    /// 归一化方法带可选参数时不报 STIA0017。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenNormalizerHasOptionalParameter()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Normalizer = nameof(Normalize))]
            public partial record struct OrderId(Guid Value)
            {
                private static Guid Normalize(Guid value, bool trim = true) => value;
            }
            """";
        await Verify.VerifyAnalyzerAsync(code);
    }

    /// <summary>
    /// 泛型归一化方法 <c>static T Normalize&lt;T&gt;(T value)</c> 不报 STIA0017：返回类型是类型参数本身，
    /// 绑定后即基元类型。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenNormalizerIsGenericOverItsParameter()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Normalizer = nameof(Normalize))]
            public partial record struct OrderId(Guid Value)
            {
                private static T Normalize<T>(T value) => value;
            }
            """";
        await Verify.VerifyAnalyzerAsync(code);
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
