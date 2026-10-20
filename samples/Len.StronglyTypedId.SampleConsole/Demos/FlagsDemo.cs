using System.Globalization;

namespace Len.StronglyTypedId.SampleConsole.Demos;

internal static class FlagsDemo
{
    public static void Run()
    {
        Demo.Section("[Flags] 枚举基元：位组合合法，未定义位被拒");

        var permissions = PermissionId.Create(Permission.Read | Permission.Write);
        Demo.Line($"Create(Read | Write)              -> {permissions}");
        Demo.Line($"Parse(\"Read, Write\")               -> {PermissionId.Parse("Read, Write", CultureInfo.InvariantCulture)}");
        Demo.Line($"TryParse(\"Read, Delete\")           -> {PermissionId.TryParse("Read, Delete", CultureInfo.InvariantCulture, out var parsed)}, {parsed}");
        Demo.Line($"TryParse(\"999\")（含未定义位）      -> {PermissionId.TryParse("999", CultureInfo.InvariantCulture, out _)}");
        Demo.Line($"TryParse(\"-1\")（满位）              -> {PermissionId.TryParse("-1", CultureInfo.InvariantCulture, out _)}");
    }
}
