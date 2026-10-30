using System.Text;
using FluentAssertions;
using Xunit;

namespace Len.StronglyTypedId;

/// <summary>
/// 「分析器一键静音」的端到端回归：把真实打好的包装进一个临时消费工程，再跑真实的 <c>dotnet build</c>。
/// </summary>
/// <remarks>
/// <para>
/// 为什么单测与包内容断言都不够：<c>StronglyTypedIdAnalyzerMutingTests</c> 手工构造
/// <c>AnalyzerConfigOptionsProvider</c>，证明的是「分析器读得到这个键就静音」；<c>PackageContentsTests</c>
/// 证明的是「props 在包里、内容放行了属性名」。两段各自为真，合起来仍可能整条链路不通 —— 而这正是
/// E13 记录过的失败形态：props 文件名与包 ID 不同名时 NuGet 根本不导入它，包「带着」文件却静默失效。
/// 只有真的装一次包、真的构建一次，才能证明 MSBuild → <c>build_property.*</c> → 分析器这条通道是通的。
/// </para>
/// <para>
/// 用例用 <c>-warnaserror</c> 把 STIA0011（Warning 级）升成错误：静音时构建必须绿，不静音时必须红。
/// 选 Warning 级而不是 Info 级，是因为 <c>dotnet build</c> 默认不打印 Info 级诊断，用它验证会得到
/// 「两种情况都没诊断」的假阴性（<c>dotnet build</c> 默认不打印 Info 级诊断）。
/// </para>
/// </remarks>
[Collection(PackedPackagesCollection.Name)]
public class AnalyzerMutingEndToEndTests(PackedPackagesFixture fixture)
{
    /// <summary>
    /// 消费工程里那条故意违反 STIA0011 的代码：Id 配了 Validator，却直接 <c>new</c> 绕过它。
    /// </summary>
    private const string IdSource = """
        using Len.StronglyTypedId;

        namespace Consumer;

        [StronglyTypedId(Validator = nameof(Validate))]
        public partial record struct OrderId(Guid Value)
        {
            private static bool Validate(Guid value) => value != Guid.Empty;
        }

        public static class Use
        {
            // STIA0011：直接构造绕过了 Create 执行的校验。
            public static OrderId Bypass() => new OrderId(Guid.NewGuid());
        }
        """;

    /// <summary>
    /// 属性为 <c>false</c> 时，真实消费工程的构建必须成功——分析器一个诊断动作都不注册。
    /// </summary>
    [Theory]
    [InlineData("false")]
    [InlineData("False")]
    [InlineData("0")]
    [InlineData("no")]
    [InlineData("off")]
    public async Task ConsumerBuild_Should_Succeed_WhenMutingPropertyIsFalsy(string value)
    {
        using var project = await ConsumerProject.CreateAsync(fixture, value);

        var result = await project.BuildAsync(warningsAsErrors: true);

        result.ExitCode.Should().Be(0,
            $"属性取 {value} 时分析器应完全静音，STIA0011 不该被报出。输出：{Environment.NewLine}{result.Output}");
        result.Output.Should().NotContain("STIA0011");
    }

    /// <summary>
    /// 属性缺失时行为不变：STIA0011 照常报出，<c>-warnaserror</c> 下构建失败。
    /// </summary>
    /// <remarks>
    /// 这条是对照组。少了它，上面那条「构建成功」可能只是因为诊断压根没被触发（比如分析器没加载），
    /// 而不是因为静音生效。
    /// </remarks>
    [Fact]
    public async Task ConsumerBuild_Should_ReportDiagnostic_WhenMutingPropertyIsAbsent()
    {
        using var project = await ConsumerProject.CreateAsync(fixture, propertyValue: null);

        var result = await project.BuildAsync(warningsAsErrors: true);

        result.ExitCode.Should().NotBe(0, "属性缺失时分析器照常工作，STIA0011 在 -warnaserror 下应让构建失败");
        result.Output.Should().Contain("STIA0011");
    }

    /// <summary>
    /// 属性为 <c>true</c> 时同样照常报诊断——只有「否」才静音。
    /// </summary>
    [Fact]
    public async Task ConsumerBuild_Should_ReportDiagnostic_WhenMutingPropertyIsTrue()
    {
        using var project = await ConsumerProject.CreateAsync(fixture, "true");

        var result = await project.BuildAsync(warningsAsErrors: true);

        result.ExitCode.Should().NotBe(0, "属性为 true 时分析器照常工作");
        result.Output.Should().Contain("STIA0011");
    }

    /// <summary>
    /// 属性必须真的经由 <c>build_property.*</c> 到达分析器，而不是靠别的巧合。
    /// </summary>
    /// <remarks>
    /// 直接检查 SDK 生成的 analyzer config：这是 MSBuild 与分析器之间的唯一通道。属性若没被
    /// <c>CompilerVisibleProperty</c> 放行，这一行根本不会出现，静音也就无从谈起。
    /// </remarks>
    [Fact]
    public async Task ConsumerBuild_Should_ExposeMutingPropertyThroughBuildPropertyChannel()
    {
        using var project = await ConsumerProject.CreateAsync(fixture, "false");

        var result = await project.BuildAsync(warningsAsErrors: false);
        result.ExitCode.Should().Be(0, result.Output);

        var editorConfig = project.ReadGeneratedEditorConfig();

        editorConfig.Should().Contain("build_property.StronglyTypedIdEnableAnalyzers = false",
            "属性必须经 CompilerVisibleProperty 放行后出现在 analyzer config 的全局段里，否则分析器读不到");
    }

    /// <summary>
    /// 静音只关诊断，不关代码生成：Id 的生成产物必须照常出现。
    /// </summary>
    [Fact]
    public async Task ConsumerBuild_Should_StillGenerateIds_WhenMuted()
    {
        using var project = await ConsumerProject.CreateAsync(fixture, "false");

        var result = await project.BuildAsync(warningsAsErrors: false);
        result.ExitCode.Should().Be(0, result.Output);

        var generated = project.ReadGeneratedFiles();

        generated.Should().Contain(path => path.EndsWith("Consumer.OrderId.g.cs", StringComparison.Ordinal),
            "静音只针对诊断，生成器必须照常产出 Id 的核心实现");
    }

    /// <summary>
    /// 同一个工程连续构建两次必须都成功。
    /// </summary>
    /// <remarks>
    /// 守的是「落盘生成文件被默认 glob 编进项目」这个坑：<c>CompilerGeneratedFilesOutputPath</c> 若指向
    /// 工程根目录，第一次构建写出的 <c>.cs</c> 会在第二次构建时被当作源码再编一遍，撞 CS0111
    /// （同一个转换器成员定义了两次）。增量构建下这条路径很容易被忽略，因为它只在「已经构建过」时出现。
    /// </remarks>
    [Fact]
    public async Task ConsumerBuild_Should_Succeed_WhenBuiltTwice()
    {
        using var project = await ConsumerProject.CreateAsync(fixture, "false");

        var first = await project.BuildAsync(warningsAsErrors: false);
        first.ExitCode.Should().Be(0, first.Output);

        var second = await project.BuildAsync(warningsAsErrors: false);
        second.ExitCode.Should().Be(0,
            $"第二次构建不该因落盘的生成文件重复编译而失败。输出：{Environment.NewLine}{second.Output}");
        second.Output.Should().NotContain("CS0111");
    }

    /// <summary>
    /// 一个装好本地包的临时消费工程。
    /// </summary>
    /// <remarks>
    /// 每个用例一份独立目录：包缓存（<c>RestorePackagesPath</c>）也放在里面，避免两个 TFM 的测试腿
    /// 并行还原到同一个全局缓存而互相踩踏。
    /// </remarks>
    private sealed class ConsumerProject : IDisposable
    {
        private const string ProjectFileName = "Consumer.csproj";

        private readonly string _directory;

        private ConsumerProject(string directory) => _directory = directory;

        public static async Task<ConsumerProject> CreateAsync(PackedPackagesFixture fixture, string? propertyValue)
        {
            var directory = Path.Combine(
                Path.GetTempPath(),
                "Len.StronglyTypedId.AnalyzerMutingE2E",
                Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(directory);

            var project = new ConsumerProject(directory);

            try
            {
                await project.WriteFilesAsync(fixture, propertyValue);

                return project;
            }
            catch
            {
                project.Dispose();
                throw;
            }
        }

        /// <summary>
        /// 跑一次构建。<paramref name="warningsAsErrors"/> 打开时 STIA0011 会升级成错误。
        /// </summary>
        public async Task<(int ExitCode, string Output)> BuildAsync(bool warningsAsErrors)
        {
            var arguments = new List<string>
            {
                "build",
                ProjectFileName,
                "--nologo",
                "-v", "minimal",
                // 生成产物落盘，供「静音不影响生成」那条用例检查。
                // 输出目录必须放在 obj/ 下：放在工程根目录时，落盘的 .cs 会被默认 glob 编进项目，
                // 第二次构建就会撞 CS0111（同一个转换器成员定义了两次）。
                "-p:EmitCompilerGeneratedFiles=true",
                "-p:CompilerGeneratedFilesOutputPath=obj/generated",
                // 还原目录放在工程内，避免并行测试腿共用全局缓存。
                $"-p:RestorePackagesPath={Path.Combine(_directory, "packages")}",
            };

            if (warningsAsErrors)
            {
                arguments.Add("-warnaserror");
            }

            return await PackedPackagesFixture.RunProcessAsync(
                PackedPackagesFixture.ResolveDotNetHost(), arguments, _directory);
        }

        /// <summary>
        /// 读 SDK 为本次编译生成的 analyzer config。
        /// </summary>
        public string ReadGeneratedEditorConfig()
        {
            var path = Directory
                .EnumerateFiles(Path.Combine(_directory, "obj"), "*.GeneratedMSBuildEditorConfig.editorconfig", SearchOption.AllDirectories)
                .Single();

            return File.ReadAllText(path);
        }

        /// <summary>
        /// 读本次编译落盘的生成文件路径。
        /// </summary>
        public IReadOnlyList<string> ReadGeneratedFiles()
        {
            var root = Path.Combine(_directory, "obj", "generated");

            return Directory.Exists(root)
                ? [.. Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)]
                : [];
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_directory, recursive: true);
            }
            catch (IOException)
            {
                // 临时目录清不掉不该让测试失败：进程退出后由系统回收。
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private async Task WriteFilesAsync(PackedPackagesFixture fixture, string? propertyValue)
        {
            // 只留本地包源：消费工程必须真的从打好的 .nupkg 里取包，不能悄悄回落到仓库的工程引用。
            var nugetConfig = """
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                  <packageSources>
                    <clear />
                    <add key="local" value="PACKAGES_DIRECTORY" />
                  </packageSources>
                </configuration>
                """.Replace("PACKAGES_DIRECTORY", fixture.OutputDirectory, StringComparison.Ordinal);

            await File.WriteAllTextAsync(Path.Combine(_directory, "nuget.config"), nugetConfig);

            var property = propertyValue is null
                ? string.Empty
                : $"    <StronglyTypedIdEnableAnalyzers>{propertyValue}</StronglyTypedIdEnableAnalyzers>{Environment.NewLine}";

            // 目标框架取 net8.0：它是本库支持的最低 TFM，也是「装包后能不能用」最该守的那条腿。
            // 不引任何第三方包，因此离线也能还原（引用程序集来自 SDK 自带的 targeting pack）。
            var project = $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net8.0</TargetFramework>
                    <Nullable>enable</Nullable>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <OutputType>Library</OutputType>
                {property}  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Len.StronglyTypedId" Version="PACKAGE_VERSION" />
                  </ItemGroup>
                </Project>
                """.Replace("PACKAGE_VERSION", PackedPackagesFixture.PackageVersion, StringComparison.Ordinal);

            await File.WriteAllTextAsync(Path.Combine(_directory, ProjectFileName), project);
            await File.WriteAllTextAsync(Path.Combine(_directory, "Ids.cs"), IdSource, Encoding.UTF8);
        }
    }
}
