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

        Demo.Section("运行时反射：可空 Id 按底层类型判定");

        // Nullable<T>.GetInterfaces() 恒为空，不拆壳就会把 OrderId? 判成「不是强类型 Id」。
        // 这与 MVC 模型绑定器、EF Core 转换器的口径必须一致，否则同一个 Id 在两处结论不同。
        Report(typeof(OrderId?));
        Report(typeof(CustomerId?));

        Demo.Section("运行时反射：TryGetPrimitiveIdType 的非抛异常形态");

        // 三个方法的分工：IsStronglyTypedId 只要布尔；GetPrimitiveIdType 返回 null 表示「不是」；
        // TryGetPrimitiveIdType 用 out 参数，适合「先判断再取值」的写法。
        Demo.Line($"TryGet(OrderId)                    -> {typeof(OrderId).TryGetPrimitiveIdType(out var orderPrimitive)}, {orderPrimitive?.Name}");
        Demo.Line($"TryGet(string)                     -> {typeof(string).TryGetPrimitiveIdType(out var stringPrimitive)}, {stringPrimitive?.Name ?? "—"}");
        Demo.Line($"GetPrimitiveIdType(int)            -> {typeof(int).GetPrimitiveIdType()?.Name ?? "—"}");

        Demo.Section("运行时反射：泛型容器与嵌套容器里的 Id 同样能识别");

        Report(typeof(Box<int>.BoxedId));
        Report(typeof(OrderAggregate.AggregateId));
    }

    private static void Report(Type type)
    {
        var isId = type.IsStronglyTypedId();
        var primitive = type.GetPrimitiveIdType();
        Demo.Line($"{Describe(type),-22} IsStronglyTypedId = {isId,-5} 基元 = {primitive?.Name ?? "—"}");
    }

    /// <summary>
    /// 取可读的类型名：<c>Nullable&lt;T&gt;</c> 的 <see cref="Type.Name"/> 只有 <c>Nullable`1</c>，
    /// 两个可空 Id 会显示成同一行，看不出区别。
    /// </summary>
    private static string Describe(Type type)
        => Nullable.GetUnderlyingType(type) is { } underlying ? $"{underlying.Name}?" : type.Name;
}
