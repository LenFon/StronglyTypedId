using Dapper;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Len.StronglyTypedId;

/// <summary>
/// N12：自定义基元的端到端集成往返。
/// </summary>
/// <remarks>
/// 生成器层用例只能证明「产物里写了什么」；这里跑真实的 EF Core 转换器与 Dapper 管道，
/// 证明自定义基元在「按基元类型生成代码」的集成里确实可用 —— 尤其是 Dapper 的
/// <c>Parse</c> 不能落到按短名分派的 <c>Convert.ToXxx</c>（自定义类型没有对应重载）。
/// </remarks>
public class CustomPrimitiveIntegrationTests
{
    private static readonly IntegrationMoneyId _id = IntegrationMoneyId.Create(new IntegrationMoney(12.5m));

    /// <summary>
    /// EF Core 的转换器对自定义基元按规范文本往返：<c>ConvertToProvider</c> / <c>ConvertFromProvider</c> 必须对称。
    /// </summary>
    /// <remarks>
    /// provider 类型是 <c>string</c> 而非基元本身：数据库提供程序不认识用户自定义的 CLR 类型，
    /// 用基元本身会让模型构建失败。不跑真实数据库 —— 本工程只引了 <c>Microsoft.EntityFrameworkCore</c>
    /// 与裸 ADO.NET 的 <c>Microsoft.Data.Sqlite</c>，没有 EF 的 Sqlite provider；转换器是纯函数，直接调它即可。
    /// </remarks>
    [Fact]
    public void EfCore_Should_RoundTripCustomPrimitive()
    {
        var converter = GetEfCoreConverter();

        var provider = converter.ConvertToProvider(_id);

        provider.Should().Be("12.5");
        converter.ConvertFromProvider(provider).Should().Be(_id);
    }

    /// <summary>
    /// EF 的转换器对自定义基元以 <c>string</c> 为 provider 类型。
    /// </summary>
    [Fact]
    public void EfCore_Should_GenerateConverter_ForCustomPrimitive()
        => GetEfCoreConverter().Should().BeAssignableTo<ValueConverter<IntegrationMoneyId, string>>();

    /// <summary>
    /// Dapper 的 <c>Parse</c> 对自定义基元走文本入口：驱动返回 <c>string</c> 时也能还原。
    /// </summary>
    [Fact]
    public void Dapper_Should_ParseCustomPrimitive_FromDriverText()
        => GetDapperHandler().Parse("12.5").Should().Be(_id);

    /// <summary>
    /// 驱动直接返回基元本身时走直通分支，不经过文本转换。
    /// </summary>
    [Fact]
    public void Dapper_Should_ParseCustomPrimitive_FromPrimitiveValue()
        => GetDapperHandler().Parse(new IntegrationMoney(12.5m)).Should().Be(_id);

    /// <summary>
    /// 真实 SQLite 往返：写入侧把基元交给驱动，读取侧由驱动返回 <c>string</c>（TEXT 列）。
    /// </summary>
    [Fact]
    public void Dapper_Should_RoundTripCustomPrimitive_ThroughSqlite()
    {
        Len.StronglyTypedId.StronglyTypedIds.AddStronglyTypedIds();

        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        connection.Open();

        connection.Execute("CREATE TABLE Probe (Id TEXT NOT NULL)");
        connection.Execute("INSERT INTO Probe (Id) VALUES (@Id)", new { Id = _id });

        connection.QuerySingle<IntegrationMoneyId>("SELECT Id FROM Probe").Should().Be(_id);
    }

    /// <summary>
    /// 取自定义基元 Id 的 EF Core 转换器实例（生成在 <c>StronglyTypedIds</c> 里的 internal 嵌套类）。
    /// </summary>
    private static ValueConverter<IntegrationMoneyId, string> GetEfCoreConverter()
        => (ValueConverter<IntegrationMoneyId, string>)Activator.CreateInstance(
            typeof(Len.StronglyTypedId.StronglyTypedIds)
                .Assembly
                .GetType("Len.StronglyTypedId.StronglyTypedIds+IntegrationMoneyIdConverter")!)!;

    /// <summary>
    /// 取自定义基元 Id 的 Dapper 处理器实例（生成在 <c>StronglyTypedIds</c> 里的 internal 嵌套类）。
    /// </summary>
    private static SqlMapper.TypeHandler<IntegrationMoneyId> GetDapperHandler()
        => (SqlMapper.TypeHandler<IntegrationMoneyId>)Activator.CreateInstance(
            typeof(Len.StronglyTypedId.StronglyTypedIds)
                .Assembly
                .GetType("Len.StronglyTypedId.StronglyTypedIds+IntegrationMoneyIdTypeHandler")!)!;
}
