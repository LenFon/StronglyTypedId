using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Len.StronglyTypedId.DocGen.Changelog;

/// <summary>
/// 一个 git tag 及其对应的包版本与提交日期。
/// </summary>
internal sealed record GitTag(string Tag, string Version, string Date);

/// <summary>
/// 调用 <c>git</c> 读取 tag 与提交标题。只读，不改工作区。
/// </summary>
internal static class GitRepository
{
    /// <summary>
    /// 按创建时间升序读出全部 tag。浅克隆（<c>fetch-depth: 1</c>）下会得到空集合，
    /// 调用方必须把「一个 tag 都没有」当成错误而不是「无需校验」。
    /// </summary>
    public static IReadOnlyList<GitTag> ReadTags(string root)
    {
        var tags = new List<GitTag>();

        foreach (var tag in Run(root, "tag", "--sort=creatordate").Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var name = tag.Trim();

            if (name.Length == 0)
            {
                continue;
            }

            var date = Run(root, "log", "-1", "--format=%ad", "--date=short", name).Trim();
            tags.Add(new GitTag(name, NormalizeVersion(name), date));
        }

        return tags;
    }

    /// <summary>
    /// 最近一个可从 <c>HEAD</c> 到达的 tag；没有 tag 时返回 <see langword="null"/>。
    /// </summary>
    public static string? FindLatestTag(string root)
    {
        var result = TryRun(root, out _, "describe", "--tags", "--abbrev=0");

        return string.IsNullOrWhiteSpace(result) ? null : result.Trim();
    }

    /// <summary>
    /// 取出区间内的提交标题（<c>%s</c>），保持 git 的默认顺序（新到旧）。
    /// </summary>
    public static IReadOnlyList<string> ReadSubjects(string root, string? range)
    {
        var arguments = range is null
            ? new[] { "log", "--pretty=format:%s" }
            : ["log", "--pretty=format:%s", range];

        return Run(root, arguments)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(subject => subject.TrimEnd('\r'))
            .Where(subject => subject.Length > 0)
            .ToArray();
    }

    /// <summary>
    /// 去掉 <c>v</c> / <c>V</c> 前缀：早期 tag 是 <c>v1.0.5</c>，1.3.0 起不带前缀。
    /// </summary>
    public static string NormalizeVersion(string tag) =>
        tag.Length > 1 && (tag[0] == 'v' || tag[0] == 'V') ? tag[1..] : tag;

    private static string Run(string root, params string[] arguments)
    {
        var output = TryRun(root, out var error, arguments);

        if (output is not null)
        {
            return output;
        }

        var message = $"`git {string.Join(' ', arguments)}` 执行失败；请确认当前目录是 git 仓库，且已拉取 tag（`git fetch --tags`）。";

        throw new InvalidOperationException(
            string.IsNullOrWhiteSpace(error)
                ? message
                : $"{message}{Environment.NewLine}git stderr:{Environment.NewLine}{error.Trim()}");
    }

    private static string? TryRun(string root, out string error, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("无法启动 git 进程。");

            // 两个管道各有约 4 KB 的内核缓冲：必须并发读。顺序 ReadToEnd 时，只要 git 往
            // stderr 写满缓冲（dubious ownership、LF→CRLF 告警、LFS 钩子……），它就会阻塞在
            // 写 stderr 上，而我们阻塞在 stdout 的 EOF 上，双方互等，永久挂起。
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            process.WaitForExit();

            error = errorTask.GetAwaiter().GetResult();

            return process.ExitCode == 0 ? outputTask.GetAwaiter().GetResult() : null;
        }
        catch (Win32Exception exception)
        {
            throw new InvalidOperationException($"找不到 git 可执行文件：{exception.Message}", exception);
        }
    }
}
