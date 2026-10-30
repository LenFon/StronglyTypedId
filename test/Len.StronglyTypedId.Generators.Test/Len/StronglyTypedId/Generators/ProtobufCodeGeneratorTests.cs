using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Len.StronglyTypedId.Generators;

/// <summary>
/// protobuf-net 集成：surrogate 产物的文本契约。
/// </summary>
/// <remarks>
/// 这些断言锁的是「产物长什么样」，运行时行为由 <c>ProtobufIntegrationTests</c> 覆盖。
/// 关键约束有三条，且都不是编译期能发现的：surrogate 必须配一对转换运算符、载荷必须按基元能力
/// 选原生值或规范文本、失败必须抛 <c>ProtoException</c>。
/// </remarks>
public class ProtobufCodeGeneratorTests
{
    /// <summary>
    /// 内置基元：Id 上带 <c>[ProtoContract(Surrogate = …)]</c>，surrogate 是嵌套类且字段装裸基元值。
    /// </summary>
    [Fact]
    public void BuiltInPrimitive_Should_EmitSurrogateContract()
    {
        var generated = GetGeneratedCode()["Probe.OrderId.Protobuf.g.cs"];

        generated.Should().Contain("[global::ProtoBuf.ProtoContract(Surrogate = typeof(global::Probe.OrderId.OrderIdSurrogate))]");
        generated.Should().Contain("internal sealed class OrderIdSurrogate");
        generated.Should().Contain("[global::ProtoBuf.ProtoMember(1)]");
        generated.Should().Contain("public global::System.Guid Value { get; set; }");
    }

    /// <summary>
    /// 转换运算符必须成对出现：protobuf-net 找不到运算符时会在运行期抛
    /// 「No suitable conversion operator found for surrogate」。
    /// </summary>
    [Fact]
    public void Surrogate_Should_DeclareBothConversionOperators()
    {
        var generated = GetGeneratedCode()["Probe.OrderId.Protobuf.g.cs"];

        generated.Should().Contain("public static implicit operator OrderIdSurrogate(OrderId id)");
        generated.Should().Contain("public static implicit operator OrderId(OrderIdSurrogate surrogate)");
    }

    /// <summary>
    /// 反序列化必须走 <c>TryCreate</c> 并抛 <c>ProtoException</c>：<c>Create</c> 抛的是
    /// <see cref="ArgumentException"/>，会穿透 protobuf-net 的异常契约。
    /// </summary>
    [Fact]
    public void Surrogate_Should_UseTryCreateAndThrowProtoException()
    {
        var generated = GetGeneratedCode()["Probe.OrderId.Protobuf.g.cs"];

        generated.Should().Contain("global::Probe.OrderId.TryCreate(surrogate.Value, out var id)");
        generated.Should().Contain("throw new global::ProtoBuf.ProtoException(\"Cannot convert the protobuf value to a OrderId.\");");
        generated.Should().NotContain("global::Probe.OrderId.Create(");
    }

    /// <summary>
    /// protobuf-net 没有原生表示的基元（<c>Int128</c> / <c>UInt128</c> / <c>Half</c> /
    /// <c>DateTimeOffset</c> / <c>BigInteger</c>）必须按规范文本承载，否则运行期抛
    /// 「Type is not expected, and no contract can be inferred」。
    /// </summary>
    [Theory]
    [InlineData("System.Int128")]
    [InlineData("System.UInt128")]
    [InlineData("System.Half")]
    [InlineData("System.DateTimeOffset")]
    [InlineData("System.Numerics.BigInteger")]
    public void UnsupportedPrimitive_Should_UseTextPayload(string primitiveType)
    {
        var generated = GetGeneratedCode(TextPrimitiveSource(primitiveType))["Probe.OrderId.Protobuf.g.cs"];

        generated.Should().Contain("public string? Value { get; set; }");
        generated.Should().Contain($"global::{primitiveType}.TryParse(surrogate.Value, global::System.Globalization.CultureInfo.InvariantCulture, out var value)");
    }

    /// <summary>
    /// <c>DateTimeOffset</c> 必须用 <c>"O"</c> 格式：默认格式会丢掉亚秒精度。
    /// </summary>
    [Fact]
    public void DateTimeOffset_Should_UseRoundTripFormat()
    {
        var generated = GetGeneratedCode(TextPrimitiveSource("System.DateTimeOffset"))["Probe.OrderId.Protobuf.g.cs"];

        generated.Should().Contain("id.Value.ToString(\"O\", global::System.Globalization.CultureInfo.InvariantCulture)");
    }

    /// <summary>
    /// protobuf-net 能原生编码的基元（<c>DateOnly</c> / <c>decimal</c> / <c>char</c> / 枚举）不该退回文本。
    /// </summary>
    /// <remarks>
    /// 这条与上面那条互为反向约束：把 <c>IsProtobufTextPrimitive</c> 误写成「复用二进制那套判定」
    /// 会让 <c>DateOnly</c> 变成文本载荷，线格式与既有数据不兼容。
    /// <para>
    /// 断言用「字段不是 <c>string?</c>」而非精确类型名：产物里的类型名走 Roslyn 的显示格式，
    /// <c>decimal</c> / <c>char</c> 这类关键字基元渲染成关键字而非 <c>global::System.Decimal</c>。
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData("System.DateOnly")]
    [InlineData("System.Decimal")]
    [InlineData("System.Char")]
    public void NativelySupportedPrimitive_Should_KeepRawPayload(string primitiveType)
    {
        var generated = GetGeneratedCode(TextPrimitiveSource(primitiveType))["Probe.OrderId.Protobuf.g.cs"];

        generated.Should().NotContain("public string? Value { get; set; }");
        generated.Should().Contain($" Value {{ get; set; }}");
        generated.Should().NotContain(".TryParse(surrogate.Value");
    }

    /// <summary>
    /// 引用类型 Id 的 surrogate 必须容忍 <c>null</c>，且 null 载荷还原成 null Id。
    /// </summary>
    [Fact]
    public void ReferenceTypeId_Should_TolerateNull()
    {
        var generated = GetGeneratedCode(ReferenceTypeSource)["Probe.OrderId.Protobuf.g.cs"];

        generated.Should().Contain("public static implicit operator OrderIdSurrogate?(OrderId? id)");
        generated.Should().Contain("id is null ? null : new() { Value = id.Value }");
        generated.Should().Contain("if (surrogate is null || surrogate.Value is null)");
    }

    /// <summary>
    /// 泛型容器里的 Id 不生成 surrogate：特性实参含类型参数会 CS0416，且没有接线时它只是死代码。
    /// </summary>
    [Fact]
    public void GenericContainer_Should_NotEmitSurrogate()
    {
        var generated = GetGeneratedCode(GenericContainerSource);

        generated.Keys.Should().NotContain(hintName => hintName.Contains("Protobuf", StringComparison.Ordinal));
    }

    /// <summary>
    /// 用户已加 <c>[ProtoContract]</c> 时跳过：特性不允许重复，再补一个会 CS0579。
    /// </summary>
    [Fact]
    public void UserAnnotatedId_Should_BeSkipped()
    {
        var generated = GetGeneratedCode(UserAnnotatedSource);

        generated.Keys.Should().NotContain(hintName => hintName.Contains("Protobuf", StringComparison.Ordinal));
    }

    /// <summary>
    /// 未引用 protobuf-net 时不产出任何 protobuf 产物。
    /// </summary>
    [Fact]
    public void WithoutProtobufReference_Should_NotEmitAnything()
    {
        var generated = GetGeneratedCode(protobuf: false);

        generated.Keys.Should().NotContain(hintName => hintName.Contains("Protobuf", StringComparison.Ordinal));
    }

    /// <summary>
    /// <c>Integrations = None</c> 时即使引用了 protobuf-net 也不产出。
    /// </summary>
    [Fact]
    public void IntegrationsNone_Should_NotEmitAnything()
    {
        var generated = GetGeneratedCode(IntegrationsNoneSource);

        generated.Keys.Should().NotContain(hintName => hintName.Contains("Protobuf", StringComparison.Ordinal));
    }

    #region 源码与辅助

    /// <summary>
    /// 跑生成器并把全部产物按 hint 名索引。
    /// </summary>
    private static Dictionary<string, string> GetGeneratedCode(string? source = null, bool protobuf = true)
    {
        var compilation = CreateCompilation(source ?? DefaultSource, protobuf);

        var result = CSharpGeneratorDriver
            .Create(new StronglyTypedIdGenerator())
            .RunGenerators(compilation)
            .GetRunResult();

        result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        return result.Results[0].GeneratedSources
            .ToDictionary(generated => generated.HintName, generated => generated.SourceText.ToString());
    }

    private static string DefaultSource => """
        using System;
        using Len.StronglyTypedId;

        namespace Probe;

        [StronglyTypedId]
        public partial record struct OrderId(Guid Value);
        """;

    private static string TextPrimitiveSource(string primitiveType) => $$"""
        using System;
        using Len.StronglyTypedId;

        namespace Probe;

        [StronglyTypedId]
        public partial record struct OrderId(global::{{primitiveType}} Value);
        """;

    private const string ReferenceTypeSource = """
        using System;
        using Len.StronglyTypedId;

        namespace Probe;

        [StronglyTypedId]
        public partial record OrderId(string Value);
        """;

    private const string GenericContainerSource = """
        using System;
        using Len.StronglyTypedId;

        namespace Probe;

        public partial class Box<T>
        {
            [StronglyTypedId]
            public partial record struct BoxedId(Guid Value);
        }
        """;

    private const string UserAnnotatedSource = """
        using System;
        using Len.StronglyTypedId;
        using ProtoBuf;

        namespace Probe;

        [StronglyTypedId]
        [ProtoContract]
        public partial record struct OrderId(Guid Value);
        """;

    private const string IntegrationsNoneSource = """
        using System;
        using Len.StronglyTypedId;

        namespace Probe;

        [StronglyTypedId(Integrations = StronglyTypedIdIntegrations.None)]
        public partial record struct OrderId(Guid Value);
        """;

    private static CSharpCompilation CreateCompilation(string sourceCode, bool protobuf)
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
        references.Add(AssemblyReferenceCache.FromAssembly(typeof(Newtonsoft.Json.JsonConvert).Assembly));

        // protobuf-net 的门控查 protobuf-net.dll；特性本身在 protobuf-net.Core.dll，两个都要给。
        // 按路径建引用而不是 typeof(...).Assembly：后者会把程序集加载进 AppDomain，于是本工程其余用例
        // 的 AppDomain 扫描也会带上它，protobuf 产物凭空出现在「只该有 STJ / Newtonsoft」的断言里。
        if (protobuf)
        {
            references.Add(AssemblyReferenceCache.FromFile(Path.Combine(AppContext.BaseDirectory, "protobuf-net.Core.dll")));
            references.Add(AssemblyReferenceCache.FromFile(Path.Combine(AppContext.BaseDirectory, "protobuf-net.dll")));
        }

        return CSharpCompilation.Create(
            "Probe",
            [CSharpSyntaxTree.ParseText(sourceCode)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
    }

    /// <summary>
    /// 生成器测试不应引用以下程序集，否则会触发 EF Core / Swagger 生成器或被 TestBase 中已实现的强类型 Id 污染发现结果。
    /// </summary>
    private static bool ShouldReferenceAssembly(System.Reflection.Assembly assembly)
        => assembly.GetName().Name is not
            ("Len.StronglyTypedId.TestBase" or "Microsoft.EntityFrameworkCore" or "Swashbuckle.AspNetCore.SwaggerGen" or "Microsoft.OpenApi" or "Microsoft.AspNetCore.Mvc" or "Microsoft.AspNetCore.Mvc.Core" or "Microsoft.AspNetCore.Mvc.Abstractions" or "Microsoft.AspNetCore.OpenApi" or "Microsoft.AspNetCore.Routing");

    #endregion
}
