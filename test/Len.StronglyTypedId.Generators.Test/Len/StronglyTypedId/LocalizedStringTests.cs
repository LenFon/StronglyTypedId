using System.Globalization;
using FluentAssertions;

namespace Len.StronglyTypedId;

/// <summary>
/// <see cref="LocalizedString"/> 的单元测试：相等性、哈希与区域性回退。
/// </summary>
/// <remarks>
/// 该类型存在的理由是分析器规范禁止直接读取 <c>CultureInfo.CurrentCulture</c>（RS1035），
/// 改为实现 <c>LocalizableString</c> 由 Roslyn 在格式化诊断时注入目标区域性。因此
/// <c>GetHash</c> / <c>AreEqual</c> 不只是装饰：诊断对象会参与比较与去重，实现错误会导致
/// 同一条诊断被判为不同，或不同诊断被判为相同。
/// </remarks>
public class LocalizedStringTests
{
    #region 相等性与哈希

    [Fact]
    public void SameKey_Should_BeEqual_AndShareHashCode()
    {
        var left = new LocalizedString("TypeMustBePartialTitle");
        var right = new LocalizedString("TypeMustBePartialTitle");

        left.Equals(right).Should().BeTrue();
        left.GetHashCode().Should().Be(right.GetHashCode());
        left.ToString().Should().Be(right.ToString());
    }

    [Fact]
    public void DifferentKeys_Should_NotBeEqual()
    {
        var left = new LocalizedString("TypeMustBePartialTitle");
        var right = new LocalizedString("TypeMustBeRecordTitle");

        left.Equals(right).Should().BeFalse();
    }

    /// <summary>
    /// 与其它类型的对象比较必须返回 <see langword="false"/>，而不是抛出。
    /// </summary>
    [Fact]
    public void Equals_Should_ReturnFalse_ForOtherTypes()
    {
        var localized = new LocalizedString("TypeMustBePartialTitle");

        localized.Equals("TypeMustBePartialTitle").Should().BeFalse();
        localized.Equals(null).Should().BeFalse();
    }

    #endregion

    #region 区域性回退

    /// <summary>
    /// 资源键不存在时必须回退显示键本身，而不是抛异常 —— 漏配一条文案不应让整个诊断崩溃。
    /// </summary>
    [Fact]
    public void ToString_Should_FallBackToKey_WhenResourceIsMissing()
    {
        new LocalizedString("NoSuchResourceKey").ToString().Should().Be("NoSuchResourceKey");
    }

    [Fact]
    public void ToString_Should_ReturnLocalizedText_WhenResourceExists()
    {
        var text = new LocalizedString("TypeMustBePartialTitle").ToString();

        // 未显式指定区域性时回退到内嵌的 en 资源。
        text.Should().NotBe("TypeMustBePartialTitle");
        text.Should().NotBeEmpty();
    }

    [Fact]
    public void ToString_Should_HonourExplicitFormatProvider()
    {
        var localized = new LocalizedString("TypeMustBePartialTitle");

        localized.ToString(CultureInfo.GetCultureInfo("zh-CN")).Should().Contain("类型");
    }

    [Fact]
    public void ToString_Should_UseInvariantCulture_WhenProviderIsNotCulture()
    {
        var localized = new LocalizedString("TypeMustBePartialTitle");

        // 非 CultureInfo 的 formatProvider 按固定区域性处理，仍然命中 en 回退资源。
        localized.ToString(CultureInfo.InvariantCulture).Should().NotBe("TypeMustBePartialTitle");
    }

    /// <summary>
    /// 未知区域性没有专属资源文件时，逐级回退到两字母语言名、再到 en，而不是抛异常。
    /// </summary>
    [Fact]
    public void ToString_Should_FallBackToEnglish_ForUnsupportedCulture()
    {
        var localized = new LocalizedString("TypeMustBePartialTitle");

        localized.ToString(CultureInfo.GetCultureInfo("fr-FR")).Should().NotBe("TypeMustBePartialTitle");
    }

    #endregion
}
