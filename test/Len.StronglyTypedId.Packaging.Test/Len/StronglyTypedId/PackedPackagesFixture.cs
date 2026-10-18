using System.Diagnostics;
using System.IO.Compression;
using System.Xml.Linq;
using Xunit;

namespace Len.StronglyTypedId;

/// <summary>
/// 把两个可发布项目各打一次包，供全部包内容断言共享。
/// </summary>
/// <remarks>
/// <para>
/// 打包是重活（要跑一遍 Release 构建 + 两次 <c>dotnet pack</c>），因此放在集合夹具里只做一次，
/// 而不是每个用例各打一次。产物落在临时目录，按「输入指纹」命名 —— 同一份源码在两个 TFM 的测试进程
/// 里算出同一个指纹，于是第二个进程直接复用第一个的产物，不重复打包。
/// </para>
/// <para>
/// 断言对象是产物包本身，不是「打包命令有没有成功」：<c>dotnet pack</c> 退出码非 0 时直接抛异常，
/// 让失败原因（stdout / stderr）出现在测试输出里，而不是变成一个语焉不详的断言失败。
/// </para>
/// </remarks>
public sealed class PackedPackagesFixture : IAsyncLifetime
{
    /// <summary>本地占位版本号（与 <c>version.props</c> 一致），显式钉住以保证断言确定。</summary>
    private const string PackageVersion = "1.0.0";

    private const string GeneratorsProject = "src/Len.StronglyTypedId.Generators/Len.StronglyTypedId.Generators.csproj";
    private const string MainProject = "src/Len.StronglyTypedId/Len.StronglyTypedId.csproj";

    /// <summary>仓库根目录（含 <c>Len.StronglyTypedId.slnx</c> 的那一层）。</summary>
    public string RepositoryRoot { get; private set; } = string.Empty;

    /// <summary>两个包落地的目录（临时目录，按输入指纹隔离）。</summary>
    public string OutputDirectory { get; private set; } = string.Empty;

    public string GeneratorsPackagePath => Path.Combine(OutputDirectory, $"Len.StronglyTypedId.Generators.{PackageVersion}.nupkg");

    public string MainPackagePath => Path.Combine(OutputDirectory, $"Len.StronglyTypedId.{PackageVersion}.nupkg");

    public string SymbolsPackagePath => Path.Combine(OutputDirectory, $"Len.StronglyTypedId.{PackageVersion}.snupkg");

    public async ValueTask InitializeAsync()
    {
        RepositoryRoot = FindRepositoryRoot();
        OutputDirectory = Path.Combine(
            Path.GetTempPath(),
            "Len.StronglyTypedId.Packaging.Test",
            ComputeInputStamp(RepositoryRoot));

        Directory.CreateDirectory(OutputDirectory);

        if (File.Exists(GeneratorsPackagePath)
            && File.Exists(MainPackagePath)
            && File.Exists(SymbolsPackagePath))
        {
            return;
        }

        await PackAsync(GeneratorsProject).ConfigureAwait(false);
        await PackAsync(MainProject).ConfigureAwait(false);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// 打开一个包，读出它的条目清单。
    /// </summary>
    public static IReadOnlyList<string> ReadEntries(string packagePath)
    {
        using var archive = ZipFile.OpenRead(packagePath);

        return [.. archive.Entries.Select(entry => entry.FullName)];
    }

    /// <summary>
    /// 读出包内 nuspec 的 XML 文档。
    /// </summary>
    public static XDocument ReadNuspec(string packagePath)
    {
        using var archive = ZipFile.OpenRead(packagePath);
        var entry = archive.Entries.Single(candidate => candidate.FullName.EndsWith(".nuspec", StringComparison.Ordinal));

        using var stream = entry.Open();

        return XDocument.Load(stream);
    }

    private async Task PackAsync(string relativeProjectPath)
    {
        var projectPath = Path.Combine(RepositoryRoot, relativeProjectPath.Replace('/', Path.DirectorySeparatorChar));

        // GeneratePackageOnBuild 在 Release 下会把包同时写进项目默认输出目录，这里显式关掉，
        // 让产物只落在我们指定的临时目录里，不污染仓库。
        //
        // EnablePackageValidation 也关掉：本测试断言的是「包内容清单」，与 API 兼容性校验无关
        // （后者由 CI 的 pack job 在真实基线下覆盖）。开着它会让本地占位版本号（1.0.0 < 基线
        // 1.3.0-preview.5）下的基线抑制被判为「不必要」（CP0021），把一个与内容无关的失败
        // 混进来。
        var arguments = new[]
        {
            "pack",
            projectPath,
            "-c", "Release",
            "--no-restore",
            "-o", OutputDirectory,
            $"-p:Version={PackageVersion}",
            "-p:GeneratePackageOnBuild=false",
            "-p:EnablePackageValidation=false",
            "--nologo",
        };

        var result = await RunProcessAsync(ResolveDotNetHost(), arguments, RepositoryRoot).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"dotnet pack {relativeProjectPath} 失败（退出码 {result.ExitCode}）。{Environment.NewLine}{result.Output}");
        }
    }

    /// <summary>
    /// 从测试程序集所在目录向上找仓库根目录。
    /// </summary>
    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Len.StronglyTypedId.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"未能从 {AppContext.BaseDirectory} 向上找到 Len.StronglyTypedId.slnx。");
    }

    /// <summary>
    /// 按「会影响包内容的输入文件的最新写入时间」算一个指纹，用作临时目录名。
    /// </summary>
    /// <remarks>
    /// 指纹变了就重新打包，没变就复用 —— 既让同一轮里两个 TFM 的测试进程共享产物，又不会在源码改动后
    /// 复用过期包。取 <c>src/</c> 下全部文件与几个打包相关 props 的最大写入时间即可。
    /// </remarks>
    private static string ComputeInputStamp(string repositoryRoot)
    {
        var inputs = new List<string>
        {
            Path.Combine(repositoryRoot, "common.props"),
            Path.Combine(repositoryRoot, "version.props"),
            Path.Combine(repositoryRoot, "Directory.Packages.props"),
            Path.Combine(repositoryRoot, "src", "Len.StronglyTypedId", "Len.StronglyTypedId.csproj"),
            Path.Combine(repositoryRoot, "src", "Len.StronglyTypedId", "PublicAPI.Shipped.txt"),
            Path.Combine(repositoryRoot, "src", "Len.StronglyTypedId", "PublicAPI.Unshipped.txt"),
            Path.Combine(repositoryRoot, "src", "Len.StronglyTypedId.Generators", "Len.StronglyTypedId.Generators.csproj"),
        };

        var sourceRoot = Path.Combine(repositoryRoot, "src");

        if (Directory.Exists(sourceRoot))
        {
            inputs.AddRange(Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)));
        }

        var ticks = inputs
            .Where(File.Exists)
            .Select(File.GetLastWriteTimeUtc)
            .DefaultIfEmpty(DateTime.UnixEpoch)
            .Max()
            .Ticks;

        return ticks.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string ResolveDotNetHost()
    {
        var host = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");

        return string.IsNullOrEmpty(host) ? "dotnet" : host;
    }

    private static async Task<(int ExitCode, string Output)> RunProcessAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        // 当前进程是被 SDK 拉起来的，环境里带着一套指向「当前 SDK」的 MSBuild 变量；
        // 嵌套调用 dotnet 时把它们清掉，让子进程自己解析自己的 SDK。
        startInfo.Environment.Remove("MSBuildSDKsPath");
        startInfo.Environment.Remove("MSBuildExtensionsPath");
        startInfo.Environment.Remove("MSBuildToolsPath");

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"无法启动进程 {fileName}。");

        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync().ConfigureAwait(false);

        return (process.ExitCode, await standardOutput.ConfigureAwait(false) + await standardError.ConfigureAwait(false));
    }
}

/// <summary>
/// 把 <see cref="PackedPackagesFixture"/> 挂成集合夹具，让全部包内容用例共享同一份产物。
/// </summary>
[CollectionDefinition(Name)]
public sealed class PackedPackagesCollection : ICollectionFixture<PackedPackagesFixture>
{
    public const string Name = "packed-packages";
}
