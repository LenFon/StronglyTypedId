using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using System.Globalization;

namespace Len.StronglyTypedId.Analyzers;

public class StronglyTypedIdAnalyzerInstanceTests
{
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
    /// 回归用例：STIA0007 / STIA0008 的文案必须保留占位符。
    /// </summary>
    /// <remarks>
    /// STIA0008 自 N12 起有两个占位符：<c>{0}</c> 是参数类型，<c>{1}</c> 说明缺了哪些契约。
    /// 多传一个实参对只有一个占位符的 STIA0007 无害，故两条规则共用这一份断言。
    /// </remarks>
    [Theory]
    [MemberData(nameof(ParameterDiagnostics))]
    public void Diagnostic_MessageFormat_Should_ContainArgument(string diagnosticId, string argument)
    {
        var descriptor = Descriptors.All.Single(candidate => candidate.Id == diagnosticId);

        var message = string.Format(
            CultureInfo.InvariantCulture,
            descriptor.MessageFormat.ToString(),
            argument,
            "It is not a struct or class.");

        message.Should().Contain(argument);
    }
}
