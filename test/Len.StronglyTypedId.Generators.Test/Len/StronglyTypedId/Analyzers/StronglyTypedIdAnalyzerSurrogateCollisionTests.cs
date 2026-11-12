// xUnit1051：本文件中的 CSharpSyntaxTree.ParseText / Emit 等均为测试内同步调用，取消令牌无意义，
// 统一在此文件禁用该测试框架分析器警告。
#pragma warning disable xUnit1051
using System.Reflection;
using FluentAssertions;
using Len.StronglyTypedId.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Len.StronglyTypedId.Generators;

/// <summary>
/// STIA0029 对 <c>{TypeName}Surrogate</c> 的判定必须与 protobuf-net 生成器的真实条件同源。
/// </summary>
/// <remarks>
/// 缺陷回归：判据曾无条件把 <c>{TypeName}Surrogate</c> 当作生成成员，于是「没引用 protobuf-net」
/// 「Id 在泛型容器里」「用户自带 <c>[ProtoContract]</c>」三种情况下，用户的同名嵌套类型被误报成 Error，
/// 而生成器根本不产出该类型。
/// <para>
/// 这里不能走 <c>Verify.VerifyAnalyzerAsync</c>：那套引用集不含 protobuf-net，测不到「引用了但生成器让位」
/// 这一档。故自建编译，按需附加 protobuf-net 引用。
/// </para>
/// </remarks>
public class StronglyTypedIdAnalyzerSurrogateCollisionTests
{
    /// <summary>
    /// 引用了 protobuf-net 且用户没写 <c>[ProtoContract]</c>：生成器产出 surrogate，撞名必须报告。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenSurrogateCollidesAndGeneratorEmitsIt()
    {
        const string Code = """
            using System;
            using Len.StronglyTypedId;

            namespace Probe;

            [StronglyTypedId]
            public partial record struct OrderId(Guid Value)
            {
                public sealed class OrderIdSurrogate { }
            }
            """;

        var diagnostics = await AnalyzeAsync(Code, protobuf: true);

        diagnostics.Should().ContainSingle(d => d.Id == Descriptors.MemberCollidesWithGeneratedMemberId);
    }

    /// <summary>
    /// 没引用 protobuf-net：生成器不产出 surrogate，用户的同名嵌套类型合法，不报告。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenSurrogateIsDeclaredWithoutProtobufReference()
    {
        const string Code = """
            using System;
            using Len.StronglyTypedId;

            namespace Probe;

            [StronglyTypedId]
            public partial record struct OrderId(Guid Value)
            {
                public sealed class OrderIdSurrogate { }
            }
            """;

        var diagnostics = await AnalyzeAsync(Code, protobuf: false);

        diagnostics.Should().NotContain(d => d.Id == Descriptors.MemberCollidesWithGeneratedMemberId);
    }

    /// <summary>
    /// 用户自带 <c>[ProtoContract]</c>：生成器主动让位，不产出 surrogate，同名嵌套类型合法。
    /// </summary>
    /// <remarks>
    /// 这一档最容易判错：分析器跑在<b>生成后</b>的编译上，此时 <c>[ProtoContract]</c> 可能正是生成器自己加的。
    /// 判据必须区分「特性在用户源文件里」与「特性在生成的 <c>.Protobuf.g.cs</c> 里」。
    /// </remarks>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenUserTakesOverProtoContract()
    {
        const string Code = """
            using System;
            using Len.StronglyTypedId;

            namespace Probe;

            [StronglyTypedId]
            [ProtoBuf.ProtoContract]
            public partial record struct OrderId(Guid Value)
            {
                public sealed class OrderIdSurrogate { }
            }
            """;

        var diagnostics = await AnalyzeAsync(Code, protobuf: true);

        diagnostics.Should().NotContain(d => d.Id == Descriptors.MemberCollidesWithGeneratedMemberId);
    }

    /// <summary>
    /// Id 声明在泛型容器里：生成器跳过 surrogate，同名嵌套类型合法。
    /// </summary>
    [Fact]
    public async Task AnalyzingCode_Should_NoDiagnostic_WhenSurrogateIsDeclaredInsideGenericContainer()
    {
        const string Code = """
            using System;
            using Len.StronglyTypedId;

            namespace Probe;

            public partial class Box<T>
            {
                [StronglyTypedId]
                public partial record struct BoxedId(Guid Value)
                {
                    public sealed class BoxedIdSurrogate { }
                }
            }
            """;

        var diagnostics = await AnalyzeAsync(Code, protobuf: true);

        diagnostics.Should().NotContain(d => d.Id == Descriptors.MemberCollidesWithGeneratedMemberId);
    }

    /// <summary>
    /// 用户自定义同名 <c>ProtoContractAttribute</c> 不算「接管 protobuf 契约」：生成器仍会产出 surrogate，
    /// 撞名必须报告。
    /// </summary>
    /// <remarks>
    /// 缺陷回归：判据曾按<b>短名</b> <c>ProtoContractAttribute</c> 匹配，于是用户自己定义的同名特性
    /// （或别的命名空间下的同名特性）被误判成「用户接管」，生成器照常产出 surrogate 而分析器不报，
    /// 漏掉一条 CS0102。生成器侧用的是全限定名 <c>global::ProtoBuf.ProtoContractAttribute</c>。
    /// </remarks>
    [Fact]
    public async Task AnalyzingCode_Should_ReturnDiagnostic_WhenUserDefinesItsOwnProtoContractAttribute()
    {
        const string Code = """
            using System;
            using Len.StronglyTypedId;

            namespace Probe;

            [StronglyTypedId]
            [ProtoContract]
            public partial record struct OrderId(Guid Value)
            {
                public sealed class OrderIdSurrogate { }
            }

            public sealed class ProtoContractAttribute : Attribute { }
            """;

        var diagnostics = await AnalyzeAsync(Code, protobuf: true);

        diagnostics.Should().ContainSingle(d => d.Id == Descriptors.MemberCollidesWithGeneratedMemberId);
    }

    /// <summary>
    /// 生成文件的识别必须按「路径里含生成器程序集名」，不能只看 <c>.g.cs</c> 后缀。
    /// </summary>
    /// <remarks>
    /// 缺陷回归：判据曾按 <c>.g.cs</c> 后缀排除生成文件，于是用户给文件起这个名字时，
    /// 生成器自己加的 <c>[ProtoContract]</c> 会被当成用户写的。
    /// <para>
    /// 这一档无法用端到端用例覆盖：Roslyn 的 <c>GeneratedCodeAnalysisFlags.None</c> 会把 <c>*.g.cs</c>
    /// 整体当生成代码跳过，分析器根本不跑。故直接断言判据本身。
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(@"Len.StronglyTypedId.Generators\Len.StronglyTypedId.Generators.StronglyTypedIdGenerator\Probe.OrderId.Protobuf.g.cs", true)]
    [InlineData(@"C:\src\OrderIds.g.cs", false)]
    [InlineData(@"C:\src\OrderIds.cs", false)]
    [InlineData(@"Len.StronglyTypedId.Generators/Probe.OrderId.g.cs", true)]
    public void IsGeneratedFile_Should_RecognizeGeneratorOutputByAssemblyName(string path, bool expected)
    {
        var method = typeof(StronglyTypedIdAnalyzer).GetMethod(
            "IsGeneratedFile",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

        method.Invoke(null, [path]).Should().Be(expected);
    }

    /// <summary>
    /// 跑生成器与分析器，返回分析器诊断。
    /// </summary>
    /// <remarks>
    /// 必须先跑生成器：STIA0029 的判据依赖「本次实际会生成哪些成员」，而分析器在真实构建里同样跑在
    /// 生成后的编译上。只跑分析器会漏掉「生成器自己加了 <c>[ProtoContract]</c>」这一档。
    /// </remarks>
    private static async Task<System.Collections.Immutable.ImmutableArray<Diagnostic>> AnalyzeAsync(
        string source,
        bool protobuf,
        string? filePath = null)
    {
        var compilation = CreateCompilation(source, protobuf, filePath);

        CSharpGeneratorDriver
            .Create(new StronglyTypedIdGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);

        return await outputCompilation
            .WithAnalyzers([new StronglyTypedIdAnalyzer()])
            .GetAnalyzerDiagnosticsAsync();
    }

    private static CSharpCompilation CreateCompilation(string sourceCode, bool protobuf, string? filePath = null)
    {
        var references = AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
            .Where(ShouldReferenceAssembly)
            .Select(AssemblyReferenceCache.TryResolveReference)
            .OfType<MetadataReference>()
            .ToList();

        references.Add(AssemblyReferenceCache.FromAssembly(typeof(StronglyTypedIdAttribute).Assembly));
        references.Add(AssemblyReferenceCache.FromAssembly(typeof(System.Text.Json.JsonSerializer).Assembly));

        // protobuf-net 的门控查 protobuf-net.dll；特性本身在 protobuf-net.Core.dll，两个都要给。
        // 按路径建引用而不是 typeof(...).Assembly：后者会把程序集加载进 AppDomain，污染其余用例的扫描结果。
        if (protobuf)
        {
            references.Add(AssemblyReferenceCache.FromFile(Path.Combine(AppContext.BaseDirectory, "protobuf-net.Core.dll")));
            references.Add(AssemblyReferenceCache.FromFile(Path.Combine(AppContext.BaseDirectory, "protobuf-net.dll")));
        }

        return CSharpCompilation.Create(
            "Probe",
            [CSharpSyntaxTree.ParseText(sourceCode, path: filePath ?? string.Empty)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
    }

    /// <summary>
    /// 生成器测试不应引用以下程序集，否则会触发 EF Core / Swagger 生成器或被 TestBase 中已实现的强类型 Id 污染发现结果。
    /// </summary>
    private static bool ShouldReferenceAssembly(Assembly assembly)
        => assembly.GetName().Name is not
            ("Len.StronglyTypedId.TestBase" or "Microsoft.EntityFrameworkCore" or "Swashbuckle.AspNetCore.SwaggerGen" or "Microsoft.OpenApi" or "Microsoft.AspNetCore.Mvc" or "Microsoft.AspNetCore.Mvc.Core" or "Microsoft.AspNetCore.Mvc.Abstractions" or "Microsoft.AspNetCore.OpenApi" or "Microsoft.AspNetCore.Routing");
}
