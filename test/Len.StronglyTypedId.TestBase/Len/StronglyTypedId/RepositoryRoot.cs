namespace Len.StronglyTypedId;

/// <summary>
/// 仓库根目录定位。
/// </summary>
/// <remarks>
/// 测试从 <c>bin/Debug/net&lt;tfm&gt;</c> 运行，而快照基线、README 等文件在源码树里，
/// 故按解决方案文件向上回溯。找不到即抛异常，避免测试在错误的目录下「通过」。
/// </remarks>
public static class RepositoryRoot
{
    /// <summary>
    /// 仓库根目录的绝对路径。
    /// </summary>
    public static string Path { get; } = Find();

    private static string Find()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(System.IO.Path.Combine(directory.FullName, "Len.StronglyTypedId.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("找不到仓库根目录（未找到 Len.StronglyTypedId.slnx）。");
    }
}
