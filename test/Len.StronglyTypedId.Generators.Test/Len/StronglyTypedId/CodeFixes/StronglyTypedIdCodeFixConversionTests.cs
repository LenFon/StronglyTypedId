using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Len.StronglyTypedId.CodeFixes;

public class StronglyTypedIdCodeFixConversionTests
{
    /// <summary>
    /// STIA0000：把 class 转成 record（保留修饰符 / 主构造函数 / 成员 / 标识符）。
    /// </summary>
    [Fact]
    public async Task CodeFix_Should_ConvertClassToRecord_WhenTypeMustBeRecord()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial class OrderId(Guid Value) { }
            """";

        var fixedCode = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record OrderId(Guid Value) { }
            """";

        var expected = Verify.Diagnostic(Descriptors.TypeMustBeRecord)
            .WithSpan(5, 1, 6, 45).WithArguments("OrderId");

        await Verify.VerifyCodeFixAsync(code, expected, fixedCode);
    }

    /// <summary>
    /// STIA0004：把最外层类型包进命名空间块（占位名固定 MyNamespace，由使用者改名）。
    /// </summary>
    [Fact]
    public async Task CodeFix_Should_WrapInNamespace_WhenTypeMustHaveNamespace()
    {
        var code = """"
            using System;
            using Len.StronglyTypedId;

            [StronglyTypedId]
            public partial record struct OrderId(Guid Value);
            """";

        var fixedCode = """"
            using System;
            using Len.StronglyTypedId;

            namespace MyNamespace
            {
                [StronglyTypedId]
                public partial record struct OrderId(Guid Value);
            }
            """";

        var expected = Verify.Diagnostic(Descriptors.TypeMustHaveNamespace)
            .WithSpan(4, 1, 5, 50).WithArguments("OrderId");

        await Verify.VerifyCodeFixAsync(code, expected, fixedCode);
    }

    /// <summary>
    /// STIA0005：缺主构造函数时补一个单参主构造 (Guid Value)。
    /// </summary>
    [Fact]
    public async Task CodeFix_Should_AddPrimaryConstructor_WhenMissing()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record OrderId { }
            """";

        var fixedCode = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record OrderId(Guid Value) { }
            """";

        var expected = Verify.Diagnostic(Descriptors.TypeMustHaveSingleParameterPrimaryConstructor)
            .WithSpan(5, 1, 6, 34).WithArguments("OrderId");

        await Verify.VerifyCodeFixAsync(code, expected, fixedCode);
    }

    /// <summary>
    /// STIA0005：主构造函数多参数时整段替换为单参版本，并把参数名规整为 Value。
    /// </summary>
    [Fact]
    public async Task CodeFix_Should_FixPrimaryConstructor_WhenMultipleParameters()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record OrderId(Guid value, string name);
            """";

        var fixedCode = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record OrderId(Guid Value);
            """";

        var expected = Verify.Diagnostic(Descriptors.TypeMustHaveSingleParameterPrimaryConstructor)
            .WithSpan(5, 1, 6, 56).WithArguments("OrderId");

        await Verify.VerifyCodeFixAsync(code, expected, fixedCode);
    }

    /// <summary>
    /// STIA0008：把非受支持基元类型参数改成 Guid（名称此前已是 Value）。
    /// </summary>
    [Fact]
    public async Task CodeFix_Should_ChangeParameterTypeToGuid_WhenTypeInvalid()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record OrderId(Foo Value);
            """";

        var fixedCode = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record OrderId(Guid Value);
            """";

        var expected = Verify.Diagnostic(Descriptors.ParameterTypeIsInvalid)
            .WithSpan(6, 31, 6, 34).WithArguments("Foo", "It is not a struct or class.");

        await Verify.VerifyCodeFixAsync(code, expected, fixedCode);
    }

    /// <summary>
    /// STIA0008：文件里没有 <c>using System;</c> 时，修复产物不能是裸 <c>Guid</c>，否则会留下新的 CS0246
    ///，把一条诊断换成另一条诊断，等于没修。Simplifier 在没有 using 时只削掉 <c>global::</c>，
    /// 停在 <c>System.Guid</c>；这已足够解析，不必强求保留 <c>global::</c>。
    /// </summary>
    [Fact]
    public async Task CodeFix_Should_EmitQualifiedGuid_WhenSystemNamespaceNotImported()
    {
        var code = """"
            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record OrderId(Foo Value);
            """";

        var fixedCode = """"
            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record OrderId(System.Guid Value);
            """";

        var expected = Verify.Diagnostic(Descriptors.ParameterTypeIsInvalid)
            .WithSpan(4, 31, 4, 34).WithArguments("Foo", "It is not a struct or class.");

        await Verify.VerifyCodeFixAsync(code, expected, fixedCode);
    }

    /// <summary>
    /// STIA0010：删掉 [StronglyTypedId] 里无效的 Validator 命名实参（唯一实参时连同括号一起删）。
    /// </summary>
    [Fact]
    public async Task CodeFix_Should_RemoveValidatorArgument_WhenValidatorInvalid()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Validator = nameof(Validate))]
            public partial record OrderId(Guid Value)
            {
                private static int Validate(Guid value) => 0;
            }
            """";

        var fixedCode = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record OrderId(Guid Value)
            {
                private static int Validate(Guid value) => 0;
            }
            """";

        var expected = Verify.Diagnostic(Descriptors.ValidatorReferenceInvalid)
            .WithSpan(5, 18, 5, 46).WithArguments("Validate", "System.Guid");

        await Verify.VerifyCodeFixAsync(code, expected, fixedCode);
    }

    /// <summary>
    /// STIA0000：把 struct 转成 <c>record struct</c>。一律写成 <c>record</c> 会把值类型悄悄换成引用类型
    /// （装箱行为、默认值、<c>==</c> 语义全变），所以这里必须守住 <c>struct</c> 关键字。
    /// </summary>
    [Fact]
    public async Task CodeFix_Should_ConvertStructToRecord_WhenTypeMustBeRecord()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial struct OrderId(Guid Value) { }
            """";

        var fixedCode = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct OrderId(Guid Value) { }
            """";

        var expected = Verify.Diagnostic(Descriptors.TypeMustBeRecord)
            .WithSpan(5, 1, 6, 46).WithArguments("OrderId");

        await Verify.VerifyCodeFixAsync(code, expected, fixedCode);
    }

    /// <summary>
    /// STIA0000：<c>readonly struct</c> 转成 <c>readonly record struct</c>。<c>readonly</c> 修饰符原样搬运，
    /// 若只补 <c>record</c> 会得到 <c>readonly record</c>（CS8809，编译不过）。
    /// </summary>
    [Fact]
    public async Task CodeFix_Should_ConvertReadonlyStructToReadonlyRecordStruct()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public readonly partial struct OrderId(Guid Value) { }
            """";

        var fixedCode = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public readonly partial record struct OrderId(Guid Value) { }
            """";

        var expected = Verify.Diagnostic(Descriptors.TypeMustBeRecord)
            .WithSpan(5, 1, 6, 55).WithArguments("OrderId");

        await Verify.VerifyCodeFixAsync(code, expected, fixedCode);
    }

    /// <summary>
    /// STIA0000：<c>ref struct</c> 不提供修复，它不能装箱、不能实现接口，转成任何一种 record 都不可能成为
    /// 合法的强类型 Id，机械转换只会把「一条诊断」换成「一片编译错误」。
    /// </summary>
    [Fact]
    public async Task CodeFix_ShouldNotOfferFix_WhenRefStruct()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public ref partial struct OrderId(Guid Value) { }
            """";

        var offered = await Verify.CountOfferedCodeFixesAsync(
            code,
            Descriptors.TypeMustBeRecord,
            "OrderId",
            static root => root.DescendantNodes().OfType<StructDeclarationSyntax>().First());

        Assert.Empty(offered);
    }

    /// <summary>
    /// STIA0000：转换时基列表（BaseList）必须原样搬移，不能随关键字一起丢掉。 ConvertToRecord 用 <c>WithBaseList</c> 保留接口实现，否则产物会丢失
    /// <c>: IComparable&lt;OrderId&gt;</c>。
    /// </summary>
    [Fact]
    public async Task CodeFix_Should_PreserveBaseList_WhenConvertingToRecord()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial class OrderId(Guid Value) : IComparable<OrderId> { }
            """";

        var fixedCode = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record OrderId(Guid Value) : IComparable<OrderId> { }
            """";

        var expected = Verify.Diagnostic(Descriptors.TypeMustBeRecord)
            .WithSpan(5, 1, 6, 68).WithArguments("OrderId");

        await Verify.VerifyCodeFixAsync(code, expected, fixedCode);
    }

    /// <summary>
    /// STIA0008：参数带 attribute 时把非法类型改成 Guid 仍需保留 attribute。 ChangeParameterTypeToGuid 用
    /// <c>WithType(newType.WithTriviaFrom(parameter.Type))</c> 顶掉旧类型，参数上的 <c>[AllowNull]</c> 属于节点本身（不在类型
    /// trivia 里），不会被丢掉。
    /// </summary>
    [Fact]
    public async Task CodeFix_Should_PreserveParameterAttribute_WhenChangingTypeToGuid()
    {
        var code = """"
            using System;
            using System.Diagnostics.CodeAnalysis;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record OrderId([AllowNull] Foo Value);
            """";

        var fixedCode = """"
            using System;
            using System.Diagnostics.CodeAnalysis;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record OrderId([AllowNull] Guid Value);
            """";

        var expected = Verify.Diagnostic(Descriptors.ParameterTypeIsInvalid)
            .WithSpan(7, 43, 7, 46).WithArguments("Foo", "It is not a struct or class.");

        await Verify.VerifyCodeFixAsync(code, expected, fixedCode);
    }

    /// <summary>
    /// STIA0010：attribute 有多个命名实参时，只删无效的 Validator，保留其余（TypeConverter）。走 RemoveValidatorArgument 里
    /// <c>Arguments.Count != 1</c> 的分支， <c>SeparatedSyntaxList.Remove</c> 自动收拾相邻的逗号分隔符。
    /// </summary>
    [Fact]
    public async Task CodeFix_Should_KeepOtherArguments_WhenRemovingValidator()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(TypeConverter = true, Validator = nameof(Validate))]
            public partial record OrderId(Guid Value)
            {
                private static int Validate(Guid value) => 0;
            }
            """";

        var fixedCode = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(TypeConverter = true)]
            public partial record OrderId(Guid Value)
            {
                private static int Validate(Guid value) => 0;
            }
            """";

        var expected = Verify.Diagnostic(Descriptors.ValidatorReferenceInvalid)
            .WithSpan(5, 40, 5, 68).WithArguments("Validate", "System.Guid");

        await Verify.VerifyCodeFixAsync(code, expected, fixedCode);
    }

    /// <summary>
    /// STIA0013：<c>Prefix = "   "</c>（纯空白）时删掉 <c>Prefix</c> 命名实参（等同「未设置」）。
    /// </summary>
    /// <remarks>
    /// 用纯空白而非空串：空串在生成器侧等价于「未设置」，分析器不再为它报警。
    /// </remarks>
    [Fact]
    public async Task CodeFix_Should_RemovePrefixArgument_WhenPrefixIsBlank()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Prefix = "   ")]
            public partial record struct OrderId(Guid Value);
            """";

        var fixedCode = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId]
            public partial record struct OrderId(Guid Value);
            """";

        var expected = Verify.Diagnostic(Descriptors.InvalidPrefix)
            .WithSpan(5, 18, 5, 32);

        await Verify.VerifyCodeFixAsync(code, expected, fixedCode);
    }

    /// <summary>
    /// STIA0013：attribute 有多个命名实参时，只删空白的 Prefix，保留其余（JsonShape）。
    /// </summary>
    [Fact]
    public async Task CodeFix_Should_KeepOtherArguments_WhenRemovingPrefix()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(JsonShape = JsonShape.Object, Prefix = "   ")]
            public partial record struct OrderId(Guid Value);
            """";

        var fixedCode = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(JsonShape = JsonShape.Object)]
            public partial record struct OrderId(Guid Value);
            """";

        var expected = Verify.Diagnostic(Descriptors.InvalidPrefix)
            .WithSpan(5, 48, 5, 62);

        await Verify.VerifyCodeFixAsync(code, expected, fixedCode);
    }

    /// <summary>
    /// STIA0014：把对外可见的 Id 收窄为 <c>internal</c>，使主构造函数无法被外部直接调用绕过验证器。
    /// </summary>
    [Fact]
    public async Task CodeFix_Should_NarrowVisibilityToInternal_WhenValidatorCanBeBypassed()
    {
        var code = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Validator = "Validate")]
            public partial record struct OrderId(Guid Value)
            {
                private static bool Validate(Guid value) => value != Guid.Empty;
            }
            """";

        var fixedCode = """"
            using System;

            namespace Len.StronglyTypedId.Tests;

            [StronglyTypedId(Validator = "Validate")]
            internal partial record struct OrderId(Guid Value)
            {
                private static bool Validate(Guid value) => value != Guid.Empty;
            }
            """";

        var expected = Verify.Diagnostic(Descriptors.ValidatorCanBeBypassed)
            .WithArguments("OrderId")
            .WithSpan(5, 1, 9, 2);

        await Verify.VerifyCodeFixWithEditorConfigAsync(code, EnableValidatorBypassRule, expected, fixedCode);
    }

    /// <summary>
    /// 启用 STIA0014 所用的 <c>.editorconfig</c> 片段（该规则默认关闭）。
    /// </summary>
    private const string EnableValidatorBypassRule = """
        root = true

        [*.cs]
        dotnet_diagnostic.STIA0014.severity = suggestion
        """;
}
