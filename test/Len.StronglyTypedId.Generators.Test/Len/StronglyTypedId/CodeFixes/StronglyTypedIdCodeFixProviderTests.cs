using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Len.StronglyTypedId.CodeFixes;

public class StronglyTypedIdCodeFixProviderTests
{
    private readonly static string FixedCode = """
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

        await Verify.VerifyCodeFixAsync(code, expected, FixedCode);
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

        await Verify.VerifyCodeFixAsync(code, expected, FixedCode);
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

        await Verify.VerifyCodeFixAsync(code, expected, FixedCode);
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

        await Verify.VerifyCodeFixAsync(code, expected, FixedCode);
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

        await Verify.VerifyCodeFixAsync(code, expected, FixedCode);
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
    /// <remarks>
    /// 既有用例都是「一条诊断、一次修复」。FixAll 走的是 <c>GetFixAllProvider()</c> 返回的批处理器，
    /// 与逐条修复是两条不同的代码路径：批处理会把各诊断的改动合并到同一份语法树后再整体应用，
    /// 因此「两条诊断分别位于不同声明上、且都要就地加 partial」这一情形需要单独把守。
    /// </remarks>
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

    /// <summary>
    /// 绕过验证器直接用 <c>new</c> 构造（STIAO011）时，CodeFix 应把它重写成 <c>Xxx.Create(...)</c>。
    /// </summary>
    /// <remarks>
    /// 该诊断只在 Id 设了验证器时触发，而该情形下生成代码已提供静态 <c>Create</c> 工厂，
    /// 重写后的调用点强制经过校验，与诊断的意图一致。
    /// </remarks>
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
    /// STIAO000：把 class 转成 record（保留修饰符 / 主构造函数 / 成员 / 标识符）。
    /// </summary>
    /// <remarks>
    /// 输入特意写成「命名空间内、已 partial、主构造函数合法」的 class，使修复只改关键字、不再级联其它规则：
    /// 转换后得到的 <c>partial record OrderId(Guid Value)</c> 已满足 partial / 命名空间 / 主构造全部约束。
    /// </remarks>
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
    /// STIAO004：把最外层类型包进命名空间块（占位名固定 MyNamespace，由使用者改名）。
    /// </summary>
    /// <remarks>
    /// 顶层类型（直接挂在编译单元下）才触发 STIAO004；此处必须显式 <c>using Len.StronglyTypedId;</c>
    /// 让特性在命名空间外也能绑定，否则分析器会因找不到特性而零产出。
    /// </remarks>
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
    /// STIAO005：缺主构造函数时补一个单参主构造 (Guid Value)。
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
    /// STIAO005：主构造函数多参数时整段替换为单参版本，并把参数名规整为 Value。
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
    /// STIAO008：把非受支持基元类型参数改成 Guid（名称此前已是 Value）。
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
            .WithSpan(6, 31, 6, 34).WithArguments("Foo");

        await Verify.VerifyCodeFixAsync(code, expected, fixedCode);
    }

    /// <summary>
    /// STIAO010：删掉 [StronglyTypedId] 里无效的 Validator 命名实参（唯一实参时连同括号一起删）。
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
    /// STIAO000：把 struct 转成 record。IsFixable 里 STIAO000 分支判定的是
    /// <c>ClassDeclarationSyntax or StructDeclarationSyntax</c>，struct 走的是 StructDeclarationSyntax 这一支，
    /// 与 class 是不同代码路径，需单独把守。
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
            public partial record OrderId(Guid Value) { }
            """";

        var expected = Verify.Diagnostic(Descriptors.TypeMustBeRecord)
            .WithSpan(5, 1, 6, 46).WithArguments("OrderId");

        await Verify.VerifyCodeFixAsync(code, expected, fixedCode);
    }

    /// <summary>
    /// STIAO000：转换时基列表（BaseList）必须原样搬移，不能随关键字一起丢掉。
    /// ConvertToRecord 用 <c>WithBaseList</c> 保留接口实现，否则产物会丢失 <c>: IComparable&lt;OrderId&gt;</c>。
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
    /// STIAO008：参数带 attribute 时把非法类型改成 Guid 仍需保留 attribute。
    /// ChangeParameterTypeToGuid 用 <c>WithType(newType.WithTriviaFrom(parameter.Type))</c> 顶掉旧类型，
    /// 参数上的 <c>[AllowNull]</c> 属于节点本身（不在类型 trivia 里），不会被丢掉。
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
            .WithSpan(7, 43, 7, 46).WithArguments("Foo");

        await Verify.VerifyCodeFixAsync(code, expected, fixedCode);
    }

    /// <summary>
    /// STIAO010：attribute 有多个命名实参时，只删无效的 Validator，保留其余（TypeConverter）。
    /// 走 RemoveValidatorArgument 里 <c>Arguments.Count != 1</c> 的分支，
    /// <c>SeparatedSyntaxList.Remove</c> 自动收拾相邻的逗号分隔符。
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
    /// STIAO010 增强：当 <c>Validator</c> 指向的方法根本不存在时，CodeFix 应在 Id 类型里生成一个符合契约的
    /// 桩方法（静态、返回 bool、恰好一个参数且类型等于基元 Id 类型），而非删掉实参。
    /// 桩方法用全限定名渲染基元类型，避免与任何同名局部类型冲突。
    /// </summary>
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
                private static bool Validate(global::System.Guid value) => true;
            }
            """";

        var expected = Verify.Diagnostic(Descriptors.ValidatorReferenceInvalid)
            .WithSpan(5, 18, 5, 46).WithArguments("Validate", "System.Guid");

        await Verify.VerifyCodeFixAsync(code, expected, fixedCode);
    }
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
    /// STIAO005 的 IsFixable 负向分支：record 已有 body 构造函数、却无单参主构造函数时，
    /// 再加主构造会与既有 body 构造撞 CS0111。此时诊断虽触发，但 CodeFix 不应提供任何修复入口 ——
    /// RegisterCodeFixesAsync 经 IsFixable 门禁直接跳过，注册 0 个 CodeAction。
    /// 该场景无法用 VerifyCodeFixAsync 表达（框架假定修复一定会被应用），故直接驱动 provider 断言 0 个修复。
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

        var offered = await CountOfferedCodeFixesAsync(
            code,
            Descriptors.TypeMustHaveSingleParameterPrimaryConstructor,
            "OrderId");

        Assert.Empty(offered);
    }

    /// <summary>
    /// 对照：同一 harness 在「可修复」情形下确实会注册修复（record 无主构造、也无 body 构造时 STIAO005 可修），
    /// 证明上面的空集合断言是真门禁生效、而非 harness 根本没驱动 provider。
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

        var offered = await CountOfferedCodeFixesAsync(
            code,
            Descriptors.TypeMustHaveSingleParameterPrimaryConstructor,
            "OrderId");

        Assert.Single(offered);
    }

    /// <summary>
    /// 直接驱动 CodeFixProvider：把源码解析成 Document，在指定诊断（定位在 record 声明上）上调用
    /// RegisterCodeFixesAsync，收集它注册的 CodeAction。用于断言「不提供修复」这类 VerifyCodeFixAsync
    /// 框架本身不支持的场景（后者假定修复一定会被应用）。
    /// </summary>
    private static async Task<IReadOnlyList<CodeAction>> CountOfferedCodeFixesAsync(
        string source,
        DiagnosticDescriptor descriptor,
        string typeName)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
        var tree = CSharpSyntaxTree.ParseText(source, parseOptions);
        var root = tree.GetRoot();
        var recordDeclaration = root.DescendantNodes().OfType<RecordDeclarationSyntax>().First();
        var location = Location.Create(tree, recordDeclaration.Span);
        var diagnostic = Diagnostic.Create(descriptor, location, typeName);

        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var project = workspace.CurrentSolution
            .AddProject(projectId, "TestProject", "TestProject", LanguageNames.CSharp)
            .GetProject(projectId)!
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithParseOptions(parseOptions);
        var document = project.AddDocument("Test.cs", tree.GetRoot());

        var actions = new List<CodeAction>();
        var context = new CodeFixContext(
            document,
            diagnostic,
            (action, _) => actions.Add(action),
            CancellationToken.None);

        var provider = new StronglyTypedIdCodeFixProvider();
        await provider.RegisterCodeFixesAsync(context);

        return actions;
    }
}
