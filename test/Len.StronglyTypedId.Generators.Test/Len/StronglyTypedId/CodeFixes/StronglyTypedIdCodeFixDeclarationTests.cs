using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Len.StronglyTypedId.CodeFixes;

public class StronglyTypedIdCodeFixDeclarationTests
{
    private readonly static string _fixedCode = """
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

        await Verify.VerifyCodeFixAsync(code, expected, _fixedCode);
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

        await Verify.VerifyCodeFixAsync(code, expected, _fixedCode);
    }

    [Fact]
    public async Task CodeFix_Should_ReturnDiagnostic_WhenIdItselfIsFileLocal()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            file partial record struct OrderId(Guid Value);
            """";

        var expected = Verify.Diagnostic(Descriptors.TypeCannotBeFileLocal)
            .WithSpan(5, 1, 6, 48).WithArguments("OrderId");

        // 摘掉 file 后类型回到普通 partial record：生成器终于能在自己的文件里重开它。
        var fixedCode = """
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            partial record struct OrderId(Guid Value);
            """;

        await Verify.VerifyCodeFixAsync(code, expected, fixedCode);
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

        await Verify.VerifyCodeFixAsync(code, expected, _fixedCode);
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

        await Verify.VerifyCodeFixAsync(code, expected, _fixedCode);
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

        await Verify.VerifyCodeFixAsync(code, expected, _fixedCode);
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
