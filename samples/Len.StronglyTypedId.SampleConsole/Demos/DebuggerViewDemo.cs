using System.Diagnostics;
using System.Reflection;

namespace Len.StronglyTypedId.SampleConsole.Demos;

internal static class DebuggerViewDemo
{
    public static void Run()
    {
        Demo.Section("调试器视图：[DebuggerDisplay] 与 [DebuggerTypeProxy]");

        var display = typeof(InvoiceId).GetCustomAttribute<DebuggerDisplayAttribute>();
        Demo.Line($"折叠时显示（DebuggerDisplay）       -> {display?.Value}");

        var proxy = typeof(InvoiceId).GetCustomAttribute<DebuggerTypeProxyAttribute>();
        Demo.Line($"展开时用（DebuggerTypeProxy）       -> {proxy?.ProxyTypeName}");

        // 代理类是私有的，正常代码不会碰它；这里用反射模拟「调试器展开变量」，
        // 好让「展开后能看到裸基元值与规范文本」这件事在控制台里也看得见。
        if (proxy is not null && Type.GetType(proxy.ProxyTypeName, throwOnError: false) is { } proxyType)
        {
            var id = InvoiceId.Create(1234);
            var view = Activator.CreateInstance(
                proxyType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                args: [id],
                culture: null);

            Demo.Line($"展开后的 Value（裸基元）            -> {proxyType.GetProperty("Value")?.GetValue(view)}");
            Demo.Line($"展开后的 Text（含前缀）             -> {proxyType.GetProperty("Text")?.GetValue(view)}");
        }

        Demo.Section("调试器视图：泛型容器里的 Id 两者都不生成");

        var boxedId = typeof(Box<int>.BoxedId);
        Demo.Line($"Box<int>.BoxedId 有 DebuggerDisplay -> {boxedId.GetCustomAttribute<DebuggerDisplayAttribute>() is not null}");
        Demo.Line($"Box<int>.BoxedId 有 DebuggerTypeProxy -> {boxedId.GetCustomAttribute<DebuggerTypeProxyAttribute>() is not null}");
    }
}
