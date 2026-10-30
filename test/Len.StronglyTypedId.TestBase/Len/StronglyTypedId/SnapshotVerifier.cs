using System.Text.RegularExpressions;

namespace Len.StronglyTypedId;

/// <summary>
/// 文本快照校验：把实际产物与仓库里的基线文件逐字符比对，不一致时写出 <c>.received.txt</c> 并失败。
/// </summary>
/// <remarks>
/// 与 DocGen 的 <c>--verify</c> 同一思路 —— 基线是仓库里的普通文本文件，可评审、可 diff，
/// 不引入第三方快照库。更新基线时设环境变量 <c>STRONGLY_TYPED_ID_UPDATE_SNAPSHOTS=1</c> 重跑测试。
/// </remarks>
public static partial class SnapshotVerifier
{
    /// <summary>
    /// 设置该环境变量后，测试会把实际产物写回基线而不是比对。
    /// </summary>
    public const string UpdateEnvironmentVariable = "STRONGLY_TYPED_ID_UPDATE_SNAPSHOTS";

    /// <summary>
    /// 比对 <paramref name="actual"/> 与 <paramref name="snapshotPath"/> 处的基线。
    /// </summary>
    /// <param name="snapshotPath">基线文件的绝对路径。</param>
    /// <param name="actual">实际产物。</param>
    public static void Verify(string snapshotPath, string actual)
    {
        var normalized = Normalize(actual);
        var receivedPath = System.IO.Path.ChangeExtension(snapshotPath, ".received.txt");

        if (IsUpdateRequested())
        {
            Write(snapshotPath, normalized);
            DeleteIfExists(receivedPath);

            return;
        }

        if (!File.Exists(snapshotPath))
        {
            Write(receivedPath, normalized);

            throw new InvalidOperationException(
                $"缺少快照基线：{snapshotPath}{Environment.NewLine}"
                + $"已写出实际产物到 {receivedPath}。确认无误后把它重命名为基线，"
                + $"或设 {UpdateEnvironmentVariable}=1 重跑测试自动更新。");
        }

        var expected = Normalize(File.ReadAllText(snapshotPath));

        if (string.Equals(expected, normalized, StringComparison.Ordinal))
        {
            DeleteIfExists(receivedPath);

            return;
        }

        Write(receivedPath, normalized);

        throw new InvalidOperationException(
            $"快照与基线不一致：{snapshotPath}{Environment.NewLine}"
            + $"实际产物已写到 {receivedPath}，可用它替换基线；"
            + $"或设 {UpdateEnvironmentVariable}=1 重跑测试自动更新。{Environment.NewLine}"
            + DescribeDifference(expected, normalized));
    }

    /// <summary>
    /// 统一换行符并去掉首尾空白差异，使基线在 CRLF / LF 检出下都成立。
    /// </summary>
    private static string Normalize(string text)
        => text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd() + "\n";

    private static bool IsUpdateRequested()
        => string.Equals(
            Environment.GetEnvironmentVariable(UpdateEnvironmentVariable),
            "1",
            StringComparison.Ordinal);

    private static void Write(string path, string text)
    {
        var directory = System.IO.Path.GetDirectoryName(path);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // 基线按仓库约定落 CRLF；比对前两侧都会归一化，故检出时的行尾不影响结果。
        File.WriteAllText(path, text.Replace("\n", "\r\n", StringComparison.Ordinal));
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// 给出第一处差异的行号与上下文，避免只报「不相等」。
    /// </summary>
    private static string DescribeDifference(string expected, string actual)
    {
        var expectedLines = expected.Split('\n');
        var actualLines = actual.Split('\n');
        var lineCount = Math.Max(expectedLines.Length, actualLines.Length);

        for (var index = 0; index < lineCount; index++)
        {
            var expectedLine = index < expectedLines.Length ? expectedLines[index] : "<缺少该行>";
            var actualLine = index < actualLines.Length ? actualLines[index] : "<缺少该行>";

            if (!string.Equals(expectedLine, actualLine, StringComparison.Ordinal))
            {
                return $"第一处差异在第 {index + 1} 行：{Environment.NewLine}"
                    + $"  基线：{expectedLine}{Environment.NewLine}"
                    + $"  实际：{actualLine}";
            }
        }

        return "两侧内容相同，差异来自首尾空白。";
    }

    /// <summary>
    /// 把生成器版本号替换成固定占位符，使版本升级不产生快照噪声。
    /// </summary>
    public static string NormalizeGeneratorVersion(string text)
        => GeneratedCodeVersionPattern().Replace(text, "GeneratedCodeAttribute(\"$1\", \"<version>\")");

    [GeneratedRegex("GeneratedCodeAttribute\\(\"([^\"]+)\", \"[^\"]*\"\\)")]
    private static partial Regex GeneratedCodeVersionPattern();
}
