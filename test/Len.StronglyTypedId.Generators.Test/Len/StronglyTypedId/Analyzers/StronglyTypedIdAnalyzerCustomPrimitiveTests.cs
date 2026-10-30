using FluentAssertions;

namespace Len.StronglyTypedId.Analyzers;

/// <summary>
/// N12：自定义基元（实现 <c>IParsable&lt;TSelf&gt;</c> + <c>IComparable&lt;TSelf&gt;</c> 的用户类型）的判定。
/// </summary>
/// <remarks>
/// 判定是纯符号级的，不需要语义模型或编译产物，故全部用例都走分析器验证。
/// 每条负例都断言「消息里点名缺了什么」—— 只说「类型无效」等于让人自己猜。
/// </remarks>
public class StronglyTypedIdAnalyzerCustomPrimitiveTests
{
    /// <summary>
    /// 满足全部契约的自定义基元不产生诊断。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenCustomPrimitiveSatisfiesContract()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            public readonly record struct Email(string Value)
                : IParsable<Email>, IComparable<Email>, IComparable
            {
                public static Email Parse(string s, IFormatProvider? provider) => new(s);

                public static bool TryParse(string? s, IFormatProvider? provider, out Email result)
                {
                    result = new Email(s ?? string.Empty);
                    return s is not null;
                }

                public int CompareTo(Email other) => string.CompareOrdinal(Value, other.Value);

                public int CompareTo(object? obj) => obj is Email other ? CompareTo(other) : throw new ArgumentException();
            }

            [StronglyTypedId]
            public partial record struct EmailId(Email Value);
            """";

        await Verify.VerifyAnalyzerAsync(code);
    }

    /// <summary>
    /// 缺 <c>IComparable&lt;TSelf&gt;</c>：消息必须点名它。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReportMissingGenericComparable()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            public readonly record struct NoCompare(string Value) : IParsable<NoCompare>, IComparable
            {
                public static NoCompare Parse(string s, IFormatProvider? provider) => new(s);

                public static bool TryParse(string? s, IFormatProvider? provider, out NoCompare result)
                {
                    result = new NoCompare(s ?? string.Empty);
                    return s is not null;
                }

                public int CompareTo(object? obj) => 0;
            }

            [StronglyTypedId]
            public partial record struct NoCompareId(NoCompare Value);
            """";

        var expected = Verify.Diagnostic(Descriptors.ParameterTypeIsInvalid)
            .WithSpan(19, 42, 19, 51)
            .WithArguments("NoCompare", "It does not implement `IComparable<NoCompare>`.");

        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// 缺非泛型 <c>IComparable</c>：<c>IStronglyTypedId&lt;,&gt;</c> 的约束就是它，只实现泛型版本会 CS0315。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReportMissingNonGenericComparable()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            public readonly record struct NoNonGeneric(string Value) : IParsable<NoNonGeneric>, IComparable<NoNonGeneric>
            {
                public static NoNonGeneric Parse(string s, IFormatProvider? provider) => new(s);

                public static bool TryParse(string? s, IFormatProvider? provider, out NoNonGeneric result)
                {
                    result = new NoNonGeneric(s ?? string.Empty);
                    return s is not null;
                }

                public int CompareTo(NoNonGeneric other) => string.CompareOrdinal(Value, other.Value);
            }

            [StronglyTypedId]
            public partial record struct NoNonGenericId(NoNonGeneric Value);
            """";

        var expected = Verify.Diagnostic(Descriptors.ParameterTypeIsInvalid)
            .WithSpan(19, 45, 19, 57)
            .WithArguments("NoNonGeneric", "It does not implement non-generic `System.IComparable`.");

        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// 缺 <c>IParsable&lt;TSelf&gt;</c>：消息必须点名它。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReportMissingParsable()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            public readonly record struct NoParse(string Value) : IComparable<NoParse>, IComparable
            {
                public int CompareTo(NoParse other) => string.CompareOrdinal(Value, other.Value);

                public int CompareTo(object? obj) => 0;
            }

            [StronglyTypedId]
            public partial record struct NoParseId(NoParse Value);
            """";

        var expected = Verify.Diagnostic(Descriptors.ParameterTypeIsInvalid)
            .WithSpan(13, 40, 13, 47)
            .WithArguments("NoParse", "It does not implement `IParsable<NoParse>`, does not expose `public static NoParse Parse(string, IFormatProvider)`, does not expose `public static bool TryParse(string, IFormatProvider, out NoParse)`.");

        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// 显式接口实现的 <c>Parse</c> / <c>TryParse</c> 按类型名直呼会 CS1501，故必须在分析阶段拦下。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReportExplicitInterfaceImplementation()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            public readonly record struct ExplicitImpl(string Value)
                : IParsable<ExplicitImpl>, IComparable<ExplicitImpl>, IComparable
            {
                static ExplicitImpl IParsable<ExplicitImpl>.Parse(string s, IFormatProvider? provider) => new(s);

                static bool IParsable<ExplicitImpl>.TryParse(string? s, IFormatProvider? provider, out ExplicitImpl result)
                {
                    result = new ExplicitImpl(s ?? string.Empty);
                    return s is not null;
                }

                public int CompareTo(ExplicitImpl other) => string.CompareOrdinal(Value, other.Value);

                public int CompareTo(object? obj) => 0;
            }

            [StronglyTypedId]
            public partial record struct ExplicitImplId(ExplicitImpl Value);
            """";

        var expected = Verify.Diagnostic(Descriptors.ParameterTypeIsInvalid)
            .WithSpan(22, 45, 22, 57)
            .WithArguments("ExplicitImpl", "It does not expose `public static ExplicitImpl Parse(string, IFormatProvider)`, does not expose `public static bool TryParse(string, IFormatProvider, out ExplicitImpl)`.");

        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// <c>System.Uri</c> 满足契约但相等语义与 Id 冲突，必须显式排除并给出专门解释。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReportUriWithExplanation()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct UriId(Uri Value);
            """";

        var expected = Verify.Diagnostic(Descriptors.ParameterTypeIsInvalid)
            .WithSpan(6, 36, 6, 39)
            .WithArguments("Uri", "System.Uri is not supported: its equality ignores the fragment and normalizes case and the default port, so two different URIs would compare equal.");

        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// 可空自定义基元仍由 STIA0006 拦截，不应被当成合法基元。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReportNullable_ForCustomPrimitive()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            public readonly record struct Email(string Value)
                : IParsable<Email>, IComparable<Email>, IComparable
            {
                public static Email Parse(string s, IFormatProvider? provider) => new(s);

                public static bool TryParse(string? s, IFormatProvider? provider, out Email result)
                {
                    result = new Email(s ?? string.Empty);
                    return s is not null;
                }

                public int CompareTo(Email other) => string.CompareOrdinal(Value, other.Value);

                public int CompareTo(object? obj) => 0;
            }

            [StronglyTypedId]
            public partial record struct EmailId(Email? Value);
            """";

        var expected = Verify.Diagnostic(Descriptors.ParameterCannotBeNullable)
            .WithSpan(22, 38, 22, 44)
            .WithArguments("Email?");

        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// 回归：内置基元的诊断消息必须逐字不变 —— 自定义基元能力不得给它们多带一个实参。
    /// </summary>
    [Fact]
    public void Diagnostic_MessageFormat_Should_KeepBuiltInWording()
    {
        var message = string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            Descriptors.ParameterTypeIsInvalid.MessageFormat.ToString(),
            "bool",
            "It is not a struct or class.");

        message.Should().StartWith("The parameter type ‘bool’ must be a supported primitive type:");
        message.Should().Contain("or a custom type that implements IParsable<TSelf> and IComparable<TSelf>");
        message.Should().EndWith("It is not a struct or class.");
    }
}
