using System.Reflection;
using FluentAssertions;
using ProtoBuf;

namespace Len.StronglyTypedId;

/// <summary>
/// protobuf-net 集成：surrogate 契约的端到端往返。
/// </summary>
/// <remarks>
/// 生成器层用例只能证明「产物里写了什么」；这里跑真实的 protobuf-net 序列化管道，
/// 证明 <c>[ProtoContract(Surrogate = …)]</c> 与转换运算符确实接得上。
/// <para>
/// 覆盖重点是「protobuf-net 没有原生表示」的那几类基元：它们的载荷是规范文本，
/// 若误按原生值写出，运行期会抛「no contract can be inferred」。
/// </para>
/// </remarks>
public class ProtobufIntegrationTests
{
    /// <summary>
    /// 每个 Id 都必须带 surrogate 特性，且 surrogate 类型真的存在。
    /// </summary>
    [Fact]
    public void EveryLocalId_Should_CarrySurrogateContract()
    {
        var ids = LocalIds();

        ids.Should().NotBeEmpty("本编译声明了多个 [StronglyTypedId]，protobuf 门控应当全部覆盖");

        foreach (var idType in ids)
        {
            var contract = idType.GetCustomAttribute<ProtoContractAttribute>();

            contract.Should().NotBeNull($"{idType.Name} 应当带 [ProtoContract]");
            contract!.Surrogate.Should().NotBeNull($"{idType.Name} 的 [ProtoContract] 应当指定 Surrogate");
            contract.Surrogate!.DeclaringType.Should().Be(idType, "surrogate 应当是 Id 的嵌套类型");
        }
    }

    /// <summary>
    /// 每种基元形态都要能经 protobuf-net 往返。
    /// </summary>
    /// <remarks>
    /// 逐 Id 反射调用 <c>Serializer.Serialize</c> / <c>Deserialize</c>：Id 类型在编译期各不相同，
    /// 泛型入口只能按运行时类型闭合。
    /// </remarks>
    [Fact]
    public void EveryLocalId_Should_RoundTrip()
    {
        foreach (var (idType, value) in LocalIdSamples())
        {
            var bytes = Serialize(value, idType);
            var back = Deserialize(bytes, idType);

            back.Should().Be(value, $"{idType.Name} 必须经 protobuf-net 往返后相等");
        }
    }

    /// <summary>
    /// 文本形基元的载荷必须是规范文本，而不是被 protobuf-net 当成未知类型。
    /// </summary>
    /// <remarks>
    /// <c>Int128</c> / <c>UInt128</c> / <c>Half</c> / <c>DateTimeOffset</c> / <c>BigInteger</c> 与自定义基元
    /// 都没有原生表示；这条用例直接断言「写出的字节里含 UTF-8 文本」，从而锁住「走文本」这一决策。
    /// </remarks>
    [Fact]
    public void TextPrimitiveIds_Should_WriteCanonicalText()
    {
        foreach (var (idType, value) in LocalIdSamples())
        {
            if (!IsTextPrimitive(idType))
            {
                continue;
            }

            var bytes = Serialize(value, idType);
            var text = System.Text.Encoding.UTF8.GetString(bytes);

            text.Should().Contain(ExpectedText(idType, value),
                $"{idType.Name} 的载荷应当是规范文本");
        }
    }

    /// <summary>
    /// <c>DateTimeOffset</c> 必须保留 offset 与亚秒精度。
    /// </summary>
    /// <remarks>
    /// 默认格式（<c>ToString()</c>）会丢掉亚秒，往返后 ticks 不等；只有 <c>"O"</c> 两者都保留。
    /// 这条用例锁住生成器里的格式选择，避免日后被「简化」成默认格式。
    /// </remarks>
    [Fact]
    public void DateTimeOffsetId_Should_PreserveOffsetAndSubSecondTicks()
    {
        var value = new DateTimeOffset(2024, 1, 2, 3, 4, 5, 678, TimeSpan.FromHours(8)).AddTicks(9);
        var id = IntegrationDateTimeOffsetId.Create(value);

        var back = (IntegrationDateTimeOffsetId)Deserialize(Serialize(id, typeof(IntegrationDateTimeOffsetId)), typeof(IntegrationDateTimeOffsetId))!;

        back.Value.Ticks.Should().Be(value.Ticks);
        back.Value.Offset.Should().Be(value.Offset);
    }

    /// <summary>
    /// 验证器拒绝的载荷必须抛 <c>ProtoException</c>，而不是让 <c>ArgumentException</c> 穿透。
    /// </summary>
    /// <remarks>
    /// protobuf-net 的契约要求转换失败时抛它自己的异常类型；<c>Create</c> 抛的是
    /// <see cref="ArgumentException"/>，故生成器改走 <c>TryCreate</c> 再显式抛 <c>ProtoException</c>。
    /// </remarks>
    [Fact]
    public void ValidatedId_Should_ThrowProtoException_WhenPayloadRejected()
    {
        // 直接构造一个「验证器会拒绝」的载荷：绕过 Create 的校验，只测反序列化侧。
        var surrogateType = typeof(IntegrationValidatedGuidId).GetNestedType(
            "IntegrationValidatedGuidIdSurrogate",
            BindingFlags.NonPublic)!;
        var surrogate = Activator.CreateInstance(surrogateType)!;
        surrogateType.GetProperty("Value")!.SetValue(surrogate, Guid.Empty);

        var act = () => Deserialize(Serialize(surrogate, surrogateType), typeof(IntegrationValidatedGuidId));

        act.Should().Throw<Exception>()
            .Which.Should().Match<Exception>(exception => exception is ProtoException || exception.InnerException is ProtoException,
                "载荷被验证器拒绝时必须抛 ProtoException");
    }

    /// <summary>
    /// 引用类型 Id 的 <c>null</c> 必须能往返成 <c>null</c>。
    /// </summary>
    [Fact]
    public void ReferenceTypeId_Should_RoundTripNull()
    {
        var bytes = Serialize(null, typeof(IntegrationProtobufReferenceId));

        Deserialize(bytes, typeof(IntegrationProtobufReferenceId)).Should().BeNull();
    }

    /// <summary>
    /// 带前缀的 Id 载荷存的是裸基元值，前缀只影响 <c>ToString()</c>。
    /// </summary>
    /// <remarks>
    /// 与 MessagePack / BSON 的口径一致：前缀是「文本表示」的一部分，不是数据本身。
    /// 若把带前缀的文本写进载荷，读回时前缀策略一变就再也解析不出来。
    /// </remarks>
    [Fact]
    public void PrefixedId_Should_StoreRawPrimitiveValue()
    {
        var value = Guid.Parse("3f2c1b4e-0000-4000-8000-000000000001");
        var id = PrefixedProtobufGuidId.Create(value);

        var back = (PrefixedProtobufGuidId)Deserialize(Serialize(id, typeof(PrefixedProtobufGuidId)), typeof(PrefixedProtobufGuidId))!;

        back.Value.Should().Be(value);
        back.ToString().Should().StartWith("ord_");
    }

    /// <summary>
    /// 自定义基元（<c>IntegrationMoney</c>）走文本形往返。
    /// </summary>
    [Fact]
    public void CustomPrimitiveId_Should_RoundTripAsText()
    {
        var id = IntegrationMoneyId.Create(new IntegrationMoney(12.5m));

        var back = (IntegrationMoneyId)Deserialize(Serialize(id, typeof(IntegrationMoneyId)), typeof(IntegrationMoneyId))!;

        back.Value.Amount.Should().Be(12.5m);
    }

    /// <summary>
    /// 本编译声明的全部强类型 Id（不含泛型容器里的）。
    /// </summary>
    private static IEnumerable<Type> LocalIds()
        => typeof(ProtobufIntegrationTests).Assembly
            .GetTypes()
            .Where(type => type.GetCustomAttribute<StronglyTypedIdAttribute>() is not null)
            .Where(type => !type.IsGenericTypeDefinition && type.DeclaringType?.IsGenericTypeDefinition != true);

    /// <summary>
    /// 每个 Id 配一个可往返的样本值。
    /// </summary>
    private static IEnumerable<(Type IdType, object Value)> LocalIdSamples()
    {
        yield return (typeof(IntegrationGuidId), IntegrationGuidId.Create(Guid.Parse("3f2c1b4e-0000-4000-8000-000000000001")));
        yield return (typeof(PrefixedProtobufGuidId), PrefixedProtobufGuidId.Create(Guid.Parse("3f2c1b4e-0000-4000-8000-000000000001")));
        yield return (typeof(IntegrationProtobufReferenceId), IntegrationProtobufReferenceId.Create("abc"));
        yield return (typeof(IntegrationValidatedGuidId), IntegrationValidatedGuidId.Create(Guid.Parse("3f2c1b4e-0000-4000-8000-000000000001")));
        yield return (typeof(IntegrationDateTimeOffsetId), IntegrationDateTimeOffsetId.Create(new DateTimeOffset(2020, 1, 2, 3, 4, 5, TimeSpan.FromHours(8))));
        yield return (typeof(IntegrationInt128Id), IntegrationInt128Id.Create((Int128)12345678901234567890m));
        yield return (typeof(IntegrationUInt128Id), IntegrationUInt128Id.Create((UInt128)12345678901234567890m));
        yield return (typeof(IntegrationHalfId), IntegrationHalfId.Create((Half)0.1));
        yield return (typeof(IntegrationBigIntegerId), IntegrationBigIntegerId.Create(System.Numerics.BigInteger.Parse("123456789012345678901234567890")));
        yield return (typeof(IntegrationMoneyId), IntegrationMoneyId.Create(new IntegrationMoney(12.5m)));
        yield return (typeof(IntegrationUlidId), IntegrationUlidId.Create(Ulid.Parse("01ARZ3NDEKTSV4RRFFQ69G5FAV", null)));
        yield return (typeof(IntegrationProtobufEnumId), IntegrationProtobufEnumId.Create(SampleStatus.Suspended));
        yield return (typeof(IntegrationProtobufDateOnlyId), IntegrationProtobufDateOnlyId.Create(new DateOnly(2020, 1, 2)));
        yield return (typeof(IntegrationProtobufDecimalId), IntegrationProtobufDecimalId.Create(1.5m));
        yield return (typeof(IntegrationProtobufCharId), IntegrationProtobufCharId.Create('A'));
    }

    /// <summary>
    /// 该 Id 的基元是否只能按规范文本承载（与生成器的 <c>IsProtobufTextPrimitive</c> 对应）。
    /// </summary>
    private static bool IsTextPrimitive(Type idType)
        => idType.GetInterfaces()
            .Single(@interface => @interface.IsGenericType
                && @interface.GetGenericTypeDefinition() == typeof(IStronglyTypedId<,>))
            .GetGenericArguments()[1] is var primitive
            && (primitive == typeof(Int128)
                || primitive == typeof(UInt128)
                || primitive == typeof(Half)
                || primitive == typeof(DateTimeOffset)
                || primitive == typeof(System.Numerics.BigInteger)
                || primitive == typeof(IntegrationMoney)
                || primitive == typeof(Ulid));

    /// <summary>
    /// 该 Id 的规范文本（用于断言载荷内容）。
    /// </summary>
    /// <remarks>
    /// <c>DateTimeOffset</c> 用 <c>"O"</c> 格式，与生成器的载荷口径一致；默认格式会丢掉亚秒精度。
    /// </remarks>
    private static string ExpectedText(Type idType, object value)
    {
        var primitive = idType.GetProperty("Value")!.GetValue(value)!;

        return primitive is DateTimeOffset offset
            ? offset.ToString("O", System.Globalization.CultureInfo.InvariantCulture)
            : primitive.ToString()!;
    }

    private static byte[] Serialize(object? value, Type type)
    {
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, value, type);
        return stream.ToArray();
    }

    private static object? Deserialize(byte[] bytes, Type type)
    {
        using var stream = new MemoryStream(bytes);
        return Serializer.Deserialize(type, stream);
    }
}
