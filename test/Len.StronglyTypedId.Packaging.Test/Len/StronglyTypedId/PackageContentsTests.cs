using System.Xml.Linq;
using FluentAssertions;
using Xunit;

namespace Len.StronglyTypedId;

/// <summary>
/// 对 <c>dotnet pack</c> 产物的条目清单做断言：使用者装包后能不能拿到生成器、包里有没有混进不该有的东西。
/// </summary>
/// <remarks>
/// <para>
/// 这些断言守的是「使用者能拿到生成器」这条生命线。主包对 <c>Len.StronglyTypedId.Generators</c> 的依赖不是
/// 由 <c>ProjectReference</c> 自然产生的 —— 那条引用必须标 <c>ReferenceOutputAssembly=false</c> 才不会把
/// 生成器带的 Roslyn 程序集拖进编译引用，而 NuGet 构建 restore 图时只收 <c>ReferenceOutputAssembly</c>
/// 为空或 true 的项目引用，于是它永远进不了 assets，也就永远不会自动成为包依赖。因此只能由打包后的
/// <c>InjectGeneratorsPackageDependency</c> 目标修补 nuspec 注入。
/// </para>
/// <para>
/// 那个目标内部已经对「产物包不存在 / nuspec 缺 <c>dependencies</c> 节点 / 写入后读回为空」逐条断言并中止构建，
/// 所以正常情况下不会静默失效。下面的断言是第二道哨兵：万一修补逻辑本身被改坏（比如条件写反导致整段跳过），
/// 仍然能在 CI 里被抓住。
/// </para>
/// <para>
/// 断言全部落在「产物包」上，而不是「打包命令有没有成功」：命令失败时夹具会直接抛出，不会伪装成断言失败。
/// </para>
/// </remarks>
[Collection(PackedPackagesCollection.Name)]
public class PackageContentsTests(PackedPackagesFixture fixture)
{
    private const string GeneratorsAssemblyEntry = "analyzers/dotnet/cs/Len.StronglyTypedId.Generators.dll";
    private const string GeneratorsPackageId = "Len.StronglyTypedId.Generators";
    private const string MainPackageId = "Len.StronglyTypedId";
    private const string PackageVersion = "1.0.0";
    private const string ReadmeEntry = "PACKAGE.md";

    #region 生成器包：使用者能拿到生成器

    /// <summary>
    /// 生成器程序集必须落在 <c>analyzers/dotnet/cs/</c> 下 —— 这是 Roslyn 加载分析器 / 源生成器的约定目录，
    /// 放错目录（比如落到 <c>lib/</c>）使用者就完全拿不到生成器。
    /// </summary>
    [Fact]
    public void GeneratorsPackage_Should_PlaceAssemblyUnderAnalyzersDirectory()
    {
        var entries = PackedPackagesFixture.ReadEntries(fixture.GeneratorsPackagePath);

        entries.Should().Contain(GeneratorsAssemblyEntry,
            "生成器程序集必须落在 analyzers/dotnet/cs/ 下才会被 Roslyn 加载");
    }

    /// <summary>
    /// 生成器包只该有分析器产物：<c>IncludeBuildOutput=false</c> 意味着它不该有任何 <c>lib/</c> 条目，
    /// 也不该有调试符号 —— 分析器包没有可被引用的运行期程序集。
    /// </summary>
    [Fact]
    public void GeneratorsPackage_Should_Not_ContainLibraryOrSymbols()
    {
        var entries = PackedPackagesFixture.ReadEntries(fixture.GeneratorsPackagePath);

        entries.Should().NotContain(entry => entry.StartsWith("lib/", StringComparison.Ordinal),
            "分析器包不应有 lib/ 条目（IncludeBuildOutput=false）");
        entries.Should().NotContain(entry => entry.EndsWith(".pdb", StringComparison.Ordinal),
            "分析器包不应带调试符号");
    }

    /// <summary>
    /// 分析器发布跟踪文件必须随包发布，否则 Roslyn 会在消费者侧报 RS2008 / RS2007。
    /// </summary>
    [Fact]
    public void GeneratorsPackage_Should_IncludeAnalyzerReleaseTrackingFiles()
    {
        var entries = PackedPackagesFixture.ReadEntries(fixture.GeneratorsPackagePath);

        entries.Should().Contain("AnalyzerReleases.Shipped.md");
        entries.Should().Contain("AnalyzerReleases.Unshipped.md");
    }

    /// <summary>
    /// 生成器包自身不带依赖（<c>SuppressDependenciesWhenPacking=true</c>）：它的依赖是 Roslyn 提供的，
    /// 不该把 Microsoft.CodeAnalysis.* 写进 nuspec 拖累使用者。
    /// </summary>
    [Fact]
    public void GeneratorsPackage_Should_HaveNoDependencies()
    {
        var nuspec = PackedPackagesFixture.ReadNuspec(fixture.GeneratorsPackagePath);

        var dependencies = Metadata(nuspec).Elements()
            .Where(element => element.Name.LocalName == "dependencies")
            .SelectMany(element => element.Descendants())
            .Where(element => element.Name.LocalName == "dependency")
            .ToArray();

        dependencies.Should().BeEmpty("分析器包不应把 Roslyn 依赖写进 nuspec");
    }

    #endregion

    #region 主包：运行期程序集与依赖

    /// <summary>
    /// 两个目标框架的运行期程序集与 XML 文档都必须进包。
    /// </summary>
    [Fact]
    public void MainPackage_Should_IncludeBothTargetFrameworks()
    {
        var entries = PackedPackagesFixture.ReadEntries(fixture.MainPackagePath);

        entries.Should().Contain("lib/net8.0/Len.StronglyTypedId.dll");
        entries.Should().Contain("lib/net10.0/Len.StronglyTypedId.dll");
        entries.Should().Contain("lib/net8.0/Len.StronglyTypedId.xml");
        entries.Should().Contain("lib/net10.0/Len.StronglyTypedId.xml");
    }

    /// <summary>
    /// 主包里不该有调试符号：符号单独走 <c>.snupkg</c>，否则每个使用者的还原目录里都会多躺一份用不上的 pdb。
    /// </summary>
    [Fact]
    public void MainPackage_Should_Not_ContainSymbols()
    {
        var entries = PackedPackagesFixture.ReadEntries(fixture.MainPackagePath);

        entries.Should().NotContain(entry => entry.EndsWith(".pdb", StringComparison.Ordinal),
            "调试符号应单独成 .snupkg，主包 lib/ 下不应有 pdb");
    }

    /// <summary>
    /// 主包里不该有分析器产物：生成器单独成包，主包只留使用者运行期真正需要的东西。
    /// </summary>
    [Fact]
    public void MainPackage_Should_Not_ContainAnalyzers()
    {
        var entries = PackedPackagesFixture.ReadEntries(fixture.MainPackagePath);

        entries.Should().NotContain(entry => entry.StartsWith("analyzers/", StringComparison.Ordinal),
            "生成器应单独成包，主包不应内嵌分析器");
    }

    /// <summary>
    /// 主包必须依赖生成器包 —— 这是「使用者装主包就能拿到生成器」的唯一途径。
    /// </summary>
    /// <remarks>
    /// 该依赖由打包后的 <c>InjectGeneratorsPackageDependency</c> 注入，不是 <c>ProjectReference</c> 的自然产物。
    /// csproj 里的那个目标已经对注入前提逐条断言并会在不满足时中止构建，这里再逐目标框架断言依赖存在、
    /// 且版本与包版本一致 —— 两道防线互为补充：构建期断言防「静默跳过」，这里防「注入到了错误的形态」。
    /// </remarks>
    [Fact]
    public void MainPackage_Should_DependOnGeneratorsPackage_ForEveryTargetFramework()
    {
        var nuspec = PackedPackagesFixture.ReadNuspec(fixture.MainPackagePath);

        var groups = Metadata(nuspec).Elements()
            .Where(element => element.Name.LocalName == "dependencies")
            .SelectMany(element => element.Elements())
            .Where(element => element.Name.LocalName == "group")
            .ToArray();

        groups.Select(group => (string?)group.Attribute("targetFramework"))
            .Should().BeEquivalentTo(["net8.0", "net10.0"],
                "主包应对每个目标框架各有一组依赖");

        foreach (var group in groups)
        {
            var targetFramework = (string?)group.Attribute("targetFramework");
            var dependencies = group.Elements()
                .Where(element => element.Name.LocalName == "dependency")
                .ToArray();

            dependencies.Should().ContainSingle(
                $"目标框架 {targetFramework} 下应恰好有一个依赖（生成器包）");
            dependencies[0].Attribute("id")?.Value.Should().Be(GeneratorsPackageId);
            dependencies[0].Attribute("version")?.Value.Should().Be(PackageVersion,
                "生成器依赖的版本应与主包版本一致（同源同版本）");
        }
    }

    #endregion

    #region 符号包与包元数据

    /// <summary>
    /// 符号包必须带上两个目标框架的 pdb。
    /// </summary>
    [Fact]
    public void SymbolsPackage_Should_ContainPdbForBothTargetFrameworks()
    {
        var entries = PackedPackagesFixture.ReadEntries(fixture.SymbolsPackagePath);

        entries.Should().Contain("lib/net8.0/Len.StronglyTypedId.pdb");
        entries.Should().Contain("lib/net10.0/Len.StronglyTypedId.pdb");
        entries.Should().NotContain(entry => entry.EndsWith(".dll", StringComparison.Ordinal),
            "符号包只放调试符号，不放程序集");
    }

    /// <summary>
    /// 两个包都要把 README 打进包并写进 nuspec，否则 NuGet 页面上看不到说明。
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Package_Should_IncludeReadme(bool generators)
    {
        var packagePath = generators ? fixture.GeneratorsPackagePath : fixture.MainPackagePath;
        var entries = PackedPackagesFixture.ReadEntries(packagePath);
        var nuspec = PackedPackagesFixture.ReadNuspec(packagePath);

        entries.Should().Contain(ReadmeEntry);

        var readme = Metadata(nuspec).Elements()
            .SingleOrDefault(element => element.Name.LocalName == "readme")?.Value;

        readme.Should().Be(ReadmeEntry, "nuspec 的 readme 应指向包内的 README 文件");
    }

    /// <summary>
    /// 两个包的 id 必须与项目名一致，避免包名与程序集名漂移。
    /// </summary>
    [Theory]
    [InlineData(true, GeneratorsPackageId)]
    [InlineData(false, MainPackageId)]
    public void Package_Should_CarryExpectedId(bool generators, string expectedId)
    {
        var packagePath = generators ? fixture.GeneratorsPackagePath : fixture.MainPackagePath;
        var nuspec = PackedPackagesFixture.ReadNuspec(packagePath);

        var id = Metadata(nuspec).Elements()
            .SingleOrDefault(element => element.Name.LocalName == "id")?.Value;

        id.Should().Be(expectedId);
    }

    #endregion

    private static XElement Metadata(XDocument nuspec)
        => nuspec.Root!.Elements().Single(element => element.Name.LocalName == "metadata");
}
