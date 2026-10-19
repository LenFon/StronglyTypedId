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
    /// <remarks>
    /// 基元类型判据曾只比简单名：这种同名类型会被当作合法基元放行，生成器随后把它嵌进生成代码，
    /// 使用者最终看到的是若干条报在<b>生成文件</b>里的 CS0315（类型不满足泛型约束），
    /// 而不是一条指向自己源码的 STIA0008。
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

    #endregion

    #region 空前缀（STIA0013）

    /// <summary>
    /// <c>Prefix = ""</c> 没有任何语义，应报告 STIA0013。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenPrefixIsEmpty()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Prefix = "")]
            public partial record struct OrderId(Guid Value);
            """";
        var expected = Verify.Diagnostic(Descriptors.InvalidPrefix)
            .WithSpan(5, 18, 5, 29);
        await Verify.VerifyAnalyzerAsync(code, expected);
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
    /// 常量引用的空前缀同样能判定：取值走符号级命名实参，而非只看字符串字面量。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenPrefixConstantIsEmpty()
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
        var expected = Verify.Diagnostic(Descriptors.InvalidPrefix)
            .WithSpan(5, 18, 5, 32);
        await Verify.VerifyAnalyzerAsync(code, expected);
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

    #region 验证器可被绕过（STIA0014）

    /// <summary>
    /// 规则默认关闭：即便「public + Validator」这种可被绕过的组合，未在 <c>.editorconfig</c> 调级时也不上报。
    /// </summary>
    /// <remarks>
    /// 这是该规则的设计前提 —— 绝大多数公开 Id 都会配验证器，默认开启只会制造噪音。
    /// </remarks>
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
    /// <remarks>
    /// 可见性必须逐层向上判定：只看类型自身的 <c>public</c> 会产生误报。
    /// </remarks>
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
    /// <remarks>
    /// 若建议名简单地取「原名 + Id」，这里会得到 <c>OrderIDId</c> —— 一条没法照着改的建议。
    /// </remarks>
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
    /// <remarks>
    /// 每个成员各报一次会让 n 个同基元 Id 产出 n 条重复提示，而这条规则只是「回头看一眼」的提醒。
    /// </remarks>
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

    /// <summary>
    /// 嵌套 Id 的作用域由包含类型界定，不属于命名空间级重复，不参与分组。
    /// </summary>
    /// <remarks>
    /// <c>GetTypeMembers</c> 只返回命名空间的直接成员，嵌套类型天然不在其中 —— 这里把该行为固化成用例，
    /// 免得日后换成别的枚举方式时悄悄把嵌套类型也拉进来。
    /// </remarks>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenNestedIdSharesPrimitiveWithTopLevelId()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct OrderId(Guid Value);

            public partial class Aggregate
            {
                [StronglyTypedId]
                public partial record struct AggregateId(Guid Value);
            }
            """";
        await Verify.VerifyAnalyzerWithStyleHintsAsync(code);
    }

    #endregion

    /// <summary>
    /// STIA0007 / STIA0008 的规则 ID 与对应的实参样例。
    /// </summary>
    /// <remarks>
    /// 数据里放规则 ID（<see cref="string"/>）而不是 <see cref="DiagnosticDescriptor"/> 本身：
    /// xUnit1044 要求 <c>TheoryData</c> 的类型实参可序列化，否则 Test Explorer 无法逐行枚举
    /// 数据（每行会塌成一个用例）；<see cref="DiagnosticDescriptor"/> 不可序列化，规则 ID 可以。
    /// 描述符在用例内按 ID 从 <see cref="Descriptors.All"/> 取回。
    /// </remarks>
    public static TheoryData<string, string> ParameterDiagnostics => new()
    {
        { Descriptors.ParameterNameMustBeValueId, "Value1" },
        { Descriptors.ParameterTypeIsInvalidId, "bool" },
    };

    /// <summary>
    /// 回归用例：STIA0007 / STIA0008 的文案必须保留 <c>{0}</c> 占位符。
    /// </summary>
    /// <remarks>
    /// 分析器实现传入了实参（参数名 / 参数类型），文案缺少占位符时它们被静默丢弃，
    /// 诊断里看不到究竟是哪个名字或哪个类型。断言「渲染文本包含实参」与当前区域性无关：
    /// 不显式指定区域性时 <see cref="LocalizedString"/> 回退到内嵌的 en 资源。
    /// </remarks>
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
