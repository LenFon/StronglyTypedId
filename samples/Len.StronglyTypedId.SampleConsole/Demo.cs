namespace Len.StronglyTypedId.SampleConsole;

internal static class Demo
{
    public static void Section(string title)
    {
        Console.WriteLine();
        Console.WriteLine($"── {title} ──");
    }

    public static void Line(string text) => Console.WriteLine($"   {text}");

    public static void End()
    {
        Console.WriteLine();
        Console.WriteLine("全部示例执行完毕。");
    }
}
