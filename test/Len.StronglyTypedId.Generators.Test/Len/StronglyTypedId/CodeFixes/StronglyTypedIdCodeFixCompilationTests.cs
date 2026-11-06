using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Len.StronglyTypedId.CodeFixes;

/// <summary>
/// 逐个可修复诊断验证「修复产物真的能编译」。
/// </summary>
/// <remarks>
/// 其余 CodeFix 用例只比对修复后的文本，而 <see cref="Verify.ApplyDefaults"/> 把
/// <c>CompilerDiagnostics</c> 设成了 <c>None</c>，于是「修复产物能否编译」此前从未被验证。
/// 一个把一条诊断换成一片编译错误的修复（漏 <c>using</c>、产出非法语法、指向不存在的类型）
/// 在纯文本断言下完全看不出来。
/// <para>
/// 本文件对每个可修复诊断各跑一次修复并真正编译产物。它不替代那些精确的文本断言，
/// 而是补上它们共同缺的那一层：产物必须是合法 C#。
/// </para>
/// </remarks>
public class StronglyTypedIdCodeFixCompilationTests
{
    /// <summary>
    /// 修复前源码里必须有一个可被 <paramref name="descriptor"/> 命中的写法。
    /// </summary>
    private static Task AssertCompilable(string source, DiagnosticDescriptor descriptor, Func<SyntaxNode, SyntaxNode> selectTarget)
        => Verify.AssertCodeFixProducesCompilableCodeAsync(source, descriptor, selectTarget);

    private static Func<SyntaxNode, SyntaxNode> Record()
        => static root => root.DescendantNodes().OfType<RecordDeclarationSyntax>().First();

    private static Func<SyntaxNode, SyntaxNode> TypeDeclaration()
        => static root => root.DescendantNodes().OfType<TypeDeclarationSyntax>().First();

    private static Func<SyntaxNode, SyntaxNode> Parameter()
        => static root => root.DescendantNodes().OfType<ParameterSyntax>().First();

    private static Func<SyntaxNode, SyntaxNode> AttributeArgument(string name)
        => root => root.DescendantNodes()
            .OfType<AttributeArgumentSyntax>()
            .First(argument => argument.NameEquals is not null
                && argument.NameEquals.Name.Identifier.ValueText == name);

    private static Func<SyntaxNode, SyntaxNode> InstanceAttribute()
        => static root => root.DescendantNodes().OfType<AttributeSyntax>().First();

    private static Func<SyntaxNode, SyntaxNode> ObjectCreation()
        => static root => root.DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>().First();

    #region 声明形状

    /// <summary>STIA0001：补 <c>partial</c>。</summary>
    [Fact]
    public Task TypeMustBePartial_Should_ProduceCompilableCode()
        => AssertCompilable(
            """
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public record struct OrderId(Guid Value);
            """,
            Descriptors.TypeMustBePartial,
            Record());

    /// <summary>STIA0003：去掉 <c>abstract</c>。</summary>
    [Fact]
    public Task TypeCannotBeAbstract_Should_ProduceCompilableCode()
        => AssertCompilable(
            """
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public abstract partial record OrderId(Guid Value);
            """,
            Descriptors.TypeCannotBeAbstract,
            Record());

    /// <summary>STIA0002：<c>class</c> → <c>record</c>。</summary>
    [Fact]
    public Task TypeMustBeRecord_Should_ProduceCompilableCode()
        => AssertCompilable(
            """
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial class OrderId(Guid Value);
            """,
            Descriptors.TypeMustBeRecord,
            TypeDeclaration());

    /// <summary>STIA0002：<c>struct</c> → <c>record struct</c>（不能变成引用类型）。</summary>
    [Fact]
    public Task TypeMustBeRecord_FromStruct_Should_ProduceCompilableCode()
        => AssertCompilable(
            """
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial struct OrderId(Guid Value);
            """,
            Descriptors.TypeMustBeRecord,
            TypeDeclaration());

    /// <summary>STIA0004：包进命名空间。</summary>
    /// <remarks>
    /// 源码必须自带 <c>using Len.StronglyTypedId;</c>：STIA0004 的修复只负责包命名空间，
    /// 不会补 using；缺了它，修复产物里的 <c>[StronglyTypedId]</c> 解析不到，编译必然失败。
    /// 这与真实场景一致——能写出 <c>[StronglyTypedId]</c> 的源码本来就已能解析到该特性。
    /// </remarks>
    [Fact]
    public Task TypeMustHaveNamespace_Should_ProduceCompilableCode()
        => AssertCompilable(
            """
            using System;
            using Len.StronglyTypedId;

            [StronglyTypedId]
            public partial record OrderId(Guid Value);
            """,
            Descriptors.TypeMustHaveNamespace,
            TypeDeclaration());

    /// <summary>STIA0025：摘掉 <c>file</c>。</summary>
    [Fact]
    public Task TypeCannotBeFileLocal_Should_ProduceCompilableCode()
        => AssertCompilable(
            """
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            file partial record OrderId(Guid Value);
            """,
            Descriptors.TypeCannotBeFileLocal,
            Record());

    /// <summary>STIA0004：包含类型补 <c>partial</c>。</summary>
    [Fact]
    public Task ContainingTypeMustBePartial_Should_ProduceCompilableCode()
        => AssertCompilable(
            """
            using System;

            namespace Len.StronglyTypedId.Tests;

            public class Outer
            {
                [StronglyTypedId]
                public partial record struct OrderId(Guid Value);
            }
            """,
            Descriptors.ContainingTypeMustBePartial,
            TypeDeclaration());

    #endregion

    #region 主构造函数

    /// <summary>STIA0005：补 <c>(Guid Value)</c> 主构造。</summary>
    [Fact]
    public Task TypeMustHaveSingleParameterPrimaryConstructor_Should_ProduceCompilableCode()
        => AssertCompilable(
            """
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record OrderId;
            """,
            Descriptors.TypeMustHaveSingleParameterPrimaryConstructor,
            Record());

    /// <summary>STIA0006：去掉参数的可空标注。</summary>
    [Fact]
    public Task ParameterCannotBeNullable_Should_ProduceCompilableCode()
        => AssertCompilable(
            """
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record OrderId(Guid? Value);
            """,
            Descriptors.ParameterCannotBeNullable,
            Parameter());

    /// <summary>STIA0007：参数改名为 <c>Value</c>。</summary>
    [Fact]
    public Task ParameterNameMustBeValue_Should_ProduceCompilableCode()
        => AssertCompilable(
            """
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record OrderId(Guid Id);
            """,
            Descriptors.ParameterNameMustBeValue,
            Parameter());

    /// <summary>STIA0008：不支持的基元改成 <c>Guid</c>（文件里没有 <c>using System;</c>）。</summary>
    [Fact]
    public Task ParameterTypeIsInvalid_Should_ProduceCompilableCode()
        => AssertCompilable(
            """
            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record OrderId(Foo Value);
            """,
            Descriptors.ParameterTypeIsInvalid,
            Parameter());

    #endregion

    #region 成员引用

    /// <summary>STIA0010：删掉指向签名不符方法的 <c>Validator</c> 实参。</summary>
    /// <remarks>
    /// 源码本身必须可编译：用「存在但签名不符」的方法，而不是 <c>nameof(NotAMethod)</c>——
    /// 后者本身就是 CS0103，会让编译验证失去意义。
    /// </remarks>
    [Fact]
    public Task ValidatorReferenceInvalid_Should_ProduceCompilableCode()
        => AssertCompilable(
            """
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Validator = nameof(NotAValidator))]
            public partial record OrderId(Guid Value)
            {
                private static bool NotAValidator(int value) => true;
            }
            """,
            Descriptors.ValidatorReferenceInvalid,
            AttributeArgument("Validator"));

    /// <summary>STIA0017：删掉指向签名不符方法的 <c>Normalizer</c> 实参。</summary>
    /// <remarks>同上：方法必须真实存在，否则源码本身就有 CS0103。</remarks>
    [Fact]
    public Task NormalizerReferenceInvalid_Should_ProduceCompilableCode()
        => AssertCompilable(
            """
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Normalizer = nameof(NotANormalizer))]
            public partial record OrderId(Guid Value)
            {
                private static int NotANormalizer(Guid value) => 0;
            }
            """,
            Descriptors.NormalizerReferenceInvalid,
            AttributeArgument("Normalizer"));

    /// <summary>STIA0018：删掉无效的 <c>ExceptionType</c> 实参。</summary>
    [Fact]
    public Task ExceptionTypeInvalid_Should_ProduceCompilableCode()
        => AssertCompilable(
            """
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Validator = nameof(Ok), ExceptionType = typeof(string))]
            public partial record OrderId(Guid Value)
            {
                private static bool Ok(Guid value) => true;
            }
            """,
            Descriptors.ExceptionTypeInvalid,
            AttributeArgument("ExceptionType"));

    /// <summary>STIA0013：删掉纯空白前缀。</summary>
    [Fact]
    public Task InvalidPrefix_Should_ProduceCompilableCode()
        => AssertCompilable(
            """
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Prefix = "   ")]
            public partial record OrderId(Guid Value);
            """,
            Descriptors.InvalidPrefix,
            AttributeArgument("Prefix"));

    #endregion

    #region 可见性与绕过

    /// <summary>STIA0014：<c>public</c> → <c>internal</c>。</summary>
    [Fact]
    public Task ValidatorCanBeBypassed_Should_ProduceCompilableCode()
        => AssertCompilable(
            """
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Validator = nameof(Ok))]
            public partial record OrderId(Guid Value)
            {
                private static bool Ok(Guid value) => value != Guid.Empty;
            }
            """,
            Descriptors.ValidatorCanBeBypassed,
            TypeDeclaration());

    /// <summary>STIA0011：<c>new Xxx(v)</c> → <c>Xxx.Create(v)</c>。</summary>
    [Fact]
    public Task BypassCreate_Should_ProduceCompilableCode()
        => AssertCompilable(
            """
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Validator = nameof(Ok))]
            public partial record struct OrderId(Guid Value)
            {
                private static bool Ok(Guid value) => value != Guid.Empty;
            }

            public static class Uses
            {
                public static OrderId Make() => new OrderId(Guid.NewGuid());
            }
            """,
            Descriptors.BypassCreate,
            ObjectCreation());

    #endregion

    #region 集成与命名实例

    /// <summary>STIA0019：删掉请求了未引用集成的 <c>Integrations</c> 实参。</summary>
    [Fact]
    public Task IntegrationUnavailable_Should_ProduceCompilableCode()
        => AssertCompilable(
            """
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Integrations = StronglyTypedIdIntegrations.MessagePack)]
            public partial record OrderId(Guid Value);
            """,
            Descriptors.IntegrationUnavailable,
            AttributeArgument("Integrations"));

    /// <summary>STIA0024：删掉含越界位的 <c>Integrations</c> 实参。</summary>
    [Fact]
    public Task UnknownIntegration_Should_ProduceCompilableCode()
        => AssertCompilable(
            """
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Integrations = (StronglyTypedIdIntegrations)1048576)]
            public partial record OrderId(Guid Value);
            """,
            Descriptors.UnknownIntegration,
            AttributeArgument("Integrations"));

    /// <summary>STIA0020：删掉名字非法的命名实例特性。</summary>
    [Fact]
    public Task InvalidInstanceName_Should_ProduceCompilableCode()
        => AssertCompilable(
            """
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            [StronglyTypedIdInstance("123abc", "00000000-0000-0000-0000-000000000000")]
            public partial record OrderId(Guid Value);
            """,
            Descriptors.InvalidInstanceName,
            InstanceAttribute());

    /// <summary>STIA0021：删掉取值无法解析的命名实例特性。</summary>
    [Fact]
    public Task InvalidInstanceValue_Should_ProduceCompilableCode()
        => AssertCompilable(
            """
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            [StronglyTypedIdInstance("Empty", "not-a-guid")]
            public partial record OrderId(Guid Value);
            """,
            Descriptors.InvalidInstanceValue,
            InstanceAttribute());

    #endregion
}
