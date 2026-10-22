using System.Globalization;
using Len.StronglyTypedId.DocGen.Changelog;

namespace Len.StronglyTypedId.DocGen;

/// <summary>
/// 文档工具：文档里由源码派生的四张表的生成器 + <c>CHANGELOG.md</c> / <c>CHANGELOG.zh-CN.md</c> 的结构校验与草稿。
/// </summary>
/// <remarks>
/// 用法（退出码：<c>0</c> 一致，<c>1</c> 不一致，<c>2</c> 运行出错）：
/// <code>
/// dotnet run --project tools/Len.StronglyTypedId.DocGen                          # 重新生成 README 与 docs 里的表格
/// dotnet run --project tools/Len.StronglyTypedId.DocGen -- --verify              # CI 门禁：文档与源码不一致则退出码 1
/// dotnet run --project tools/Len.StronglyTypedId.DocGen -- --verify-changelog    # CI 门禁：tag 与两份 CHANGELOG 的小节对不上则退出码 1
/// dotnet run --project tools/Len.StronglyTypedId.DocGen -- --verify-package-readme  # CI 门禁：PACKAGE.md 与源码不一致则退出码 1
/// dotnet run --project tools/Len.StronglyTypedId.DocGen -- --draft-changelog     # 打印 [Unreleased] 草稿（按 Conventional Commits 分类）
/// </code>
/// </remarks>
internal static class Program
{
    /// <summary>
    /// 需要维护的文档、其目标区域性，以及该文件承载的生成区块。
    /// 中文版取 <c>zh-CN</c>，与生成器内嵌的 <c>Locales/zh-CN.txt</c> 同名，诊断标题才能取到中文文案。
    /// </summary>
    /// <remarks>
    /// 区块按文件分配：README 只留导览级两张表，诊断表与生成文件表随章节搬到 <c>docs/</c>；漏写某个文件不会静默放过，<see cref="ReadmeBlocks.Replace"/> 找不到标记就直接抛异常。
    /// </remarks>
    private static readonly (string File, string Culture, string[] Blocks)[] _targets =
    [
        ("README.md", "en", ["primitive-types", "generated-code"]),
        ("README.zh-CN.md", "zh-CN", ["primitive-types", "generated-code"]),
        ("docs/generated-code.md", "en", ["generated-files"]),
        ("docs/zh-CN/generated-code.md", "zh-CN", ["generated-files"]),
        ("docs/diagnostics.md", "en", ["diagnostics"]),
        ("docs/zh-CN/diagnostics.md", "zh-CN", ["diagnostics"]),
    ];

    /// <summary>
    /// 区块 ID 与对应的渲染函数，顺序即替换顺序（彼此独立，不影响结果）。
    /// </summary>
    private static readonly (string BlockId, Func<CultureInfo, string> Render)[] _blocks =
    [
        ("primitive-types", DocumentationRenderer.RenderPrimitiveTypes),
        ("generated-code", DocumentationRenderer.RenderGeneratedCode),
        ("generated-files", DocumentationRenderer.RenderGeneratedFiles),
        ("diagnostics", DocumentationRenderer.RenderDiagnostics),
    ];

    public static int Main(string[] args)
    {
        var verify = args.Contains("--verify", StringComparer.Ordinal);
        var verifyChangelog = args.Contains("--verify-changelog", StringComparer.Ordinal);
        var draftChangelog = args.Contains("--draft-changelog", StringComparer.Ordinal);
        var verifyPackageReadme = args.Contains("--verify-package-readme", StringComparer.Ordinal);

        try
        {
            var root = ResolveRepositoryRoot(args);

            // 必须写成单个 return 表达式：拆成一串 if + return 会被 IDE0046 判为「可简化的 if 语句」。
            return draftChangelog
                ? ChangelogDrafter.Draft(root)
                : verifyChangelog
                    ? ChangelogVerifier.Verify(root)
                    : verifyPackageReadme ? PackageReadmeVerifier.Verify(root) : Run(root, verify);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Console.Error.WriteLine(exception.Message);

            return 2;
        }
    }

    private static int Run(string root, bool verify)
    {
        var stale = false;

        foreach (var (file, cultureName, blocks) in _targets)
        {
            var path = Path.Combine(root, file);
            var current = File.ReadAllText(path);
            var newLine = ReadmeBlocks.DetectNewLine(current);
            var culture = CultureInfo.GetCultureInfo(cultureName);
            var generated = current;

            foreach (var blockId in blocks)
            {
                generated = ReadmeBlocks.Replace(generated, blockId, Render(blockId, culture), newLine);
            }

            if (string.Equals(generated, current, StringComparison.Ordinal))
            {
                Console.WriteLine($"{file}: 已是最新。");

                continue;
            }

            if (verify)
            {
                stale = true;

                Console.WriteLine($"{file}: 与源码不一致。");
                Console.WriteLine(DescribeFirstDifference(current, generated));

                continue;
            }

            File.WriteAllText(path, generated);
            Console.WriteLine($"{file}: 已更新。");
        }

        if (stale)
        {
            Console.WriteLine();
            Console.WriteLine("运行 `dotnet run --project tools/Len.StronglyTypedId.DocGen` 重新生成。");
        }

        return stale ? 1 : 0;
    }

    /// <summary>
    /// 按区块 ID 取渲染函数。<paramref name="blockId"/> 拼错会在第一次运行时炸出来，
    /// 而不是留下一段永远不再更新的文档。测试复用同一入口，避免两处各写一份映射。
    /// </summary>
    internal static string Render(string blockId, CultureInfo culture)
        => _blocks.First(entry => string.Equals(entry.BlockId, blockId, StringComparison.Ordinal)).Render(culture);

    /// <summary>
    /// 定位仓库根目录：优先取 <c>--root &lt;path&gt;</c>，否则从程序集所在目录逐级上溯找解决方案文件。
    /// </summary>
    private static string ResolveRepositoryRoot(string[] args)
    {
        var index = Array.IndexOf(args, "--root");

        if (index >= 0 && index + 1 < args.Length)
        {
            return Path.GetFullPath(args[index + 1]);
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Len.StronglyTypedId.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("找不到仓库根目录（未找到 Len.StronglyTypedId.slnx）；可用 --root <path> 指定。");
    }

    /// <summary>
    /// 描述首个差异行，让 CI 日志直接指出「哪一行、原来是什么、应该是什么」。
    /// </summary>
    private static string DescribeFirstDifference(string current, string generated)
    {
        var currentLines = current.Split('\n');
        var generatedLines = generated.Split('\n');
        var count = Math.Min(currentLines.Length, generatedLines.Length);

        for (var index = 0; index < count; index++)
        {
            if (!string.Equals(currentLines[index], generatedLines[index], StringComparison.Ordinal))
            {
                return $"  首个差异在第 {index + 1} 行：{Environment.NewLine}"
                    + $"    文件: {currentLines[index].TrimEnd('\r')}{Environment.NewLine}"
                    + $"    应为: {generatedLines[index].TrimEnd('\r')}";
            }
        }

        return $"  行数不同：文件 {currentLines.Length} 行，应为 {generatedLines.Length} 行。";
    }
}
