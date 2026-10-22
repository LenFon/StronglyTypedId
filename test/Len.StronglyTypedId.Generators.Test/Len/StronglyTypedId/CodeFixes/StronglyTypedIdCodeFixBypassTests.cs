using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Len.StronglyTypedId.CodeFixes;

public class StronglyTypedIdCodeFixBypassTests
{
    /// <summary>
    /// 绕过验证器直接用 <c>new</c> 构造（STIA0011）时，CodeFix 应把它重写成 <c>Xxx.Create(...)</c>。
    /// </summary>
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
    /// 目标类型化的 <c>new(...)</c> 没有 <c>Type</c> 属性，修复必须回查语义模型才能拼出 <c>OrderId.Create(...)</c>。
    /// </summary>
    [Fact]
    public async Task CodeFix_Should_ReplaceTargetTypedNewWithCreate_WhenBypassingValidator()
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
                public OrderId Build() => new(Guid.NewGuid());
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
            .WithArguments("OrderId").WithSpan(13, 31, 13, 50);

        await Verify.VerifyCodeFixAsync(code, expected, fixedCode);
    }

    /// <summary>
    /// <c>default</c> 不提供修复：改成 <c>Create(default)</c> 只会把编译期提示换成运行时异常。
    /// </summary>
    [Fact]
    public async Task CodeFix_ShouldNotOfferFix_WhenDefaultLiteralBypassesValidator()
    {
        var code = """
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Validator = nameof(Validate))]
            public partial record struct OrderId(Guid Value)
            {
                private static bool Validate(Guid value) => value != Guid.Empty;
            }

            public class Builder
            {
                public OrderId Build() => default;
            }
            """;

        var offered = await Verify.CountOfferedCodeFixesAsync(
            code,
            Descriptors.BypassCreate,
            "OrderId",
            static root => root.DescendantNodes()
                .OfType<LiteralExpressionSyntax>()
                .First(literal => literal.IsKind(SyntaxKind.DefaultLiteralExpression)));

        Assert.Empty(offered);
    }

    /// <summary>
    /// 空实参的 <c>new Xxx()</c> 不提供修复：<c>ArgumentList</c> 不是 <see langword="null"/> 而是空表，
    /// 只判 null 会产出缺参数的 <c>Xxx.Create()</c>，修复后反而编译不过。
    /// </summary>
    [Fact]
    public async Task CodeFix_ShouldNotOfferFix_WhenObjectCreationHasNoArguments()
    {
        var code = """
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Validator = nameof(Validate))]
            public partial record struct OrderId(Guid Value)
            {
                private static bool Validate(Guid value) => value != Guid.Empty;
            }

            public class Builder
            {
                public OrderId Build() => new OrderId();
            }
            """;

        var offered = await Verify.CountOfferedCodeFixesAsync(
            code,
            Descriptors.BypassCreate,
            "OrderId",
            static root => root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>().First());

        Assert.Empty(offered);
    }

    /// <summary>
    /// 对照：同一 harness 在目标类型化 <c>new(...)</c> 上确实会注册修复，证明上面的空集合断言是真门禁生效、而非 harness 根本没驱动 provider。
    /// </summary>
    [Fact]
    public async Task CodeFix_ShouldOfferFix_WhenTargetTypedNewBypassesValidator()
    {
        var code = """
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Validator = nameof(Validate))]
            public partial record struct OrderId(Guid Value)
            {
                private static bool Validate(Guid value) => value != Guid.Empty;
            }

            public class Builder
            {
                public OrderId Build() => new(Guid.NewGuid());
            }
            """;

        var offered = await Verify.CountOfferedCodeFixesAsync(
            code,
            Descriptors.BypassCreate,
            "OrderId",
            static root => root.DescendantNodes().OfType<ImplicitObjectCreationExpressionSyntax>().First());

        Assert.Single(offered);
    }
}
