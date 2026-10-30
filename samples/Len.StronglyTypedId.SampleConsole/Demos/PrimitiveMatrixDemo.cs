using System.Text.Json;
using Len.StronglyTypedId.Testing;

namespace Len.StronglyTypedId.SampleConsole.Demos;

internal static class PrimitiveMatrixDemo
{
    public static void Run()
    {
        Demo.Section("基元矩阵：23 种内置基元 + enum，逐个看规范文本与 JSON 形态");

        // 用固定种子造值，输出可复现；Any 会尊重验证器，因此这里拿到的都是合法取值。
        var generator = new AnyIdGenerator(new AnyIdOptions { Seed = 20240101 });

        foreach (var idType in PrimitiveIds.All)
        {
            var value = generator.Create(idType);
            var json = JsonSerializer.Serialize(value, idType);

            Demo.Line($"{idType.Name}: text = {value}, json = {json}");
        }

        Demo.Section("基元矩阵：JSON 往返");

        foreach (var idType in PrimitiveIds.All)
        {
            var value = generator.Create(idType);
            var json = JsonSerializer.Serialize(value, idType);
            var restored = JsonSerializer.Deserialize(json, idType);

            Demo.Line($"{idType.Name}: roundtrip = {Equals(value, restored)}");
        }
    }
}
