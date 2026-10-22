using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;
using FluentAssertions;
using Xunit;

namespace Len.StronglyTypedId;

/// <summary>
/// 对 <c>dotnet pack</c> 产物的条目清单做断言：使用者装包后能不能拿到生成器、包里有没有混进不该有的东西。
/// </summary>
[Collection(PackedPackagesCollection.Name)]
public class PackageContentsTests(PackedPackagesFixture fixture)
{
    private const string GeneratorsAssemblyEntry = "analyzers/dotnet/cs/Len.StronglyTypedId.Generators.dll";
    private const string GeneratorsPackageId = "Len.StronglyTypedId.Generators";
    private const string MainPackageId = "Len.StronglyTypedId";
    private const string PackageVersion = "1.0.0";
    private const string ReadmeEntry = "PACKAGE.md";
    private const string IconEntry = "icon.png";

    #region 生成器包：使用者能拿到生成器

    /// <summary>
    /// 生成器程序集必须落在 <c>analyzers/dotnet/cs/</c> 下——这是 Roslyn 加载分析器 / 源生成器的约定目录，放错目录（比如落到
    /// <c>lib/</c>）使用者就完全拿不到生成器。
    /// </summary>
    [Fact]
    public void GeneratorsPackage_Should_PlaceAssemblyUnderAnalyzersDirectory()
    {
        var entries = PackedPackagesFixture.ReadEntries(fixture.GeneratorsPackagePath);

        entries.Should().Contain(GeneratorsAssemblyEntry,
            "生成器程序集必须落在 analyzers/dotnet/cs/ 下才会被 Roslyn 加载");
    }

    /// <summary>
    /// 生成器包只该有分析器产物：<c>IncludeBuildOutput=false</c> 意味着它不该有任何 <c>lib/</c> 条目，也不该有调试符号——分析器包没有可被引用的运行期程序集。
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
    /// 生成器包自身不带依赖（<c>SuppressDependenciesWhenPacking=true</c>）：它的依赖是 Roslyn 提供的，不该把 Microsoft.CodeAnalysis.*
    /// 写进 nuspec 拖累使用者。
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

    /// <summary>
    /// 分析器目录里只允许出现生成器程序集本身。该目录是 Roslyn 的加载面，多放一个文件就等于多让一份
    /// 代码进入使用者的编译过程；<c>IndexRange</c> 是源码包（不产生运行期程序集），所以「只有一个 dll」
    /// 正是当前的预期形态。
    /// </summary>
    [Fact]
    public void GeneratorsPackage_Should_ShipOnlyWhitelistedAnalyzerFiles()
    {
        var entries = PackedPackagesFixture.ReadEntries(fixture.GeneratorsPackagePath);

        entries
            .Where(entry => entry.StartsWith("analyzers/dotnet/cs/", StringComparison.Ordinal))
            .Should().BeEquivalentTo([GeneratorsAssemblyEntry],
                "分析器目录里只应有生成器程序集；新增文件必须在此处显式登记");
    }

    /// <summary>
    /// 生成器程序集的引用必须全部来自 Roslyn / BCL。分析器目录里只有这一个 dll，任何第三方运行期依赖
    /// 都会在使用者**编译期**变成 <c>FileNotFoundException</c> —— <c>IndexRange</c> 之所以安全，是因为它是
    /// 源码包（只提供 polyfill 源码，不产生程序集引用）。
    /// </summary>
    [Fact]
    public void GeneratorsAssembly_Should_ReferenceOnlyRoslynAndBclAssemblies()
    {
        var image = PackedPackagesFixture.ReadEntryBytes(fixture.GeneratorsPackagePath, GeneratorsAssemblyEntry);

        using var peReader = new PEReader(ImmutableArray.Create(image));
        var metadata = peReader.GetMetadataReader();

        var references = metadata.AssemblyReferences
            .Select(handle => metadata.GetString(metadata.GetAssemblyReference(handle).Name))
            .ToArray();

        // OnlyContain 收的是表达式树，里面不能用 is 模式（CS8122），只能逐个比较。
        references.Should().OnlyContain(name =>
            name == "netstandard" || name == "mscorlib"
            || name.StartsWith("System.", StringComparison.Ordinal)
            || name.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal),
            "分析器程序集不得依赖 Roslyn / BCL 之外的运行期程序集");
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
    /// 主包必须依赖生成器包——这是「使用者装主包就能拿到生成器」的唯一途径。
    /// </summary>
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
    /// 两个包都要把图标打进包并写进 nuspec。<c>PackageIcon</c> 声明的是「包内路径」，与真正被打包的文件
    /// 是两条独立的配置 —— 只改一处时 pack 会报 NU5046，但若把文件放错目录、nuspec 仍指向它，
    /// 就只有使用者看到默认灰块。这里同时校验条目、nuspec 指针与 PNG 魔数。
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Package_Should_IncludeIcon(bool generators)
    {
        var packagePath = generators ? fixture.GeneratorsPackagePath : fixture.MainPackagePath;
        var entries = PackedPackagesFixture.ReadEntries(packagePath);
        var nuspec = PackedPackagesFixture.ReadNuspec(packagePath);

        entries.Should().Contain(IconEntry);

        var icon = Metadata(nuspec).Elements()
            .SingleOrDefault(element => element.Name.LocalName == "icon")?.Value;

        icon.Should().Be(IconEntry, "nuspec 的 icon 应指向包内的图标文件");

        var bytes = PackedPackagesFixture.ReadEntryBytes(packagePath, IconEntry);

        // NuGet 只接受 PNG / JPEG；这里钉 PNG，顺带挡住「占位文本文件被当成图标」的情况。
        // 必须显式写成 byte[]：Equal 的 params 重载会把后面的 reason 当成第 9 个元素（CS1503）。
        bytes.Take(8).Should().Equal(
            new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A },
            "图标必须是 PNG（NuGet 不接受其它格式）");
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
