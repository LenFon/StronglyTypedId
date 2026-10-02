namespace Len.StronglyTypedId.CodeFixes;

public class StronglyTypedIdCodeFixProviderTests
{
    private readonly static string FixedCode = """
        using System;
        
        namespace Len.StronglyTypedId.Tests;
        
        [StronglyTypedId]
        public partial record struct OrderId(Guid Value);
        """;

    [Fact]
    public async Task CodeFix_Should_ReturnDiagnostic_WhenCannotPartial()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;
            
            [StronglyTypedId]
            public record struct OrderId(Guid Value);
            """";

        var expected = Verify.Diagnostic(Descriptors.TypeMustBePartial)
            .WithSpan(5, 1, 6, 42).WithArguments("OrderId");

        await Verify.VerifyCodeFixAsync(code, expected, FixedCode);
    }

    [Fact]
    public async Task CodeFix_Should_ReturnDiagnostic_WhenAbstract()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;
            
            [StronglyTypedId]
            public abstract partial record struct OrderId(Guid Value);
            """";

        var expected = Verify.Diagnostic(Descriptors.TypeCannotBeAbstract)
            .WithSpan(5, 1, 6, 59).WithArguments("OrderId");

        await Verify.VerifyCodeFixAsync(code, expected, FixedCode);
    }

    [Fact]
    public async Task CodeFix_Should_ReturnDiagnostic_WhenParameterNameCannotBeValue()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;
            
            [StronglyTypedId]
            public partial record struct OrderId(Guid Value1);
            """";

        var expected = Verify.Diagnostic(Descriptors.ParameterNameMustBeValue)
            .WithSpan(6, 43, 6, 49).WithArguments("Value1");

        await Verify.VerifyCodeFixAsync(code, expected, FixedCode);
    }

    [Fact]
    public async Task CodeFix_Should_ReturnDiagnostic_WhenParameterNullable()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;
            
            [StronglyTypedId]
            public partial record struct OrderId(Guid? Value);
            """";

        var expected = Verify.Diagnostic(Descriptors.ParameterCannotBeNullable)
            .WithSpan(6, 38, 6, 43).WithArguments("Guid?");

        await Verify.VerifyCodeFixAsync(code, expected, FixedCode);
    }

    [Fact]
    public async Task CodeFix_Should_ReturnDiagnostic_WhenGeneric()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;
            
            [StronglyTypedId]
            public partial record struct OrderId<T>(Guid Value);
            """";

        var expected = Verify.Diagnostic(Descriptors.TypeCannotBeGeneric)
            .WithSpan(5, 1, 6, 53).WithArguments("OrderId");

        await Verify.VerifyCodeFixAsync(code, expected, FixedCode);
    }

    [Fact]
    public async Task CodeFix_Should_RemoveConstraintClauses_WhenGeneric()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct OrderId<T>(Guid Value) where T : class;
            """";

        // 只删 <T> 会留下 `where T : class`，而约束不允许出现在非泛型声明上（CS0080）。
        var fixedCode = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct OrderId(Guid Value);
            """";

        var expected = Verify.Diagnostic(Descriptors.TypeCannotBeGeneric)
            .WithSpan(5, 1, 6, 69).WithArguments("OrderId");

        await Verify.VerifyCodeFixAsync(code, expected, fixedCode);
    }

    [Fact]
    public async Task CodeFix_Should_PreserveParameterAttribute_WhenRenamingParameter()
    {
        var code = """"
            using System;
            using System.Diagnostics.CodeAnalysis;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct OrderId([AllowNull] Guid Value1);
            """";

        // 重建参数节点会丢掉参数上的 attribute，只剩「类型 + 名字」。
        var fixedCode = """"
            using System;
            using System.Diagnostics.CodeAnalysis;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct OrderId([AllowNull] Guid Value);
            """";

        var expected = Verify.Diagnostic(Descriptors.ParameterNameMustBeValue)
            .WithSpan(7, 55, 7, 61).WithArguments("Value1");

        await Verify.VerifyCodeFixAsync(code, expected, fixedCode);
    }

    [Fact]
    public async Task CodeFix_Should_PreserveParameterAttributeAndTrivia_WhenRemovingNullable()
    {
        var code = """"
            using System;
            using System.Diagnostics.CodeAnalysis;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct OrderId([AllowNull] /* raw */ Guid? Value);
            """";

        var fixedCode = """"
            using System;
            using System.Diagnostics.CodeAnalysis;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct OrderId([AllowNull] /* raw */ Guid Value);
            """";

        var expected = Verify.Diagnostic(Descriptors.ParameterCannotBeNullable)
            .WithSpan(7, 60, 7, 65).WithArguments("Guid?");

        await Verify.VerifyCodeFixAsync(code, expected, fixedCode);
    }

    /// <summary>
    /// FixAll（<c>BatchFixer</c>）：一次把源码里全部可修复诊断处理完。
    /// </summary>
    /// <remarks>
    /// 既有用例都是「一条诊断、一次修复」。FixAll 走的是 <c>GetFixAllProvider()</c> 返回的批处理器，
    /// 与逐条修复是两条不同的代码路径：批处理会把各诊断的改动合并到同一份语法树后再整体应用，
    /// 因此「两条诊断分别位于不同声明上、且都要就地加 partial」这一情形需要单独把守。
    /// </remarks>
    [Fact]
    public async Task CodeFix_Should_AddPartialToContainingType()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;
            
            public class Container
            {
                [StronglyTypedId]
                public partial record struct OrderId(Guid Value);
            }
            """";

        var fixedCode = """"
            using System;

            namespace Len.StronglyTypedId.Tests;
            
            public partial class Container
            {
                [StronglyTypedId]
                public partial record struct OrderId(Guid Value);
            }
            """";

        var expected = Verify.Diagnostic(Descriptors.ContainingTypeMustBePartial)
            .WithSpan(5, 1, 9, 2).WithArguments("Container");

        await Verify.VerifyCodeFixAsync(code, expected, fixedCode);
    }

    [Fact]
    public async Task CodeFixAll_Should_FixEveryRepairableDiagnosticInOnePass()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;
            
            [StronglyTypedId]
            public record struct OrderId(Guid Value);

            [StronglyTypedId]
            public record struct ProductId(Guid Value);
            """";

        var batchFixedCode = """"
            using System;

            namespace Len.StronglyTypedId.Tests;
            
            [StronglyTypedId]
            public partial record struct OrderId(Guid Value);

            [StronglyTypedId]
            public partial record struct ProductId(Guid Value);
            """";

        var expected = new[]
        {
            Verify.Diagnostic(Descriptors.TypeMustBePartial)
                .WithSpan(5, 1, 6, 42).WithArguments("OrderId"),
            Verify.Diagnostic(Descriptors.TypeMustBePartial)
                .WithSpan(8, 1, 9, 44).WithArguments("ProductId"),
        };

        await Verify.VerifyCodeFixAllAsync(code, expected, batchFixedCode);
    }

    /// <summary>
    /// 绕过验证器直接用 <c>new</c> 构造（STIAO011）时，CodeFix 应把它重写成 <c>Xxx.Create(...)</c>。
    /// </summary>
    /// <remarks>
    /// 该诊断只在 Id 设了验证器时触发，而该情形下生成代码已提供静态 <c>Create</c> 工厂，
    /// 重写后的调用点强制经过校验，与诊断的意图一致。
    /// </remarks>
    [Fact]
    public async Task CodeFix_Should_ReplaceNewWithCreate_WhenBypassingValidator()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Validator = nameof(Validate))]
            public partial record struct OrderId(Guid Value)
            {
                private static bool Validate(Guid value) => value != Guid.Empty;
            }

            public class Builder
            {
                public OrderId Build() => new OrderId(Guid.NewGuid());
            }
            """";

        var fixedCode = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Validator = nameof(Validate))]
            public partial record struct OrderId(Guid Value)
            {
                private static bool Validate(Guid value) => value != Guid.Empty;
            }

            public class Builder
            {
                public OrderId Build() => OrderId.Create(Guid.NewGuid());
            }
            """";

        var expected = Verify.Diagnostic(Descriptors.BypassCreate)
            .WithArguments("OrderId").WithSpan(13, 31, 13, 58);

        await Verify.VerifyCodeFixAsync(code, expected, fixedCode);
    }

    /// <summary>
    /// STIAO000：把 class 转成 record（保留修饰符 / 主构造函数 / 成员 / 标识符）。
    /// </summary>
    /// <remarks>
    /// 输入特意写成「命名空间内、已 partial、主构造函数合法」的 class，使修复只改关键字、不再级联其它规则：
    /// 转换后得到的 <c>partial record OrderId(Guid Value)</c> 已满足 partial / 命名空间 / 主构造全部约束。
    /// </remarks>
    [Fact]
    public async Task CodeFix_Should_ConvertClassToRecord_WhenTypeMustBeRecord()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial class OrderId(Guid Value) { }
            """";

        var fixedCode = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record OrderId(Guid Value) { }
            """";

        var expected = Verify.Diagnostic(Descriptors.TypeMustBeRecord)
            .WithSpan(5, 1, 6, 45).WithArguments("OrderId");

        await Verify.VerifyCodeFixAsync(code, expected, fixedCode);
    }

    /// <summary>
    /// STIAO004：把最外层类型包进命名空间块（占位名固定 MyNamespace，由使用者改名）。
    /// </summary>
    /// <remarks>
    /// 顶层类型（直接挂在编译单元下）才触发 STIAO004；此处必须显式 <c>using Len.StronglyTypedId;</c>
    /// 让特性在命名空间外也能绑定，否则分析器会因找不到特性而零产出。
    /// </remarks>
    [Fact]
    public async Task CodeFix_Should_WrapInNamespace_WhenTypeMustHaveNamespace()
    {
        var code = """"
            using System;
            using Len.StronglyTypedId;

            [StronglyTypedId]
            public partial record struct OrderId(Guid Value);
            """";

        var fixedCode = """"
            using System;
            using Len.StronglyTypedId;

            namespace MyNamespace
            {
                [StronglyTypedId]
                public partial record struct OrderId(Guid Value);
            }
            """";

        var expected = Verify.Diagnostic(Descriptors.TypeMustHaveNamespace)
            .WithSpan(4, 1, 5, 50).WithArguments("OrderId");

        await Verify.VerifyCodeFixAsync(code, expected, fixedCode);
    }

    /// <summary>
    /// STIAO005：缺主构造函数时补一个单参主构造 (Guid Value)。
    /// </summary>
    [Fact]
    public async Task CodeFix_Should_AddPrimaryConstructor_WhenMissing()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record OrderId { }
            """";

        var fixedCode = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record OrderId(Guid Value) { }
            """";

        var expected = Verify.Diagnostic(Descriptors.TypeMustHaveSingleParameterPrimaryConstructor)
            .WithSpan(5, 1, 6, 34).WithArguments("OrderId");

        await Verify.VerifyCodeFixAsync(code, expected, fixedCode);
    }

    /// <summary>
    /// STIAO005：主构造函数多参数时整段替换为单参版本，并把参数名规整为 Value。
    /// </summary>
    [Fact]
    public async Task CodeFix_Should_FixPrimaryConstructor_WhenMultipleParameters()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record OrderId(Guid value, string name);
            """";

        var fixedCode = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record OrderId(Guid Value);
            """";

        var expected = Verify.Diagnostic(Descriptors.TypeMustHaveSingleParameterPrimaryConstructor)
            .WithSpan(5, 1, 6, 56).WithArguments("OrderId");

        await Verify.VerifyCodeFixAsync(code, expected, fixedCode);
    }

    /// <summary>
    /// STIAO008：把非受支持基元类型参数改成 Guid（名称此前已是 Value）。
    /// </summary>
    [Fact]
    public async Task CodeFix_Should_ChangeParameterTypeToGuid_WhenTypeInvalid()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record OrderId(Foo Value);
            """";

        var fixedCode = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record OrderId(Guid Value);
            """";

        var expected = Verify.Diagnostic(Descriptors.ParameterTypeIsInvalid)
            .WithSpan(6, 31, 6, 34).WithArguments("Foo");

        await Verify.VerifyCodeFixAsync(code, expected, fixedCode);
    }

    /// <summary>
    /// STIAO010：删掉 [StronglyTypedId] 里无效的 Validator 命名实参（唯一实参时连同括号一起删）。
    /// </summary>
    [Fact]
    public async Task CodeFix_Should_RemoveValidatorArgument_WhenValidatorInvalid()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Validator = nameof(Validate))]
            public partial record OrderId(Guid Value)
            {
                private static int Validate(Guid value) => 0;
            }
            """";

        var fixedCode = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record OrderId(Guid Value)
            {
                private static int Validate(Guid value) => 0;
            }
            """";

        var expected = Verify.Diagnostic(Descriptors.ValidatorReferenceInvalid)
            .WithSpan(5, 18, 5, 46).WithArguments("Validate", "System.Guid");

        await Verify.VerifyCodeFixAsync(code, expected, fixedCode);
    }
}
