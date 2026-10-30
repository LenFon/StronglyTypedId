using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace Len.StronglyTypedId;

/// <summary>
/// 配置绑定（<c>Microsoft.Extensions.Configuration</c>）经 <c>TypeDescriptor</c> 与强类型 Id 往返。
/// </summary>
/// <remarks>
/// 配置 binder 的字符串转换走 <see cref="System.ComponentModel.TypeDescriptor"/>，因此只有开了
/// <c>TypeConverter = true</c> 的 Id 才能被绑定。这条路径此前只在文档里提了一句「覆盖配置绑定」，
/// 没有任何可执行证据；而它失败时的表现是<b>静默</b>的 —— 对象绑定得到 <c>default</c>，
/// 编译无警告、运行不抛异常，与 Newtonsoft 跨程序集时静默写成对象形态属同一类问题。
/// <para>
/// 不开 <c>TypeConverter</c> 的对照组只断言「转换器不具备字符串转换能力」这一根因，
/// 不钉住 binder 内部的异常类型 —— 那是框架实现细节，跨版本可能变化。
/// </para>
/// </remarks>
public class ConfigurationBindingTests
{
    private static readonly Guid _value = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");

    [Fact]
    public void ObjectBinding_Should_ReadId_WhenTypeConverterEnabled()
    {
        var config = BuildConfig();

        var settings = config.GetSection("Convertible").Get<ConvertibleSettings>()!;

        settings.Id.Should().Be(ConvertibleIntegrationGuidId.Create(_value));
    }

    [Fact]
    public void ObjectBinding_Should_ReadId_WhenTypeConverterEnabled_ForReferenceTypeId()
    {
        var config = BuildConfig();

        var settings = config.GetSection("ConvertibleReference").Get<ConvertibleReferenceSettings>()!;

        settings.Id.Should().Be(IntegrationConvertibleReferenceId.Create(_value));
    }

    [Fact]
    public void ObjectBinding_Should_ReadPrefixedText_WhenTypeConverterEnabled()
    {
        var config = BuildConfig();

        var settings = config.GetSection("Prefixed").Get<PrefixedSettings>()!;

        settings.Id.Should().Be(PrefixedConvertibleIntegrationGuidId.Create(_value));
        settings.Id.ToString().Should().Be("ord_" + _value, "前缀是规范文本形的一部分");
    }

    [Fact]
    public void ScalarBinding_Should_ReadId_WhenTypeConverterEnabled()
    {
        var config = BuildConfig();

        config.GetValue<ConvertibleIntegrationGuidId>("Convertible:Id")
            .Should().Be(ConvertibleIntegrationGuidId.Create(_value));
    }

    /// <summary>
    /// 对照组：不开 <c>TypeConverter</c> 时，<c>TypeDescriptor</c> 拿不到能读字符串的转换器 ——
    /// 这是配置绑定静默失败（对象绑定得到 <c>default</c>）的根因。
    /// </summary>
    [Fact]
    public void TypeDescriptor_Should_NotConvertFromString_WhenTypeConverterDisabled()
    {
        var converter = System.ComponentModel.TypeDescriptor.GetConverter(typeof(PlainIntegrationGuidId));

        converter.CanConvertFrom(typeof(string)).Should().BeFalse(
            "没有 TypeConverter 的 Id 无法被配置 binder 读取，绑定会静默得到 default");
    }

    /// <summary>
    /// 把「静默失败」本身钉成用例：配置里写了值，绑出来却是 <c>default</c>，且不抛异常。
    /// 这是使用者最容易踩的坑，也是文档必须写明 <c>TypeConverter = true</c> 的原因。
    /// </summary>
    [Fact]
    public void ObjectBinding_Should_SilentlyYieldDefault_WhenTypeConverterDisabled()
    {
        var config = BuildConfig();

        var settings = config.GetSection("Plain").Get<PlainSettings>()!;

        settings.Id.Should().Be(default,
            "缺少 TypeConverter 时 binder 不会报错，只会留下 default —— 这正是需要文档提醒的陷阱");
    }

    [Fact]
    public void Bind_Should_PopulateExistingInstance_WhenTypeConverterEnabled()
    {
        var config = BuildConfig();
        var settings = new ConvertibleSettings();

        config.GetSection("Convertible").Bind(settings);

        settings.Id.Should().Be(ConvertibleIntegrationGuidId.Create(_value));
    }

    private static IConfiguration BuildConfig()
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Convertible:Id"] = _value.ToString(),
                ["ConvertibleReference:Id"] = _value.ToString(),
                ["Prefixed:Id"] = "ord_" + _value,
                ["Plain:Id"] = _value.ToString(),
            })
            .Build();

    private sealed class ConvertibleSettings
    {
        public ConvertibleIntegrationGuidId Id { get; set; }
    }

    private sealed class ConvertibleReferenceSettings
    {
        public IntegrationConvertibleReferenceId Id { get; set; } = null!;
    }

    private sealed class PrefixedSettings
    {
        public PrefixedConvertibleIntegrationGuidId Id { get; set; }
    }

    private sealed class PlainSettings
    {
        public PlainIntegrationGuidId Id { get; set; }
    }
}
