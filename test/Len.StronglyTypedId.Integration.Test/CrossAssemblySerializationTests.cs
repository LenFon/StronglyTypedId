using Newtonsoft.Json;
using System.Text.Json;
using FluentAssertions;
using Len.StronglyTypedId.Layered;

namespace Len.StronglyTypedId;

/// <summary>
/// 跨程序集（分层）的序列化回归：领域层不引用序列化基础设施，宿主应用引用。
/// </summary>
/// <remarks>
/// 领域层 <c>Len.StronglyTypedId.TestBase.Layered</c> 没有引用 Newtonsoft.Json，那里的 Id 不带内嵌转换器；
/// 而内嵌转换器只能生成在声明 Id 的程序集里，宿主应用补内嵌的会撞 CS0426。
/// <list type="number">
/// <item>宿主应用能自己补一份顶层 Newtonsoft 转换器并注册；</item>
/// <item>补出的转换器写出的形态必须与 System.Text.Json 一致，否则同一份数据会有两种表示且无任何报错。</item>
/// </list>
/// </remarks>
public class CrossAssemblySerializationTests
{
    private static readonly Guid _value = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");

    [Fact]
    public void Newtonsoft_Should_WriteSameShapeAsSystemTextJson_ForLayeredId()
    {
        var id = new LayeredOrderId(_value);

        var stj = System.Text.Json.JsonSerializer.Serialize(id);
        var newtonsoft = JsonConvert.SerializeObject(id, CreateSettings());

        newtonsoft.Should().Be(stj, "同一个 Id 在两个 JSON 库下必须是同一种形态");
        newtonsoft.Should().Be("\"3fa85f64-5717-4562-b3fc-2c963f66afa6\"", "Newtonsoft 写出的必须是字符串形态");
    }

    [Fact]
    public void Newtonsoft_Should_RoundTripLayeredId()
    {
        var id = new LayeredOrderId(_value);
        var settings = CreateSettings();

        var json = JsonConvert.SerializeObject(id, settings);

        JsonConvert.DeserializeObject<LayeredOrderId>(json, settings).Should().Be(id);
    }

    [Fact]
    public void Newtonsoft_Should_RegisterEveryLayeredId()
    {
        // 两个 Id 都在「没有内嵌转换器」的程序集里 → 宿主应用要为两个都补上顶层转换器。
        // 本工程自己也声明了若干 Id（它们带内嵌转换器），故这里只断言领域层那两个在列。
        var settings = CreateSettings();

        settings.Converters.Should().ContainSingle(converter => converter.CanConvert(typeof(LayeredOrderId)));
        settings.Converters.Should().ContainSingle(converter => converter.CanConvert(typeof(LayeredInvoiceId)));
    }

    [Fact]
    public void Newtonsoft_Should_StayIdempotent_WhenAppliedTwice()
    {
        var settings = CreateSettings();
        var countAfterFirst = settings.Converters.Count;

        settings.AddStronglyTypedId();

        settings.Converters.Should().HaveCount(countAfterFirst, "重复调用不得让转换器列表增长");
    }

    private static JsonSerializerSettings CreateSettings()
    {
        var settings = new JsonSerializerSettings();

        settings.AddStronglyTypedId();

        return settings;
    }
}
