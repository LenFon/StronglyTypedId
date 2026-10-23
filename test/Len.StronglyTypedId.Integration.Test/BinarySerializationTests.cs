using System.Reflection;
using FluentAssertions;
using MessagePack;
using MessagePack.Formatters;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Len.StronglyTypedId;

/// <summary>
/// 二进制序列化矩阵：把生成器为「每一个 Id」产出的 MessagePack 格式化器与 BSON 序列化器全部拿出来跑一遍。
/// </summary>
/// <remarks>
/// 这两条链路的风险在于格式化器是<b>按基元委托</b>给上游库的，「上游有没有该基元的原生表示」只有运行期才知道，故必须逐个真跑。
/// 断言分两层：往返还原同一个 Id；载荷里只有基元值本身（前缀一旦漏进二进制，改前缀就让已落盘数据读不回来）。
/// </remarks>
public class BinarySerializationTests
{
    /// <summary>
    /// 生成器产出的装配入口：先查生成的格式化器，再退回原有解析器。
    /// </summary>
    private static readonly MessagePackSerializerOptions _messagePackOptions =
        StronglyTypedIds.ApplyTo(MessagePackSerializerOptions.Standard);

    /// <summary>
    /// 普通数组载荷：用来验证「生成的格式化器找不到时退回原有解析器」。
    /// 内容恒定，提成字段以免每次调用都重新分配一个数组（CA1861）。
    /// </summary>
    private static readonly int[] _plainInts = [1, 2, 3];

    /// <summary>
    /// BSON 的序列化器注册在进程级注册表里，整个测试类只注册一次。
    /// </summary>
    /// <remarks>
    /// <c>Guid</c> 还要额外指定表示形式：driver 3.0 起没有全局默认值，未指定会抛 <c>BsonSerializationException</c>。
    /// </remarks>
    static BinarySerializationTests()
    {
        BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
        StronglyTypedIds.RegisterBsonSerializers();
    }

    [Fact]
    public void MessagePack_Should_GenerateFormatter_ForEveryId()
        => MessagePackFormatterIdTypes().Should().NotBeEmpty("集成测试程序集里必须有 Id 才能验证生成产物");

    [Fact]
    public void MongoDb_Should_GenerateSerializer_ForEveryId()
        => BsonSerializerIdTypes().Should().NotBeEmpty("集成测试程序集里必须有 Id 才能验证生成产物");

    /// <summary>
    /// 两个集成生成器共用同一份发现结果与同一套过滤规则，覆盖面不一致就说明有一边的门控写错了。
    /// </summary>
    [Fact]
    public void BinaryIntegrations_Should_CoverTheSameIds()
        => BsonSerializerIdTypes().Should().BeEquivalentTo(MessagePackFormatterIdTypes());

    [Fact]
    public void MessagePack_Should_RoundTripEveryId()
    {
        foreach (var idType in MessagePackFormatterIdTypes())
        {
            var id = CreateId(idType);

            var bytes = MessagePackSerializer.Serialize(idType, id, _messagePackOptions, TestContext.Current.CancellationToken);
            var restored = MessagePackSerializer.Deserialize(idType, bytes, _messagePackOptions, TestContext.Current.CancellationToken);

            restored.Should().Be(id, $"{idType.Name}：MessagePack 往返必须还原同一个 Id");
        }
    }

    [Fact]
    public void MongoDb_Should_RoundTripEveryId()
    {
        foreach (var idType in BsonSerializerIdTypes())
        {
            var id = CreateId(idType);

            // 必须读「字段」而不是整篇文档：BSON 的根只能是文档，把文档喂给标量序列化器必然类型不符。
            var restored = ReadValue(idType, WriteDocument(idType, id)["v"]);

            restored.Should().Be(id, $"{idType.Name}：BSON 往返必须还原同一个 Id");
        }
    }

    [Fact]
    public void MessagePack_Should_StorePrimitivePayload_WithoutPrefix()
    {
        foreach (var idType in MessagePackFormatterIdTypes())
        {
            var id = CreateId(idType);
            var primitiveType = PrimitiveOf(idType);
            var bytes = MessagePackSerializer.Serialize(idType, id, _messagePackOptions, TestContext.Current.CancellationToken);

            if (IntegrationSampleValues.IsBinaryTextPrimitive(primitiveType))
            {
                // 这几种基元没有原生表示，退回规范文本 —— 前缀在这条路上是「文本形」的一部分，理应带上。
                MessagePackSerializer.Deserialize<string>(bytes, _messagePackOptions, TestContext.Current.CancellationToken)
                    .Should().Be(id.ToString(), $"{idType.Name}：退回文本形时必须写规范文本");

                continue;
            }

            var primitiveBytes = MessagePackSerializer.Serialize(
                primitiveType, ValueOf(id)!, _messagePackOptions, TestContext.Current.CancellationToken);

            bytes.Should().Equal(primitiveBytes, $"{idType.Name}：二进制载荷必须是基元值本身，文本前缀不得进入");
        }
    }

    [Fact]
    public void MongoDb_Should_StorePrimitivePayload_WithoutPrefix()
    {
        foreach (var idType in BsonSerializerIdTypes())
        {
            var id = CreateId(idType);
            var primitiveType = PrimitiveOf(idType);
            var stored = WriteDocument(idType, id)["v"];

            if (IntegrationSampleValues.IsBinaryTextPrimitive(primitiveType))
            {
                stored.Should().Be(BsonString.Create(id.ToString()), $"{idType.Name}：退回文本形时必须写规范文本");

                continue;
            }

            stored.Should().Be(
                WriteDocument(primitiveType, ValueOf(id)!)["v"],
                $"{idType.Name}：BSON 载荷必须是基元值本身，文本前缀不得进入");
        }
    }

    [Fact]
    public void MessagePack_Should_RejectNil_ForValueTypeId()
    {
        foreach (var idType in MessagePackFormatterIdTypes().Where(static type => type.IsValueType))
        {
            Action deserialize = () => MessagePackSerializer.Deserialize(
                idType, NilPayload, _messagePackOptions, TestContext.Current.CancellationToken);

            deserialize.Should().Throw<MessagePackSerializationException>(
                $"{idType.Name}：值类型 Id 收到 nil 说明载荷与契约不符，静默落成 default 会把数据缺失伪装成合法值");
        }
    }

    [Fact]
    public void MessagePack_Should_MapNil_ToNull_ForReferenceTypeId()
    {
        var referenceIdTypes = MessagePackFormatterIdTypes().Where(static type => !type.IsValueType).ToList();

        referenceIdTypes.Should().NotBeEmpty("集成测试程序集里必须有引用类型 Id 才能覆盖这条分支");

        foreach (var idType in referenceIdTypes)
        {
            MessagePackSerializer.Deserialize(idType, NilPayload, _messagePackOptions, TestContext.Current.CancellationToken)
                .Should().BeNull($"{idType.Name}：引用类型 Id 的 nil 必须还原成 null");
        }
    }

    [Fact]
    public void MongoDb_Should_RejectNull_ForValueTypeId()
    {
        foreach (var idType in BsonSerializerIdTypes().Where(static type => type.IsValueType))
        {
            Action deserialize = () => ReadValue(idType, BsonNull.Value);

            deserialize.Should().Throw<BsonSerializationException>(
                $"{idType.Name}：值类型 Id 收到 BSON null 说明载荷与契约不符，静默落成 default 会把数据缺失伪装成合法值");
        }
    }

    [Fact]
    public void MongoDb_Should_MapNull_ToNull_ForReferenceTypeId()
    {
        var referenceIdTypes = BsonSerializerIdTypes().Where(static type => !type.IsValueType).ToList();

        referenceIdTypes.Should().NotBeEmpty("集成测试程序集里必须有引用类型 Id 才能覆盖这条分支");

        foreach (var idType in referenceIdTypes)
        {
            ReadValue(idType, BsonNull.Value).Should().BeNull($"{idType.Name}：引用类型 Id 的 BSON null 必须还原成 null");
        }
    }

    /// <summary>
    /// 验证器拒绝时，MessagePack 反序列化必须抛 <see cref="MessagePackSerializationException"/>。
    /// </summary>
    /// <remarks>
    /// 只断言外层类型不够：<c>MessagePackSerializer</c> 本来就会把格式化器抛出的任意异常包一层，
    /// 所以即使格式化器直接调 <c>Create</c>，外层也仍是这个类型。真正的差别在内层 ——
    /// 格式化器自己判定失败时内层是它抛的 <see cref="MessagePackSerializationException"/>，
    /// 而直接调 <c>Create</c> 时内层是 <see cref="ArgumentException"/>。
    /// </remarks>
    [Fact]
    public void MessagePack_Should_ThrowSerializationException_WhenValidatorRejects()
    {
        var bytes = MessagePackSerializer.Serialize(
            Guid.Empty, _messagePackOptions, TestContext.Current.CancellationToken);

        Action deserialize = () => MessagePackSerializer.Deserialize<ValidatedIntegrationGuidId>(
            bytes, _messagePackOptions, TestContext.Current.CancellationToken);

        deserialize.Should().Throw<MessagePackSerializationException>()
            .Which.InnerException.Should().NotBeOfType<ArgumentException>(
                "验证失败应由格式化器自己判定并抛错，而不是让 Create 的 ArgumentException 被上层包装");
    }

    /// <summary>
    /// 同上：BSON 的序列化器契约要求失败一律抛 <see cref="BsonSerializationException"/>。
    /// </summary>
    [Fact]
    public void MongoDb_Should_ThrowSerializationException_WhenValidatorRejects()
    {
        // 用 Guid 自己的序列化器产出载荷，保证 BSON 表示形式（subtype / 字节序）与生成代码读到的完全一致。
        var payload = WriteDocument(typeof(Guid), Guid.Empty)["v"];

        Action deserialize = () => ReadValue(typeof(ValidatedIntegrationGuidId), payload);

        deserialize.Should().Throw<BsonSerializationException>();
    }

    [Fact]
    public void MessagePack_ApplyTo_Should_FallBackToOriginalResolver()
    {
        // 装配入口是「先查生成的格式化器，再退回原有解析器」，不能把别人的解析能力挤掉。
        var bytes = MessagePackSerializer.Serialize(
            _plainInts, _messagePackOptions, TestContext.Current.CancellationToken);

        MessagePackSerializer.Deserialize<int[]>(bytes, _messagePackOptions, TestContext.Current.CancellationToken)
            .Should().Equal(1, 2, 3);
    }

    [Fact]
    public void MongoDb_RegisterBsonSerializers_Should_BeIdempotent()
    {
        // 注册是进程级全局副作用，重复调用（多个入口各调一次）必须无害。
        Action register = StronglyTypedIds.RegisterBsonSerializers;

        register.Should().NotThrow();
        register.Should().NotThrow();
    }

    /// <summary>
    /// MessagePack 的 nil 编码：单字节 <c>0xc0</c>。
    /// </summary>
    private static ReadOnlyMemory<byte> NilPayload => new byte[] { 0xC0 };

    /// <summary>
    /// 本编译里生成了 MessagePack 格式化器的全部 Id 类型。
    /// </summary>
    private static IEnumerable<Type> MessagePackFormatterIdTypes()
        => typeof(StronglyTypedIds)
            .GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public)
            .SelectMany(static type => type.GetInterfaces())
            .Where(static @interface => @interface.IsGenericType
                && @interface.GetGenericTypeDefinition() == typeof(IMessagePackFormatter<>))
            .Select(static @interface => @interface.GetGenericArguments()[0]);

    /// <summary>
    /// 本编译里生成了 BSON 序列化器的全部 Id 类型。
    /// </summary>
    private static IEnumerable<Type> BsonSerializerIdTypes()
        => typeof(StronglyTypedIds)
            .GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public)
            .Where(static type => type.BaseType is { IsGenericType: true } baseType
                && baseType.GetGenericTypeDefinition() == typeof(SerializerBase<>))
            .Select(static type => type.BaseType!.GetGenericArguments()[0]);

    /// <summary>
    /// 把单个值写成一个只含 <c>v</c> 字段的 BSON 文档。
    /// </summary>
    /// <remarks>
    /// BSON 的根必须是文档，裸标量写不进 <see cref="BsonDocumentWriter"/>，故统一包一层 —— 这也正是真实用法。
    /// </remarks>
    private static BsonDocument WriteDocument(Type nominalType, object value)
    {
        var document = new BsonDocument();

        using (var writer = new BsonDocumentWriter(document))
        {
            writer.WriteStartDocument();
            writer.WriteName("v");
            BsonSerializer.Serialize(writer, nominalType, value);
            writer.WriteEndDocument();
        }

        return document;
    }

    /// <summary>
    /// 从文档字段的位置读出一个值（用于构造「字段为 null」这类无法走整篇文档反序列化的场景）。
    /// </summary>
    private static object? ReadValue(Type nominalType, BsonValue value)
    {
        using var reader = new BsonDocumentReader(new BsonDocument { { "v", value } });

        reader.ReadStartDocument();
        reader.ReadName();

        return BsonSerializer.Deserialize(reader, nominalType);
    }

    /// <summary>
    /// 从 <c>IStronglyTypedId&lt;TSelf, TPrimitiveId&gt;</c> 读出基元类型。
    /// </summary>
    private static Type PrimitiveOf(Type idType)
        => idType
            .GetInterfaces()
            .Single(@interface => @interface.IsGenericType
                && @interface.GetGenericTypeDefinition() == typeof(IStronglyTypedId<,>))
            .GetGenericArguments()[1];

    /// <summary>
    /// 按 Id 的 <c>Create</c> 静态方法构造实例（不直接 <c>new</c>，以免绕过验证器）。
    /// </summary>
    private static object CreateId(Type idType)
        => CreateId(idType, IntegrationSampleValues.ByPrimitive[PrimitiveOf(idType)]);

    private static object CreateId(Type idType, object value)
    {
        var primitiveType = PrimitiveOf(idType);
        var create = idType
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "Create"
                && method.GetParameters() is [{ ParameterType: var parameterType }]
                && parameterType == primitiveType);

        return create.Invoke(null, [value])!;
    }

    /// <summary>
    /// 读 Id 实际持有的基元值（归一化之后）。
    /// </summary>
    private static object? ValueOf(object id) => id.GetType().GetProperty("Value")!.GetValue(id);
}
