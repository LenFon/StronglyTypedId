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
/// <para>
/// 与 Dapper / EF Core 的矩阵同构，但这两条链路有一处独有的风险：格式化器是<b>按基元委托</b>给上游库的
/// （<c>GetFormatterWithVerify&lt;TPrimitive&gt;</c> / <c>LookupSerializer&lt;TPrimitive&gt;</c>），
/// 而「上游库有没有这个基元的原生表示」只能在运行期才知道。编译通过不代表 <c>Half</c>、<c>DateOnly</c>
/// 这些较新的基元真的能序列化，所以这里必须逐个真跑一遍。
/// </para>
/// <para>
/// 断言分两层：一是往返能还原同一个 Id；二是<b>载荷里只有基元值本身</b> —— 文本前缀是文本形的装饰，
/// 一旦漏进二进制格式，改前缀就会让已落盘的数据读不回来。
/// </para>
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
    /// <c>Guid</c> 还要额外指定表示形式：driver 3.0 起没有全局默认值，未指定时序列化会直接抛
    /// <c>BsonSerializationException</c>。这里按新库的惯例选 <c>Standard</c>，真实应用应按既有数据的
    /// 表示形式来选。
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
    /// BSON 的根必须是文档，裸标量写不进 <see cref="BsonDocumentWriter"/>，故统一包一层。
    /// 这也正是真实用法：Id 总是某个文档的字段。
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
