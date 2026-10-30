using System.Globalization;
using System.Text.Json;

namespace Len.StronglyTypedId.SampleConsole.Demos;

internal static class CustomPrimitiveDemo
{
    public static void Run()
    {
        MoneySection();
        UlidSection();
    }

    private static void MoneySection()
    {
        Demo.Section("自定义基元：Money 满足 IParsable + IComparable，本库零改动即可用");

        var money = MoneyId.Create(new Money(12.5m, "USD"));
        Demo.Line($"Create(new Money(12.5m, \"USD\"))   -> {money}");
        Demo.Line($"Parse(\"USD 12.50\")                 -> {MoneyId.Parse("USD 12.50", CultureInfo.InvariantCulture)}");
        Demo.Line($"TryParse(\"bad\")                    -> {MoneyId.TryParse("bad", CultureInfo.InvariantCulture, out _)}");
        Demo.Line($"ToString() 往返                     -> {MoneyId.Parse(money.ToString(), CultureInfo.InvariantCulture) == money}");

        Demo.Section("自定义基元：比较与排序走基元自己的实现");

        var cheap = MoneyId.Create(new Money(1m, "USD"));
        var pricey = MoneyId.Create(new Money(100m, "USD"));
        Demo.Line($"cheap < pricey                    -> {cheap < pricey}");
        Demo.Line($"SortedSet 顺序                     -> {string.Join(", ", new SortedSet<MoneyId> { pricey, cheap })}");

        Demo.Section("自定义基元：JSON 写规范文本，而不是按公开属性展开");

        var json = JsonSerializer.Serialize(money);
        Demo.Line($"Serialize                         -> {json}");
        Demo.Line($"Deserialize                       -> {JsonSerializer.Deserialize<MoneyId>(json)}");
        Demo.Line($"字典键                             -> {JsonSerializer.Serialize(new Dictionary<MoneyId, int> { [money] = 1 })}");
    }

    private static void UlidSection()
    {
        Demo.Section("第三方自定义基元：ULID 零改动可用");

        // Cysharp 的 Ulid 已实现 IParsable<Ulid>、IComparable<Ulid> 与非泛型 IComparable，
        // 正好满足自定义基元契约 —— 本库不需要为它加任何代码，引用包、声明一行即可。
        var ulid = UlidId.Create(Ulid.Parse("01ARZ3NDEKTSV4RRFFQ69G5FAV", null));

        Demo.Line($"Create(Ulid.Parse(...))           -> {ulid}");
        Demo.Line($"规范文本长度                        -> {ulid.ToString().Length}（26 字符 Crockford Base32）");
        Demo.Line($"Parse(ToString()) 往返             -> {UlidId.Parse(ulid.ToString(), CultureInfo.InvariantCulture) == ulid}");
        Demo.Line($"JSON                              -> {JsonSerializer.Serialize(ulid)}");
        Demo.Line($"字典键                             -> {JsonSerializer.Serialize(new Dictionary<UlidId, int> { [ulid] = 1 })}");

        Demo.Section("第三方自定义基元：ULID 自带时间序，比较走基元实现");

        // 用显式时间戳构造：同一毫秒内的两个 ULID 只有随机部分不同，随机部分不保证单调。
        var earlier = UlidId.Create(Ulid.NewUlid(DateTimeOffset.UnixEpoch));
        var later = UlidId.Create(Ulid.NewUlid(DateTimeOffset.UnixEpoch.AddSeconds(1)));

        Demo.Line($"earlier < later                   -> {earlier < later}");
        Demo.Line($"SortedSet 顺序                     -> {string.Join(", ", new SortedSet<UlidId> { later, earlier })}");

        Demo.Section("第三方自定义基元：前缀与命名实例同样生效");

        var prefixed = PrefixedUlidId.Create(Ulid.Parse("01ARZ3NDEKTSV4RRFFQ69G5FAV", null));
        Demo.Line($"带前缀的 ToString()                 -> {prefixed}");
        Demo.Line($"命名实例 Nil                       -> {InstanceUlidId.Nil}（== default：{InstanceUlidId.Nil == default}）");

        Demo.Section("第三方自定义基元：非法文本被拒绝");

        Demo.Line($"TryParse(\"not-a-ulid\")             -> {UlidId.TryParse("not-a-ulid", CultureInfo.InvariantCulture, out _)}");
        Demo.Line($"TryParse(25 字符)                  -> {UlidId.TryParse("01M60GFMBVKNE19YX1YAZQB3K", CultureInfo.InvariantCulture, out _)}");
    }
}
