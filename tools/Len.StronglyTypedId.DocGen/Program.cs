using System.Globalization;

namespace Len.StronglyTypedId.DocGen;

/// <summary>
/// README 三张表的生成器：默认就地重写，<c>--verify</c> 只校验不改文件。
/// </summary>
/// <remarks>
/// <para>
/// 用法：
/// </para>
/// <code>
/// dotnet run --project tools/Len.StronglyTypedId.DocGen              # 重新生成两张 README 的表格
/// dotnet run --project tools/Len.StronglyTypedId.DocGen -- --verify  # CI 门禁：不一致则退出码 1
/// </code>
/// <para>
/// 退出码：<c>0</c> 一致（或已更新），<c>1</c> <c>--verify</c> 下发现不一致，<c>2</c> 运行出错。
/// </para>
/// </remarks>
internal static class Program
{
    /// <summary>
    /// 需要维护的 README 及其目标区域性。中文版取 <c>zh-CN</c>，与生成器内嵌的
    /// <c>Locales/zh-CN.txt</c> 同名，诊断标题才能取到中文文案。
    /// </summary>
    private static readonly (string File, string Culture)[] _targets =
    [
        ("README.md", "en"),
        ("README.zh-CN.md", "zh-CN"),
    ];

    /// <summary>区块 ID 与对应的渲染函数，顺序即替换顺序（彼此独立，不影响结果）。</summary>
    private static readonly (string BlockId, Func<CultureInfo, string> Render)[] _blocks =
    [
        ("primitive-types", DocumentationRenderer.RenderPrimitiveTypes),
        ("generated-code", DocumentationRenderer.RenderGeneratedCode),
        ("diagnostics", DocumentationRenderer.RenderDiagnostics),
    ];

    public static int Main(string[] args)
    {
        var verify = args.Contains("--verify", StringComparer.Ordinal);

        try
        {
            return Run(ResolveRepositoryRoot(args), verify);
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

        foreach (var (file, cultureName) in _targets)
        {
            var path = Path.Combine(root, file);
            var current = File.ReadAllText(path);
            var newLine = ReadmeBlocks.DetectNewLine(current);
            var culture = CultureInfo.GetCultureInfo(cultureName);
            var generated = current;

            foreach (var (blockId, render) in _blocks)
            {
                generated = ReadmeBlocks.Replace(generated, blockId, render(culture), newLine);
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
