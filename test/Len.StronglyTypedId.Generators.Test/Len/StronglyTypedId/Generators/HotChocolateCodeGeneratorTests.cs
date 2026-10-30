// xUnit1051：本文件中的 CSharpSyntaxTree.ParseText 均为测试内同步调用，取消令牌无意义，
// 统一在此文件禁用该测试框架分析器警告。
#pragma warning disable xUnit1051
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Len.StronglyTypedId.Generators;

/// <summary>
/// <see cref="HotChocolateCodeGenerator"/> 的单元测试：门控、产物形态、基元字面量分派与注册入口幂等。
/// </summary>
/// <remarks>
/// 生成器只产出文本，故这里用合成程序集满足门控，不引用真实的 HotChocolate 包 —— 真实往返由
/// <c>Len.StronglyTypedId.GraphQL.Test</c> 覆盖。
/// </remarks>
public class HotChocolateCodeGeneratorTests
{
    #region 门控：HotChocolate 主版本 >= 13

    [Fact]
    public void HotChocolate_Should_Generate_WhenMajorVersionIsThirteen()
    {
        var generated = RunGenerator(IdSource, HotChocolate13Reference);

        generated.Should().ContainKey("Len.StronglyTypedId.OrderId.HotChocolate.g.cs");
        generated.Should().ContainKey("StronglyTypedIds.HotChocolate.g.cs");
    }

    [Fact]
    public void HotChocolate_Should_NotGenerate_WhenMajorVersionIsTwelve()
    {
        var generated = RunGenerator(IdSource, SyntheticAssembly.GetOrCreate("HotChocolate.Types", "12.0.0.0"));

        generated.Keys.Should().NotContain(key => key.EndsWith(".HotChocolate.g.cs", StringComparison.Ordinal));
    }

    /// <summary>
    /// 闸门只看主版本：13.x 的任意补丁号都必须生成。
    /// </summary>
    [Fact]
    public void HotChocolate_Should_IgnoreMinorAndPatchVersion()
    {
        var generated = RunGenerator(IdSource, SyntheticAssembly.GetOrCreate("HotChocolate.Types", "13.42.13.7"));

        generated.Should().ContainKey("StronglyTypedIds.HotChocolate.g.cs");
    }

    #endregion

    #region 产物形态

    [Fact]
    public void HotChocolate_Should_GenerateScalarAndAddStronglyTypedIds()
    {
        var generated = RunGenerator(IdSource, HotChocolate13Reference);

        generated["Len.StronglyTypedId.OrderId.HotChocolate.g.cs"]
            .Should().Contain("internal sealed class OrderIdHotChocolateType : global::HotChocolate.Types.ScalarType<global::Len.StronglyTypedId.OrderId>")
            .And.Contain("public override bool TrySerialize(")
            .And.Contain("public override bool TryDeserialize(")
            .And.Contain("public override object ParseLiteral(")
            .And.Contain("public override global::HotChocolate.Language.IValueNode ParseResult(");

        generated["StronglyTypedIds.HotChocolate.g.cs"]
            .Should().Contain("public static global::HotChocolate.Execution.Configuration.IRequestExecutorBuilder AddStronglyTypedIds(")
            .And.Contain("this global::HotChocolate.Execution.Configuration.IRequestExecutorBuilder builder)")
            // 注册语句用扩展方法语法书写，与 HotChocolate 自己的 AddGraphQLServer() 风格一致。
            .And.Contain("builder.Services.TryAddSingleton(typeof(OrderIdHotChocolateType), typeof(OrderIdHotChocolateType));")
            .And.Contain("builder.BindRuntimeType<global::Len.StronglyTypedId.OrderId, OrderIdHotChocolateType>();");
    }

    /// <summary>
    /// 注册入口必须幂等：<c>TryAddSingleton</c> 保证重复调用不会留下第二条注册，
    /// <c>BindRuntimeType</c> 重复调用也不会让 schema 出现重复条目。
    /// </summary>
    [Fact]
    public void HotChocolate_AddStronglyTypedIds_Should_BeIdempotent()
    {
        var generated = RunGenerator(IdSource, HotChocolate13Reference);

        generated["StronglyTypedIds.HotChocolate.g.cs"]
            .Should().Contain("builder.Services.TryAddSingleton(")
            .And.NotContain("builder.Services.AddSingleton(")
            .And.NotContain("builder.Services.Any(");
    }

    /// <summary>
    /// 一个 Id 都没有时不产出空壳注册类，与其余集成一致。
    /// </summary>
    [Fact]
    public void HotChocolate_Should_NotGenerate_WhenNoId()
    {
        var generated = RunGenerator(NoIdSource, HotChocolate13Reference);

        generated.Keys.Should().NotContain(key => key.EndsWith(".HotChocolate.g.cs", StringComparison.Ordinal));
    }

    /// <summary>
    /// <c>Integrations</c> 收窄到不含 HotChocolate 时不得产出任何 HotChocolate 产物。
    /// </summary>
    [Fact]
    public void HotChocolate_Should_NotGenerate_WhenIntegrationIsFilteredOut()
    {
        var generated = RunGenerator(NoneSource, HotChocolate13Reference);

        generated.Keys.Should().NotContain(key => key.EndsWith(".HotChocolate.g.cs", StringComparison.Ordinal));
    }

    #endregion

    #region 字面量分派：口径与 JSON 一致

    /// <summary>
    /// 字符串类基元（<c>Guid</c> / <c>string</c> / <c>char</c> / 日期时间）在 GraphQL 里是 <c>String</c> 字面量，
    /// 与 JSON 的字符串形态一致。
    /// </summary>
    [Theory]
    [InlineData("Guid")]
    [InlineData("string")]
    [InlineData("char")]
    [InlineData("DateTime")]
    [InlineData("DateTimeOffset")]
    [InlineData("TimeSpan")]
    [InlineData("DateOnly")]
    [InlineData("TimeOnly")]
    public void HotChocolate_Should_UseStringLiteral_ForTextPrimitive(string primitive)
    {
        var generated = RunGenerator(IdSourceFor(primitive), HotChocolate13Reference);

        generated["Len.StronglyTypedId.OrderId.HotChocolate.g.cs"]
            .Should().Contain("new global::HotChocolate.Language.StringValueNode(");
    }

    /// <summary>
    /// 整数类基元在 GraphQL 里是 <c>Int</c> 字面量；枚举按底层整数（与 STJ 默认一致），
    /// 不按成员名 —— 否则同一个 Id 在 REST 与 GraphQL 两侧表示不同。
    /// </summary>
    [Theory]
    [InlineData("byte")]
    [InlineData("sbyte")]
    [InlineData("short")]
    [InlineData("ushort")]
    [InlineData("int")]
    [InlineData("uint")]
    [InlineData("long")]
    [InlineData("ulong")]
    [InlineData("Int128")]
    [InlineData("UInt128")]
    [InlineData("BigInteger")]
    public void HotChocolate_Should_UseIntLiteral_ForIntegerPrimitive(string primitive)
    {
        var generated = RunGenerator(IdSourceFor(primitive), HotChocolate13Reference);

        generated["Len.StronglyTypedId.OrderId.HotChocolate.g.cs"]
            .Should().Contain("new global::HotChocolate.Language.IntValueNode(");
    }

    [Fact]
    public void HotChocolate_Should_UseIntLiteral_ForEnumPrimitive()
    {
        var generated = RunGenerator(EnumIdSource, HotChocolate13Reference);

        generated["Len.StronglyTypedId.StatusId.HotChocolate.g.cs"]
            .Should().Contain("new global::HotChocolate.Language.IntValueNode(")
            .And.NotContain("new global::HotChocolate.Language.StringValueNode(");
    }

    /// <summary>
    /// 浮点类基元在 GraphQL 里是 <c>Float</c> 字面量。
    /// </summary>
    [Theory]
    [InlineData("decimal")]
    [InlineData("double")]
    [InlineData("float")]
    [InlineData("Half")]
    public void HotChocolate_Should_UseFloatLiteral_ForFloatingPrimitive(string primitive)
    {
        var generated = RunGenerator(IdSourceFor(primitive), HotChocolate13Reference);

        generated["Len.StronglyTypedId.OrderId.HotChocolate.g.cs"]
            .Should().Contain("new global::HotChocolate.Language.FloatValueNode(");
    }

    /// <summary>
    /// 带前缀的 Id 对外暴露的是规范文本形，数字基元也一样 —— 与 OpenAPI schema 的口径一致。
    /// </summary>
    [Fact]
    public void HotChocolate_Should_UseStringLiteral_WhenIdHasPrefix()
    {
        var generated = RunGenerator(PrefixedIntIdSource, HotChocolate13Reference);

        generated["Len.StronglyTypedId.OrderId.HotChocolate.g.cs"]
            .Should().Contain("new global::HotChocolate.Language.StringValueNode(")
            .And.NotContain("new global::HotChocolate.Language.IntValueNode(");
    }

    #endregion

    #region 失败路径与可空性

    /// <summary>
    /// 解析失败必须抛 HotChocolate 的 <c>SerializationException</c>，而不是让 <c>FormatException</c> 穿透 ——
    /// 后者会被框架报成「Unexpected Execution Error」，客户端拿不到可读消息。
    /// </summary>
    [Fact]
    public void HotChocolate_Should_ThrowSerializationException_OnParseFailure()
    {
        var generated = RunGenerator(IdSource, HotChocolate13Reference);

        generated["Len.StronglyTypedId.OrderId.HotChocolate.g.cs"]
            .Should().Contain("throw new global::HotChocolate.Types.SerializationException(")
            .And.NotContain("throw new global::System.FormatException(");
    }

    /// <summary>
    /// 引用类型 Id 的 <c>TrySerialize</c> 收到 <see langword="null"/> 时必须返回 <see langword="false"/>，
    /// 让 HotChocolate 写出 GraphQL <c>null</c>，而不是抛 <see cref="NullReferenceException"/>。
    /// </summary>
    [Fact]
    public void HotChocolate_Should_HandleNull_ForReferenceTypeId()
    {
        var generated = RunGenerator(ClassIdSource, HotChocolate13Reference);

        generated["Len.StronglyTypedId.OrderClassId.HotChocolate.g.cs"]
            .Should().Contain("if (runtimeValue is global::Len.StronglyTypedId.OrderClassId id)");
    }

    /// <summary>
    /// <c>TryDeserialize</c> 必须同时接受 <c>string</c> 与 <c>JsonElement</c>：变量经真实 HTTP 管道进来时
    /// 是 <c>JsonElement</c>，经 <c>QueryRequestBuilder</c> 直接构造时是 <c>string</c>。
    /// </summary>
    [Fact]
    public void HotChocolate_Should_AcceptJsonElement_InTryDeserialize()
    {
        var generated = RunGenerator(IdSource, HotChocolate13Reference);

        generated["Len.StronglyTypedId.OrderId.HotChocolate.g.cs"]
            .Should().Contain("global::System.Text.Json.JsonElement");
    }

    #endregion

    #region 辅助

    private static MetadataReference HotChocolate13Reference { get; }
        = SyntheticAssembly.GetOrCreate("HotChocolate.Types", "13.0.0.0");

    private const string IdSource = """
        using System;

        namespace Len.StronglyTypedId;

        [StronglyTypedId]
        public partial record struct OrderId(Guid Value);
        """;

    private const string ClassIdSource = """
        using System;

        namespace Len.StronglyTypedId;

        [StronglyTypedId]
        public partial record OrderClassId(Guid Value);
        """;

    private const string EnumIdSource = """
        namespace Len.StronglyTypedId;

        public enum OrderStatus
        {
            Pending = 0,
            Paid = 1,
        }

        [StronglyTypedId]
        public partial record struct StatusId(OrderStatus Value);
        """;

    private const string PrefixedIntIdSource = """
        namespace Len.StronglyTypedId;

        [StronglyTypedId(Prefix = "ord_")]
        public partial record struct OrderId(int Value);
        """;

    private const string NoIdSource = """
        namespace Len.StronglyTypedId;

        public class NotAnId
        {
        }
        """;

    private const string NoneSource = """
        using System;

        namespace Len.StronglyTypedId;

        [StronglyTypedId(Integrations = StronglyTypedIdIntegrations.None)]
        public partial record struct OrderId(Guid Value);
        """;

    private static string IdSourceFor(string primitive)
        => $$"""
            using System;
            using System.Numerics;

            namespace Len.StronglyTypedId;

            [StronglyTypedId]
            public partial record struct OrderId({{primitive}} Value);
            """;

    private static Dictionary<string, string> RunGenerator(string sourceCode, params MetadataReference[] extraReferences)
    {
        var compilation = CreateCompilation(sourceCode, extraReferences);

        var result = CSharpGeneratorDriver
            .Create(new StronglyTypedIdGenerator())
            .RunGenerators(compilation)
            .GetRunResult();

        result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        return result.Results[0].GeneratedSources
            .ToDictionary(source => source.HintName, source => source.SourceText.ToString());
    }

    private static CSharpCompilation CreateCompilation(string sourceCode, params MetadataReference[] extraReferences)
    {
        // System.Text.Json 是延迟加载的：必须显式引用一次，它才会出现在编译引用里。
        var seed = new[]
        {
            typeof(StronglyTypedIdAttribute).Assembly,
            typeof(System.Text.Json.JsonSerializer).Assembly,
        };

        var references = AppDomain.CurrentDomain
            .GetAssemblies()
            .Union(seed)
            .Distinct()
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
            .Where(assembly => !_versionGatedAssemblies.Contains(assembly.GetName().Name, StringComparer.Ordinal))
            .Select(AssemblyReferenceCache.FromAssembly)
            .Concat(extraReferences)
            .ToArray();

        return CSharpCompilation.Create(
            "Len.StronglyTypedId.HotChocolate.Test",
            [CSharpSyntaxTree.ParseText(sourceCode)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    /// <summary>
    /// 测试宿主里可能已加载这些包的真实版本，必须排除在默认引用之外，只由用例按需注入。
    /// </summary>
    private static readonly string[] _versionGatedAssemblies =
    [
        "Len.StronglyTypedId.TestBase",
        "Microsoft.EntityFrameworkCore",
        "Swashbuckle.AspNetCore.SwaggerGen",
        "Microsoft.OpenApi",
        "Newtonsoft.Json",
        "Microsoft.AspNetCore.Mvc",
        "Microsoft.AspNetCore.Mvc.Core",
        "Microsoft.AspNetCore.Mvc.Abstractions",
        "Microsoft.AspNetCore.OpenApi",
        "Microsoft.AspNetCore.Routing",
        "MessagePack",
        "MongoDB.Bson",
        "HotChocolate",
        "HotChocolate.Types",
        "HotChocolate.Execution",
        "HotChocolate.Execution.Abstractions",
        "HotChocolate.Abstractions",
        "HotChocolate.AspNetCore",
    ];

    #endregion
}
