using FluentAssertions;

namespace Len.StronglyTypedId;

/// <summary>
/// <see cref="IntegrationFlags"/> 的单元测试：运行时枚举的镜像值与掩码规范化。
/// </summary>
/// <remarks>
/// 生成器与分析器不引用运行时程序集，只能按底层整数匹配，因此两张表必须逐位一致。
/// 这里同时钉住「镜像值」与「规范化规则」两件事：前者错了会让某个集成静默失效，
/// 后者错了会让 <c>Integrations = Default</c>（显式写 0）关掉全部集成。
/// </remarks>
public class IntegrationFlagsTests
{
    #region 镜像值必须与运行时枚举逐位一致

    /// <summary>
    /// 逐成员比对：生成器侧的常量一旦与 <see cref="StronglyTypedIdIntegrations"/> 漂移，
    /// 用户写的 <c>Integrations = Dapper</c> 就会静默指向另一个集成。
    /// </summary>
    [Theory]
    [InlineData(nameof(StronglyTypedIdIntegrations.None), IntegrationFlags.None)]
    [InlineData(nameof(StronglyTypedIdIntegrations.EntityFrameworkCore), IntegrationFlags.EntityFrameworkCore)]
    [InlineData(nameof(StronglyTypedIdIntegrations.Dapper), IntegrationFlags.Dapper)]
    [InlineData(nameof(StronglyTypedIdIntegrations.SystemTextJson), IntegrationFlags.SystemTextJson)]
    [InlineData(nameof(StronglyTypedIdIntegrations.NewtonsoftJson), IntegrationFlags.NewtonsoftJson)]
    [InlineData(nameof(StronglyTypedIdIntegrations.Swagger), IntegrationFlags.Swagger)]
    [InlineData(nameof(StronglyTypedIdIntegrations.AspNetCoreOpenApi), IntegrationFlags.AspNetCoreOpenApi)]
    [InlineData(nameof(StronglyTypedIdIntegrations.AspNetCoreMvc), IntegrationFlags.AspNetCoreMvc)]
    [InlineData(nameof(StronglyTypedIdIntegrations.MessagePack), IntegrationFlags.MessagePack)]
    [InlineData(nameof(StronglyTypedIdIntegrations.MongoDb), IntegrationFlags.MongoDb)]
    [InlineData(nameof(StronglyTypedIdIntegrations.HotChocolate), IntegrationFlags.HotChocolate)]
    [InlineData(nameof(StronglyTypedIdIntegrations.Protobuf), IntegrationFlags.Protobuf)]
    [InlineData(nameof(StronglyTypedIdIntegrations.All), IntegrationFlags.All)]
    public void Mirror_Should_MatchRuntimeEnumValue(string memberName, int mirrorValue)
        => ((int)Enum.Parse<StronglyTypedIdIntegrations>(memberName)).Should().Be(mirrorValue);

    /// <summary>
    /// <c>All</c> 必须是全部集成位的并集，且不含 <c>None</c> 标记位。
    /// </summary>
    [Fact]
    public void All_Should_BeUnionOfEveryIntegration_WithoutNoneBit()
    {
        var expected = IntegrationFlags.EntityFrameworkCore
            | IntegrationFlags.Dapper
            | IntegrationFlags.SystemTextJson
            | IntegrationFlags.NewtonsoftJson
            | IntegrationFlags.Swagger
            | IntegrationFlags.AspNetCoreOpenApi
            | IntegrationFlags.AspNetCoreMvc
            | IntegrationFlags.MessagePack
            | IntegrationFlags.MongoDb
            | IntegrationFlags.HotChocolate
            | IntegrationFlags.Protobuf;

        IntegrationFlags.All.Should().Be(expected);
        (IntegrationFlags.All & IntegrationFlags.None).Should().Be(0);
    }

    #endregion

    #region Normalize：未指定 / 显式 Default / 剔除 None 位

    /// <summary>
    /// 未指定（<see langword="null"/>）保持 <see langword="null"/>，让调用方继续回退到程序集默认值。
    /// </summary>
    [Fact]
    public void Normalize_Should_ReturnNull_WhenNotSpecified()
        => IntegrationFlags.Normalize(null).Should().BeNull();

    /// <summary>
    /// 显式写 <c>Default</c>（0）归一为 <see cref="IntegrationFlags.All"/>：这是「写了但什么都没选」
    /// 与「没写」的区分点，一旦被当成「全部关闭」，所有集成都会静默消失。
    /// </summary>
    [Fact]
    public void Normalize_Should_ReturnAll_WhenExplicitDefault()
        => IntegrationFlags.Normalize(0).Should().Be(IntegrationFlags.All);

    /// <summary>
    /// <c>None</c> 只是「已指定」的标记位，参与掩码时被剔除：<c>None | Dapper</c> 等价于 <c>Dapper</c>。
    /// </summary>
    [Fact]
    public void Normalize_Should_StripNoneBit()
        => IntegrationFlags.Normalize(IntegrationFlags.None | IntegrationFlags.Dapper)
            .Should().Be(IntegrationFlags.Dapper);

    /// <summary>
    /// 只写 <c>None</c> 时归一为 0，表示「显式关闭全部集成」。
    /// </summary>
    [Fact]
    public void Normalize_Should_ReturnZero_WhenOnlyNone()
        => IntegrationFlags.Normalize(IntegrationFlags.None).Should().Be(0);

    /// <summary>
    /// 普通掩码原样保留。
    /// </summary>
    [Fact]
    public void Normalize_Should_PreserveMask_WhenNoNoneBit()
    {
        var raw = IntegrationFlags.Dapper | IntegrationFlags.SystemTextJson;

        IntegrationFlags.Normalize(raw).Should().Be(raw);
    }

    #endregion
}
