using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Len.StronglyTypedId.CodeFixes;

/// <summary>
/// M3 新增 CodeFix 的覆盖：STIA0019 / STIA0024（删掉 <c>Integrations</c> 实参）、STIA0020 / STIA0021（删掉整条
/// <c>[StronglyTypedIdInstance]</c> 特性）。STIA0022（PascalCase 改名）刻意不提供修复——改名会牵动所有引用点，单文件无法安全完成。
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
    /// <c>Integrations</c> 取值含有超出 <c>All</c> 的位（<c>(StronglyTypedIdIntegrations)2048</c>），STIA0024 触发；
    /// 修复与 STIA0019 同源：删掉 <c>Integrations</c> 实参即去掉未定义的位。这里用轻量的「修复是否被提供」断言验证接线，
    /// 实际改写结果已由上面的 STIA0019 用例覆盖。
    /// </summary>
    [Fact]
    public async Task CodeFix_ShouldOfferFix_WhenUnknownIntegrationBits()
    {
        var code = """
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Integrations = (StronglyTypedIdIntegrations)2048)]
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

    #region STIA0022 —— PascalCase 改名不给修复

    /// <summary>
    /// STIA0022 的修复会牵动所有引用点，不是单文件 CodeFix 能安全完成的，故它不应出现在可修复集合里。
    /// </summary>
    [Fact]
    public void CodeFix_ShouldNotListInstanceNameNotPascalCaseAsFixable()
        => new StronglyTypedIdCodeFixProvider().FixableDiagnosticIds
            .Should().NotContain(Descriptors.InstanceNameNotPascalCaseId);

    #endregion
}
