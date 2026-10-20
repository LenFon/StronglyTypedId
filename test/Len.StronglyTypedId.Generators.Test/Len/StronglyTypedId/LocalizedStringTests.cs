using System.Globalization;
using FluentAssertions;

namespace Len.StronglyTypedId;

/// <summary>
/// <see cref="LocalizedString"/> 的单元测试：相等性、哈希与区域性回退。
/// </summary>
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
    /// 资源键不存在时必须回退显示键本身，而不是抛异常——漏配一条文案不应让整个诊断崩溃。
    /// </summary>
    [Fact]
    public void ToString_Should_FallBackToKey_WhenResourceIsMissing() => new LocalizedString("NoSuchResourceKey").ToString().Should().Be("NoSuchResourceKey");

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

    /// <summary>
    /// 回退链的最后一环是内嵌的 en 资源。它一旦缺失，加载器会拿到 null 流并静默回退到资源键——不再抛异常，
    /// 但所有诊断文案都会退化成键名。这条断言把「资源确实被打进了程序集」钉住，让那种退化能被发现。
    /// </summary>
    [Fact]
    public void EmbeddedResources_Should_IncludeEnglishFallback()
        => typeof(LocalizedString).Assembly.GetManifestResourceNames()
            .Should().Contain(name => name.EndsWith(".Locales.en.txt", StringComparison.OrdinalIgnoreCase));

    #endregion
}
