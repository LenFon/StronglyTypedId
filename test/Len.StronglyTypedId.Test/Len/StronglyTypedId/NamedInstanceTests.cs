using System;
using System.Text.Json;
using FluentAssertions;

namespace Len.StronglyTypedId;

/// <summary>
/// N2（命名实例）的运行时契约：实例经 <c>Create</c> 构造，因此归一化、验证器与前缀都照常生效，
/// 且 <c>Parse(instance.ToString())</c> 必然读得回来。
/// </summary>
public class NamedInstanceTests
{
    [Fact]
    public void Instance_Should_CarryDeclaredPrimitiveValue()
    {
        InstanceInt32Id.Unspecified.Value.Should().Be(-1);
        InstanceInt32Id.Anonymous.Value.Should().Be(0);
    }

    /// <summary>
    /// 特性本身也是公开 API：反射读到的 <c>Name</c> / <c>Value</c> 必须与声明逐字一致，
    /// 且 <c>AllowMultiple</c> 让同一个 Id 能挂多条实例声明。
    /// </summary>
    [Fact]
    public void InstanceAttribute_Should_ExposeDeclaredNameAndValue()
    {
        var attributes = (StronglyTypedIdInstanceAttribute[])Attribute.GetCustomAttributes(
            typeof(InstanceInt32Id),
            typeof(StronglyTypedIdInstanceAttribute));

        attributes.Should().HaveCount(2);
        attributes[0].Name.Should().Be("Unspecified");
        attributes[0].Value.Should().Be("-1");
        attributes[1].Name.Should().Be("Anonymous");
        attributes[1].Value.Should().Be("0");
    }

    /// <summary>
    /// <c>long.MinValue</c> 是字面量发射最容易写错的边界：一元负号必须作用于整个字面量。
    /// </summary>
    [Fact]
    public void Instance_Should_SupportInt64MinValue() => InstanceInt64Id.Min.Value.Should().Be(long.MinValue);

    [Fact]
    public void Instance_Should_SupportEmptyGuid() => InstanceGuidId.Empty.Value.Should().Be(Guid.Empty);

    [Fact]
    public void Instance_Should_SupportEmptyString()
    {
        StringInstanceId.Blank.Value.Should().BeEmpty();
        StringInstanceId.Unknown.Value.Should().Be("unknown");
    }

    [Fact]
    public void Instance_Should_SupportEnumMember() => InstanceStatusId.Active.Value.Should().Be(SampleStatus.Active);

    /// <summary>
    /// 硬不变式：实例是 <c>Create</c> 的产物，故规范文本形一定读得回来。
    /// </summary>
    /// <remarks>
    /// 时间基元不在此列：无参 <c>ToString()</c> 走默认格式串，"G" 会丢掉亚秒与 <c>Kind</c>，
    /// 文本往返本来就有损（见 K1 的既有结论），与实例无关。
    /// </remarks>
    [Fact]
    public void Instance_Should_RoundTripThroughCanonicalText()
    {
        InstanceInt32Id.Parse(InstanceInt32Id.Unspecified.ToString(), null).Should().Be(InstanceInt32Id.Unspecified);
        InstanceInt64Id.Parse(InstanceInt64Id.Min.ToString(), null).Should().Be(InstanceInt64Id.Min);
        InstanceGuidId.Parse(InstanceGuidId.Empty.ToString(), null).Should().Be(InstanceGuidId.Empty);
        StringInstanceId.Parse(StringInstanceId.Unknown.ToString(), null).Should().Be(StringInstanceId.Unknown);
        InstanceStatusId.Parse(InstanceStatusId.Active.ToString(), null).Should().Be(InstanceStatusId.Active);
    }

    /// <summary>
    /// 归一化在 <c>Create</c> 内部发生，故声明的 <c>"abc"</c> 落成 <c>"ABC"</c>。
    /// </summary>
    [Fact]
    public void Instance_Should_ApplyNormalizer() => NormalizedInstanceStringId.Upper.Value.Should().Be("ABC");

    /// <summary>
    /// 前缀只装饰规范文本形：实例携带的是基元值，<c>ToString()</c> 照常带前缀，且带前缀的文本能读回。
    /// </summary>
    [Fact]
    public void Instance_Should_KeepPrefixInCanonicalText()
    {
        PrefixedInstanceGuidId.Empty.Value.Should().Be(Guid.Empty);
        PrefixedInstanceGuidId.Empty.ToString().Should().Be("ord_00000000-0000-0000-0000-000000000000");
        PrefixedInstanceGuidId.Parse(PrefixedInstanceGuidId.Empty.ToString(), null).Should().Be(PrefixedInstanceGuidId.Empty);
    }

    /// <summary>
    /// <c>DateTime</c> 的 <c>Kind</c> 必须原样保留，否则往返后不再相等。
    /// </summary>
    [Fact]
    public void Instance_Should_PreserveDateTimeKind()
    {
        InstanceDateTimeId.Epoch.Value.Kind.Should().Be(DateTimeKind.Utc);
        InstanceDateTimeId.Epoch.Value.Should().Be(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    /// <summary>
    /// <c>DateTimeOffset</c> 的偏移必须原样保留，否则 <c>+08:00</c> 会被折成 UTC。
    /// </summary>
    [Fact]
    public void Instance_Should_PreserveDateTimeOffsetOffset()
    {
        InstanceDateTimeOffsetId.PlusEight.Value.Offset.Should().Be(TimeSpan.FromHours(8));
        InstanceDateTimeOffsetId.PlusEight.Value.Should().Be(new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.FromHours(8)));
    }

    /// <summary>
    /// D4：<c>NaN</c> 与 <c>±Infinity</c> 是 <c>Half</c> 的合法取值，编译期校验不能把它们判成非法；
    /// <c>(65504, 65520)</c> 会舍入回最大有限值，同样不是溢出。
    /// </summary>
    [Fact]
    public void Instance_Should_SupportHalfSpecialValuesAndRounding()
    {
        Half.IsNaN(InstanceHalfId.NotANumber.Value).Should().BeTrue();
        InstanceHalfId.PositiveInfinity.Value.Should().Be(Half.PositiveInfinity);
        InstanceHalfId.NegativeInfinity.Value.Should().Be(Half.NegativeInfinity);
        InstanceHalfId.RoundedToMax.Value.Should().Be(Half.MaxValue);
    }

    /// <summary>
    /// 引用类型 Id 的实例是单例：<c>static readonly</c> 保证同一引用，且与 <c>default</c>（null）不相等。
    /// </summary>
    [Fact]
    public void Instance_Should_BeSingletonReference_ForRecordClass()
    {
        InstanceGuidRefId.Empty.Should().BeSameAs(InstanceGuidRefId.Empty);
        InstanceGuidRefId.Empty.Should().NotBeNull();
    }

    /// <summary>
    /// 值类型 Id 的零值实例与 <c>default</c> 相等 —— 这是值语义的必然结果，不是缺陷。
    /// 需要「与 default 可区分」的哨兵值应避开基元的零值。
    /// </summary>
    [Fact]
    public void Instance_Should_EqualDefault_WhenValueIsPrimitiveDefault()
        => InstanceInt32Id.Anonymous.Should().Be(new InstanceInt32Id(0));

    /// <summary>
    /// 实例与普通值走同一条 JSON 路径，不需要任何额外注册。
    /// </summary>
    [Fact]
    public void Instance_Should_RoundTripThroughJson()
    {
        var json = JsonSerializer.Serialize(InstanceInt32Id.Unspecified);

        json.Should().Be("-1");
        JsonSerializer.Deserialize<InstanceInt32Id>(json).Should().Be(InstanceInt32Id.Unspecified);
    }

    /// <summary>
    /// 验证器拒绝哨兵值时，类型初始化器抛 <see cref="TypeInitializationException"/>。
    /// </summary>
    /// <remarks>
    /// 这是 N2 的既定取舍：编译期无法静态判定「验证器会不会接受这个值」（验证器是任意用户代码），
    /// 与其让一个不可能存在的 Id 悄悄流进领域模型，不如在第一次触碰时立刻失败。
    /// </remarks>
    [Fact]
    public void Instance_Should_ThrowTypeInitializationException_WhenValidatorRejectsValue()
    {
        Action access = () => _ = RejectedInstanceInt32Id.Zero;

        access.Should().Throw<TypeInitializationException>()
            .WithInnerException<ArgumentException>();
    }
}
