using FluentAssertions;
using Len.StronglyTypedId.CodeRefactorings;
using Xunit;

namespace Len.StronglyTypedId.CodeRefactorings;

/// <summary>
/// <see cref="StronglyTypedIdRefactoringProvider"/> 的用例：正向「Convert to strongly typed id」与
/// 反向「Inline strongly typed id」。
/// </summary>
/// <remarks>
/// 代码重构没有像诊断那样的「期望清单」可供测试框架比对，故经 <c>Verify.ApplyRefactoringAsync</c> 直接驱动
/// provider 并应用它注册的唯一重构，再按精确文本断言产物。
/// </remarks>
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
    /// <remarks>
    /// 期望值用显式 <c>\r\n</c> / <c>\n</c> 拼出，而不是原始字符串字面量 —— 后者的行尾随
    /// <c>text=auto</c> 在 CRLF（Windows 工作区）与 LF（Linux / macOS 检出）之间变化，
    /// 断言会隐含依赖平台。这里把「产物行尾必须等于文档行尾」这条不变式显式钉死。
    /// </remarks>
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
    /// 基元类型不受支持：加上特性后会触发 STIAO008，不提供重构。
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
    /// 泛型 record 不能成为强类型 Id（STIAO003），不提供重构。
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
    /// <remarks>与正向用例同理，期望值用显式行尾拼出，不依赖检出时的 <c>text=auto</c>。</remarks>
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
}
