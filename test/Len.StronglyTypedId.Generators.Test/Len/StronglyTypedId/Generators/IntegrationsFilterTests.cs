// xUnit1051：本文件中的 CSharpSyntaxTree.ParseText 均为测试内同步调用，取消令牌无意义，
// 统一在此文件禁用该测试框架分析器警告。
#pragma warning disable xUnit1051
using System.Globalization;
using System.Reflection;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Len.StronglyTypedId.Generators;

/// <summary>
/// <c>Integrations</c> 过滤器的单元测试。它是<b>过滤器</b>而非开关：只能在「已引用对应程序集」之上
/// 进一步收窄，永远不能凭空造出依赖。
/// </summary>
public class IntegrationsFilterTests
{
    #region 未指定：行为与引入该参数之前一致

    /// <summary>
    /// 不写 <c>Integrations</c> 时按引用生成，是本次改动的回归护栏：一旦默认值被写成
    /// <c>Default</c>（0）而非 <c>All</c>，所有集成都会静默消失。
    /// </summary>
    [Fact]
    public void Integrations_Should_GenerateEveryReferencedIntegration_WhenNotSpecified()
    {
        var generated = RunGenerator(IdSource, AllIntegrationReferences);

        generated.Should().ContainKeys(
            "Len.StronglyTypedId.OrderId.g.cs",
            "Len.StronglyTypedId.OrderId.SystemTextJson.g.cs",
            "Len.StronglyTypedId.OrderId.NewtonsoftJson.g.cs",
            "Len.StronglyTypedId.OrderId.Dapper.g.cs",
            "StronglyTypedIds.SystemTextJson.g.cs",
            "StronglyTypedIds.NewtonsoftJson.g.cs",
            "StronglyTypedIds.Dapper.g.cs",
            "StronglyTypedIds.Swagger.g.cs");
    }

    #endregion

    #region 显式关闭

    /// <summary>
    /// <c>None</c> 关掉全部集成代码，但核心实现照旧产出。
    /// </summary>
    [Fact]
    public void Integrations_Should_SuppressEveryIntegration_WhenNone()
    {
        var generated = RunGenerator(NoneSource, AllIntegrationReferences);

        generated.Keys.Should().BeEquivalentTo(["Len.StronglyTypedId.OrderId.g.cs"]);
    }

    /// <summary>
    /// <c>TypeConverter</c> 是显式的「我要」，不属于「引用了就有」的集成范畴，故 <c>None</c> 不得关掉它。
    /// 这条容易被人当成 bug 报，用例把语义钉住。
    /// </summary>
    [Fact]
    public void Integrations_Should_KeepTypeConverter_WhenNone()
    {
        var generated = RunGenerator(NoneWithTypeConverterSource, AllIntegrationReferences);

        generated["Len.StronglyTypedId.OrderId.g.cs"]
            .Should().Contain("TypeConverter");
    }

    #endregion

    #region 白名单

    /// <summary>
    /// 只列 <c>Dapper | SystemTextJson</c> 时，即使同时引用了 Swashbuckle 与 Newtonsoft.Json，
    /// 也只产出这两类集成代码。
    /// </summary>
    [Fact]
    public void Integrations_Should_LimitToSelectedOnes_WhenWhitelisted()
    {
        var generated = RunGenerator(WhitelistSource, AllIntegrationReferences);

        generated.Should().ContainKeys(
            "Len.StronglyTypedId.OrderId.Dapper.g.cs",
            "Len.StronglyTypedId.OrderId.SystemTextJson.g.cs",
            "StronglyTypedIds.Dapper.g.cs",
            "StronglyTypedIds.SystemTextJson.g.cs");

        generated.Keys.Should().NotContain(key => key.Contains("NewtonsoftJson", StringComparison.Ordinal)
            || key.Contains("Swagger", StringComparison.Ordinal));
    }

    #endregion

    #region 程序集级默认值与逐类型覆盖

    /// <summary>
    /// 程序集默认 <c>None</c> 时，单个 Id 用 <c>All</c> 可以把集成捞回来。
    /// </summary>
    [Fact]
    public void Integrations_Should_RestoreAll_WhenTypeOverridesAssemblyDefaultNone()
    {
        var generated = RunGenerator(AssemblyNoneTypeAllSource, AllIntegrationReferences);

        generated.Should().ContainKey("Len.StronglyTypedId.OrderId.Dapper.g.cs");
        generated.Should().ContainKey("StronglyTypedIds.Dapper.g.cs");
    }

    /// <summary>
    /// 逐类型优先于程序集默认：默认全开、单 Id 写 <c>None</c> 时该 Id 不产出任何集成代码。
    /// </summary>
    [Fact]
    public void Integrations_Should_PreferTypeLevel_WhenAssemblyDefaultIsAll()
    {
        var generated = RunGenerator(AssemblyAllTypeNoneSource, AllIntegrationReferences);

        generated.Keys.Should().BeEquivalentTo(["Len.StronglyTypedId.OrderId.g.cs"]);
    }

    #endregion

    #region 外部程序集里的 Id

    /// <summary>
    /// 对照组：声明程序集没有限制时，消费者会为外部 Id 生成集成代码。若本用例失败，
    /// 下面那条「尊重外部程序集默认值」的用例就失去了判别力。
    /// </summary>
    [Fact]
    public void Integrations_Should_IncludeExternalId_WhenItsAssemblyDoesNotRestrict()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Dapper", "2.0.0.0"),
            CreateReferencedAssembly("AlreadyGenerated", ReferencedIdSource));

        generated["StronglyTypedIds.Dapper.g.cs"].Should().Contain("LegacyOrderId");
    }

    /// <summary>
    /// 库作者在自己的程序集上写 <c>[assembly: StronglyTypedIdDefaults(Integrations = None)]</c> 后，
    /// 消费者不得再为该程序集里的 Id 生成集成代码，否则「我不想发布集成」这个意图跨程序集就失效了。
    /// </summary>
    [Fact]
    public void Integrations_Should_SkipExternalId_WhenItsAssemblyDefaultsToNone()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Dapper", "2.0.0.0"),
            CreateReferencedAssembly("AlreadyGenerated", ReferencedIdWithIntegrationsNoneSource));

        generated["StronglyTypedIds.Dapper.g.cs"]
            .Should().Contain("OrderId")
            .And.NotContain("LegacyOrderId");
    }

    #endregion

    #region 生成器侧镜像值

    /// <summary>
    /// 生成器不引用运行时程序集（分析器包必须自包含），只能按底层整数匹配。两侧取值一旦漂移，
    /// 用户写的 <c>Integrations = Dapper</c> 就会静默失效，故逐成员比对。
    /// </summary>
    [Fact]
    public void IntegrationFlags_Should_MirrorRuntimeEnum()
    {
        var runtimeValues = Enum.GetValues<StronglyTypedIdIntegrations>()
            .ToDictionary(value => value.ToString(), value => Convert.ToInt32(value, CultureInfo.InvariantCulture));

        var mirroredValues = typeof(IntegrationFlags)
            .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(int))
            .ToDictionary(field => field.Name, field => (int)field.GetRawConstantValue()!);

        mirroredValues.Should().BeEquivalentTo(runtimeValues);
    }

    #endregion

    #region 辅助

    private const string IdSource = """
        using System;

        namespace Len.StronglyTypedId;

        [StronglyTypedId]
        public partial record struct OrderId(Guid Value);
        """;

    private const string NoneSource = """
        using System;

        namespace Len.StronglyTypedId;

        [StronglyTypedId(Integrations = StronglyTypedIdIntegrations.None)]
        public partial record struct OrderId(Guid Value);
        """;

    private const string NoneWithTypeConverterSource = """
        using System;

        namespace Len.StronglyTypedId;

        [StronglyTypedId(Integrations = StronglyTypedIdIntegrations.None, TypeConverter = true)]
        public partial record struct OrderId(Guid Value);
        """;

    private const string WhitelistSource = """
        using System;

        namespace Len.StronglyTypedId;

        [StronglyTypedId(Integrations = StronglyTypedIdIntegrations.Dapper | StronglyTypedIdIntegrations.SystemTextJson)]
        public partial record struct OrderId(Guid Value);
        """;

    private const string AssemblyNoneTypeAllSource = """
        using System;

        [assembly: Len.StronglyTypedId.StronglyTypedIdDefaults(
            Integrations = Len.StronglyTypedId.StronglyTypedIdIntegrations.None)]

        namespace Len.StronglyTypedId;

        [StronglyTypedId(Integrations = StronglyTypedIdIntegrations.All)]
        public partial record struct OrderId(Guid Value);
        """;

    private const string AssemblyAllTypeNoneSource = """
        using System;

        [assembly: Len.StronglyTypedId.StronglyTypedIdDefaults(
            Integrations = Len.StronglyTypedId.StronglyTypedIdIntegrations.All)]

        namespace Len.StronglyTypedId;

        [StronglyTypedId(Integrations = StronglyTypedIdIntegrations.None)]
        public partial record struct OrderId(Guid Value);
        """;

    /// <summary>
    /// 一个「已由生成器补齐接口实现」的外部强类型 Id，等价于领域库里已发布的产物。
    /// </summary>
    private const string ReferencedIdSource = """
        namespace Already.Generated;

        public readonly record struct LegacyOrderId(global::System.Guid Value)
            : global::Len.StronglyTypedId.IStronglyTypedId<LegacyOrderId, global::System.Guid>
        {
            public static LegacyOrderId Create(global::System.Guid value) => new(value);

            public static LegacyOrderId Parse(string value, global::System.IFormatProvider? provider)
                => new(global::System.Guid.Parse(value, provider));

            public static bool TryParse(
                string? value,
                global::System.IFormatProvider? provider,
                out LegacyOrderId result)
            {
                if (global::System.Guid.TryParse(value, provider, out var primitiveId))
                {
                    result = new LegacyOrderId(primitiveId);
                    return true;
                }

                result = default;
                return false;
            }
        }
        """;

    /// <summary>
    /// 同上，但声明它的程序集显式关闭了全部集成。
    /// </summary>
    private const string ReferencedIdWithIntegrationsNoneSource = """
        [assembly: Len.StronglyTypedId.StronglyTypedIdDefaults(
            Integrations = Len.StronglyTypedId.StronglyTypedIdIntegrations.None)]

        namespace Already.Generated;

        public readonly record struct LegacyOrderId(global::System.Guid Value)
            : global::Len.StronglyTypedId.IStronglyTypedId<LegacyOrderId, global::System.Guid>
        {
            public static LegacyOrderId Create(global::System.Guid value) => new(value);

            public static LegacyOrderId Parse(string value, global::System.IFormatProvider? provider)
                => new(global::System.Guid.Parse(value, provider));

            public static bool TryParse(
                string? value,
                global::System.IFormatProvider? provider,
                out LegacyOrderId result)
            {
                if (global::System.Guid.TryParse(value, provider, out var primitiveId))
                {
                    result = new LegacyOrderId(primitiveId);
                    return true;
                }

                result = default;
                return false;
            }
        }
        """;

    /// <summary>
    /// 四类集成的合成引用，版本均满足各自的模块闸门。
    /// </summary>
    private static MetadataReference[] AllIntegrationReferences =>
    [
        SyntheticAssembly.GetOrCreate("Dapper", "2.0.0.0"),
        SyntheticAssembly.GetOrCreate("Swashbuckle.AspNetCore.SwaggerGen", "6.0.0.0"),
        SyntheticAssembly.GetOrCreate("Newtonsoft.Json", "13.0.0.0"),
    ];

    /// <summary>
    /// 测试宿主 AppDomain 里可能已加载这些包的真实版本。必须排除在默认引用之外，只由用例按需注入，
    /// 否则同名程序集出现两份引用，闸门判定的将是真实版本而非用例指定的版本。
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
    ];

    private static Dictionary<string, string> RunGenerator(string sourceCode, params MetadataReference[] extraReferences)
    {
        var compilation = CreateCompilation("Len.StronglyTypedId.Integrations.Test", sourceCode, extraReferences);

        var result = CSharpGeneratorDriver
            .Create(new StronglyTypedIdGenerator())
            .RunGenerators(compilation)
            .GetRunResult();

        result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        return result.Results[0].GeneratedSources
            .ToDictionary(source => source.HintName, source => source.SourceText.ToString());
    }

    private static CompilationReference CreateReferencedAssembly(string assemblyName, string sourceCode)
        => (CompilationReference)CreateCompilation(assemblyName, sourceCode, []).ToMetadataReference();

    private static CSharpCompilation CreateCompilation(
        string assemblyName,
        string sourceCode,
        MetadataReference[] extraReferences)
    {
        // System.Text.Json 是延迟加载的：必须显式引用一次，它才会出现在编译引用里，
        // SystemTextJsonCodeGenerator 也才会被选入。
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
            assemblyName,
            [CSharpSyntaxTree.ParseText(sourceCode)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    #endregion
}
