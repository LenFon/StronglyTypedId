using System.Reflection;
using FluentAssertions;

namespace Len.StronglyTypedId;

public class StronglyTypedIdTests
{
    [Theory]
    [InlineData(typeof(GuidId), typeof(Guid))]
    [InlineData(typeof(StringId), typeof(string))]
    [InlineData(typeof(Int32Id), typeof(int))]
    public void TryGetPrimitiveIdType_Should_ReturnTrue_When(Type type, Type expectedPrimitiveId)
    {
        type.TryGetPrimitiveIdType(out var actualPrimitiveIdType).Should().BeTrue();
        actualPrimitiveIdType.Should().Be(expectedPrimitiveId);
    }

    [Theory]
    [InlineData(typeof(NotStronglyTypedId), null)]
    [InlineData(typeof(IStronglyTypedId<GuidId, Guid>), null)]
    public void TryGetPrimitiveIdType_Should_ReturnFalse_When(Type type, Type? expectedPrimitiveId)
    {
        type.TryGetPrimitiveIdType(out var actualPrimitiveIdType).Should().BeFalse();
        actualPrimitiveIdType.Should().Be(expectedPrimitiveId);
    }

    [Fact]
    public void TryGetPrimitiveIdType_Should_ThrowArgumentNullException_WhenTypeIsNull()
    {
        Type? type = null;

        var ex = Assert.Throws<ArgumentNullException>(() => type!.TryGetPrimitiveIdType(out _));

        Assert.Equal("type", ex.ParamName);
    }

    [Theory]
    [InlineData(typeof(GuidId), typeof(Guid))]
    [InlineData(typeof(StringId), typeof(string))]
    [InlineData(typeof(Int32Id), typeof(int))]
    public void GetPrimitiveIdType_Should_ReturnType_When(Type type, Type expectedPrimitiveId) => type.GetPrimitiveIdType().Should().Be(expectedPrimitiveId);

    [Theory]
    [InlineData(typeof(NotStronglyTypedId), null)]
    [InlineData(typeof(IStronglyTypedId<GuidId, Guid>), null)]
    public void GetPrimitiveIdType_Should_ReturnNull_When(Type type, Type? expectedPrimitiveId) => type.GetPrimitiveIdType().Should().Be(expectedPrimitiveId);

    [Theory]
    [InlineData(typeof(GuidId))]
    [InlineData(typeof(StringId))]
    [InlineData(typeof(Int32Id))]
    public void IsStronglyTypedId_Should_ReturnTrue_When(Type type) => type.IsStronglyTypedId().Should().BeTrue();

    [Theory]
    [InlineData(typeof(NotStronglyTypedId))]
    [InlineData(typeof(IStronglyTypedId<GuidId, Guid>))]
    [InlineData(typeof(Guid))]
    public void IsStronglyTypedId_Should_ReturnFalse_When(Type type) => type.IsStronglyTypedId().Should().BeFalse();

    /// <summary>
    /// 可空 Id 必须按底层 Id 判定。<c>Nullable&lt;T&gt;</c> 的 <c>GetInterfaces()</c> 恒为空数组，
    /// 不先拆壳就会把 <c>GuidId?</c> 判成「不是强类型 Id」，而 MVC 绑定器与 EF 转换器都按 Id 处理。
    /// </summary>
    [Fact]
    public void IsStronglyTypedId_Should_ReturnTrue_ForNullableId() => typeof(GuidId?).IsStronglyTypedId().Should().BeTrue();

    /// <summary>
    /// 拆壳只对可空 Id 生效：可空的普通基元仍然不是强类型 Id。
    /// </summary>
    [Fact]
    public void IsStronglyTypedId_Should_ReturnFalse_ForNullablePrimitive() => typeof(int?).IsStronglyTypedId().Should().BeFalse();

    [Fact]
    public void GetPrimitiveIdType_Should_ReturnPrimitive_ForNullableId() => typeof(GuidId?).GetPrimitiveIdType().Should().Be<Guid>();

    [Fact]
    public void TryGetPrimitiveIdType_Should_ReturnTrue_ForNullableId()
    {
        typeof(GuidId?).TryGetPrimitiveIdType(out var primitiveIdType).Should().BeTrue();
        primitiveIdType.Should().Be<Guid>();
    }

    /// <summary>
    /// 判定结果按类型缓存：「不是强类型 Id」也要能缓存（字典值为 <see langword="null"/>），
    /// 否则热路径上每次都要重走 <c>GetInterfaces()</c> + LINQ。
    /// </summary>
    [Fact]
    public void GetPrimitiveIdType_Should_BeRepeatable_ForNonIdTypes()
    {
        typeof(NotStronglyTypedId).GetPrimitiveIdType().Should().BeNull();
        typeof(NotStronglyTypedId).GetPrimitiveIdType().Should().BeNull();

        typeof(GuidId).GetPrimitiveIdType().Should().Be<Guid>();
        typeof(GuidId).GetPrimitiveIdType().Should().Be<Guid>();
    }

    /// <summary>
    /// 两个入参为 null 的重载都必须先抛 <see cref="ArgumentNullException"/>，而不是在解引用时抛 NRE。
    /// </summary>
    [Fact]
    public void GetPrimitiveIdType_Should_ThrowArgumentNullException_WhenTypeIsNull()
    {
        Type? type = null;

        var exception = Assert.Throws<ArgumentNullException>(() => type!.GetPrimitiveIdType());

        Assert.Equal("type", exception.ParamName);
    }

    [Fact]
    public void IsStronglyTypedId_Should_ThrowArgumentNullException_WhenTypeIsNull()
    {
        Type? type = null;

        var exception = Assert.Throws<ArgumentNullException>(() => type!.IsStronglyTypedId());

        Assert.Equal("type", exception.ParamName);
    }

    /// <summary>
    /// 特性契约：只能标在 class / struct 上，且不可继承、不允许多次标注。
    /// </summary>
    [Fact]
    public void StronglyTypedIdAttribute_Should_DeclareExpectedUsage()
    {
        var usage = typeof(StronglyTypedIdAttribute).GetCustomAttribute<AttributeUsageAttribute>();

        usage.Should().NotBeNull();
        usage!.ValidOn.Should().Be(AttributeTargets.Class | AttributeTargets.Struct);
        usage.AllowMultiple.Should().BeFalse();
        usage.Inherited.Should().BeFalse();
    }
}