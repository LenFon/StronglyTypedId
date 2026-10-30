using System.Text.Json;

namespace Len.StronglyTypedId.SampleConsole.Demos;

internal static class ReferenceTypeDemo
{
    public static void Run()
    {
        Demo.Section("引用类型 Id：record class 可以为 null，default 是空引用");

        var id = SessionId.Create(Guid.Parse("3f2c1b4e-0000-4000-8000-000000000001"));
        SessionId? missing = default;

        Demo.Line($"Create(...)                        -> {id}");
        Demo.Line($"default(SessionId) is null        -> {missing is null}");

        Demo.Section("引用类型 Id：比较把 null 视为最小");

        Demo.Line($"id.CompareTo(null)                -> {id.CompareTo(null)}");
        Demo.Line($"null < id                         -> {null < id}");
        Demo.Line($"id == null                        -> {id == null}");
        Demo.Line($"null == null                      -> {missing == null}");

        Demo.Section("引用类型 Id：JSON 的 null 往返");

        Demo.Line($"Serialize(null)                   -> {JsonSerializer.Serialize<SessionId?>(null)}");
        Demo.Line($"Deserialize(\"null\") is null       -> {JsonSerializer.Deserialize<SessionId>("null") is null}");

        Demo.Section("值类型 Id：null 被拒绝，而不是悄悄落成 default");

        try
        {
            _ = JsonSerializer.Deserialize<OrderId>("null");
        }
        catch (JsonException ex)
        {
            Demo.Line($"Deserialize<OrderId>(\"null\")      -> 抛出 {ex.GetType().Name}");
        }
    }
}
