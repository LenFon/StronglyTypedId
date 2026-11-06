using Xunit;

namespace Len.StronglyTypedId.CodeRefactorings;

/// <summary>
/// 逐个重构形态验证「产物真的能编译」。
/// </summary>
/// <remarks>
/// 其余重构用例只比对改写后的文本。Convert 会补特性、<c>partial</c> 与 <c>using</c>，Inline 会摘掉它们，
/// 任何一步写错都会把「一次重构」变成「一片编译错误」，而纯文本断言看不出来。
/// <para>
/// 本文件不替代那些精确的文本断言，而是补上它们共同缺的那一层：产物必须是合法 C#，
/// 且补上的成员确实能被源生成器接上。
/// </para>
/// </remarks>
public class StronglyTypedIdRefactoringCompilationTests
{
    #region 正向：Convert to strongly typed id

    /// <summary>最基本的形态：补特性、<c>partial</c> 与 <c>using</c>。</summary>
    [Fact]
    public Task Convert_Should_ProduceCompilableCode()
        => Verify.AssertRefactoringProducesCompilableCodeAsync(
            """
            using System;

            namespace Len.StronglyTypedId.Tests;

            public record OrderId(Guid Value);
            """);

    /// <summary><c>record struct</c> 形态。</summary>
    [Fact]
    public Task Convert_RecordStruct_Should_ProduceCompilableCode()
        => Verify.AssertRefactoringProducesCompilableCodeAsync(
            """
            using System;

            namespace Len.StronglyTypedId.Tests;

            public record struct OrderId(Guid Value);
            """);

    /// <summary>文件里没有任何 <c>using</c>：新 using 必须插在正确位置。</summary>
    [Fact]
    public Task Convert_WithoutUsings_Should_ProduceCompilableCode()
        => Verify.AssertRefactoringProducesCompilableCodeAsync(
            """
            namespace Len.StronglyTypedId.Tests;

            public record OrderId(System.Guid Value);
            """);

    /// <summary>已有 <c>using Len.StronglyTypedId;</c>：不能重复插入。</summary>
    [Fact]
    public Task Convert_WithExistingUsing_Should_ProduceCompilableCode()
        => Verify.AssertRefactoringProducesCompilableCodeAsync(
            """
            using System;
            using Len.StronglyTypedId;

            namespace Len.StronglyTypedId.Tests;

            public record OrderId(Guid Value);
            """);

    /// <summary>已经是 <c>partial</c>：不能出现 <c>partial partial</c>。</summary>
    [Fact]
    public Task Convert_AlreadyPartial_Should_ProduceCompilableCode()
        => Verify.AssertRefactoringProducesCompilableCodeAsync(
            """
            using System;

            namespace Len.StronglyTypedId.Tests;

            public partial record OrderId(Guid Value);
            """);

    /// <summary>已有其它特性：新特性插在最前，原特性保留。</summary>
    [Fact]
    public Task Convert_WithExistingAttribute_Should_ProduceCompilableCode()
        => Verify.AssertRefactoringProducesCompilableCodeAsync(
            """
            using System;
            using System.Diagnostics.CodeAnalysis;

            namespace Len.StronglyTypedId.Tests;

            [ExcludeFromCodeCoverage]
            public record OrderId(Guid Value);
            """);

    /// <summary>嵌套在容器类型里。</summary>
    [Fact]
    public Task Convert_Nested_Should_ProduceCompilableCode()
        => Verify.AssertRefactoringProducesCompilableCodeAsync(
            """
            using System;

            namespace Len.StronglyTypedId.Tests;

            public partial class Outer
            {
                public record OrderId(Guid Value);
            }
            """);

    /// <summary>字符串基元。</summary>
    [Fact]
    public Task Convert_StringPrimitive_Should_ProduceCompilableCode()
        => Verify.AssertRefactoringProducesCompilableCodeAsync(
            """
            namespace Len.StronglyTypedId.Tests;

            public record SkuId(string Value);
            """);

    #endregion

    #region 反向：Inline strongly typed id

    /// <summary>摘掉特性、<c>partial</c> 与不再需要的 using。</summary>
    [Fact]
    public Task Inline_Should_ProduceCompilableCode()
        => Verify.AssertRefactoringProducesCompilableCodeAsync(
            """
            using System;
            using Len.StronglyTypedId;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record OrderId(Guid Value);
            """);

    /// <summary>文件里还有别的 Id：using 必须保留。</summary>
    [Fact]
    public Task Inline_WithAnotherId_Should_ProduceCompilableCode()
        => Verify.AssertRefactoringProducesCompilableCodeAsync(
            """
            using System;
            using Len.StronglyTypedId;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record OrderId(Guid Value);

            [StronglyTypedId]
            public partial record CustomerId(Guid Value);
            """);

    /// <summary>文件里还在用该命名空间的其它类型（<c>StronglyTypedIdIntegrations</c>）：using 必须保留。</summary>
    /// <remarks>
    /// 不能用 <c>IStronglyTypedId&lt;OrderId&gt;</c>：Inline 之后 <c>OrderId</c> 不再是强类型 Id，
    /// 那个泛型约束自然不成立，编译失败与 using 是否保留无关。改用与 Id 身份无关的类型。
    /// </remarks>
    [Fact]
    public Task Inline_WithOtherNamespaceType_Should_ProduceCompilableCode()
        => Verify.AssertRefactoringProducesCompilableCodeAsync(
            """
            using System;
            using Len.StronglyTypedId;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record OrderId(Guid Value);

            public static class Settings
            {
                public static StronglyTypedIdIntegrations Integrations => StronglyTypedIdIntegrations.All;
            }
            """);

    /// <summary>与其它特性共存：只摘掉 <c>[StronglyTypedId]</c>。</summary>
    [Fact]
    public Task Inline_WithOtherAttribute_Should_ProduceCompilableCode()
        => Verify.AssertRefactoringProducesCompilableCodeAsync(
            """
            using System;
            using System.Diagnostics.CodeAnalysis;
            using Len.StronglyTypedId;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            [ExcludeFromCodeCoverage]
            public partial record OrderId(Guid Value);
            """);

    /// <summary>嵌套在容器类型里。</summary>
    [Fact]
    public Task Inline_Nested_Should_ProduceCompilableCode()
        => Verify.AssertRefactoringProducesCompilableCodeAsync(
            """
            using System;
            using Len.StronglyTypedId;

            namespace Len.StronglyTypedId.Tests;

            public partial class Outer
            {
                [StronglyTypedId]
                public partial record struct OrderId(Guid Value);
            }
            """);

    #endregion
}
