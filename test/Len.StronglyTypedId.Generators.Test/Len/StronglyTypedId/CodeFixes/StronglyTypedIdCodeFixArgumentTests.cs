using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Len.StronglyTypedId.CodeFixes;

public class StronglyTypedIdCodeFixArgumentTests
{
    /// <summary>
    /// STIA0010 增强：当 <c>Validator</c> 指向的方法根本不存在时，CodeFix 应在 Id 类型里生成一个符合契约的桩方法（静态、返回 bool、恰好一个参数且类型等于基元 Id
    /// 类型），而非删掉实参。桩方法用全限定名渲染基元类型，避免与任何同名局部类型冲突。
    /// </summary>
    /// <remarks>
    /// 桩体必须是 <c>throw</c> 而不是 <c>=&gt; true</c>：Fix All 会把桩铺到几十个 Id 上，恒真的桩等于把校验整体架空。
    /// </remarks>
    [Fact]
    public async Task CodeFix_Should_GenerateValidatorStub_WhenValidatorMethodMissing()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Validator = nameof(Validate))]
            public partial record OrderId(Guid Value);
            """";

        var fixedCode = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Validator = nameof(Validate))]
            public partial record OrderId(Guid Value)
            {
                private static bool Validate(global::System.Guid value) => throw new global::System.NotImplementedException("TODO: implement the validation rule.");
            }
            """";

        var expected = Verify.Diagnostic(Descriptors.ValidatorReferenceInvalid)
            .WithSpan(5, 18, 5, 46).WithArguments("Validate", "System.Guid");

        await Verify.VerifyCodeFixAsync(code, expected, fixedCode);
    }

    /// <summary>
    /// STIA0017：<c>Normalizer</c> 指向的方法不存在时，CodeFix 生成恒等变换的桩方法。
    /// </summary>
    [Fact]
    public async Task CodeFix_Should_GenerateNormalizerStub_WhenNormalizerMethodMissing()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Normalizer = nameof(Normalize))]
            public partial record OrderId(Guid Value);
            """";

        var fixedCode = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Normalizer = nameof(Normalize))]
            public partial record OrderId(Guid Value)
            {
                private static global::System.Guid Normalize(global::System.Guid value) => value;
            }
            """";

        var expected = Verify.Diagnostic(Descriptors.NormalizerReferenceInvalid)
            .WithSpan(5, 18, 5, 48).WithArguments("Normalize", "System.Guid");

        await Verify.VerifyCodeFixAsync(code, expected, fixedCode);
    }

    /// <summary>
    /// STIA0017：归一化方法已存在但签名不符时删掉实参——再生成同名方法会撞 CS0101。
    /// </summary>
    [Fact]
    public async Task CodeFix_Should_RemoveNormalizerArgument_WhenNormalizerSignatureInvalid()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Normalizer = nameof(Normalize))]
            public partial record OrderId(Guid Value)
            {
                private static void Normalize(Guid value) { }
            }
            """";

        var fixedCode = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record OrderId(Guid Value)
            {
                private static void Normalize(Guid value) { }
            }
            """";

        var expected = Verify.Diagnostic(Descriptors.NormalizerReferenceInvalid)
            .WithSpan(5, 18, 5, 48).WithArguments("Normalize", "System.Guid");

        await Verify.VerifyCodeFixAsync(code, expected, fixedCode);
    }

    /// <summary>
    /// STIA0018：异常类型不可用时删掉 <c>ExceptionType</c> 实参，回退到 <see cref="ArgumentException"/>。
    /// </summary>
    [Fact]
    public async Task CodeFix_Should_RemoveExceptionTypeArgument_WhenExceptionTypeIsInvalid()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(ExceptionType = typeof(string))]
            public partial record OrderId(Guid Value);
            """";

        var fixedCode = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record OrderId(Guid Value);
            """";

        var expected = Verify.Diagnostic(Descriptors.ExceptionTypeInvalid)
            .WithSpan(5, 18, 5, 48).WithArguments("string");

        await Verify.VerifyCodeFixAsync(code, expected, fixedCode);
    }

    /// <summary>
    /// 证明新修复同样走 BatchFixer 的「合并到同一语法树后整体应用」路径（与既有 TypeMustBePartial 的 FixAll 互补）。
    /// </summary>
    [Fact]
    public async Task CodeFixAll_Should_ConvertMultipleClasses_WhenTypeMustBeRecord()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial class OrderId(Guid Value) { }

            [StronglyTypedId]
            public partial class ProductId(Guid Value) { }
            """";

        var batchFixedCode = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record OrderId(Guid Value) { }

            [StronglyTypedId]
            public partial record ProductId(Guid Value) { }
            """";

        var expected = new[]
        {
            Verify.Diagnostic(Descriptors.TypeMustBeRecord)
                .WithSpan(5, 1, 6, 45).WithArguments("OrderId"),
            Verify.Diagnostic(Descriptors.TypeMustBeRecord)
                .WithSpan(8, 1, 9, 47).WithArguments("ProductId"),
        };

        await Verify.VerifyCodeFixAllAsync(code, expected, batchFixedCode);
    }

    /// <summary>
    /// STIA0005 的 IsFixable 负向分支：record 已有 body 构造函数、却无单参主构造函数时，再加主构造会与既有 body 构造撞 CS0111。此时诊断虽触发，但 CodeFix
    /// 不应提供任何修复入口—— RegisterCodeFixesAsync 经 IsFixable 门禁直接跳过，注册 0 个 CodeAction。该场景无法用 VerifyCodeFixAsync
    /// 表达（框架假定修复一定会被应用），故直接驱动 provider 断言 0 个修复。
    /// </summary>
    [Fact]
    public async Task CodeFix_ShouldNotOfferFix_WhenRecordHasBodyConstructor()
    {
        var code = """
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record OrderId
            {
                public OrderId(Guid value) { }
            }
            """;

        var offered = await Verify.CountOfferedCodeFixesAsync(
            code,
            Descriptors.TypeMustHaveSingleParameterPrimaryConstructor,
            "OrderId");

        Assert.Empty(offered);
    }

    /// <summary>
    /// 对照：同一 harness 在「可修复」情形下确实会注册修复（record 无主构造、也无 body 构造时 STIA0005 可修），证明上面的空集合断言是真门禁生效、而非 harness 根本没驱动
    /// provider。
    /// </summary>
    [Fact]
    public async Task CodeFix_ShouldOfferFix_WhenRecordHasNoConstructor()
    {
        var code = """
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record OrderId { }
            """;

        var offered = await Verify.CountOfferedCodeFixesAsync(
            code,
            Descriptors.TypeMustHaveSingleParameterPrimaryConstructor,
            "OrderId");

        Assert.Single(offered);
    }
}
