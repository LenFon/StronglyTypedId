namespace Len.StronglyTypedId.SampleConsole.Demos;

internal static class ReflectionDemo
{
    public static void Run()
    {
        Demo.Section("运行时反射：识别强类型 Id 与底层基元");

        Report(typeof(OrderId));
        Report(typeof(CustomerId));
        Report(typeof(RegionId));
        Report(typeof(string));
        Report(typeof(Region));
    }

    private static void Report(Type type)
    {
        var isId = type.IsStronglyTypedId();
        var primitive = type.GetPrimitiveIdType();
        Demo.Line($"{type.Name,-18} IsStronglyTypedId = {isId,-5} 基元 = {primitive?.Name ?? "—"}");
    }
}
