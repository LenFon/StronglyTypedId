using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using System.Globalization;

namespace Len.StronglyTypedId.Analyzers;

public class StronglyTypedIdAnalyzerConstructionTests
{
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
}
