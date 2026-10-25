using System.Diagnostics;
using System.IO.Compression;
using System.Xml.Linq;
using Xunit;

namespace Len.StronglyTypedId;

/// <summary>
/// 把两个可发布项目各打一次包，供全部包内容断言共享。
/// </summary>
public sealed class PackedPackagesFixture : IAsyncLifetime
{
    /// <summary>
    /// 本地占位版本号（与 <c>version.props</c> 一致），显式钉住以保证断言确定。
    /// </summary>
    private const string PackageVersion = "1.0.0";

    private const string GeneratorsProject = "src/Len.StronglyTypedId.Generators/Len.StronglyTypedId.Generators.csproj";
    private const string MainProject = "src/Len.StronglyTypedId/Len.StronglyTypedId.csproj";
    private const string TestingProject = "src/Len.StronglyTypedId.Testing/Len.StronglyTypedId.Testing.csproj";
    private const string TestingBogusProject = "src/Len.StronglyTypedId.Testing.Bogus/Len.StronglyTypedId.Testing.Bogus.csproj";

    /// <summary>
    /// 打包期间持有的跨进程锁文件名（放在输出目录里，随输入指纹一起隔离）。
    /// </summary>
    private const string LockFileName = ".pack.lock";

    private static readonly TimeSpan _lockTimeout = TimeSpan.FromMinutes(5);

    private static readonly TimeSpan _lockPollInterval = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// 仓库根目录（含 <c>Len.StronglyTypedId.slnx</c> 的那一层）。
    /// </summary>
    public string RepositoryRoot { get; private set; } = string.Empty;

    /// <summary>
    /// 两个包落地的目录（临时目录，按输入指纹隔离）。
    /// </summary>
    public string OutputDirectory { get; private set; } = string.Empty;

    public string GeneratorsPackagePath => Path.Combine(OutputDirectory, $"Len.StronglyTypedId.Generators.{PackageVersion}.nupkg");

    public string MainPackagePath => Path.Combine(OutputDirectory, $"Len.StronglyTypedId.{PackageVersion}.nupkg");

    public string SymbolsPackagePath => Path.Combine(OutputDirectory, $"Len.StronglyTypedId.{PackageVersion}.snupkg");

    /// <summary>
    /// 测试辅助包的产物路径。
    /// </summary>
    /// <remarks>
    /// 这两个包尚未打开 <c>IsPackable</c>（尚未发布），打包时用 <c>-p:IsPackable=true</c> 临时打开 ——
    /// 依赖声明的验证不能等到它们真的发布那天才做。
    /// </remarks>
    public string TestingPackagePath => Path.Combine(OutputDirectory, $"Len.StronglyTypedId.Testing.{PackageVersion}.nupkg");

    /// <inheritdoc cref="TestingPackagePath"/>
    public string TestingBogusPackagePath => Path.Combine(OutputDirectory, $"Len.StronglyTypedId.Testing.Bogus.{PackageVersion}.nupkg");

    public async ValueTask InitializeAsync()
    {
        RepositoryRoot = FindRepositoryRoot();
        OutputDirectory = Path.Combine(
            Path.GetTempPath(),
            "Len.StronglyTypedId.Packaging.Test",
            ComputeInputStamp(RepositoryRoot));

        Directory.CreateDirectory(OutputDirectory);

        // 两个 TFM 的测试模块会被 MTP 并行调度，而产物目录按输入指纹隔离、与 TFM 无关 —— 不加锁就会
        // 两边同时判定「还没有包」、同时往同一目录写包，撞文件锁或 NU5026。这里用跨进程独占文件锁
        // 串行化：先到者打包，后到者等锁释放后重新检查产物、直接复用。
        await using var lockHandle = await AcquirePackLockAsync(
            Path.Combine(OutputDirectory, LockFileName)).ConfigureAwait(false);

        if (HasAllPackages())
        {
            return;
        }

        await PackAsync(GeneratorsProject).ConfigureAwait(false);
        await PackAsync(MainProject).ConfigureAwait(false);
        await PackAsync(TestingProject).ConfigureAwait(false);
        await PackAsync(TestingBogusProject).ConfigureAwait(false);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// 三个包是否都已落地（锁内复查：另一个 TFM 可能刚刚打完）。
    /// </summary>
    private bool HasAllPackages()
        => File.Exists(GeneratorsPackagePath)
            && File.Exists(MainPackagePath)
            && File.Exists(SymbolsPackagePath)
            && File.Exists(TestingPackagePath)
            && File.Exists(TestingBogusPackagePath);

    /// <summary>
    /// 取得跨进程独占锁。拿不到就轮询等待，直到超时后把最后一次异常抛出去。
    /// </summary>
    private static async Task<FileStream> AcquirePackLockAsync(string lockPath)
    {
        var deadline = DateTime.UtcNow + _lockTimeout;

        while (true)
        {
            try
            {
                return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                await Task.Delay(_lockPollInterval).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// 打开一个包，读出它的条目清单。
    /// </summary>
    public static IReadOnlyList<string> ReadEntries(string packagePath)
    {
        using var archive = ZipFile.OpenRead(packagePath);

        return [.. archive.Entries.Select(entry => entry.FullName)];
    }

    /// <summary>
    /// 读出包内某个条目的字节内容。
    /// </summary>
    public static byte[] ReadEntryBytes(string packagePath, string entryName)
    {
        using var archive = ZipFile.OpenRead(packagePath);
        var entry = archive.Entries.Single(candidate => candidate.FullName == entryName);

        using var stream = entry.Open();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);

        return buffer.ToArray();
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

        // 关掉 GeneratePackageOnBuild，让包只落在指定的临时目录里，不污染仓库。
        // 也关掉 EnablePackageValidation：本测试断言的是包内容清单，开着会让本地占位版本号下的基线抑制被判为「不必要」（CP0021），混进与内容无关的失败。
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
            // 测试辅助包尚未发布（IsPackable 默认关闭），临时打开才能验证它们的依赖声明。
            "-p:IsPackable=true",
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
    private static string ComputeInputStamp(string repositoryRoot)
    {
        var inputs = new List<string>
        {
            Path.Combine(repositoryRoot, "common.props"),
            Path.Combine(repositoryRoot, "version.props"),
            Path.Combine(repositoryRoot, "Directory.Packages.props"),
            Path.Combine(repositoryRoot, "PACKAGE.md"),
            Path.Combine(repositoryRoot, "src", "Len.StronglyTypedId", "Len.StronglyTypedId.csproj"),
            Path.Combine(repositoryRoot, "src", "Len.StronglyTypedId", "PublicAPI.Shipped.txt"),
            Path.Combine(repositoryRoot, "src", "Len.StronglyTypedId", "PublicAPI.Unshipped.txt"),
            Path.Combine(repositoryRoot, "src", "Len.StronglyTypedId.Generators", "Len.StronglyTypedId.Generators.csproj"),
        };

        // 图标同样是包内容：不计入指纹的话，只改 assets/icon.png 时指纹不变，夹具会直接复用
        // 上一次打好的包，图标断言就对着旧包通过了 —— 换了图标却「测过了」是最坏的一种绿。
        var assetsRoot = Path.Combine(repositoryRoot, "assets");

        if (Directory.Exists(assetsRoot))
        {
            inputs.AddRange(Directory.EnumerateFiles(assetsRoot, "*", SearchOption.AllDirectories));
        }

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
