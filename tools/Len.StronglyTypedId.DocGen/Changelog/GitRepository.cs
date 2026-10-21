using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Len.StronglyTypedId.DocGen.Changelog;

/// <summary>
/// 一个 git tag 及其对应的包版本与提交日期。
/// </summary>
/// <param name="Tag">仓库里的原始 tag 名（可能是 <c>v1.0.5</c>）。</param>
/// <param name="Version">去掉前导 <c>v</c> 后的版本号，与 <c>CHANGELOG.md</c> 的小节标题对应。</param>
/// <param name="Date">tag 指向提交的日期（<c>yyyy-MM-dd</c>）。</param>
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

    /// <summary>最近一个可从 <c>HEAD</c> 到达的 tag；没有 tag 时返回 <see langword="null"/>。</summary>
    public static string? FindLatestTag(string root)
    {
        var result = TryRun(root, "describe", "--tags", "--abbrev=0");

        return string.IsNullOrWhiteSpace(result) ? null : result.Trim();
    }

    /// <summary>取出区间内的提交标题（<c>%s</c>），保持 git 的默认顺序（新到旧）。</summary>
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

    /// <summary>去掉 <c>v</c> / <c>V</c> 前缀：早期 tag 是 <c>v1.0.5</c>，1.3.0 起不带前缀。</summary>
    public static string NormalizeVersion(string tag) =>
        tag.Length > 1 && (tag[0] == 'v' || tag[0] == 'V') ? tag[1..] : tag;

    private static string Run(string root, params string[] arguments) =>
        TryRun(root, arguments)
        ?? throw new InvalidOperationException(
            $"`git {string.Join(' ', arguments)}` 执行失败；请确认当前目录是 git 仓库，且已拉取 tag（CI 需 fetch-depth: 0）。");

    private static string? TryRun(string root, params string[] arguments)
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

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            return process.ExitCode == 0 ? output : null;
        }
        catch (Win32Exception exception)
        {
            throw new InvalidOperationException($"找不到 git 可执行文件：{exception.Message}", exception);
        }
    }
}
