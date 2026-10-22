using FluentAssertions;
using System.Globalization;

namespace Len.StronglyTypedId;

/// <summary>
/// 枚举基元的解析契约：非 <c>[Flags]</c> 枚举只接受已定义的成员，<c>[Flags]</c> 枚举接受位组合但拒绝未定义的位。
/// </summary>
/// <remarks>
/// <c>Enum.TryParse</c> 对 <c>"999"</c>、<c>"1,2"</c>、<c>" Active "</c> 都返回 <see langword="true"/>，故非 <c>[Flags]</c> 枚举补 <c>Enum.IsDefined</c>，<c>[Flags]</c> 枚举补「未定义位掩码」按位与。
/// </remarks>
public class EnumPrimitiveTests
{
    #region 非 [Flags]：拒绝未定义取值

    [Theory]
    [InlineData("999")]
    [InlineData("-1")]
    [InlineData("1,2")]
    public void TryParse_Should_RejectUndefinedOrMalformedText(string text)
        => SampleStatusId.TryParse(text, CultureInfo.InvariantCulture, out _).Should().BeFalse();

    [Theory]
    [InlineData("Active", SampleStatus.Active)]
    [InlineData("Suspended", SampleStatus.Suspended)]
    [InlineData("0", SampleStatus.Active)]
    [InlineData("1", SampleStatus.Suspended)]
    // 前后空白照旧接受：Enum.TryParse 会 Trim，与 int.Parse 等 BCL 解析器的惯例一致，不属于本次收紧范围。
    [InlineData(" Active ", SampleStatus.Active)]
    public void TryParse_Should_AcceptDefinedMember(string text, SampleStatus expected)
    {
        SampleStatusId.TryParse(text, CultureInfo.InvariantCulture, out var id).Should().BeTrue();

        id.Value.Should().Be(expected);
    }

    [Fact]
    public void Parse_Should_Throw_ForUndefinedValue()
    {
        // 按 IParsable 契约抛 FormatException（与 int.Parse / Guid.Parse 一致）。
        Action act = () => SampleStatusId.Parse("999", CultureInfo.InvariantCulture);

        act.Should().Throw<FormatException>();
    }

    #endregion

    #region [Flags]：位组合合法，未定义的位不合法

    [Theory]
    [InlineData("Read", SampleAccess.Read)]
    [InlineData("Write", SampleAccess.Write)]
    [InlineData("0", SampleAccess.None)]
    [InlineData("3", SampleAccess.Read | SampleAccess.Write)]
    [InlineData("Read, Write", SampleAccess.Read | SampleAccess.Write)]
    public void TryParse_Should_KeepFlagCombinations(string text, SampleAccess expected)
    {
        SampleAccessId.TryParse(text, CultureInfo.InvariantCulture, out var id).Should().BeTrue();

        id.Value.Should().Be(expected);
    }

    /// <summary>
    /// 位组合合法不等于任意数值合法：<c>999</c>（含未定义位）、<c>4</c>（Read/Write 之外的位）、
    /// <c>-1</c>（补码满位）、<c>"1,2,4"</c>（组合里混入未定义位）都必须被拒。
    /// </summary>
    [Theory]
    [InlineData("999")]
    [InlineData("4")]
    [InlineData("-1")]
    [InlineData("1,2,4")]
    public void TryParse_Should_RejectUndefinedFlagBits(string text)
        => SampleAccessId.TryParse(text, CultureInfo.InvariantCulture, out _).Should().BeFalse();

    [Fact]
    public void TryParse_Should_RejectUndefinedFlagBits_ForSpan()
        => SampleAccessId.TryParse("999".AsSpan(), CultureInfo.InvariantCulture, out _).Should().BeFalse();

    [Fact]
    public void Parse_Should_Throw_ForUndefinedFlagBits()
    {
        Action act = () => SampleAccessId.Parse("999", CultureInfo.InvariantCulture);

        act.Should().Throw<FormatException>();
    }

    #endregion
}
