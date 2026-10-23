using System.Numerics;
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
    /// 抽象类型即便继承了强类型 Id 的接口实现，也不能算强类型 Id —— 它没有可构造的实例，
    /// 而模型绑定器与 EF 转换器拿到它只会尝试实例化。
    /// </summary>
    /// <remarks>
    /// 这条分支在 <c>ResolvePrimitiveIdType</c> 里排在接口扫描之前，故必须显式覆盖：
    /// 只测「普通 record」与「接口」两条负向用例时，删掉 <c>IsAbstract</c> 判断不会有任何用例变红。
    /// </remarks>
    [Fact]
    public void IsStronglyTypedId_Should_ReturnFalse_ForAbstractId()
    {
        typeof(AbstractGuidId).IsStronglyTypedId().Should().BeFalse();
        typeof(AbstractGuidId).GetPrimitiveIdType().Should().BeNull();
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

    /// <summary>
    /// 一个类型同时实现多份 <c>IStronglyTypedId&lt;,&gt;</c> 时，必须选 <c>TSelf</c> 等于自身的那个接口。
    /// </summary>
    /// <remarks>
    /// <see cref="Type.GetInterfaces"/> 的返回顺序不作保证，直接取 <c>FirstOrDefault</c> 会让同一类型在不同
    /// 运行时（甚至同一次运行的不同时刻）给出不同的基元类型 —— 模型绑定器与 EF 转换器据此选转换器，
    /// 结果就成了不确定的。这里让「外来契约」排在前面也无所谓：选中自身契约是规则，不是运气。
    /// </remarks>
    [Fact]
    public void GetPrimitiveIdType_Should_PreferOwnContract_WhenTypeImplementsSeveral()
    {
        typeof(MultiContractId).GetInterfaces()
            .Count(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IStronglyTypedId<,>))
            .Should().BeGreaterThan(1, "前置条件：该类型必须真的实现多份契约，否则这条用例测不到选择规则");

        typeof(MultiContractId).GetPrimitiveIdType().Should().Be<Guid>(
            "自身契约是 IStronglyTypedId<MultiContractId, Guid>，不能因为接口枚举顺序而选中外来契约的 int");
    }
}

/// <summary>
/// 抽象 Id：继承了 <see cref="GuidIdV2"/> 的接口实现，但自身不可实例化。
/// </summary>
/// <remarks>
/// 刻意从真实的生成类型派生，而不是手写一份接口实现 —— 这样「接口齐全但类型抽象」才是唯一让它被判否的原因。
/// </remarks>
public abstract record AbstractGuidId(Guid Value) : GuidIdV2(Value);

/// <summary>
/// 同时实现两份 <c>IStronglyTypedId&lt;,&gt;</c> 的类型：自身契约是 <c>Guid</c>，另带一份指向
/// <see cref="ForeignId"/> 的 <c>int</c> 契约。用来锁定「优先选 TSelf 等于自身」的选择规则。
/// </summary>
/// <remarks>
/// 第二份契约的 <c>TSelf</c> 故意不是本类型 —— 这正是 CA2260 要警告的写法，也正是本用例要覆盖的场景。
/// </remarks>
#pragma warning disable CA2260
public readonly struct MultiContractId(Guid value)
    : IStronglyTypedId<ForeignId, int>, IStronglyTypedId<MultiContractId, Guid>
{
    public Guid Value { get; } = value;

    int IStronglyTypedId<ForeignId, int>.Value => 0;

    public static MultiContractId Create(Guid value) => new(value);

    static ForeignId IStronglyTypedId<ForeignId, int>.Create(int value) => ForeignId.Create(value);

    // 第二份契约继承的 IParsable<ForeignId> / IEqualityOperators<ForeignId, ForeignId, bool> 与自身的
    // 同名成员签名冲突（返回类型不同），只能显式实现。它们不参与本用例的判定，保持最小实现即可。
    static ForeignId IParsable<ForeignId>.Parse(string s, IFormatProvider? provider) => ForeignId.Parse(s, provider);

    static bool IParsable<ForeignId>.TryParse(string? s, IFormatProvider? provider, out ForeignId result)
        => ForeignId.TryParse(s, provider, out result);

    static bool IEqualityOperators<ForeignId, ForeignId, bool>.operator ==(ForeignId left, ForeignId right) => left == right;

    static bool IEqualityOperators<ForeignId, ForeignId, bool>.operator !=(ForeignId left, ForeignId right) => left != right;

    public static MultiContractId Parse(string s, IFormatProvider? provider) => new(Guid.Parse(s));

    public static bool TryParse(string? s, IFormatProvider? provider, out MultiContractId result)
    {
        if (Guid.TryParse(s, out var parsed))
        {
            result = new(parsed);
            return true;
        }

        result = default;
        return false;
    }

    public static bool operator ==(MultiContractId left, MultiContractId right) => left.Value == right.Value;

    public static bool operator !=(MultiContractId left, MultiContractId right) => left.Value != right.Value;

    public override bool Equals(object? obj) => obj is MultiContractId other && Value == other.Value;

    public override int GetHashCode() => Value.GetHashCode();
}
#pragma warning restore CA2260

/// <summary>
/// <see cref="MultiContractId"/> 那份外来契约的 <c>TSelf</c>，只为让第二份接口闭合而存在。
/// </summary>
public readonly struct ForeignId(int value) : IStronglyTypedId<ForeignId, int>
{
    public int Value { get; } = value;

    public static ForeignId Create(int value) => new(value);

    public static ForeignId Parse(string s, IFormatProvider? provider) => new(int.Parse(s, provider));

    public static bool TryParse(string? s, IFormatProvider? provider, out ForeignId result)
    {
        if (int.TryParse(s, out var parsed))
        {
            result = new(parsed);
            return true;
        }

        result = default;
        return false;
    }

    public static bool operator ==(ForeignId left, ForeignId right) => left.Value == right.Value;

    public static bool operator !=(ForeignId left, ForeignId right) => left.Value != right.Value;

    public override bool Equals(object? obj) => obj is ForeignId other && Value == other.Value;

    public override int GetHashCode() => Value.GetHashCode();
}