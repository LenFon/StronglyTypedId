using System.Text;

namespace Len.StronglyTypedId.Benchmarks.Consumer;

/// <summary>
/// 消费者画像：本工程的编译引用集，就是基准要用的那份引用集。
/// </summary>
public static class ConsumerProfile
{
    private const string ResourceName = "reference-paths.txt";

    /// <summary>
    /// 本工程解析出的编译引用路径（含框架引用程序集），顺序与编译器所见一致。
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// 嵌入清单不存在，说明生成本清单的 MSBuild 目标没有执行（需重新生成一次）。
    /// </exception>
    public static IReadOnlyList<string> ReferencePaths()
    {
        using var stream = typeof(ConsumerProfile).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"未找到嵌入的 {ResourceName}：生成本清单的 MSBuild 目标未执行，重新生成一次即可。");

        using var reader = new StreamReader(stream, Encoding.UTF8);

        var paths = new List<string>();

        while (reader.ReadLine() is { } line)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                paths.Add(line.Trim());
            }
        }

        return paths;
    }
}
