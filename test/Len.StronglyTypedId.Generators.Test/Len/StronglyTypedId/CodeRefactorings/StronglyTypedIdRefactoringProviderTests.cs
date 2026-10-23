using FluentAssertions;
using Len.StronglyTypedId.CodeRefactorings;
using Xunit;

namespace Len.StronglyTypedId.CodeRefactorings;

/// <summary>
/// <see cref="StronglyTypedIdRefactoringProvider"/> 的用例：正向「Convert to strongly typed id」与反向「Inline strongly
/// typed id」。
/// </summary>
public class StronglyTypedIdRefactoringProviderTests
{
    #region 正向：Convert to strongly typed id

    /// <summary>
    /// 最基本的形态：补 <c>[StronglyTypedId]</c>、<c>partial</c> 与 <c>using Len.StronglyTypedId;</c>。
    /// </summary>
    [Fact]
    public async Task Refactoring_Should_AddAttributePartialAndUsing()
    {
        var code = """
            using System;

            namespace Len.StronglyTypedId.Tests;

            public record OrderId(Guid Value);
            """;

        var expected = """
            using System;
            using Len.StronglyTypedId;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record OrderId(Guid Value);
            """;

        (await Verify.ApplyRefactoringAsync(code)).Should().Be(expected);
    }

    /// <summary>
    /// <c>record struct</c> 形态：<c>partial</c> 必须落在 <c>record</c> 与 <c>struct</c> 之前。
    /// </summary>
    [Fact]
    public async Task Refactoring_Should_SupportRecordStruct()
    {
        var code = """
            using System;

            namespace Len.StronglyTypedId.Tests;

            public record struct OrderId(Guid Value);
            """;

        var expected = """
            using System;
            using Len.StronglyTypedId;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct OrderId(Guid Value);
            """;

        (await Verify.ApplyRefactoringAsync(code)).Should().Be(expected);
    }

    /// <summary>
    /// 已有 <c>using Len.StronglyTypedId;</c> 时不再重复插入。
    /// </summary>
    [Fact]
    public async Task Refactoring_Should_NotDuplicateUsing()
    {
        var code = """
            using System;
            using Len.StronglyTypedId;

            namespace Len.StronglyTypedId.Tests;

            public record OrderId(Guid Value);
            """;

        var expected = """
            using System;
            using Len.StronglyTypedId;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record OrderId(Guid Value);
            """;

        (await Verify.ApplyRefactoringAsync(code)).Should().Be(expected);
    }

    /// <summary>
    /// 已有其它特性时，新特性插在最前面，且原有特性的位置与空行保持不变。
    /// </summary>
    [Fact]
    public async Task Refactoring_Should_InsertAttributeBeforeExisting()
    {
        var code = """
            using System;

            namespace Len.StronglyTypedId.Tests;

            [Obsolete]
            public record OrderId(Guid Value);
            """;

        var expected = """
            using System;
            using Len.StronglyTypedId;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            [Obsolete]
            public partial record OrderId(Guid Value);
            """;

        (await Verify.ApplyRefactoringAsync(code)).Should().Be(expected);
    }

    /// <summary>
    /// 文件里一个 using 都没有（基元用全限定名）时，新 using 插在第一个成员之前并保留空行。
    /// </summary>
    [Fact]
    public async Task Refactoring_Should_AddUsing_WhenNoUsings()
    {
        var code = """
            namespace Len.StronglyTypedId.Tests;

            public record OrderId(System.Guid Value);
            """;

        var expected = """
            using Len.StronglyTypedId;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record OrderId(System.Guid Value);
            """;

        (await Verify.ApplyRefactoringAsync(code)).Should().Be(expected);
    }

    /// <summary>
    /// 光标落在 record 声明头上时提供重构。
    /// </summary>
    [Fact]
    public async Task Refactoring_Should_BeOffered_OnDeclarationHeader()
    {
        var code = """
            using System;

            namespace Len.StronglyTypedId.Tests;

            public reco$$rd OrderId(Guid Value);
            """;

        var expected = """
            using System;
            using Len.StronglyTypedId;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record OrderId(Guid Value);
            """;

        (await Verify.ApplyRefactoringAsync(code)).Should().Be(expected);
    }

    /// <summary>
    /// 正向重构插入的换行沿用文档自身的行尾：CRLF 文档得 CRLF，LF 文档得 LF。
    /// </summary>
    [Theory]
    [InlineData("\r\n")]
    [InlineData("\n")]
    public async Task Refactoring_Should_PreserveEndOfLine_WhenConverting(string endOfLine)
    {
        var code = string.Join(
            endOfLine,
            "using System;",
            string.Empty,
            "namespace Len.StronglyTypedId.Tests;",
            string.Empty,
            "public record OrderId(Guid Value);",
            string.Empty);

        var expected = string.Join(
            endOfLine,
            "using System;",
            "using Len.StronglyTypedId;",
            string.Empty,
            "namespace Len.StronglyTypedId.Tests;",
            string.Empty,
            "[StronglyTypedId]",
            "public partial record OrderId(Guid Value);",
            string.Empty);

        (await Verify.ApplyRefactoringAsync(code)).Should().Be(expected);
    }

    #endregion

    #region 正向：不提供的场景

    /// <summary>
    /// 没有主构造函数：无法确定包装的值，不提供重构。
    /// </summary>
    [Fact]
    public async Task Refactoring_ShouldNotBeOffered_WhenNoPrimaryConstructor()
    {
        var code = """
            using System;

            namespace Len.StronglyTypedId.Tests;

            public record OrderId
            {
                public Guid Value { get; init; }
            }
            """;

        (await Verify.CollectRefactoringsAsync(code)).Should().BeEmpty();
    }

    /// <summary>
    /// 主构造函数多于一个参数：强类型 Id 只包装一个值，不提供重构。
    /// </summary>
    [Fact]
    public async Task Refactoring_ShouldNotBeOffered_WhenMultipleParameters()
    {
        var code = """
            using System;

            namespace Len.StronglyTypedId.Tests;

            public record OrderId(Guid Value, string Name);
            """;

        (await Verify.CollectRefactoringsAsync(code)).Should().BeEmpty();
    }

    /// <summary>
    /// 参数名不是 <c>Value</c>：重命名位置参数会连带改掉生成的属性名，超出「转换」语义，不提供重构。
    /// </summary>
    [Fact]
    public async Task Refactoring_ShouldNotBeOffered_WhenParameterNotNamedValue()
    {
        var code = """
            using System;

            namespace Len.StronglyTypedId.Tests;

            public record OrderId(Guid Id);
            """;

        (await Verify.CollectRefactoringsAsync(code)).Should().BeEmpty();
    }

    /// <summary>
    /// 基元类型不受支持：加上特性后会触发 STIA0008，不提供重构。
    /// </summary>
    [Fact]
    public async Task Refactoring_ShouldNotBeOffered_WhenPrimitiveUnsupported()
    {
        var code = """
            namespace Len.StronglyTypedId.Tests;

            public record OrderId(Order Value);

            public class Order
            {
            }
            """;

        (await Verify.CollectRefactoringsAsync(code)).Should().BeEmpty();
    }

    /// <summary>
    /// 已经是强类型 Id：不再提供正向重构。
    /// </summary>
    [Fact]
    public async Task Refactoring_ShouldNotOfferForward_WhenAlreadyStronglyTypedId()
    {
        var code = """
            using System;
            using Len.StronglyTypedId;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record OrderId(Guid Value);
            """;

        var titles = (await Verify.CollectRefactoringsAsync(code)).Select(action => action.Title);

        titles.Should().NotContain("Convert to strongly typed id");
    }

    /// <summary>
    /// 光标落在声明体内部（成员之间）时不提供重构。
    /// </summary>
    [Fact]
    public async Task Refactoring_ShouldNotBeOffered_WhenCaretInsideBody()
    {
        var code = """
            using System;

            namespace Len.StronglyTypedId.Tests;

            public record OrderId(Guid Value)
            {
                public string Describe() => Va$$lue.ToString();
            }
            """;

        (await Verify.CollectRefactoringsAsync(code)).Should().BeEmpty();
    }

    /// <summary>
    /// 泛型 record 不能成为强类型 Id（STIA0003），不提供重构。
    /// </summary>
    [Fact]
    public async Task Refactoring_ShouldNotBeOffered_WhenGeneric()
    {
        var code = """
            using System;

            namespace Len.StronglyTypedId.Tests;

            public record OrderId<T>(Guid Value);
            """;

        (await Verify.CollectRefactoringsAsync(code)).Should().BeEmpty();
    }

    #endregion

    #region 反向：Inline strongly typed id

    /// <summary>
    /// 反向重构：摘掉 <c>[StronglyTypedId]</c>、<c>partial</c> 与不再使用的 using。
    /// </summary>
    [Fact]
    public async Task Refactoring_Should_InlineToPrimitive()
    {
        var code = """
            using System;
            using Len.StronglyTypedId;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record OrderId(Guid Value);
            """;

        var expected = """
            using System;

            namespace Len.StronglyTypedId.Tests;

            public record OrderId(Guid Value);
            """;

        (await Verify.ApplyRefactoringAsync(code)).Should().Be(expected);
    }

    /// <summary>
    /// 反向重构：文件里还有别的强类型 Id 时保留 using。
    /// </summary>
    [Fact]
    public async Task Refactoring_Should_KeepUsing_WhenAnotherIdRemains()
    {
        var code = """
            using System;
            using Len.StronglyTypedId;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record OrderId(Guid Value);

            [StronglyTypedId]
            public partial record ProductId(Guid Value);
            """;

        var expected = """
            using System;
            using Len.StronglyTypedId;

            namespace Len.StronglyTypedId.Tests;

            public record OrderId(Guid Value);

            [StronglyTypedId]
            public partial record ProductId(Guid Value);
            """;

        (await Verify.ApplyRefactoringAsync(code)).Should().Be(expected);
    }

    /// <summary>
    /// 反向重构：文件里虽无第二个 Id，但仍在用该命名空间导出的其他类型（<c>IStronglyTypedId&lt;,&gt;</c>、
    /// <c>Any</c>、<c>StronglyTypedIdIntegrations</c> 等）时必须保留 using —— 只按「还有没有带特性的 record」
    /// 判断会把它一并删掉，产出 CS0246。
    /// </summary>
    [Fact]
    public async Task Refactoring_Should_KeepUsing_WhenOtherTypesFromNamespaceAreUsed()
    {
        var code = """
            using System;
            using Len.StronglyTypedId;

            namespace Acme;

            [StronglyTypedId]
            public partial record OrderId(Guid Value);

            public static class OrderIds
            {
                public static string Describe(StronglyTypedIdIntegrations integrations) => integrations.ToString();
            }
            """;

        var expected = """
            using System;
            using Len.StronglyTypedId;

            namespace Acme;

            public record OrderId(Guid Value);

            public static class OrderIds
            {
                public static string Describe(StronglyTypedIdIntegrations integrations) => integrations.ToString();
            }
            """;

        (await Verify.ApplyRefactoringAsync(code)).Should().Be(expected);
    }

    /// <summary>
    /// 反向重构：同一 Id 有多段 partial 声明时摘掉 <c>partial</c> 会 CS0260，不提供。
    /// </summary>
    [Fact]
    public async Task Refactoring_ShouldNotOfferInline_WhenMultipleDeclarations()
    {
        var code = """
            using System;
            using Len.StronglyTypedId;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record OrderId(Guid Value);

            public partial record OrderId
            {
                public string Describe() => Value.ToString();
            }
            """;

        var titles = (await Verify.CollectRefactoringsAsync(code)).Select(action => action.Title);

        titles.Should().NotContain("Inline strongly typed id");
    }

    /// <summary>
    /// 反向重构重排后的换行同样沿用文档自身的行尾（摘掉特性后留下的空行由格式化器补齐）。
    /// </summary>
    [Theory]
    [InlineData("\r\n")]
    [InlineData("\n")]
    public async Task Refactoring_Should_PreserveEndOfLine_WhenInlining(string endOfLine)
    {
        var code = string.Join(
            endOfLine,
            "using System;",
            "using Len.StronglyTypedId;",
            string.Empty,
            "namespace Len.StronglyTypedId.Tests;",
            string.Empty,
            "[StronglyTypedId]",
            "public partial record OrderId(Guid Value);",
            string.Empty);

        var expected = string.Join(
            endOfLine,
            "using System;",
            string.Empty,
            "namespace Len.StronglyTypedId.Tests;",
            string.Empty,
            "public record OrderId(Guid Value);",
            string.Empty);

        (await Verify.ApplyRefactoringAsync(code)).Should().Be(expected);
    }

    #endregion

    #region 第三方同名特性

    /// <summary>
    /// 第三方命名空间下的同名特性不能被当成自家的：判定按语义绑定走，否则 Inline 重构会把
    /// <c>Acme.StronglyTypedIdAttribute</c> 一并物理删掉 —— 用户点一下，别人的特性就没了，且没有任何提示。
    /// </summary>
    [Fact]
    public async Task Refactoring_ShouldNotTreatForeignAttributeAsStronglyTypedId()
    {
        var code = """
            using System;
            using Len.StronglyTypedId;

            namespace Acme
            {
                [AttributeUsage(AttributeTargets.All)]
                public sealed class StronglyTypedIdAttribute : Attribute
                {
                }
            }

            namespace Len.StronglyTypedId.Tests
            {
                [Acme.StronglyTypedIdAttribute]
                public partial record OrderId(Guid Value);
            }
            """;

        var titles = (await Verify.CollectRefactoringsAsync(code)).Select(action => action.Title).ToList();

        titles.Should().NotContain("Inline strongly typed id");
        titles.Should().Contain("Convert to strongly typed id");
    }

    /// <summary>
    /// Inline 只摘自家的 <c>[StronglyTypedId]</c>，第三方同名特性必须原样留下。
    /// </summary>
    [Fact]
    public async Task Refactoring_Should_KeepForeignAttribute_WhenInlining()
    {
        var code = """
            using System;
            using Len.StronglyTypedId;

            namespace Acme
            {
                [AttributeUsage(AttributeTargets.All)]
                public sealed class StronglyTypedIdAttribute : Attribute
                {
                }
            }

            namespace Len.StronglyTypedId.Tests
            {
                [StronglyTypedId]
                [Acme.StronglyTypedIdAttribute]
                public partial record OrderId(Guid Value);
            }
            """;

        var result = await Verify.ApplyRefactoringAsync(code);

        result.Should().Contain("[Acme.StronglyTypedIdAttribute]");
        result.Should().NotContain("[StronglyTypedId]");
    }

    /// <summary>
    /// 第三方同名特性以**短名**书写时也不能被当成自家的。
    /// </summary>
    /// <remarks>
    /// 这是上一用例的加强版：<c>[Acme.StronglyTypedIdAttribute]</c> 至少还带着限定前缀，纯语法匹配就能放过；
    /// 而 <c>using Acme;</c> 之后的 <c>[StronglyTypedId]</c> 在语法上与自家写法逐字符相同，只有语义绑定能分辨。
    /// 判错的后果是用户点一下 Inline，别人的特性就被物理删掉了。
    /// </remarks>
    [Fact]
    public async Task Refactoring_ShouldNotTreatShortNamedForeignAttributeAsStronglyTypedId()
    {
        var code = """
            using System;
            using Acme;

            namespace Acme
            {
                [AttributeUsage(AttributeTargets.All)]
                public sealed class StronglyTypedIdAttribute : Attribute
                {
                }
            }

            namespace MyApp
            {
                [StronglyTypedId]
                public partial record OrderId(Guid Value);
            }
            """;

        var titles = (await Verify.CollectRefactoringsAsync(code)).Select(action => action.Title).ToList();

        titles.Should().NotContain("Inline strongly typed id",
            "短名绑定到的是 Acme 的特性，Inline 会把别人的特性删掉");
        titles.Should().Contain("Convert to strongly typed id");
    }

    /// <summary>
    /// 声明位于 <c>namespace Len.StronglyTypedId.Tests</c> 下时，<c>[StronglyTypedId]</c> 会先解析到父命名空间
    /// <c>Len.StronglyTypedId</c>，语义绑定给出的是 Namespace 候选而非特性符号。此时必须沿用语法判定，
    /// 否则 Inline 在本库自己的测试工程里都会失效。
    /// </summary>
    [Fact]
    public async Task Refactoring_ShouldStillOfferInline_WhenAttributeNameIsShadowedByNamespace()
    {
        var code = """
            using System;
            using Len.StronglyTypedId;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record OrderId(Guid Value);
            """;

        var titles = (await Verify.CollectRefactoringsAsync(code)).Select(action => action.Title).ToList();

        titles.Should().Contain("Inline strongly typed id",
            "父命名空间遮蔽了特性名，但这仍然是自家的 [StronglyTypedId]");
    }

    #endregion
}
