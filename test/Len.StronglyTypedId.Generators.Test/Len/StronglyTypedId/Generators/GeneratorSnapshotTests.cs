// xUnit1051：本文件中的 CSharpSyntaxTree.ParseText / 同步等待均为测试内调用，取消令牌无意义，
// 统一在此文件禁用该测试框架分析器警告。
#pragma warning disable xUnit1051
using System.Collections.Immutable;
using System.Text;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;

namespace Len.StronglyTypedId.Generators;

/// <summary>
/// 生成产物的文本快照：把每个场景的完整产物与仓库里的基线逐字符比对。
/// </summary>
/// <remarks>
/// <para>
/// 与既有用例的分工：其余生成器用例断言「产物里含某段文本」，本类断言「产物逐字节等于基线」。
/// 前者防漏分支，后者防「没人想到要断言的那一行」被悄悄改掉 —— 例如缩进、成员顺序、空行。
/// </para>
/// <para>
/// 两条硬约束：
/// ① 引用集固定为 net8.0 引用程序集，故 <c>Guid</c> 不带 <c>IUtf8SpanParsable</c>，产物与宿主 TFM 无关，
///    一份基线在 net8.0 / net10.0 两条腿上同时成立；
/// ② 集成门控用 <see cref="SyntheticAssembly"/> 满足，不引用真实包 —— 门控只看程序集名与主版本，
///    产物是纯文本，不需要真实 API 面。这样快照不会随第三方包升级而漂移。
/// </para>
/// <para>
/// 更新基线：设 <c>STRONGLY_TYPED_ID_UPDATE_SNAPSHOTS=1</c> 重跑测试。
/// </para>
/// </remarks>
public class GeneratorSnapshotTests
{
    /// <summary>
    /// 基线目录（相对仓库根）。
    /// </summary>
    private const string SnapshotDirectory = "test/Len.StronglyTypedId.Generators.Test/Snapshots";

    #region 场景

    /// <summary>
    /// 值类型 Id：核心产物 + System.Text.Json 产物与注册入口。
    /// </summary>
    [Fact]
    public void Snapshot_Should_MatchBaseline_ForRecordStructGuidId()
        => VerifyScenario("record-struct-guid", IdSource, []);

    /// <summary>
    /// 引用类型 Id：比较运算符走 null 安全的 <c>CompareValues</c>，与值类型产物不同。
    /// </summary>
    [Fact]
    public void Snapshot_Should_MatchBaseline_ForReferenceTypeId()
        => VerifyScenario("record-guid", ReferenceTypeIdSource, []);

    /// <summary>
    /// 带前缀的 string Id：文本形贯穿 <c>ToString</c> / <c>Parse</c> / JSON。
    /// </summary>
    [Fact]
    public void Snapshot_Should_MatchBaseline_ForPrefixedStringId()
        => VerifyScenario("string-prefix", PrefixedStringIdSource, []);

    /// <summary>
    /// <c>[Flags]</c> 枚举 Id：未定义位掩码在编译期算出，产物里是定长十六进制字面量。
    /// </summary>
    [Fact]
    public void Snapshot_Should_MatchBaseline_ForFlagsEnumId()
        => VerifyScenario("flags-enum", FlagsEnumIdSource, []);

    /// <summary>
    /// 全部集成同时启用：每个集成生成器各产出一份文件，外加注册入口。
    /// </summary>
    [Fact]
    public void Snapshot_Should_MatchBaseline_ForAllIntegrations()
        => VerifyScenario("all-integrations", IdSource, _allIntegrationGates);

    #endregion

    #region 断言

    private static void VerifyScenario(string scenario, string source, MetadataReference[] integrationGates)
    {
        var generated = RunGenerator(source, integrationGates);

        generated.Should().NotBeEmpty($"场景 {scenario} 必须产出至少一份文件");

        var directory = System.IO.Path.Combine(RepositoryRoot.Path, SnapshotDirectory, scenario);

        // 文件清单单独成基线：新增或删除产物文件时，即使每个文件内容都对，这里也会失败。
        var index = new StringBuilder();

        foreach (var hintName in generated.Keys.OrderBy(static name => name, StringComparer.Ordinal))
        {
            index.Append(hintName).Append('\n');
        }

        SnapshotVerifier.Verify(System.IO.Path.Combine(directory, "_files.txt"), index.ToString());

        foreach (var (hintName, text) in generated.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            var fileName = hintName.Replace(".g.cs", ".txt", StringComparison.Ordinal);

            SnapshotVerifier.Verify(
                System.IO.Path.Combine(directory, fileName),
                SnapshotVerifier.NormalizeGeneratorVersion(text));
        }
    }

    #endregion

    #region 生成器驱动

    /// <summary>
    /// 固定为 net8.0 的引用程序集：产物因此与宿主 TFM 无关，一份基线两条腿通用。
    /// </summary>
    private static readonly Lazy<ImmutableArray<MetadataReference>> _net8References = new(
        static () => new ReferenceAssemblies(
                "net8.0",
                new PackageIdentity("Microsoft.NETCore.App.Ref", "8.0.0"),
                System.IO.Path.Combine("ref", "net8.0"))
            .ResolveAsync(LanguageNames.CSharp, CancellationToken.None)
            .GetAwaiter()
            .GetResult(),
        LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// 运行时程序集的 net8.0 构建产物：引用集固定在 net8.0，这里也必须取同一份，否则接口符号对不上。
    /// </summary>
    private static readonly Lazy<MetadataReference> _runtimeReference = new(
        static () =>
        {
            var location = typeof(StronglyTypedIdAttribute).Assembly.Location;
            var net8Location = location.Replace(
                $"{System.IO.Path.DirectorySeparatorChar}net10.0{System.IO.Path.DirectorySeparatorChar}",
                $"{System.IO.Path.DirectorySeparatorChar}net8.0{System.IO.Path.DirectorySeparatorChar}");

            return AssemblyReferenceCache.FromFile(File.Exists(net8Location) ? net8Location : location);
        },
        LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// 全部集成门控的合成程序集：只需程序集名与主版本，产物是纯文本。
    /// </summary>
    private static readonly MetadataReference[] _allIntegrationGates =
    [
        SyntheticAssembly.GetOrCreate("Newtonsoft.Json", "13.0.0.0"),
        SyntheticAssembly.GetOrCreate("Microsoft.EntityFrameworkCore", "8.0.0.0"),
        SyntheticAssembly.GetOrCreate("Swashbuckle.AspNetCore.SwaggerGen", "6.0.0.0"),
        SyntheticAssembly.GetOrCreate("Dapper", "2.0.0.0"),
        SyntheticAssembly.GetOrCreate("MessagePack", "3.0.0.0"),
        SyntheticAssembly.GetOrCreate("MongoDB.Bson", "3.0.0.0"),
        SyntheticAssembly.GetOrCreate("HotChocolate.Types", "13.0.0.0"),
        SyntheticAssembly.GetOrCreate("protobuf-net", "3.0.0.0"),
        SyntheticAssembly.GetOrCreate("Microsoft.AspNetCore.OpenApi", "9.0.0.0"),
        SyntheticAssembly.GetOrCreate("Microsoft.AspNetCore.Mvc", "8.0.0.0"),
    ];

    private static Dictionary<string, string> RunGenerator(string source, MetadataReference[] integrationGates)
    {
        var references = _net8References.Value
            .Add(_runtimeReference.Value)
            .AddRange(integrationGates);

        var compilation = CSharpCompilation.Create(
            "SnapshotProbe",
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        var result = CSharpGeneratorDriver
            .Create(new StronglyTypedIdGenerator())
            .RunGenerators(compilation)
            .GetRunResult();

        result.Diagnostics
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Should()
            .BeEmpty();

        return result.Results[0].GeneratedSources
            .ToDictionary(
                static generated => generated.HintName,
                static generated => generated.SourceText.ToString(),
                StringComparer.Ordinal);
    }

    #endregion

    #region 场景源码

    private const string IdSource = """
        using System;
        using Len.StronglyTypedId;

        namespace Probe;

        [StronglyTypedId]
        public partial record struct OrderId(Guid Value);
        """;

    private const string ReferenceTypeIdSource = """
        using System;
        using Len.StronglyTypedId;

        namespace Probe;

        [StronglyTypedId]
        public partial record OrderId(Guid Value);
        """;

    private const string PrefixedStringIdSource = """
        using System;
        using Len.StronglyTypedId;

        namespace Probe;

        [StronglyTypedId(Prefix = "ord_")]
        public partial record struct OrderId(string Value);
        """;

    private const string FlagsEnumIdSource = """
        using System;
        using Len.StronglyTypedId;

        namespace Probe;

        [Flags]
        public enum Access
        {
            None = 0,
            Read = 1,
            Write = 2,
        }

        [StronglyTypedId]
        public partial record struct AccessId(Access Value);
        """;

    #endregion
}
