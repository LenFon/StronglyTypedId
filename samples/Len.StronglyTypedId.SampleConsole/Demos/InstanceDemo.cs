using System.Globalization;
using System.Text.Json;

namespace Len.StronglyTypedId.SampleConsole.Demos;

internal static class InstanceDemo
{
    public static void Run()
    {
        Demo.Section("命名实例：编译期校验，运行期经 Create 构造");

        Demo.Line($"TenantId.Unspecified              -> {TenantId.Unspecified}");
        Demo.Line($"TenantId.Anonymous                -> {TenantId.Anonymous}");
        Demo.Line($"== TenantId.Create(-1)            -> {TenantId.Unspecified == TenantId.Create(-1)}");
        Demo.Line($"Parse(ToString()) 往返             -> {TenantId.Parse(TenantId.Unspecified.ToString(), CultureInfo.InvariantCulture) == TenantId.Unspecified}");
        Demo.Line($"JSON                              -> {JsonSerializer.Serialize(TenantId.Unspecified)}");
        Demo.Line($"Anonymous == default              -> {TenantId.Anonymous == default}");

        Demo.Section("实例必须通过验证器：被拒时类型初始化即失败");

        try
        {
            _ = RejectedInstanceId.Zero;
        }
        catch (TypeInitializationException ex)
        {
            Demo.Line($"RejectedInstanceId.Zero           -> 抛出 {ex.GetType().Name}（内层 {ex.InnerException?.GetType().Name}）");
        }
    }
}
