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
}
