using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Len.StronglyTypedId.CodeFixes;

/// <summary>
/// M3 新增 CodeFix 的覆盖：STIA0019 / STIA0024（删掉 <c>Integrations</c> 实参）、STIA0020 / STIA0021（删掉整条
/// <c>[StronglyTypedIdInstance]</c> 特性）、STIA0022（把实例名改成 PascalCase 建议名）。
/// </summary>
public class StronglyTypedIdCodeFixIntegrationInstanceTests
{
    #region STIA0019 —— 缺包时删掉 Integrations 实参（同 STIA0024 的修复机制）

    /// <summary>
    /// 请求了 Dapper 但编译未引用其程序集（测试基建只挂了净 8.0 引用程序集），STIA0019 触发；CodeFix 删掉
    /// <c>Integrations</c> 实参，让类型退化为不带该集成的普通 Id。
    /// </summary>
    [Fact]
    public async Task CodeFix_Should_RemoveIntegrationsArgument_WhenIntegrationUnavailable()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Integrations = StronglyTypedIdIntegrations.Dapper)]
            public partial record OrderId(Guid Value);
            """";

        var fixedCode = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record OrderId(Guid Value);
            """";

        var actual = await Verify.ApplyCodeFixAsync(
            code,
            Descriptors.IntegrationUnavailable,
            static root => root.DescendantNodes()
                .OfType<AttributeArgumentSyntax>()
                .First(arg => arg.NameEquals is not null && arg.NameEquals.Name.Identifier.ValueText == "Integrations"));

        actual.Should().Be(fixedCode);
    }

    #endregion

    #region STIA0024 —— 集成掩码含未定义位时删掉 Integrations 实参

    /// <summary>
    /// <c>Integrations</c> 取值含有超出 <c>All</c> 的位（<c>(StronglyTypedIdIntegrations)1048576</c>），STIA0024 触发；
    /// 修复与 STIA0019 同源：删掉 <c>Integrations</c> 实参即去掉未定义的位。这里用轻量的「修复是否被提供」断言验证接线，
    /// 实际改写结果已由上面的 STIA0019 用例覆盖。
    /// </summary>
    /// <remarks>
    /// 取值必须真的在 <c>All</c> 之外：<c>2048</c> 曾是越界位，但加入 <c>Protobuf</c> 后它已是合法标志，
    /// 继续用它会让这条用例在「诊断根本不会触发」的前提下空转。
    /// </remarks>
    [Fact]
    public async Task CodeFix_ShouldOfferFix_WhenUnknownIntegrationBits()
    {
        var code = """
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Integrations = (StronglyTypedIdIntegrations)1048576)]
            public partial record OrderId(Guid Value);
            """;

        var offered = await Verify.CountOfferedCodeFixesAsync(
            code,
            Descriptors.UnknownIntegration,
            "OrderId",
            static root => root.DescendantNodes()
                .OfType<AttributeArgumentSyntax>()
                .First(arg => arg.NameEquals is not null && arg.NameEquals.Name.Identifier.ValueText == "Integrations"));

        offered.Should().ContainSingle();
    }

    #endregion

    #region STIA0020 / STIA0021 —— 命名实例名字非法或取值无法解析时删掉整条特性

    /// <summary>
    /// <c>[StronglyTypedIdInstance("123abc", "...")]</c> 的名字 <c>123abc</c> 不是合法标识符（STIA0020）。CodeFix 摘掉整条特性，
    /// 把「重新挑一个合法名字」留给人决定。
    /// </summary>
    [Fact]
    public async Task CodeFix_Should_RemoveInstanceAttribute_WhenInstanceNameInvalid()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            [StronglyTypedIdInstance("123abc", "00000000-0000-0000-0000-000000000000")]
            public partial record AdminId(Guid Value);
            """";

        var fixedCode = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record AdminId(Guid Value);
            """";

        var expected = Verify.Diagnostic(Descriptors.InvalidInstanceName)
            .WithArguments("123abc", "AdminId")
            .WithSpan(6, 2, 6, 75);

        await Verify.VerifyCodeFixAsync(code, expected, fixedCode);
    }

    /// <summary>
    /// <c>[StronglyTypedIdInstance("Admin", "xyz")]</c> 的值 <c>xyz</c> 无法解析为 <c>Guid</c>（STIA0021）。与 STIA0020 同源修复：删掉整条特性。
    /// 用「修复是否被提供」断言验证接线，实际改写结果已由上面的 STIA0020 用例覆盖。
    /// </summary>
    [Fact]
    public async Task CodeFix_ShouldOfferFix_WhenInstanceValueInvalid()
    {
        var code = """
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            [StronglyTypedIdInstance("Admin", "xyz")]
            public partial record AdminId(Guid Value);
            """;

        var offered = await Verify.CountOfferedCodeFixesAsync(
            code,
            Descriptors.InvalidInstanceValue,
            "AdminId",
            static root => root.DescendantNodes()
                .OfType<AttributeSyntax>()
                .First(attr => attr.Name is IdentifierNameSyntax { Identifier.ValueText: "StronglyTypedIdInstance" }));

        offered.Should().ContainSingle();
    }

    #endregion

    #region STIA0022 —— PascalCase 改名

    /// <summary>
    /// STIA0022 出现在可修复集合里。
    /// </summary>
    [Fact]
    public void CodeFix_ShouldListInstanceNameNotPascalCaseAsFixable()
        => new StronglyTypedIdCodeFixProvider().FixableDiagnosticIds
            .Should().Contain(Descriptors.InstanceNameNotPascalCaseId);

    /// <summary>
    /// 下划线分隔的名字被改写成 PascalCase；取值实参与特性其余部分原样保留。
    /// </summary>
    [Fact]
    public async Task CodeFix_Should_RenameInstanceToPascalCase()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            [StronglyTypedIdInstance("not_set", "-1")]
            public partial record struct OrderId(int Value);
            """";

        var fixedCode = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            [StronglyTypedIdInstance("NotSet", "-1")]
            public partial record struct OrderId(int Value);
            """";

        var actual = await Verify.ApplyCodeFixAsync(
            code,
            Descriptors.InstanceNameNotPascalCase,
            static root => root.DescendantNodes()
                .OfType<AttributeSyntax>()
                .First(attr => attr.Name is IdentifierNameSyntax { Identifier.ValueText: "StronglyTypedIdInstance" }));

        actual.Should().Be(fixedCode);
    }

    /// <summary>
    /// 只改首字母的情形（<c>unspecified</c> → <c>Unspecified</c>）。
    /// </summary>
    [Fact]
    public async Task CodeFix_Should_CapitalizeFirstLetterOnly()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            [StronglyTypedIdInstance("unspecified", "-1")]
            public partial record struct OrderId(int Value);
            """";

        var fixedCode = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            [StronglyTypedIdInstance("Unspecified", "-1")]
            public partial record struct OrderId(int Value);
            """";

        var actual = await Verify.ApplyCodeFixAsync(
            code,
            Descriptors.InstanceNameNotPascalCase,
            static root => root.DescendantNodes()
                .OfType<AttributeSyntax>()
                .First(attr => attr.Name is IdentifierNameSyntax { Identifier.ValueText: "StronglyTypedIdInstance" }));

        actual.Should().Be(fixedCode);
    }

    /// <summary>
    /// 同一类型上有多个命名实例时，只改诊断落点那一条，其余原样保留。
    /// </summary>
    [Fact]
    public async Task CodeFix_Should_RenameOnlyTheTargetedInstance()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            [StronglyTypedIdInstance("not_set", "-1")]
            [StronglyTypedIdInstance("another_one", "-2")]
            public partial record struct OrderId(int Value);
            """";

        var fixedCode = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            [StronglyTypedIdInstance("NotSet", "-1")]
            [StronglyTypedIdInstance("another_one", "-2")]
            public partial record struct OrderId(int Value);
            """";

        var actual = await Verify.ApplyCodeFixAsync(
            code,
            Descriptors.InstanceNameNotPascalCase,
            static root => root.DescendantNodes()
                .OfType<AttributeSyntax>()
                .First(attr => attr.Name is IdentifierNameSyntax { Identifier.ValueText: "StronglyTypedIdInstance" }));

        actual.Should().Be(fixedCode);
    }

    /// <summary>
    /// 名字实参是 <c>const</c> 引用而非字面量时不给修复：修复拿不到语义模型，无法解析出它的值。
    /// </summary>
    [Fact]
    public async Task CodeFix_ShouldOfferNothing_WhenInstanceNameIsConstReference()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            [StronglyTypedIdInstance(Name, "-1")]
            public partial record struct OrderId(int Value);

            internal static class Names
            {
                internal const string Name = "not_set";
            }
            """";

        var offered = await Verify.CountOfferedCodeFixesAsync(
            code,
            Descriptors.InstanceNameNotPascalCase,
            "OrderId",
            static root => root.DescendantNodes()
                .OfType<AttributeSyntax>()
                .First(attr => attr.Name is IdentifierNameSyntax { Identifier.ValueText: "StronglyTypedIdInstance" }));

        offered.Should().BeEmpty();
    }

    #endregion
}
