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
    /// 实例名 <c>_</c> 会遮蔽生成代码里的弃元 <c>out _</c>，必须报告 STIA0020。
    /// </summary>
    /// <remarks>
    /// 缺陷回归：<c>_</c> 是合法标识符（<c>SyntaxFacts.IsValidIdentifier</c> 放行），保留名清单里也没有它，
    /// 于是实例字段 <c>public static readonly OrderId @_</c> 与 <c>TryCreate(value, out result, out _)</c> 撞车，
    /// 产物报 CS0199（「无法将静态只读字段用作 ref 或 out 值」）且没有任何 STIA 诊断。
    /// </remarks>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenInstanceNameIsDiscard()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            [StronglyTypedIdInstance("_", "0")]
            public partial record struct OrderId(int Value);
            """";
        var expected = Verify.Diagnostic(Descriptors.InvalidInstanceName)
            .WithArguments("_", "OrderId")
            .WithSpan(6, 2, 6, 35);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// 双下划线 <c>__</c> 不构成弃元，作为实例名合法。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenInstanceNameIsDoubleUnderscore()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            [StronglyTypedIdInstance("__", "0")]
            public partial record struct OrderId(int Value);
            """";
        await Verify.VerifyAnalyzerAsync(code);
    }

    /// <summary>
    /// <c>TryFormat</c> 只在基元可 span 格式化时生成；<c>string</c> 基元不生成，同名实例合法。
    /// </summary>
    /// <remarks>
    /// 缺陷回归：保留名清单曾无条件收录 <c>TryFormat</c>，给「本来不存在的撞名」报 STIA0020（Error），
    /// 而绕过它之后实例又被生成器静默丢弃。
    /// </remarks>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenStringPrimitiveInstanceNameIsTryFormat()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            [StronglyTypedIdInstance("TryFormat", "abc")]
            public partial record struct StringId(string Value);
            """";
        await Verify.VerifyAnalyzerAsync(code);
    }

    /// <summary>
    /// <c>TryFormat</c> 在可 span 格式化的基元上确实会生成，同名实例必须报告 STIA0020。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenSpanFormattableInstanceNameIsTryFormat()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            [StronglyTypedIdInstance("TryFormat", "0")]
            public partial record struct OrderId(int Value);
            """";
        var expected = Verify.Diagnostic(Descriptors.InvalidInstanceName)
            .WithArguments("TryFormat", "OrderId")
            .WithSpan(6, 2, 6, 43);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// <c>EqualityContract</c> 只有 <c>record class</c> 由编译器合成；<c>record struct</c> 上没有它，同名实例合法。
    /// </summary>
    /// <remarks>
    /// 缺陷回归：保留名清单曾无条件收录 <c>EqualityContract</c>，值类型 Id 上会误报 STIA0020（Error）。
    /// </remarks>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenRecordStructInstanceNameIsEqualityContract()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            [StronglyTypedIdInstance("EqualityContract", "0")]
            public partial record struct OrderId(int Value);
            """";
        await Verify.VerifyAnalyzerAsync(code);
    }

    /// <summary>
    /// <c>record class</c> 上 <c>EqualityContract</c> 确实存在，同名实例必须报告 STIA0020。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenRecordClassInstanceNameIsEqualityContract()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            [StronglyTypedIdInstance("EqualityContract", "0")]
            public partial record OrderId(int Value);
            """";
        var expected = Verify.Diagnostic(Descriptors.InvalidInstanceName)
            .WithArguments("EqualityContract", "OrderId")
            .WithSpan(6, 2, 6, 50);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// <c>{TypeName}TypeConverter</c> 只在 <c>TypeConverter = true</c> 时生成；默认关闭时同名实例合法。
    /// </summary>
    /// <remarks>
    /// 缺陷回归：保留名清单曾无条件收录该名字，默认配置下会误报 STIA0020（Error）。
    /// </remarks>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenTypeConverterDisabledInstanceNameIsNestedTypeConverter()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            [StronglyTypedIdInstance("OrderIdTypeConverter", "0")]
            public partial record struct OrderId(int Value);
            """";
        await Verify.VerifyAnalyzerAsync(code);
    }

    /// <summary>
    /// <c>TypeConverter = true</c> 时该嵌套类型确实生成，同名实例必须报告 STIA0020。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenTypeConverterEnabledInstanceNameIsNestedTypeConverter()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(TypeConverter = true)]
            [StronglyTypedIdInstance("OrderIdTypeConverter", "0")]
            public partial record struct OrderId(int Value);
            """";
        var expected = Verify.Diagnostic(Descriptors.InvalidInstanceName)
            .WithArguments("OrderIdTypeConverter", "OrderId")
            .WithSpan(6, 2, 6, 54);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// <c>{TypeName}DebugView</c> 在泛型容器里不生成（<c>[DebuggerTypeProxy]</c> 会 CS0416），同名实例合法。
    /// </summary>
    /// <remarks>
    /// 泛型容器本身会报 STIA0028（集成被跳过），这里把它一并声明，只断言「没有 STIA0020」。
    /// </remarks>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenGenericContainerInstanceNameIsDebugView()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            public partial class Box<T>
            {
                [StronglyTypedId]
                [StronglyTypedIdInstance("BoxedIdDebugView", "0")]
                public partial record struct BoxedId(int Value);
            }
            """";
        var expected = Verify.Diagnostic(Descriptors.GenericContainerSkipsIntegrations)
            .WithArguments("BoxedId", "Box")
            .WithSpan(9, 34, 9, 41);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }

    /// <summary>
    /// 非泛型容器里 <c>{TypeName}DebugView</c> 确实生成，同名实例必须报告 STIA0020。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenNonGenericInstanceNameIsDebugView()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            [StronglyTypedIdInstance("OrderIdDebugView", "0")]
            public partial record struct OrderId(int Value);
            """";
        var expected = Verify.Diagnostic(Descriptors.InvalidInstanceName)
            .WithArguments("OrderIdDebugView", "OrderId")
            .WithSpan(6, 2, 6, 50);
        await Verify.VerifyAnalyzerAsync(code, expected);
    }
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

    /// <summary>
    /// 机械推导出的建议名不可用时，STIA0022 不报告。
    /// </summary>
    /// <remarks>
    /// 缺陷回归：建议名此前被无条件报出，而它可能根本不是合法实例名 ——
    /// <c>_1</c> 得到 <c>1</c>（不是标识符）、<c>create</c> 得到 <c>Create</c>（生成成员）、
    /// <c>order_id</c> 得到 <c>OrderId</c>（类型名）。用户照着改（或点 CodeFix）就把一条 Info 提示
    /// 换成了 STIA0020（Error）。现在报出前先按与 STIA0020 同一套判据验一遍。
    /// </remarks>
    [Theory]
    [InlineData("_1")]
    [InlineData("_123")]
    [InlineData("create")]
    [InlineData("try_parse")]
    [InlineData("to_string")]
    [InlineData("deconstruct")]
    [InlineData("order_id")]
    [InlineData("value")]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenSuggestedNameIsNotUsable(string instanceName)
    {
        var code = $$""""
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            [StronglyTypedIdInstance("{{instanceName}}", "0")]
            public partial record struct OrderId(int Value);
            """";

        await Verify.VerifyAnalyzerAsync(code);
    }

    /// <summary>
    /// 建议名与同类型的另一条实例重名时，STIA0022 不报告。
    /// </summary>
    /// <remarks>
    /// 兄弟实例之间的重名要单独判：保留名判定会滤掉生成器产出的实例字段（否则每条实例都会撞上自己），
    /// 因此它看不到这一档。改名为 <c>Foo</c> 会立刻撞上已有的 <c>Foo</c>，得到 STIA0020。
    /// </remarks>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenSuggestedNameCollidesWithSiblingInstance()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            [StronglyTypedIdInstance("foo", "0")]
            [StronglyTypedIdInstance("Foo", "1")]
            public partial record struct OrderId(int Value);
            """";
        await Verify.VerifyAnalyzerAsync(code);
    }

    /// <summary>
    /// 建议名与用户自己写的成员重名时，STIA0022 同样不报告。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenSuggestedNameCollidesWithUserMember()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            [StronglyTypedIdInstance("bar", "0")]
            public partial record struct OrderId(int Value)
            {
                public static readonly OrderId Bar = new(9);
            }
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
