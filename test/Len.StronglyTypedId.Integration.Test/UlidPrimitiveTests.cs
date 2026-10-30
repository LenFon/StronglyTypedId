using System.Globalization;
using Dapper;
using FluentAssertions;
using MessagePack;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using Newtonsoft.Json;
using JsonConvert = Newtonsoft.Json.JsonConvert;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Len.StronglyTypedId;

/// <summary>
/// ULID 作为自定义基元的端到端证据。
/// </summary>
/// <remarks>
/// 路线图 §5.4 曾把 ULID 判为「核心重构级」，理由是基元判据是「封闭表 + 名字特判」。
/// N12 把判据改成能力探测之后，ULID 不再需要任何专门代码：Cysharp 的 <c>Ulid</c> 实现了
/// <c>IParsable&lt;Ulid&gt;</c>、<c>IComparable&lt;Ulid&gt;</c> 与非泛型 <c>IComparable</c>，
/// 正好落在契约内。本类逐条锁住「零改动可用」，避免将来有人把它当成「需要特判的基元」再加一次。
/// </remarks>
public class UlidPrimitiveTests
{
    private static readonly Ulid _value = Ulid.NewUlid();

    private static IntegrationUlidId Id => IntegrationUlidId.Create(_value);

    #region 核心成员

    [Fact]
    public void ToString_Should_UseCrockfordBase32()
    {
        Id.ToString().Should().Be(_value.ToString());
        Id.ToString().Should().HaveLength(26);
    }

    [Fact]
    public void Parse_Should_RoundTrip()
        => IntegrationUlidId.Parse(Id.ToString(), CultureInfo.InvariantCulture).Should().Be(Id);

    [Fact]
    public void TryParse_Should_AcceptSpan()
        => IntegrationUlidId.TryParse(Id.ToString().AsSpan(), CultureInfo.InvariantCulture, out _).Should().BeTrue();

    /// <summary>
    /// ULID 的文本形是 26 字符 Crockford Base32，长度不对必须被拒。
    /// </summary>
    [Fact]
    public void TryParse_Should_RejectMalformedText()
    {
        IntegrationUlidId.TryParse("not-a-ulid", CultureInfo.InvariantCulture, out _).Should().BeFalse();
        IntegrationUlidId.TryParse("01M60GFMBVKNE19YX1YAZQB3K", CultureInfo.InvariantCulture, out _).Should().BeFalse();
    }

    /// <summary>
    /// ULID 自带时间序：时间戳更晚的 Ulid 更大。这条断言同时证明 <c>CompareTo</c> 走的是基元的实现。
    /// </summary>
    /// <remarks>
    /// 用显式时间戳构造而不是连发两次 <c>NewUlid()</c>：同一毫秒内的两个 ULID 只有随机部分不同，
    /// 而随机部分不保证递增（Cysharp 的实现就是纯随机），连发两次可能得到相等甚至逆序的值。
    /// </remarks>
    [Fact]
    public void CompareTo_Should_BeTimeOrdered()
    {
        var earlier = IntegrationUlidId.Create(Ulid.NewUlid(DateTimeOffset.UnixEpoch));
        var later = IntegrationUlidId.Create(Ulid.NewUlid(DateTimeOffset.UnixEpoch.AddSeconds(1)));

        (earlier < later).Should().BeTrue();
        earlier.CompareTo(later).Should().BeNegative();
    }

    /// <summary>
    /// <c>Ulid</c> 实现 <c>IFormattable</c>，故 <c>ToString(format, provider)</c> 会被生成。
    /// </summary>
    [Fact]
    public void FormattableMembers_Should_BeEmitted()
        => Id.ToString("D", CultureInfo.InvariantCulture).Should().Be(_value.ToString());

    #endregion

    #region 前缀与命名实例

    [Fact]
    public void Prefix_Should_FlowThroughCanonicalText()
    {
        var prefixed = PrefixedIntegrationUlidId.Create(_value);

        prefixed.ToString().Should().Be("ord_" + _value.ToString());
        PrefixedIntegrationUlidId.Parse(prefixed.ToString(), CultureInfo.InvariantCulture).Should().Be(prefixed);
    }

    [Fact]
    public void NamedInstance_Should_BeCreatedThroughPrimitiveParse()
        => InstanceIntegrationUlidId.Nil.Value.Should().Be(Ulid.Empty);

    #endregion

    #region JSON

    [Fact]
    public void SystemTextJson_Should_RoundTripAsText()
    {
        var json = JsonSerializer.Serialize(Id);

        json.Should().Be($"\"{_value}\"");
        JsonSerializer.Deserialize<IntegrationUlidId>(json).Should().Be(Id);
    }

    [Fact]
    public void NewtonsoftJson_Should_RoundTripAsText()
    {
        var json = JsonConvert.SerializeObject(Id);

        json.Should().Be($"\"{_value}\"");
        JsonConvert.DeserializeObject<IntegrationUlidId>(json).Should().Be(Id);
    }

    [Fact]
    public void SystemTextJson_Should_RoundTripAsDictionaryKey()
    {
        var dictionary = new Dictionary<IntegrationUlidId, int> { [Id] = 1 };

        var json = JsonSerializer.Serialize(dictionary);

        json.Should().Be($"{{\"{_value}\":1}}");
        JsonSerializer.Deserialize<Dictionary<IntegrationUlidId, int>>(json).Should().ContainKey(Id);
    }

    #endregion

    #region 二进制

    [Fact]
    public void MessagePack_Should_RoundTripAsText()
    {
        var options = MessagePackSerializerOptions.Standard.AddStronglyTypedIds();

        var bytes = MessagePackSerializer.Serialize(Id, options, TestContext.Current.CancellationToken);

        MessagePackSerializer.Deserialize<IntegrationUlidId>(bytes, options, TestContext.Current.CancellationToken).Should().Be(Id);
    }

    [Fact]
    public void MongoDb_Should_RoundTripAsText()
    {
        StronglyTypedIds.RegisterBsonSerializers();

        var document = new BsonDocument();

        using (var writer = new BsonDocumentWriter(document))
        {
            writer.WriteStartDocument();
            writer.WriteName("v");
            BsonSerializer.Serialize(writer, Id);
            writer.WriteEndDocument();
        }

        document["v"].Should().Be(new BsonString(_value.ToString()));

        using var reader = new BsonDocumentReader(document);
        reader.ReadStartDocument();
        reader.ReadName();

        BsonSerializer.Deserialize<IntegrationUlidId>(reader).Should().Be(Id);
    }

    #endregion

    #region EF Core / Dapper

    /// <summary>
    /// EF Core 的 provider 类型对自定义基元必须是数据库认得的类型（<c>string</c>）。
    /// 直接用基元本身会让 EF 报「The 'Ulid' property could not be mapped because the database provider
    /// does not support this type」—— 数据库不认识用户自定义的 CLR 类型。
    /// </summary>
    [Fact]
    public void EfCore_Should_UseStringAsProviderType()
    {
        var converter = GetEfCoreConverter();

        converter.ConvertToProvider(Id).Should().Be(_value.ToString());
        converter.ConvertFromProvider(_value.ToString()).Should().Be(Id);
    }

    [Fact]
    public void Dapper_Should_ParseFromDriverText()
        => GetDapperHandler().Parse(_value.ToString()).Should().Be(Id);

    [Fact]
    public void Dapper_Should_ParseFromPrimitiveValue()
        => GetDapperHandler().Parse(_value).Should().Be(Id);

    [Fact]
    public void Dapper_Should_RoundTripThroughSqlite()
    {
        StronglyTypedIds.AddStronglyTypedIds();

        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        connection.Open();

        connection.Execute("CREATE TABLE Probe (Id TEXT NOT NULL)");
        connection.Execute("INSERT INTO Probe (Id) VALUES (@Id)", new { Id });

        connection.QuerySingle<IntegrationUlidId>("SELECT Id FROM Probe").Should().Be(Id);
    }

    private static ValueConverter<IntegrationUlidId, string> GetEfCoreConverter()
        => (ValueConverter<IntegrationUlidId, string>)Activator.CreateInstance(
            typeof(StronglyTypedIds)
                .Assembly
                .GetType("Len.StronglyTypedId.StronglyTypedIds+IntegrationUlidIdConverter")!)!;

    private static SqlMapper.TypeHandler<IntegrationUlidId> GetDapperHandler()
        => (SqlMapper.TypeHandler<IntegrationUlidId>)Activator.CreateInstance(
            typeof(StronglyTypedIds)
                .Assembly
                .GetType("Len.StronglyTypedId.StronglyTypedIds+IntegrationUlidIdTypeHandler")!)!;

    #endregion
}
